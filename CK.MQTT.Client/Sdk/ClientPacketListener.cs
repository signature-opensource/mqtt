using System.Diagnostics;
using CK.MQTT.Sdk.Flows;
using CK.MQTT.Sdk.Packets;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System;

namespace CK.MQTT.Sdk
{
    internal class ClientPacketListener : IPacketListener
    {
        static readonly ITracer _tracer = Tracer.Get<ClientPacketListener>();
        IDisposable _keepAliveMonitor;
        readonly IMqttChannel<IPacket> _channel;
        readonly IProtocolFlowProvider _flowProvider;
        readonly MqttConfiguration _configuration;
        readonly ReplaySubject<IPacket> _packets;
        IDisposable _listenerDisposable;
        bool _disposed;
        string _clientId = string.Empty;

        public ClientPacketListener( IMqttChannel<IPacket> channel,
            IProtocolFlowProvider flowProvider,
            MqttConfiguration configuration )
        {
            _channel = channel;
            _flowProvider = flowProvider;
            _configuration = configuration;
            _packets = new ReplaySubject<IPacket>( window: TimeSpan.FromSeconds( configuration.WaitTimeoutSecs ) );
        }

        public IObservable<IPacket> PacketStream => _packets;

        public void Listen()
        {
            if( _disposed )
            {
                throw new ObjectDisposedException( GetType().FullName );
            }

            _listenerDisposable = new CompositeDisposable(
                ListenFirstPacket(),
                ListenNextPackets(),
                ListenCompletionAndErrors(),
                ListenSentConnectPacket(),
                ListenSentDisconnectPacket() );
        }

        public void Dispose()
        {
            Dispose( disposing: true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( bool disposing )
        {
            if( _disposed ) return;

            if( disposing )
            {
                _tracer.Info( Properties.Mqtt_Disposing, GetType().FullName );

                _listenerDisposable.Dispose();
                StopKeepAliveMonitor();
                _packets.OnCompleted();
                _disposed = true;
            }
        }

        IDisposable ListenFirstPacket()
        {
            return _channel
                .ReceiverStream
                .FirstOrDefaultAsync()
                .Subscribe( async packet =>
                {
                    if( packet == default( IPacket ) )
                    {
                        return;
                    }

                    _tracer.Info( Properties.ClientPacketListener_FirstPacketReceived, _clientId, packet.Type );

                    var connectAck = packet as ConnectAck;

                    if( connectAck == null )
                    {
                        NotifyError( Properties.ClientPacketListener_FirstReceivedPacketMustBeConnectAck );
                        return;
                    }

                    if( _configuration.KeepAliveSecs > 0 )
                    {
                        StartKeepAliveMonitor();
                    }

                    await DispatchPacketAsync( packet )
                        .ConfigureAwait( continueOnCapturedContext: false );
                }, ex =>
                {
                    NotifyError( ex );
                } );
        }

        IDisposable ListenNextPackets()
        {
            return _channel
                .ReceiverStream
                .Skip( 1 )
                .Subscribe( async packet =>
                {
                    await DispatchPacketAsync( packet )
                        .ConfigureAwait( continueOnCapturedContext: false );
                }, ex =>
                {
                    NotifyError( ex );
                } );
        }

        IDisposable ListenCompletionAndErrors()
        {
            return _channel
                .ReceiverStream
                .Subscribe( _ => { },
                    ex =>
                    {
                        NotifyError( ex );
                    }, () =>
                    {
                        _tracer.Warn( Properties.ClientPacketListener_PacketChannelCompleted, _clientId );

                        _packets.OnCompleted();
                    }
                );
        }

        IDisposable ListenSentConnectPacket()
        {
            return _channel
                .SenderStream
                .OfType<Connect>()
                .FirstAsync()
                .Subscribe( connect =>
                {
                    _clientId = connect.ClientId;
                } );
        }

        IDisposable ListenSentDisconnectPacket()
        {
            return _channel.SenderStream
                .OfType<Disconnect>()
                .FirstAsync()
                .ObserveOn( NewThreadScheduler.Default )
                .Subscribe( disconnect =>
                {
                    if( _configuration.KeepAliveSecs > 0 )
                    {
                        StopKeepAliveMonitor();
                    }
                } );
        }

        void StartKeepAliveMonitor()
        {
            _keepAliveMonitor = _channel.SenderStream.Buffer( new TimeSpan( 0, 0, _configuration.KeepAliveSecs ), 1 )
                .Where( p => p.Count == 0 )
                .Subscribe( p =>
                {
                    try
                    {
                        _tracer.Warn( Properties.ClientPacketListener_SendingKeepAlive, _clientId, _configuration.KeepAliveSecs );

                        var ping = new PingRequest();

                        _channel.SendAsync( ping );
                    }
                    catch( Exception ex )
                    {
                        NotifyError( ex );
                    }
                } );
        }

        void StopKeepAliveMonitor() => _keepAliveMonitor?.Dispose();

        async Task DispatchPacketAsync( IPacket packet )
        {
            var flow = _flowProvider.GetFlow( packet.Type );

            if( flow != null )
            {
                try
                {
                    _packets.OnNext( packet );

                    await Task.Run( async () =>
                    {
                        var publish = packet as Publish;

                        if( publish == null )
                        {
                            _tracer.Info( Properties.ClientPacketListener_DispatchingMessage, _clientId, packet.Type, flow.GetType().Name );
                        }
                        else
                        {
                            _tracer.Info( Properties.ClientPacketListener_DispatchingPublish, _clientId, flow.GetType().Name, publish.Topic );
                        }

                        await flow.ExecuteAsync( _clientId, packet, _channel )
                            .ConfigureAwait( continueOnCapturedContext: false );
                    } )
                    .ConfigureAwait( continueOnCapturedContext: false );
                }
                catch( Exception ex )
                {
                    NotifyError( ex );
                }
            }
        }

        void NotifyError( Exception exception )
        {
            _tracer.Error( exception, Properties.ClientPacketListener_Error );

            _packets.OnError( exception );
        }

        void NotifyError( string message )
        {
            NotifyError( new MqttException( message ) );
        }

        void NotifyError( string message, Exception exception )
        {
            NotifyError( new MqttException( message, exception ) );
        }
    }
}
