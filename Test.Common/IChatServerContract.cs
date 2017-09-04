using System;

namespace Test.Common
{
    public interface IChatServerContract
    {
        void Say(string text);

        void SetUserInfo(UserInfo userInfo);

        ServerInfo GetServerInfo();
    }
}