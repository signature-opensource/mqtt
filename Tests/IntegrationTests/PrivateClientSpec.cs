using CK.MQTT;
using FluentAssertions;
using IntegrationTests.Context;
using IntegrationTests.Messages;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace IntegrationTests
{
    public abstract class PrivateClientSpec : IntegrationContext
    {
        [Test]
        public async Task when_creating_in_process_client_then_it_is_already_connected()
        {
            IMqttConnectedClient client = await Server.CreateClientAsync();

            Assert.NotNull( client );
            Assert.True( client.IsConnected );
            Assert.False( string.IsNullOrEmpty( client.Id ) );
            Assert.True( client.Id.StartsWith( "private" ) );

            client.Dispose();
        }

        [Test]
        public async Task when_in_process_client_subscribe_to_topic_then_succeeds()
        {
            IMqttConnectedClient client = await Server.CreateClientAsync();
            string topicFilter = Guid.NewGuid().ToString() + "/#";

            await client.SubscribeAsync( topicFilter, MqttQualityOfService.AtMostOnce );

            Assert.True( client.IsConnected );

            await client.UnsubscribeAsync( topicFilter );

            client.Dispose();
        }

        [Test]
        public async Task when_in_process_client_subscribe_to_system_topic_then_succeeds()
        {
            IMqttConnectedClient client = await Server.CreateClientAsync();
            string topicFilter = "$SYS/" + Guid.NewGuid().ToString() + "/#";

            await client.SubscribeAsync( topicFilter, MqttQualityOfService.AtMostOnce );

            Assert.True( client.IsConnected );

            await client.UnsubscribeAsync( topicFilter );

            client.Dispose();
        }

        [Test]
        public async Task when_in_process_client_publish_messages_then_succeeds()
        {
            IMqttConnectedClient client = await Server.CreateClientAsync();
            string topic = Guid.NewGuid().ToString();
            TestMessage testMessage = new TestMessage
            {
                Name = string.Concat( "Message ", Guid.NewGuid().ToString().Substring( 0, 4 ) ),
                Value = new Random().Next()
            };
            MqttApplicationMessage message = new MqttApplicationMessage( topic, Serializer.Serialize( testMessage ) );

            await client.PublishAsync( message, MqttQualityOfService.AtMostOnce );
            await client.PublishAsync( message, MqttQualityOfService.AtLeastOnce );
            await client.PublishAsync( message, MqttQualityOfService.ExactlyOnce );

            Assert.True( client.IsConnected );

            client.Dispose();
        }

        [Test]
        public async Task when_in_process_client_publish_system_messages_then_succeeds()
        {
            IMqttConnectedClient client = await Server.CreateClientAsync();
            string topic = "$SYS/" + Guid.NewGuid().ToString();
            TestMessage testMessage = new TestMessage
            {
                Name = string.Concat( "Message ", Guid.NewGuid().ToString().Substring( 0, 4 ) ),
                Value = new Random().Next()
            };
            MqttApplicationMessage message = new MqttApplicationMessage( topic, Serializer.Serialize( testMessage ) );

            await client.PublishAsync( message, MqttQualityOfService.AtMostOnce );
            await client.PublishAsync( message, MqttQualityOfService.AtLeastOnce );
            await client.PublishAsync( message, MqttQualityOfService.ExactlyOnce );

            Assert.True( client.IsConnected );

            client.Dispose();
        }

        [Test]
        public async Task when_in_process_client_disconnect_then_succeeds()
        {
            IMqttConnectedClient client = await Server.CreateClientAsync();
            string clientId = client.Id;

            await client.DisconnectAsync();

            Assert.False( Server.ActiveClients.Any( c => c == clientId ) );
            Assert.False( client.IsConnected );
            Assert.True( string.IsNullOrEmpty( client.Id ) );

            client.Dispose();
        }

        [Test]
        public async Task when_in_process_clients_communicate_each_other_then_succeeds()
        {
            IMqttConnectedClient fooClient = await Server.CreateClientAsync();
            IMqttConnectedClient barClient = await Server.CreateClientAsync();
            string fooTopic = "foo/message";

            await fooClient.SubscribeAsync( fooTopic, MqttQualityOfService.ExactlyOnce );

            int messagesReceived = 0;

            fooClient.MessageStream.Subscribe( message =>
            {
                if( message.Topic == fooTopic )
                {
                    messagesReceived++;
                }
            } );

            await barClient.PublishAsync( new MqttApplicationMessage( fooTopic, new byte[255] ), MqttQualityOfService.AtMostOnce );
            await barClient.PublishAsync( new MqttApplicationMessage( fooTopic, new byte[10] ), MqttQualityOfService.AtLeastOnce );
            await barClient.PublishAsync( new MqttApplicationMessage( "other/topic", new byte[500] ), MqttQualityOfService.ExactlyOnce );
            await barClient.PublishAsync( new MqttApplicationMessage( fooTopic, new byte[50] ), MqttQualityOfService.ExactlyOnce );

            await Task.Delay( TimeSpan.FromMilliseconds( 1000 ) );

            Assert.True( fooClient.IsConnected );
            Assert.True( barClient.IsConnected );
            messagesReceived.Should().Be( 3 );

            fooClient.Dispose();
            barClient.Dispose();
        }

        [Test]
        public async Task when_in_process_client_communicate_with_tcp_client_then_succeeds()
        {
            IMqttConnectedClient inProcessClient = await Server.CreateClientAsync();
            IMqttClient remoteClient = await GetClientAsync();

            await remoteClient.ConnectAsync( new MqttClientCredentials( MqttTestHelper.GetClientId() ) );

            string fooTopic = "foo/message";
            string barTopic = "bar/message";

            await inProcessClient.SubscribeAsync( fooTopic, MqttQualityOfService.ExactlyOnce );
            await remoteClient.SubscribeAsync( barTopic, MqttQualityOfService.AtLeastOnce );

            int fooMessagesReceived = 0;
            int barMessagesReceived = 0;

            inProcessClient.MessageStream.Subscribe( message =>
            {
                if( message.Topic == fooTopic )
                {
                    fooMessagesReceived++;
                }
            } );
            remoteClient.MessageStream.Subscribe( message =>
            {
                if( message.Topic == barTopic )
                {
                    barMessagesReceived++;
                }
            } );

            await remoteClient.PublishAsync( new MqttApplicationMessage( fooTopic, new byte[255] ), MqttQualityOfService.AtMostOnce );
            await remoteClient.PublishAsync( new MqttApplicationMessage( fooTopic, new byte[10] ), MqttQualityOfService.AtLeastOnce );
            await remoteClient.PublishAsync( new MqttApplicationMessage( "other/topic", new byte[500] ), MqttQualityOfService.ExactlyOnce );
            await remoteClient.PublishAsync( new MqttApplicationMessage( fooTopic, new byte[50] ), MqttQualityOfService.ExactlyOnce );

            await inProcessClient.PublishAsync( new MqttApplicationMessage( barTopic, new byte[255] ), MqttQualityOfService.AtMostOnce );
            await inProcessClient.PublishAsync( new MqttApplicationMessage( barTopic, new byte[10] ), MqttQualityOfService.AtLeastOnce );
            await inProcessClient.PublishAsync( new MqttApplicationMessage( "other/topic", new byte[500] ), MqttQualityOfService.ExactlyOnce );
            await inProcessClient.PublishAsync( new MqttApplicationMessage( barTopic, new byte[50] ), MqttQualityOfService.ExactlyOnce );

            await Task.Delay( TimeSpan.FromMilliseconds( 1000 ) );

            Assert.True( inProcessClient.IsConnected );
            Assert.True( remoteClient.IsConnected );
            fooMessagesReceived.Should().Be( 3 );
            barMessagesReceived.Should().Be( 3 );

            inProcessClient.Dispose();
            remoteClient.Dispose();
        }
    }
}
