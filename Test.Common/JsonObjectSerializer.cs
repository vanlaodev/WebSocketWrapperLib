using System;
using Newtonsoft.Json;
using WebSocketWrapperLib;

namespace Test.Common
{
    public class JsonObjectSerializer : IObjectSerializer
    {
        public string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        public T Deserialize<T>(string objStr)
        {
            return JsonConvert.DeserializeObject<T>(objStr);
        }

        public object Deserialize(string objStr, Type type)
        {
            return JsonConvert.DeserializeObject(objStr, type);
        }
    }
}