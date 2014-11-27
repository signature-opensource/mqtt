﻿using System.Collections.Generic;
using System.Linq;
using Hermes.Packets;
using Hermes.Properties;

namespace Hermes
{
	public class ConnectionProvider : IConnectionProvider
	{
		//TODO: We should control concurrency in this list (ConcurrentDictionary is not available on PCL's)
		static readonly IDictionary<string, IChannel<IPacket>> connections = new Dictionary<string, IChannel<IPacket>> ();

		public int Connections { get { return connections.Count; } }

		public bool IsConnected (string clientId)
		{
			var connection = connections.FirstOrDefault (c => c.Key == clientId);

			return !connection.Equals(default(KeyValuePair<string, IChannel<IPacket>>))
				&& connection.Value.IsConnected;
		}

		public void AddConnection(string clientId, IChannel<IPacket> connection)
        {
			var existingConnection = connections.FirstOrDefault (c => c.Key == clientId);

			if (!existingConnection.Equals(default(KeyValuePair<string, IChannel<IPacket>>))) {
				this.RemoveConnection (clientId);
				existingConnection.Value.Close ();
			}

			connections.Add (clientId, connection);
        }

		/// <exception cref="ProtocolException">ProtocolException</exception>
		public IChannel<IPacket> GetConnection (string clientId)
		{
			if (!this.IsConnected(clientId)){
				var error = string.Format (Resources.ClientManager_ClientIdNotFound, clientId);
				
				throw new ProtocolException (error);
			}

			var clientConnection = connections.First (c => c.Key == clientId);

			return clientConnection.Value;

		}

		/// <exception cref="ProtocolException">ProtocolException</exception>
        public void RemoveConnection(string clientId)
        {
            if (!this.IsConnected(clientId)){
				var error = string.Format (Resources.ClientManager_ClientIdNotFound, clientId);
				
				throw new ProtocolException (error);
			}

			connections.Remove (clientId);
        }
	}
}
