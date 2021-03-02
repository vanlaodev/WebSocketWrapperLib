using System;
using Test.Common;
using WebSocketSharp;
using WebSocketSharp.Server;
using WebSocketWrapperLib;

namespace Test.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            WebSocketWrapper.Setup(new JsonObjectSerializer());
            var wssv = new WebSocketServer(6234);
            wssv.WaitTime = TimeSpan.FromSeconds(10);
            wssv.Log.Level = LogLevel.Trace;
            wssv.AddWebSocketService<ChatWebSocketService>("/");
            wssv.Start();
            Console.WriteLine("Started.");
            Console.ReadKey();
            wssv.Stop();
            Console.WriteLine("Stopped.");
        }
    }
}
