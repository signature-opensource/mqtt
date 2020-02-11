using CK.MQTT.Sdk.Packets;
using CK.MQTT.Sdk.Storage;
using System.Linq;
using System.Threading.Tasks;

namespace CK.MQTT.Sdk.Flows
{
    internal class PublishReceiverFlow : PublishFlow
    {
        protected readonly IMqttTopicEvaluator topicEvaluator;
        protected readonly IRepository<RetainedMessage> retainedRepository;

        public PublishReceiverFlow( IMqttTopicEvaluator topicEvaluator,
            IRepository<RetainedMessage> retainedRepository,
            IRepository<ClientSession> sessionRepository,
            MqttConfiguration configuration )
            : base( sessionRepository, configuration )
        {
            this.topicEvaluator = topicEvaluator;
            this.retainedRepository = retainedRepository;
        }

        public override async Task ExecuteAsync( string clientId, IPacket input, IMqttChannel<IPacket> channel )
        {
            if( input.Type == MqttPacketType.Publish )
            {
                Publish publish = input as Publish;

                await HandlePublishAsync( clientId, publish, channel );
            }
            else if( input.Type == MqttPacketType.PublishRelease )
            {
                PublishRelease publishRelease = input as PublishRelease;

                await HandlePublishReleaseAsync( clientId, publishRelease, channel );
            }
        }

        protected virtual Task ProcessPublishAsync( Publish publish, string clientId )
        {
            return Task.Delay( 0 );
        }

        protected virtual void Validate( Publish publish, string clientId )
        {
            if( publish.QualityOfService != MqttQualityOfService.AtMostOnce && !publish.PacketId.HasValue )
            {
                throw new MqttException( Properties.Resources.GetString( "PublishReceiverFlow_PacketIdRequired" ) );
            }

            if( publish.QualityOfService == MqttQualityOfService.AtMostOnce && publish.PacketId.HasValue )
            {
                throw new MqttException( Properties.Resources.GetString( "PublishReceiverFlow_PacketIdNotAllowed" ) );
            }
        }

        async Task HandlePublishAsync( string clientId, Publish publish, IMqttChannel<IPacket> channel )
        {
            Validate( publish, clientId );

            MqttQualityOfService qos = configuration.GetSupportedQos( publish.QualityOfService );
            ClientSession session = sessionRepository.Read( clientId );

            if( session == null )
            {
                throw new MqttException( string.Format( Properties.Resources.GetString( "SessionRepository_ClientSessionNotFound" ), clientId ) );
            }

            if( qos == MqttQualityOfService.ExactlyOnce && session.GetPendingAcknowledgements().Any( ack => ack.Type == MqttPacketType.PublishReceived && ack.PacketId == publish.PacketId.Value ) )
            {
                await SendQosAck( clientId, qos, publish, channel );

                return;
            }

            await SendQosAck( clientId, qos, publish, channel );
            await ProcessPublishAsync( publish, clientId );
        }

        async Task HandlePublishReleaseAsync( string clientId, PublishRelease publishRelease, IMqttChannel<IPacket> channel )
        {
            RemovePendingAcknowledgement( clientId, publishRelease.PacketId, MqttPacketType.PublishReceived );

            await SendAckAsync( clientId, new PublishComplete( publishRelease.PacketId ), channel );
        }

        async Task SendQosAck( string clientId, MqttQualityOfService qos, Publish publish, IMqttChannel<IPacket> channel )
        {
            if( qos == MqttQualityOfService.AtMostOnce )
            {
                return;
            }
            else if( qos == MqttQualityOfService.AtLeastOnce )
            {
                await SendAckAsync( clientId, new PublishAck( publish.PacketId.Value ), channel );
            }
            else
            {
                await SendAckAsync( clientId, new PublishReceived( publish.PacketId.Value ), channel );
            }
        }
    }
}
