using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using CK.MQTT;
using CK.MQTT.Sdk;
using CK.MQTT.Sdk.Flows;
using CK.MQTT.Sdk.Packets;
using CK.MQTT.Sdk.Storage;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using static CK.Testing.MonitorTestHelper;
using CK.Core;

namespace Tests.Flows
{
    public class ConnectFlowSpec
    {
        [Test]
        public async Task when_sending_connect_then_session_is_created_and_ack_is_sent()
        {
            var authenticationProvider = Mock.Of<IMqttAuthenticationProvider>( p => p.Authenticate( It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>() ) == true );
            var sessionRepository = new Mock<IRepository<ClientSession>>();
            var willRepository = new Mock<IRepository<ConnectionWill>>();
            var senderFlow = new Mock<IPublishSenderFlow>();

            var clientId = Guid.NewGuid().ToString();
            var connect = new Connect( clientId, cleanSession: true );
            var channel = new Mock<IMqttChannel<IPacket>>();
            var sentPacket = default( IPacket );

            channel.Setup( c => c.SendAsync( TestHelper.Monitor, It.IsAny<IPacket>() ) )
                .Callback<IPacket>( packet => sentPacket = packet )
                .Returns( Task.Delay( 0 ) );

            var connectionProvider = new Mock<IConnectionProvider>();

            connectionProvider
                .Setup( p => p.GetConnection(TestHelper.Monitor, It.Is<string>( c => c == clientId ) ) )
                .Returns( channel.Object );

            var flow = new ServerConnectFlow( authenticationProvider, sessionRepository.Object, willRepository.Object, senderFlow.Object );

            await flow.ExecuteAsync( TestHelper.Monitor, clientId, connect, channel.Object )
                .ConfigureAwait( continueOnCapturedContext: false );

            sessionRepository.Verify( r => r.Create( It.Is<ClientSession>( s => s.Id == clientId && s.Clean == true ) ) );
            sessionRepository.Verify( r => r.Delete( It.IsAny<string>() ), Times.Never );
            willRepository.Verify( r => r.Create( It.IsAny<ConnectionWill>() ), Times.Never );

            Assert.NotNull( sentPacket );

            var connectAck = sentPacket as ConnectAck;

            Assert.NotNull( connectAck );
            connectAck.Type.Should().Be( MqttPacketType.ConnectAck );
            connectAck.Status.Should().Be( MqttConnectionStatus.Accepted );
            connectAck.SessionPresent.Should().BeFalse();
        }

        [Test]
        public async Task when_sending_connect_with_existing_session_and_without_clean_session_then_session_is_not_deleted_and_ack_is_sent_with_session_present()
        {
            var authenticationProvider = Mock.Of<IMqttAuthenticationProvider>( p => p.Authenticate( It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>() ) == true );
            var sessionRepository = new Mock<IRepository<ClientSession>>();
            var willRepository = new Mock<IRepository<ConnectionWill>>();

            var clientId = Guid.NewGuid().ToString();
            var existingSession = new ClientSession( clientId, clean: false );

            sessionRepository
                .Setup( r => r.Read( It.IsAny<string>() ) )
                .Returns( existingSession );

            var senderFlow = new Mock<IPublishSenderFlow>();

            var connect = new Connect( clientId, cleanSession: false );
            var channel = new Mock<IMqttChannel<IPacket>>();
            var sentPacket = default( IPacket );

            channel.Setup( c => c.SendAsync( TestHelper.Monitor, It.IsAny<IPacket>() ) )
                .Callback<IPacket>( packet => sentPacket = packet )
                .Returns( Task.Delay( 0 ) );

            var connectionProvider = new Mock<IConnectionProvider>();

            connectionProvider
                .Setup( p => p.GetConnection( TestHelper.Monitor, It.Is<string>( c => c == clientId ) ) )
                .Returns( channel.Object );

            var flow = new ServerConnectFlow( authenticationProvider, sessionRepository.Object, willRepository.Object, senderFlow.Object );

            await flow.ExecuteAsync( TestHelper.Monitor, clientId, connect, channel.Object )
                .ConfigureAwait( continueOnCapturedContext: false );

            sessionRepository.Verify( r => r.Create( It.IsAny<ClientSession>() ), Times.Never );
            sessionRepository.Verify( r => r.Delete( It.IsAny<string>() ), Times.Never );
            willRepository.Verify( r => r.Create( It.IsAny<ConnectionWill>() ), Times.Never );

            var connectAck = sentPacket as ConnectAck;

            Assert.NotNull( connectAck );
            connectAck.Type.Should().Be( MqttPacketType.ConnectAck );
            connectAck.Status.Should().Be( MqttConnectionStatus.Accepted );

            connectAck.SessionPresent.Should().BeTrue();
        }

        [Test]
        public async Task when_sending_connect_with_existing_session_and_clean_session_then_session_is_deleted_and_ack_is_sent_with_session_present()
        {
            var authenticationProvider = Mock.Of<IMqttAuthenticationProvider>( p => p.Authenticate( It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>() ) == true );
            var sessionRepository = new Mock<IRepository<ClientSession>>();
            var willRepository = new Mock<IRepository<ConnectionWill>>();

            var clientId = Guid.NewGuid().ToString();
            var existingSession = new ClientSession( clientId, clean: true );

            sessionRepository
                .Setup( r => r.Read( It.IsAny<string>() ) )
                .Returns( existingSession );

            var senderFlow = new Mock<IPublishSenderFlow>();

            var connect = new Connect( clientId, cleanSession: true );
            var channel = new Mock<IMqttChannel<IPacket>>();
            var sentPacket = default( IPacket );

            channel.Setup( c => c.SendAsync( TestHelper.Monitor, It.IsAny<IPacket>() ) )
                .Callback<IPacket>( packet => sentPacket = packet )
                .Returns( Task.Delay( 0 ) );

            var connectionProvider = new Mock<IConnectionProvider>();

            connectionProvider
                .Setup( p => p.GetConnection( TestHelper.Monitor, It.Is<string>( c => c == clientId ) ) )
                .Returns( channel.Object );

            var flow = new ServerConnectFlow( authenticationProvider, sessionRepository.Object, willRepository.Object, senderFlow.Object );

            await flow.ExecuteAsync(TestHelper.Monitor, clientId, connect, channel.Object )
                .ConfigureAwait( continueOnCapturedContext: false );

            var connectAck = sentPacket as ConnectAck;

            sessionRepository.Verify( r => r.Delete( It.Is<string>( s => s == existingSession.Id ) ) );
            sessionRepository.Verify( r => r.Create( It.Is<ClientSession>( s => s.Clean == true ) ) );
            willRepository.Verify( r => r.Create( It.IsAny<ConnectionWill>() ), Times.Never );

            Assert.NotNull( connectAck );
            connectAck.Type.Should().Be( MqttPacketType.ConnectAck );
            connectAck.Status.Should().Be( MqttConnectionStatus.Accepted );
            Assert.False( connectAck.SessionPresent );
        }

        [Test]
        public async Task when_sending_connect_without_existing_session_and_without_clean_session_then_ack_is_sent_with_no_session_present()
        {
            var authenticationProvider = Mock.Of<IMqttAuthenticationProvider>( p => p.Authenticate( It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>() ) == true );
            var sessionRepository = new Mock<IRepository<ClientSession>>();
            var willRepository = new Mock<IRepository<ConnectionWill>>();

            var clientId = Guid.NewGuid().ToString();

            sessionRepository
                .Setup( r => r.Read( It.IsAny<string>() ) )
                .Returns( default( ClientSession ) );

            var senderFlow = new Mock<IPublishSenderFlow>();

            var connect = new Connect( clientId, cleanSession: false );
            var channel = new Mock<IMqttChannel<IPacket>>();
            var sentPacket = default( IPacket );

            channel.Setup( c => c.SendAsync( TestHelper.Monitor, It.IsAny<IPacket>() ) )
                .Callback<IPacket>( packet => sentPacket = packet )
                .Returns( Task.Delay( 0 ) );

            var connectionProvider = new Mock<IConnectionProvider>();

            connectionProvider
                .Setup( p => p.GetConnection( TestHelper.Monitor, It.Is<string>( c => c == clientId ) ) )
                .Returns( channel.Object );

            var flow = new ServerConnectFlow( authenticationProvider, sessionRepository.Object, willRepository.Object, senderFlow.Object );

            await flow.ExecuteAsync(TestHelper.Monitor, clientId, connect, channel.Object )
                .ConfigureAwait( continueOnCapturedContext: false );

            var connectAck = sentPacket as ConnectAck;

            Assert.False( connectAck.SessionPresent );
        }

        [Test]
        public async Task when_sending_connect_with_will_then_will_is_created_and_ack_is_sent()
        {
            var authenticationProvider = Mock.Of<IMqttAuthenticationProvider>( p => p.Authenticate( It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>() ) == true );
            var sessionRepository = new Mock<IRepository<ClientSession>>();
            var willRepository = new Mock<IRepository<ConnectionWill>>();

            var senderFlow = new Mock<IPublishSenderFlow>();

            var clientId = Guid.NewGuid().ToString();
            var connect = new Connect( clientId, cleanSession: true );

            var willMessage = new FooWillMessage { Message = "Foo Will Message" };
            var will = new MqttLastWill( "foo/bar", MqttQualityOfService.AtLeastOnce, retain: true, payload: willMessage.GetPayload() );

            connect.Will = will;

            var channel = new Mock<IMqttChannel<IPacket>>();
            var sentPacket = default( IPacket );

            channel.Setup( c => c.SendAsync( TestHelper.Monitor, It.IsAny<IPacket>() ) )
                .Callback<IPacket>( packet => sentPacket = packet )
                .Returns( Task.Delay( 0 ) );

            var connectionProvider = new Mock<IConnectionProvider>();

            connectionProvider
                .Setup( p => p.GetConnection( TestHelper.Monitor, It.Is<string>( c => c == clientId ) ) )
                .Returns( channel.Object );

            var flow = new ServerConnectFlow( authenticationProvider, sessionRepository.Object, willRepository.Object, senderFlow.Object );

            await flow.ExecuteAsync(TestHelper.Monitor, clientId, connect, channel.Object )
                .ConfigureAwait( continueOnCapturedContext: false );

            var connectAck = sentPacket as ConnectAck;

            sessionRepository.Verify( r => r.Delete( It.IsAny<string>() ), Times.Never );
            sessionRepository.Verify( r => r.Create( It.Is<ClientSession>( s => s.Id == clientId && s.Clean == true ) ) );
            willRepository.Verify( r => r.Create( It.Is<ConnectionWill>( w => w.Id == clientId && w.Will == will ) ) );

            Assert.NotNull( connectAck );
            connectAck.Type.Should().Be( MqttPacketType.ConnectAck );
            connectAck.Status.Should().Be( MqttConnectionStatus.Accepted );
            Assert.False( connectAck.SessionPresent );
        }

        [Test]
        public void when_sending_connect_with_invalid_user_credentials_then_connection_exception_is_thrown()
        {
            var authenticationProvider = Mock.Of<IMqttAuthenticationProvider>( p => p.Authenticate( It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>() ) == false );
            var sessionRepository = new Mock<IRepository<ClientSession>>();
            var willRepository = new Mock<IRepository<ConnectionWill>>();
            var senderFlow = new Mock<IPublishSenderFlow>();

            var clientId = Guid.NewGuid().ToString();
            var connect = new Connect( clientId, cleanSession: true );
            var channel = new Mock<IMqttChannel<IPacket>>();
            var sentPacket = default( IPacket );

            channel.Setup( c => c.SendAsync( TestHelper.Monitor, It.IsAny<IPacket>() ) )
                .Callback<IPacket>( packet => sentPacket = packet )
                .Returns( Task.Delay( 0 ) );

            var connectionProvider = new Mock<IConnectionProvider>();

            connectionProvider
                .Setup( p => p.GetConnection( TestHelper.Monitor, It.Is<string>( c => c == clientId ) ) )
                .Returns( channel.Object );

            var flow = new ServerConnectFlow( authenticationProvider, sessionRepository.Object, willRepository.Object, senderFlow.Object );

            var aggregateEx = Assert.Throws<AggregateException>( () => flow.ExecuteAsync(TestHelper.Monitor, clientId, connect, channel.Object ).Wait() );

            Assert.NotNull( aggregateEx.InnerException );
            Assert.True( aggregateEx.InnerException is MqttConnectionException );
            ((MqttConnectionException)aggregateEx.InnerException).ReturnCode.Should()
                .Be( MqttConnectionStatus.BadUserNameOrPassword );
        }

        [Test]
        public async Task when_sending_connect_with_existing_session_and_without_clean_session_then_pending_messages_and_acks_are_sent()
        {
            var authenticationProvider = Mock.Of<IMqttAuthenticationProvider>( p => p.Authenticate( It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>() ) == true );
            var sessionRepository = new Mock<IRepository<ClientSession>>();
            var willRepository = new Mock<IRepository<ConnectionWill>>();

            var clientId = Guid.NewGuid().ToString();
            var existingSession = new ClientSession( clientId, clean: false );

            var topic = "foo/bar";
            var payload = new byte[10];
            var qos = MqttQualityOfService.ExactlyOnce;
            var packetId = (ushort)10;

            existingSession.PendingMessages = new List<PendingMessage>
            {
                new PendingMessage {
                    Status = PendingMessageStatus.PendingToSend,
                    Topic = topic,
                    QualityOfService = qos,
                    Retain = false,
                    Duplicated = false,
                    PacketId = packetId,
                    Payload = payload
                },
                new PendingMessage {
                    Status = PendingMessageStatus.PendingToAcknowledge,
                    Topic = topic,
                    QualityOfService = qos,
                    Retain = false,
                    Duplicated = false,
                    PacketId = packetId,
                    Payload = payload
                }
            };

            existingSession.PendingAcknowledgements = new List<PendingAcknowledgement>
            {
                new PendingAcknowledgement { Type = MqttPacketType.PublishReceived, PacketId = packetId }
            };

            sessionRepository
                .Setup( r => r.Read( It.IsAny<string>() ) )
                .Returns( existingSession );

            var senderFlow = new Mock<IPublishSenderFlow>();

            senderFlow
                .Setup( f => f.SendPublishAsync( TestHelper.Monitor, It.IsAny<string>(), It.IsAny<Publish>(), It.IsAny<IMqttChannel<IPacket>>(), It.IsAny<PendingMessageStatus>() ) )
                .Callback<string, Publish, IMqttChannel<IPacket>, PendingMessageStatus>( async ( id, pub, ch, stat ) =>
                 {
                     await ch.SendAsync( TestHelper.Monitor, pub );
                 } )
                .Returns( Task.Delay( 0 ) );

            senderFlow
                .Setup( f => f.SendAckAsync( TestHelper.Monitor, It.IsAny<string>(), It.IsAny<IFlowPacket>(), It.IsAny<IMqttChannel<IPacket>>(), It.IsAny<PendingMessageStatus>() ) )
                .Callback<string, IFlowPacket, IMqttChannel<IPacket>, PendingMessageStatus>( async ( id, pack, ch, stat ) =>
                 {
                     await ch.SendAsync( TestHelper.Monitor, pack );
                 } )
                .Returns( Task.Delay( 0 ) );

            var connect = new Connect( clientId, cleanSession: false );
            var channel = new Mock<IMqttChannel<IPacket>>();
            var firstPacket = default( IPacket );
            var nextPackets = new List<IPacket>();

            channel
                .Setup( c => c.SendAsync( TestHelper.Monitor, It.IsAny<IPacket>() ) )
                .Callback<IPacket>( packet =>
                 {
                     if( firstPacket == default( IPacket ) )
                     {
                         firstPacket = packet;
                     }
                     else
                     {
                         nextPackets.Add( packet );
                     }
                 } )
                .Returns( Task.Delay( 0 ) );

            var connectionProvider = new Mock<IConnectionProvider>();

            connectionProvider
                .Setup( p => p.GetConnection( TestHelper.Monitor, It.Is<string>( c => c == clientId ) ) )
                .Returns( channel.Object );

            var flow = new ServerConnectFlow( authenticationProvider, sessionRepository.Object, willRepository.Object, senderFlow.Object );

            await flow.ExecuteAsync( TestHelper.Monitor, clientId, connect, channel.Object )
                .ConfigureAwait( continueOnCapturedContext: false );

            sessionRepository.Verify( r => r.Create( It.IsAny<ClientSession>() ), Times.Never );
            sessionRepository.Verify( r => r.Delete( It.IsAny<string>() ), Times.Never );
            sessionRepository.Verify( r => r.Update( It.IsAny<ClientSession>() ), Times.Once );
            willRepository.Verify( r => r.Create( It.IsAny<ConnectionWill>() ), Times.Never );
            senderFlow.Verify( f => f.SendPublishAsync( TestHelper.Monitor, It.Is<string>( x => x == existingSession.Id ),
                 It.Is<Publish>( x => x.Topic == topic && x.QualityOfService == qos && x.PacketId == packetId ),
                 It.IsAny<IMqttChannel<IPacket>>(),
                 It.IsAny<PendingMessageStatus>() ), Times.Exactly( 2 ) );
            senderFlow.Verify( f => f.SendAckAsync( TestHelper.Monitor, It.Is<string>( x => x == existingSession.Id ),
                 It.Is<IFlowPacket>( x => x.Type == MqttPacketType.PublishReceived && x.PacketId == packetId ),
                 It.IsAny<IMqttChannel<IPacket>>(),
                 It.Is<PendingMessageStatus>( x => x == PendingMessageStatus.PendingToAcknowledge ) ), Times.Once );

            var connectAck = firstPacket as ConnectAck;

            Assert.True( connectAck != null, "The first packet sent by the Server must be a CONNACK" );
            connectAck.Type.Should().Be( MqttPacketType.ConnectAck );
            connectAck.Status.Should().Be( MqttConnectionStatus.Accepted );

            Assert.True( connectAck.SessionPresent );
            nextPackets.Count.Should().Be( 3 );

            Assert.False( nextPackets.Any( x => x is ConnectAck ) );
        }
    }
}
