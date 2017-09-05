using System;

namespace Test.Common
{
    public interface IChatServerContract
    {
        void Say(string text);

        void SetUserInfo(UserInfo userInfo);

        ServerInfo GetServerInfo();

        int Add(int i1, int i2);
    }
}