﻿using System.Threading.Tasks;
using Hermes.Packets;
using Hermes.Storage;

namespace Hermes.Flows
{
	public class ServerConnectFlow : IProtocolFlow
	{
		readonly IRepository<ClientSession> sessionRepository;
		readonly IRepository<ConnectionWill> willRepository;
		readonly IRepository<PacketIdentifier> packetIdentifierRepository;
		readonly IPublishSenderFlow senderFlow;

		public ServerConnectFlow (IRepository<ClientSession> sessionRepository, 
			IRepository<ConnectionWill> willRepository,
			IRepository<PacketIdentifier> packetIdentifierRepository,
			IPublishSenderFlow senderFlow)
		{
			this.sessionRepository = sessionRepository;
			this.willRepository = willRepository;
			this.packetIdentifierRepository = packetIdentifierRepository;
			this.senderFlow = senderFlow;
		}

		public async Task ExecuteAsync (string clientId, IPacket input, IChannel<IPacket> channel)
		{
			if (input.Type != PacketType.Connect)
				return;

			var connect = input as Connect;
			var session = this.sessionRepository.Get (s => s.ClientId == clientId);
			var sessionPresent = connect.CleanSession ? false : session != null;

			if (connect.CleanSession && session != null) {
				this.sessionRepository.Delete(session);
				session = null;
			}

			if (session == null) {
				session = new ClientSession { ClientId = clientId, Clean = connect.CleanSession };

				this.sessionRepository.Create (session);
			} else {
				await this.SendSavedMessagesAsync (session, channel);
				await this.SendPendingMessagesAsync (session, channel);
				await this.SendPendingAcknowledgementsAsync (session, channel);
			}

			if (connect.Will != null) {
				var connectionWill = new ConnectionWill { ClientId = clientId, Will = connect.Will };

				this.willRepository.Create (connectionWill);
			}

			await channel.SendAsync(new ConnectAck (ConnectionStatus.Accepted, sessionPresent));
		}

		private async Task SendSavedMessagesAsync(ClientSession session, IChannel<IPacket> channel)
		{
			foreach (var savedMessage in session.SavedMessages) {
				var publish = new Publish(savedMessage.Topic, savedMessage.QualityOfService, 
					retain: false, duplicated: false, packetId: savedMessage.PacketId);

				await this.senderFlow.SendPublishAsync (session.ClientId, publish);
			}

			session.SavedMessages.Clear ();

			this.sessionRepository.Update (session);
		}

		private async Task SendPendingMessagesAsync(ClientSession session, IChannel<IPacket> channel)
		{
			foreach (var pendingMessage in session.PendingMessages) {
				var publish = new Publish(pendingMessage.Topic, pendingMessage.QualityOfService, 
					pendingMessage.Retain, pendingMessage.Duplicated, pendingMessage.PacketId);

				await this.senderFlow.SendPublishAsync (session.ClientId, publish, isPending: true);
			}
		}

		private async Task SendPendingAcknowledgementsAsync(ClientSession session, IChannel<IPacket> channel)
		{
			foreach (var pendingAcknowledgement in session.PendingAcknowledgements) {
				var ack = default(IFlowPacket);

				if (pendingAcknowledgement.Type == PacketType.PublishReceived)
					ack = new PublishReceived (pendingAcknowledgement.PacketId);
				else if(pendingAcknowledgement.Type == PacketType.PublishRelease)
					ack = new PublishRelease (pendingAcknowledgement.PacketId);

				await this.senderFlow.SendAckAsync (session.ClientId, ack, isPending: true);
			}
		}
	}
}
