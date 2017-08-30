﻿using System;
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
                wsClient.OnOpen += WsClientOnOnOpen;
                wsClient.OnClose += WsClientOnOnClose;
                wsClient.OnReconnecting += WsClientOnOnReconnecting;
                wsClient.MessageReceived += WsClientOnMessageReceived;
                Console.WriteLine("Connecting...");
                wsClient.Connect();
                do
                {
                    var input = Console.ReadLine();
                    if (input != null)
                    {
                        if (input.Equals("q"))
                        {
                            break;
                        }
                        wsClient.Request<AckMessage>(new TextMessage()
                        {
                            Text = input
                        }, 30000);
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
