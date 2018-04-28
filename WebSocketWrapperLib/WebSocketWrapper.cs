namespace WebSocketWrapperLib
{
    public static class WebSocketWrapper
    {
        public static IObjectSerializer ObjectSerializer { get; private set; }

        public static void Setup(IObjectSerializer objectSerializer)
        {
            ObjectSerializer = objectSerializer;
        }
    }
}