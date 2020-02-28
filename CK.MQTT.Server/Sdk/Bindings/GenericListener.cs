using CK.MQTT.Sdk;
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace CK.MQTT.Sdk.Bindings
{
    public class GenericListener<TChannel> : IMqttChannelListener
        where TChannel : IMqttChannel<byte[]>
    {
        static readonly ITracer _tracer = Tracer.Get<GenericListener<TChannel>>();

        readonly MqttConfiguration _configuration;
        /// <summary>
        /// A lazy initialized listener. Start the listener at the initialisation.
        /// </summary>
        readonly Lazy<IListener<TChannel>> _listener;
        bool _disposed;

        public GenericListener( MqttConfiguration configuration, Func<MqttConfiguration, IListener<TChannel>> listenerFactory )
        {
            _configuration = configuration;
            _listener = new Lazy<IListener<TChannel>>( () =>
            {
                IListener<TChannel> tcpListener = listenerFactory( _configuration );

                try
                {
                    tcpListener.Start();
                }
                catch( SocketException socketEx )
                {
                    _tracer.Error( socketEx, ClientProperties.TcpChannelProvider_TcpListener_Failed );

                    throw new MqttException( ClientProperties.TcpChannelProvider_TcpListener_Failed, socketEx );
                }

                return tcpListener;
            } );
        }

        public IObservable<IMqttChannel<byte[]>> GetChannelStream()
        {
            if( _disposed )
            {
                throw new ObjectDisposedException( GetType().FullName );
            }

            return Observable
                .FromAsync( SafeAcceptClient )
                .Repeat()
                .Select( client => (IMqttChannel<byte[]>)client );
        }
        async Task<TChannel> SafeAcceptClient()
        {
            while( true )
            {
                try
                {
                    return await _listener.Value.AcceptClientAsync();
                }
                catch( Exception e )
                {
                    _tracer.Warn( e, "Error while trying to accept a client." );
                }
            }
        }

        public void Dispose()
        {
            if( _disposed ) return;

            _listener.Value.Stop();
            _disposed = true;
        }
    }
}