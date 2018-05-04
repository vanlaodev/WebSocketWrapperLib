using System;
using Test.Common;

namespace Test.Client
{
    public class ClientContractImpl : IClientContract
    {
        public DateTime GetClientTime()
        {
            return DateTime.Now;
        }
    }
}