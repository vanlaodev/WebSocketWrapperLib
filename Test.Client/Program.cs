using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test.Common;
using WebSocketSharp;
using WebSocketWrapperLib;

namespace Test.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var wsClient = new WebSocketClient("ws://localhost:4579"))
            {
                var chatServerApi = wsClient.GenerateContractWrapper<IChatServerContract>();
                wsClient.OnOpen += WsClientOnOnOpen;
                wsClient.OnClose += WsClientOnOnClose;
                wsClient.OnReconnecting += WsClientOnOnReconnecting;
                wsClient.MessageReceived += WsClientOnMessageReceived;
                Console.WriteLine("Connecting...");
                wsClient.Connect();
                chatServerApi.SetUserInfo(new UserInfo() { Username = Guid.NewGuid().ToString("N") });
                var serverInfo = chatServerApi.GetServerInfo();
                Console.WriteLine("Server time: {0}", serverInfo.ServerTime);
                Console.WriteLine("{0}+{1}={2}", 123, 456, chatServerApi.Add(123, 456));
                Console.WriteLine("{0}+{1}+{2}={3}", 123, 456, 789, chatServerApi.Add(123, 456, 789));
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
                            chatServerApi.Say(input);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error: " + ex.Message);
                        }
                    }
                } while (true);
                wsClient.PrepareForDisposal();
            }
            Console.ReadKey();
        }

        private static void WsClientOnMessageReceived(Message message)
        {
            if (message.Type.Equals(TextMessage.MsgType))
            {
                var textMsg = new TextMessage(message);
                Console.WriteLine(textMsg.Text);
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
        }
    }
}
