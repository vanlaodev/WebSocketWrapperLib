using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test.Common;
using WebSocketSharp.Server;
using WebSocketWrapperLib;

namespace Test.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            var wssv = new WebSocketServer(4579);
            wssv.AddWebSocketService("/", () =>
            {
                var chatWebSocketService = new ChatWebSocketService();
                return chatWebSocketService;
            });
            wssv.Start();
            Console.WriteLine("Started.");
            Console.ReadKey();
            wssv.Stop();
            Console.WriteLine("Stopped.");
        }
    }
}
