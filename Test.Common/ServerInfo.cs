using System;

namespace Test.Common
{
    public class ServerInfo
    {
        public DateTime ServerTime { get; set; }

        public ServerInfo()
        {
            ServerTime = DateTime.Now;
        }
    }
}