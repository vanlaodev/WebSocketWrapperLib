using System;
using Test.Common;

namespace Test.Server
{
    public class ChatServerContractImpl : IChatServerContract
    {
        public void Say(string text)
        {
            Console.WriteLine("Client say: " + text);
        }
    }
}