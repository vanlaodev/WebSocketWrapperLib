using System;
using System.Collections.Generic;
using System.Linq;
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
            MaxReconnectInterval = 3 * 60 * 1000;
        }

        public bool AutoReconnect { get; set; }
        public int ReconnectBackOffMultiplier { get; set; }
        public int InitialReconnectInterval { get; set; }
        public int MaxReconnectInterval { get; set; }

        private void OnOnMessage(object sender, MessageEventArgs e)
        {
            var callback = MessageReceived;
            RequestResponseBehaviorCoordinator.OnMessage(this, e, callback, ResolveRpcContractImpl);
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
            RequestResponseBehaviorCoordinator.CancelAll();

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
                _reconnectInterval = InitialReconnectInterval < 1000 ? 1000 : InitialReconnectInterval;
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
                            try
                            {
                                Connect();
                            }
                            catch
                            {
                                // ignored
                            }
                            if (ReadyState != WebSocketState.Open)
                            {
                                lock (_lockForAutoReconnectWorkerInterval)
                                {
                                    if (_autoReconnectWorkerEnabled)
                                    {
                                        Monitor.Wait(_lockForAutoReconnectWorkerInterval, _reconnectInterval);
                                        if (_reconnectInterval * ReconnectBackOffMultiplier < MaxReconnectInterval)
                                        {
                                            _reconnectInterval *= ReconnectBackOffMultiplier;
                                        }
                                        else
                                        {
                                            _reconnectInterval = MaxReconnectInterval;
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

        public void RegisterRpcContractImpl<TInterface, TImpl>(TImpl impl) where TImpl : TInterface
        {
            RegisterRpcContractImpl<TInterface, TImpl>(() => impl);
        }

        public void RegisterRpcContractImpl<TInterface, TImpl>(Func<TImpl> funcImpl) where TImpl : TInterface
        {
            var implType = typeof(TImpl);
            var interfaceType = typeof(TInterface);
            if (interfaceType.IsInterface && implType.IsClass && !implType.IsAbstract &&
                interfaceType.IsAssignableFrom(implType))
            {
                lock (_registeredRpcContractImpls)
                {
                    _registeredRpcContractImpls[interfaceType.FullName] = () => { return funcImpl(); };
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
                    var funcImpl = _registeredRpcContractImpls[contractType];
                    return funcImpl();
                }
            }
            throw new Exception("Contract implementation not found.");
        }
    }
}