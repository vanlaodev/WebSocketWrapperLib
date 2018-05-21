﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly ManualResetEventSlim _reconnectWaitHandle = new ManualResetEventSlim(false);

        public event Action<Message> MessageReceived;
        public event Action OnReconnecting;

        public WebSocketClient(string url, params string[] protocols) : base(url, protocols)
        {
            OnClose += OnOnClose;
            OnOpen += OnOnOpen;
            OnMessage += OnOnMessage;

            AutoReconnect = true;
            ReconnectBackOffMultiplier = 2;
            ReconnectInterval = 5 * 1000;
            MaxReconnectInterval = 3 * 60 * 1000;
        }

        public bool AutoReconnect { get; set; }
        public int ReconnectBackOffMultiplier { get; set; }
        public int ReconnectInterval { get; set; }
        public int MaxReconnectInterval { get; set; }

        private void OnOnMessage(object sender, MessageEventArgs e)
        {
            var callback = MessageReceived;
            RequestResponseBehaviorCoordinator.OnMessage(this, e, callback, ResolveRpcContractImpl, OnHandleMessageError);
        }

        protected virtual void OnHandleMessageError(Exception obj)
        {

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
                    _reconnectWaitHandle.Set();
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
                _reconnectInterval = ReconnectInterval < 1000 ? 1000 : ReconnectInterval;
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
                            _reconnectWaitHandle.Reset();
                            Task.Run(() =>
                            {
                                try
                                {
                                    Connect();
                                }
                                catch (Exception ex)
                                {
                                    Log.Error("Error occured while reconnecting: " + ex);
                                }
                                finally
                                {
                                    _reconnectWaitHandle.Set();
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Log.Error("Error occured while reconnecting: " + ex);
                        }
                        _reconnectWaitHandle.Wait();
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