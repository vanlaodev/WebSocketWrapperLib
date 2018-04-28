using System;

namespace WebSocketWrapperLib
{
    public interface IObjectSerializer
    {
        string Serialize(object obj);

        T Deserialize<T>(string objStr);

        object Deserialize(string objStr, Type type);
    }
}