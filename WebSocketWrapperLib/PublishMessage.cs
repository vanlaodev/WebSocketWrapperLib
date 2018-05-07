using System.Text;

namespace WebSocketWrapperLib
{
    public class PublishMessage : Message
    {
        private const string HeaderRequireReply = "topic";
        public const string MsgType = "Publish";

        public PublishMessage(string topic, byte[] data)
        {
            Topic = topic;
            RawData = data;
        }

        public PublishMessage(Message msg) : base(msg)
        {

        }

        public string Topic
        {
            get { return (string)Headers[HeaderRequireReply]; }
            set { Headers[HeaderRequireReply] = value; }
        }

        protected override string DefaultType
        {
            get { return MsgType; }
        }
    }
}