using CK.MQTT.Sdk.Flows;
using CK.MQTT.Sdk.Packets;
using System;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace CK.MQTT.Sdk
{
    internal class ClientPacketListener : IPacketListener
    {
        static readonly ITracer _tracer = Tracer.Get<ClientPacketListener>();

        readonly IMqttChannel<IPacket> _channel;
        readonly IProtocolFlowProvider _flowProvider;
        readonly MqttConfiguration _configuration;
        readonly ReplaySubject<IPacket> _packets;
        readonly TaskRunner _flowRunner;
        IDisposable _listenerDisposable;
        bool _disposed;
        string _clientId = string.Empty;
        Timer _keepAliveTimer;

        public ClientPacketListener( IMqttChannel<IPacket> channel,
            IProtocolFlowProvider flowProvider,
            MqttConfiguration configuration )
        {
            _channel = channel;
            _flowProvider = flowProvider;
            _configuration = configuration;
            _packets = new ReplaySubject<IPacket>( window: TimeSpan.FromSeconds( configuration.WaitTimeoutSecs ) );
            _flowRunner = TaskRunner.Get();
        }

        public IObservable<IPacket> PacketStream { get { return _packets; } }

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
            if( _disposed )
            {
                return;
            }

            _tracer.Info( Properties.Resources.GetString( "Mqtt_Disposing" ), GetType().FullName );

            _listenerDisposable.Dispose();
            StopKeepAliveMonitor();
            _packets.OnCompleted();
            (_flowRunner as IDisposable)?.Dispose();
            _disposed = true;
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

                    _tracer.Info( Properties.Resources.GetString( "ClientPacketListener_FirstPacketReceived" ), _clientId, packet.Type );

                    ConnectAck connectAck = packet as ConnectAck;

                    if( connectAck == null )
                    {
                        NotifyError( Properties.Resources.GetString( "ClientPacketListener_FirstReceivedPacketMustBeConnectAck" ) );
                        return;
                    }

                    if( _configuration.KeepAliveSecs > 0 )
                    {
                        StartKeepAliveMonitor();
                    }

                    await DispatchPacketAsync( packet );
                }, ex =>
                {
                    NotifyError( ex );
                } );
        }

        IDisposable ListenNextPackets()
            => _channel
                .ReceiverStream
                .Skip( 1 )
                .Subscribe(
                    async packet => await DispatchPacketAsync( packet )
                    , ex => NotifyError( ex )
                );

        IDisposable ListenCompletionAndErrors()
            => _channel
                .ReceiverStream
                .Subscribe( _ => { },
                    ex => NotifyError( ex )
                    , () =>
                    {
                        _tracer.Warn( Properties.Resources.GetString( "ClientPacketListener_PacketChannelCompleted" ), _clientId );
                        _packets.OnCompleted();
                    }
                );

        IDisposable ListenSentConnectPacket()
            => _channel
                .SenderStream
                .OfType<Connect>()
                .FirstAsync()
                .Subscribe( connect => _clientId = connect.ClientId );

        IDisposable ListenSentDisconnectPacket()
            => _channel.SenderStream
                .OfType<Disconnect>()
                .FirstAsync()
                .ObserveOn( NewThreadScheduler.Default )
                .Subscribe( disconnect =>
                {
                    if( _configuration.KeepAliveSecs > 0 ) StopKeepAliveMonitor();
                } );

        void StartKeepAliveMonitor()
        {
            int interval = _configuration.KeepAliveSecs * 1000;

            _keepAliveTimer = new Timer
            {
                AutoReset = true,
                IntervalMillisecs = interval
            };
            _keepAliveTimer.Elapsed += async ( sender, e ) =>
            {
                try
                {
                    _tracer.Warn( Properties.Resources.GetString( "ClientPacketListener_SendingKeepAlive" ), _clientId, _configuration.KeepAliveSecs );

                    PingRequest ping = new PingRequest();

                    await _channel.SendAsync( ping );
                }
                catch( Exception ex )
                {
                    NotifyError( ex );
                }
            };
            _keepAliveTimer.Start();

            _channel.SenderStream.Subscribe( p =>
            {
                _keepAliveTimer.IntervalMillisecs = interval;
            } );
        }

        void StopKeepAliveMonitor()
        {
            if( _keepAliveTimer != null )
            {
                _keepAliveTimer.Stop();
            }
        }

        async Task DispatchPacketAsync( IPacket packet )
        {
            IProtocolFlow flow = _flowProvider.GetFlow( packet.Type );

            if( flow != null )
            {
                try
                {
                    _packets.OnNext( packet );

                    await _flowRunner.Run( async () =>
                    {
                        Publish publish = packet as Publish;

                        if( publish == null )
                        {
                            _tracer.Info( Properties.Resources.GetString( "ClientPacketListener_DispatchingMessage" ), _clientId, packet.Type, flow.GetType().Name );
                        }
                        else
                        {
                            _tracer.Info( Properties.Resources.GetString( "ClientPacketListener_DispatchingPublish" ), _clientId, flow.GetType().Name, publish.Topic );
                        }

                        await flow.ExecuteAsync( _clientId, packet, _channel );
                    } );
                }
                catch( Exception ex )
                {
                    NotifyError( ex );
                }
            }
        }

        void NotifyError( Exception exception )
        {
            _tracer.Error( exception, Properties.Resources.GetString( "ClientPacketListener_Error" ) );

            _packets.OnError( exception );
        }

        void NotifyError( string message )
        {
            NotifyError( new MqttException( message ) );
        }
    }
}
