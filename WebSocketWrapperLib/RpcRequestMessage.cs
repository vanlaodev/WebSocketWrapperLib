using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace WebSocketWrapperLib
{
    public class RpcRequestMessage : Message
    {
        public const string MsgType = "RpcRequest";

        public RpcRequestMessage()
        {
        }

        public RpcRequestMessage(string replyId) : base(replyId)
        {
        }

        public RpcRequestMessage(Message message) : base(message)
        {
        }

        public RpcRequest Request
        {
            get { return JsonConvert.DeserializeObject<RpcRequest>(Encoding.UTF8.GetString(RawData)); }
            set { RawData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value)); }
        }

        public class RpcRequest
        {
            public string Contract { get; set; }
            public string Method { get; set; }
            public List<ParameterInfo> Parameters { get; set; }
        }

        public class ParameterInfo
        {
            public string Type { get; set; }
            public bool IsValueType { get; set; }
            public object Value { get; set; }
        }

        protected override string DefaultType
        {
            get
            {
                return MsgType;
            }
        }
    }
}