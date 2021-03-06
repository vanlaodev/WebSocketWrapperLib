﻿using System.Text;

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
            get { return WebSocketWrapper.ObjectSerializer.Deserialize<RpcResponse>(Encoding.UTF8.GetString(RawData)); }
            set { RawData = Encoding.UTF8.GetBytes(WebSocketWrapper.ObjectSerializer.Serialize(value)); }
        }

        public class RpcResponse
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