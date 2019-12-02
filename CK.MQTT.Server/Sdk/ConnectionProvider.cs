using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CK.MQTT.Sdk.Packets;

namespace CK.MQTT.Sdk
{
	internal class ConnectionProvider : IConnectionProvider
	{
        static readonly ITracer tracer = Tracer.Get<ConnectionProvider> ();
        static readonly IList<string> privateClients;
        static readonly ConcurrentDictionary<string, IMqttChannel<IPacket>> connections;
        static readonly object lockObject = new object();

		static ConnectionProvider ()
		{
            privateClients = new List<string>();
            connections = new ConcurrentDictionary<string, IMqttChannel<IPacket>> ();
		}

		public int Connections => connections.Count;

		public IEnumerable<string> ActiveClients
		{
			get
			{
				return connections
					.Where (c => c.Value.IsConnected)
					.Select (c => c.Key);
			}
		}

        public IEnumerable<string> PrivateClients => privateClients;

        public void RegisterPrivateClient (string clientId)
        {
            if (privateClients.Contains (clientId)) {
                var message = string.Format (ServerProperties.Resources.GetString("ConnectionProvider_PrivateClientAlreadyRegistered"), clientId);

                throw new MqttServerException (message);
            }

            lock (lockObject) {
                privateClients.Add(clientId);
            }
        }

        public void AddConnection (string clientId, IMqttChannel<IPacket> connection)
		{
			var existingConnection = default (IMqttChannel<IPacket>);

			if (connections.TryGetValue (clientId, out existingConnection)) {
				tracer.Warn (ServerProperties.Resources.GetString("ConnectionProvider_ClientIdExists"), clientId);

				RemoveConnection (clientId);
			}

			connections.TryAdd (clientId, connection);
		}

		public IMqttChannel<IPacket> GetConnection (string clientId)
		{
			var existingConnection = default(IMqttChannel<IPacket>);

			if (connections.TryGetValue (clientId, out existingConnection)) {
				if (!existingConnection.IsConnected) {
					tracer.Warn (ServerProperties.Resources.GetString("ConnectionProvider_ClientDisconnected"), clientId);

					RemoveConnection (clientId);
					existingConnection = default (IMqttChannel<IPacket>);
				}
			}

			return existingConnection;
		}

		public void RemoveConnection (string clientId)
		{
			var existingConnection = default (IMqttChannel<IPacket>);

			if (connections.TryRemove (clientId, out existingConnection)) {
				tracer.Info (ServerProperties.Resources.GetString("ConnectionProvider_RemovingClient"), clientId);

				existingConnection.Dispose ();
			}

            if (privateClients.Contains (clientId))  {
                lock (lockObject) {
                    if (privateClients.Contains (clientId)) {
                        privateClients.Remove (clientId);
                    }
                }
            }
		}
    }
}
