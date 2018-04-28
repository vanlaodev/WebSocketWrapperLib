using System;
using System.Collections.Generic;
using System.Threading;
using WebSocketSharp;

namespace WebSocketWrapperLib
{
    public class WebSocketClient : WebSocket
    {
        private readonly Dictionary<string, Func<object>> _registeredRpcContractImpls = new Dictionary<string, Func<object>>();

        private int _reconnectInterval;
        private Thread _autoReconnectWorker;
        private volatile bool _autoReconnectWorkerEnabled;
        private readonly object _lockForAutoReconnectWorkerInterval = new object();
        private readonly object _lockForStartStopAutoReconnectWorker = new object();

        public event Action<Message> MessageReceived;
        public event Action OnReconnecting;

        public WebSocketClient(string url, params string[] protocols) : base(url, protocols)
        {
            OnClose += OnOnClose;
            OnOpen += OnOnOpen;
            OnMessage += OnOnMessage;

            AutoReconnect = true;
            ReconnectBackOffMultiplier = 2;
            InitialReconnectInterval = 5 * 1000;
            MaxReconnectInterval = 2 * 60 * 1000;
        }

        public bool AutoReconnect { get; set; }
        public int ReconnectBackOffMultiplier { get; set; }
        public int InitialReconnectInterval { get; set; }
        public int MaxReconnectInterval { get; set; }

        private void OnOnMessage(object sender, MessageEventArgs e)
        {
            var callback = MessageReceived;
            if (callback != null)
            {
                RequestResponseBehaviorCoordinator.OnMessage(this, e, callback, ResolveRpcContractImpl);
            }
        }

        private void OnOnOpen(object sender, EventArgs eventArgs)
        {
            StopAutoReconnectWorker();
        }

        private void StopAutoReconnectWorker()
        {
            lock (_lockForStartStopAutoReconnectWorker)
            {
                _autoReconnectWorkerEnabled = false;
                if (_autoReconnectWorker != null)
                {
                    lock (_lockForAutoReconnectWorkerInterval)
                    {
                        Monitor.Pulse(_lockForAutoReconnectWorkerInterval);
                    }
                    _autoReconnectWorker.Join();
                    _autoReconnectWorker = null;
                }
            }
        }

        private void OnOnClose(object sender, CloseEventArgs closeEventArgs)
        {
            if (AutoReconnect)
            {
                StartAutoReconnectWorker();
            }
        }

        private void StartAutoReconnectWorker()
        {
            lock (_lockForStartStopAutoReconnectWorker)
            {
                if (_autoReconnectWorkerEnabled) return;
                _autoReconnectWorkerEnabled = true;
                _reconnectInterval = InitialReconnectInterval < 0 ? 5 * 1000 : InitialReconnectInterval;
                _autoReconnectWorker = new Thread(() =>
                {
                    while (_autoReconnectWorkerEnabled && ReadyState != WebSocketState.Open)
                    {
                        try
                        {
                            var onReconnecting = OnReconnecting;
                            if (onReconnecting != null)
                            {
                                onReconnecting();
                            }
                            ConnectAsync();
                            if (ReadyState != WebSocketState.Open)
                            {
                                lock (_lockForAutoReconnectWorkerInterval)
                                {
                                    if (_autoReconnectWorkerEnabled)
                                    {
                                        Monitor.Wait(_lockForAutoReconnectWorkerInterval, _reconnectInterval);
                                        if (_reconnectInterval * ReconnectBackOffMultiplier <= MaxReconnectInterval)
                                        {
                                            _reconnectInterval = _reconnectInterval * ReconnectBackOffMultiplier;
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                    _autoReconnectWorkerEnabled = false;
                });
                _autoReconnectWorker.Start();
            }
        }

        public void PrepareForDisposal()
        {
            AutoReconnect = false;
            StopAutoReconnectWorker();
        }

        public void RegisterRpcContractImpl<T>(object impl)
        {
            RegisterRpcContractImpl<T>(() => impl);
        }

        public void RegisterRpcContractImpl<T>(Func<object> funcImpl)
        {
            var interfaceType = typeof(T);
            var impl = funcImpl();
            var implType = impl.GetType();
            if (interfaceType.IsInterface && implType.IsClass && !implType.IsAbstract &&
                interfaceType.IsAssignableFrom(implType))
            {
                lock (_registeredRpcContractImpls)
                {
                    _registeredRpcContractImpls[interfaceType.FullName] = funcImpl;
                }
            }
            else
            {
                throw new Exception("Can not register implementation to contract interface.");
            }
        }

        protected T ResolveRpcContractImpl<T>()
        {
            var type = typeof(T);
            return (T)ResolveRpcContractImpl(type.FullName);
        }

        protected object ResolveRpcContractImpl(string contractType)
        {
            lock (_registeredRpcContractImpls)
            {
                if (_registeredRpcContractImpls.ContainsKey(contractType))
                {
                    return _registeredRpcContractImpls[contractType];
                }
            }
            throw new Exception("Contract implementation not found.");
        }
    }
}