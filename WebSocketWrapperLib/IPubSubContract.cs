using System.Collections;
using System.Collections.Generic;

namespace WebSocketWrapperLib
{
    public interface IPubSubContract
    {
        void Subscribe(string[] topics);

        void Unsubscribe(string[] topics);

        void UnsubscribeAll();

        void Publish(string topic, byte[] data);
    }
}