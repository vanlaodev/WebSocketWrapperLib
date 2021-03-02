using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Test.Common;
using WebSocketSharp;
using WebSocketWrapperLib;

namespace Test.Client
{
    class Program
    {
        private static IChatServerContract _chatServerApi;

        static void Main(string[] args)
        {
            WebSocketWrapper.Setup(new JsonObjectSerializer());
            using (var wsClient = new WebSocketClient("ws://localhost:6234"))
            {
                wsClient.Log.Level = LogLevel.Trace;
                wsClient.WaitTime = TimeSpan.FromSeconds(10);
                wsClient.AutoPingPongInterval = TimeSpan.FromSeconds(10);
                wsClient.RegisterRpcContractImpl<IClientContract, ClientContractImpl>(new ClientContractImpl());
                _chatServerApi = wsClient.GenerateContractWrapper<IChatServerContract>(2 * 60 * 1000);
                wsClient.OnOpen += WsClientOnOnOpen;
                wsClient.OnClose += WsClientOnOnClose;
                wsClient.OnReconnecting += WsClientOnOnReconnecting;
                wsClient.MessageReceived += WsClientOnMessageReceived;
                Console.WriteLine("Connecting...");
                wsClient.ConnectAsync();
                do
                {
                    var input = Console.ReadLine();
                    if (input != null)
                    {
                        if (input.Equals("q"))
                        {
                            break;
                        }
                        try
                        {
                            _chatServerApi.UnsubscribeAll();
                            _chatServerApi.Say(input);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error: " + ex.Message);
                        }
                    }
                } while (true);
                wsClient.PrepareForDisposal();
            }
            //            Console.ReadKey();
        }

        private static void WsClientOnMessageReceived(Message message)
        {
            if (message.Type.Equals(TextMessage.MsgType))
            {
                var textMsg = new TextMessage(message);
                Console.WriteLine(textMsg.Text);
            }
            else if (message.Type.Equals(PublishMessage.MsgType))
            {
                var pubMsg = new PublishMessage(message);
                Console.WriteLine("Topic: {0} | Data: {1}", pubMsg.Topic, Encoding.UTF8.GetString(pubMsg.RawData));
            }
        }

        private static void WsClientOnOnReconnecting()
        {
            Console.WriteLine("Reconnecting...");
        }

        private static void WsClientOnOnClose(object sender, CloseEventArgs closeEventArgs)
        {
            Console.WriteLine("Disconnected.");
        }

        private static void WsClientOnOnOpen(object sender, EventArgs eventArgs)
        {
            Console.WriteLine("Connected.");

            Task.Run(() =>
            {
                _chatServerApi.SetUserInfo(new UserInfo() { Username = Guid.NewGuid().ToString("N") });
                var serverInfo = _chatServerApi.GetServerInfo();
                Console.WriteLine("Server time: {0}", serverInfo.ServerTime);
                Console.WriteLine("{0}+{1}={2}", 123, 456, _chatServerApi.Add(123, 456));
                Console.WriteLine("{0}+{1}+{2}={3}", 123, 456, 789, _chatServerApi.Add(123, 456, 789));
                _chatServerApi.Subscribe(new[] { "Test" });
                _chatServerApi.Say(new SayMultipleLinesModel() { Lines = new List<string>() { "Hello", "World", "!" } });
            });
        }
    }
}
