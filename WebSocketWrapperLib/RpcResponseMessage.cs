using System.Text;
using Newtonsoft.Json;

namespace WebSocketWrapperLib
{
    public class RpcResponseMessage : Message
    {
        public const string MsgType = "RpcResponse";

        public RpcResponseMessage()
        {
        }

        public RpcResponseMessage(string replyId) : base(replyId)
        {
        }

        public RpcResponseMessage(Message message) : base(message)
        {
        }

        public RpcResponse Response
        {
            get { return JsonConvert.DeserializeObject<RpcResponse>(Encoding.UTF8.GetString(RawData)); }
            set { RawData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value)); }
        }

        public class RpcResponse
        {
            public object Result { get; set; }
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