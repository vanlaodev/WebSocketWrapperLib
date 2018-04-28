using System;
using System.Collections.Generic;
using System.Text;

namespace WebSocketWrapperLib
{
    public class Message
    {
        private const string HeaderMsgId = "msg-id";
        private const string HeaderMsgType = "msg-type";
        private const string HeaderMsgReplyId = "msg-reply-id";
        private const string HeaderRequireReply = "require-reply";

        protected readonly Dictionary<string, object> Headers = new Dictionary<string, object>();

        internal static Message Parse(byte[] bytes)
        {
            var headerBytesLength = bytes[0] << 8 | bytes[1];
            var headerBytes = new byte[headerBytesLength];
            Array.Copy(bytes, 2, headerBytes, 0, headerBytesLength);
            var rawDataLength = bytes.Length - 2 - headerBytesLength;
            byte[] rawData = null;
            if (rawDataLength > 0)
            {
                rawData = new byte[rawDataLength];
                Array.Copy(bytes, 2 + headerBytesLength, rawData, 0, rawDataLength);
            }
            var headers = WebSocketWrapper.ObjectSerializer.Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(headerBytes));
            return new Message(headers, rawData);
        }

        public Message() : this((string)null)
        {
        }

        public Message(string replyId)
        {
            Id = Guid.NewGuid().ToString();
            Type = DefaultType;
            ReplyId = replyId;
        }

        public Message(Message message) : this(message.Headers, message.RawData)
        {
        }

        public Message(Dictionary<string, object> headers, byte[] rawData)
        {
            RawData = rawData;
            Headers = headers;
        }

        public string Id
        {
            get { return (string)Headers[HeaderMsgId]; }
            private set { Headers[HeaderMsgId] = value; }
        }

        public string Type
        {
            get { return (string)Headers[HeaderMsgType]; }
            set { Headers[HeaderMsgType] = value; }
        }

        public string ReplyId
        {
            get { return (string)Headers[HeaderMsgReplyId]; }
            set { Headers[HeaderMsgReplyId] = value; }
        }

        public bool RequireReply
        {
            get { return (bool)Headers[HeaderRequireReply]; }
            set { Headers[HeaderRequireReply] = value; }
        }

        public byte[] RawData { get; protected set; }

        public byte[] ToBytes()
        {
            var headerBytes = Encoding.UTF8.GetBytes(WebSocketWrapper.ObjectSerializer.Serialize(Headers));
            var headerBytesLength = headerBytes.Length;
            var headerBytesLengthBytes = new byte[2];
            headerBytesLengthBytes[0] = (byte)(headerBytesLength >> 8 & 0xff);
            headerBytesLengthBytes[1] = (byte)(headerBytesLength & 0xff);
            var rawDataLength = RawData == null ? 0 : RawData.Length;
            var resultBytes = new byte[headerBytesLengthBytes.Length + headerBytesLength + rawDataLength];
            Array.Copy(headerBytesLengthBytes, resultBytes, headerBytesLengthBytes.Length);
            Array.Copy(headerBytes, 0, resultBytes, headerBytesLengthBytes.Length, headerBytesLength);
            if (rawDataLength > 0)
            {
                Array.Copy(RawData, 0, resultBytes, headerBytesLengthBytes.Length + headerBytesLength, rawDataLength);
            }
            return resultBytes;
        }

        protected virtual string DefaultType
        {
            get
            {
                throw new NotImplementedException();
            }
        }
    }
}