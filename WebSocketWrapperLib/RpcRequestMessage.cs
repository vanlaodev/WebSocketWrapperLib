using System.Collections.Generic;
using System.Text;

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
            get { return WebSocketWrapper.ObjectSerializer.Deserialize<RpcRequest>(Encoding.UTF8.GetString(RawData)); }
            set { RawData = Encoding.UTF8.GetBytes(WebSocketWrapper.ObjectSerializer.Serialize(value)); }
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