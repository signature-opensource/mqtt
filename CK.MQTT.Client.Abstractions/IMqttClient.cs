using CK.Core;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.MQTT
{
    /// <summary>
    /// Represents an MQTT Client
    /// </summary>
    public interface IMqttClient : IDisposable
    {
        /// <summary>
        /// Event raised when the Client gets disconnected in asynchronous way, each async handler being called in parallel
        /// with the other ones.
        /// The Client disconnection could be caused by a protocol disconnect, an error or a remote disconnection
        /// produced by the Server.
        /// See <see cref="MqttEndpointDisconnected"/> for more details on the disconnection information
        /// </summary>
        event ParallelEventHandlerAsync<MqttEndpointDisconnected> ParallelDisconnectedAsync;

        /// <summary>
        /// Event raised when the Client gets disconnected, synchronously.
        /// The Client disconnection could be caused by a protocol disconnect, an error or a remote disconnection
        /// produced by the Server.
        /// See <see cref="MqttEndpointDisconnected"/> for more details on the disconnection information
        /// </summary>
        event SequentialEventHandler<MqttEndpointDisconnected> Disconnected;

        /// <summary>
        /// Event raised when the Client gets disconnected in asynchronous way, each async handler being called
        /// one after the other.
        /// The Client disconnection could be caused by a protocol disconnect, an error or a remote disconnection
        /// produced by the Server.
        /// See <see cref="MqttEndpointDisconnected"/> for more details on the disconnection information
        /// </summary>
        event SequentialEventHandlerAsync<MqttEndpointDisconnected> DisconnectedAsync;

        /// <summary>
        /// Id of the connected Client.
        /// This Id correspond to the <see cref="MqttClientCredentials.ClientId"/> parameter passed to 
        /// <see cref="ConnectAsync(IActivityMonitor, MqttClientCredentials, MqttLastWill, bool)"/> method or
        /// has been provided by the server.
        /// </summary>
        string ClientId { get; }

        /// <summary>
        /// Checks the connection: the Client must be connected, ie. a CONNECT packet has been sent by
        /// calling <see cref="ConnectAsync( IActivityMonitor, MqttClientCredentials, MqttLastWill, bool)"/> and
        /// the connection succeed.
        /// </summary>
        /// <param name="m">The monitor that will be used.</param>
        bool CheckConnection( IActivityMonitor m );

        /// <summary>
        /// Event raised for each received message in asynchronous way, each async handler being called in parallel
        /// with the other ones.
        /// </summary>
        event ParallelEventHandlerAsync<MqttApplicationMessage> ParallelMessageReceivedAsync;

        /// <summary>
        /// Event raised for each received message, synchronously.
        /// </summary>
        event SequentialEventHandler<MqttApplicationMessage> MessageReceived;

        /// <summary>
        /// Event raised for each received message in asynchronous way, each async handler being called
        /// one after the other.
        /// </summary>
        event SequentialEventHandlerAsync<MqttApplicationMessage> MessageReceivedAsync;

        /// <summary>
        /// Represents the protocol connection, which consists of sending a CONNECT packet
        /// and awaiting the corresponding CONNACK packet from the Server
        /// </summary>
        /// <param name="credentials">
        /// The credentials used to connect to the Server. See <see cref="MqttClientCredentials" /> for more details on the credentials information
        /// </param>
        /// <param name="will">
        /// The last will message to send from the Server when an unexpected Client disconnection occurrs. 
        /// See <see cref="MqttLastWill" /> for more details about the will message structure
        /// </param>
        /// <param name="cleanSession">
        /// Indicates if the session state between Client and Server must be cleared between connections
        /// Defaults to false, meaning that session state will be preserved by default accross connections
        /// </param>
        /// <returns>
        /// Returns the state of the client session created as part of the connection
        /// See <see cref="SessionState" /> for more details about the session state values
        /// </returns>
        /// <exception cref="MqttClientException">MqttClientException</exception>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180841">MQTT Connect</a>
        /// for more details about the protocol connection
        /// </remarks>
        Task<SessionState> ConnectAsync( IActivityMonitor m, MqttClientCredentials credentials, MqttLastWill will = null, bool cleanSession = false );

        Task<SessionState> ConnectAnonymousAsync( IActivityMonitor m, MqttLastWill will = null );

        /// <summary>
        /// Represents the protocol subscription, which consists of sending a SUBSCRIBE packet
        /// and awaiting the corresponding SUBACK packet from the Server
        /// </summary>
        /// <param name="topicFilter">
        /// The topic to subscribe for incoming application messages. 
        /// Every message sent by the Server that matches a subscribed topic, will go to the <see cref="MessageStream"/> 
        /// </param>
        /// <param name="qos">
        /// The maximum Quality Of Service (QoS) that the Server should maintain when publishing application messages for the subscribed topic to the Client
        /// This QoS is maximum because it depends on the QoS supported by the Server. 
        /// See <see cref="MqttQualityOfService" /> for more details about the QoS values
        /// </param>
        /// <exception cref="MqttClientException">MqttClientException</exception>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180876">MQTT Subscribe</a>
        /// for more details about the protocol subscription
        /// </remarks>
        Task SubscribeAsync( IActivityMonitor m, string topicFilter, MqttQualityOfService qos );

        /// <summary>
        /// Represents the protocol publish, which consists of sending a PUBLISH packet
        /// and awaiting the corresponding ACK packet, if applies, based on the QoS defined
        /// </summary>
        /// <param name="message">
        /// The application message to publish to the Server.
        /// See <see cref="MqttApplicationMessage" /> for more details about the application messages
        /// </param>
        /// <param name="qos">
        /// The Quality Of Service (QoS) associated to the application message, which determines 
        /// the sequence of acknowledgements that Client and Server should send each other to consider the message as delivered
        /// See <see cref="MqttQualityOfService" /> for more details about the QoS values
        /// </param>
        /// <param name="retain">
        /// Indicates if the application message should be retained by the Server for future subscribers.
        /// Only the last message of each topic is retained
        /// </param>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180850">MQTT Publish</a>
        /// for more details about the protocol publish
        /// </remarks>
        Task PublishAsync( IActivityMonitor m, string topic, ReadOnlyMemory<byte> payload, MqttQualityOfService qos, bool retain = false );

        /// <summary>
        /// Represents the protocol unsubscription, which consists of sending an UNSUBSCRIBE packet
        /// and awaiting the corresponding UNSUBACK packet from the Server
        /// </summary>
        /// <param name="topics">
        /// The list of topics to unsubscribe from. Once the unsubscription completes, no more application messages for those topics
        /// will arrive to <see cref="MessageReceived"/> 
        /// </param>
        /// <exception cref="MqttClientException">MqttClientException</exception>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180885">MQTT Unsubscribe</a>
        /// for more details about the protocol unsubscription
        /// </remarks>
        Task UnsubscribeAsync( IActivityMonitor m, IEnumerable<string> topics );

        /// <summary>
        /// Represents the protocol disconnection, which consists of sending a DISCONNECT packet to the Server
        /// No acknowledgement is sent by the Server on the disconnection
        /// Once the client is successfully disconnected, the <see cref="Disconnected"/> event will be fired 
        /// </summary>
        /// <remarks>
        /// See <a href="http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html#_Toc442180903">MQTT Disconnect</a>
        /// for more details about the protocol disconnection
        /// </remarks>
        Task DisconnectAsync( IActivityMonitor m );
    }

}
