using System;
using Test.Common;
using WebSocketSharp.Server;
using WebSocketWrapperLib;

namespace Test.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            WebSocketWrapper.Setup(new JsonObjectSerializer());
            var wssv = new WebSocketServer(4579);
            wssv.AddWebSocketService<ChatWebSocketService>("/");
            wssv.Start();
            Console.WriteLine("Started.");
            Console.ReadKey();
            wssv.Stop();
            Console.WriteLine("Stopped.");
        }
    }
}
