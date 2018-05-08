using System;
using System.Collections.Generic;
using WebSocketWrapperLib;

namespace Test.Common
{
    public interface IChatServerContract : IPubSubContract
    {
        void Say(string text);

        void SetUserInfo(UserInfo userInfo);

        ServerInfo GetServerInfo();

        int Add(int i1, int i2);

        int Add(int i1, int i2, int i3);

        void Say(SayMultipleLinesModel model);
    }
}