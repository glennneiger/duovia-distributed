using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DuoVia.Net.Aspects;

namespace DuoVia.Net.Distributed
{
    public class DistributedClient<TInterface> : IDisposable where TInterface : class
    {
        //from distributor
        private readonly IPEndPoint[] _servers = null;
        private readonly IPEndPoint[] _failedNodes = new IPEndPoint[0];
        private readonly Exception[] _failedNodeExceptions = new Exception[0];
        private readonly DistributedSessionNode[] _nodes = new DistributedSessionNode[0];
        private readonly float _subscriptionRate = 1.0f;
        private readonly int _logPollingInterval = 0;

        //proxy arrays
        private readonly TInterface[] _proxiesOnePerNode = new TInterface[0];

        private volatile byte[] _interceptStatuses = new byte[0];
        private readonly IPEndPoint[] _interceptEndPoints = new IPEndPoint[0];
        private readonly TInterface[] _intercepts = new TInterface[0];
        private volatile ushort[] _nodeJobCounts = new ushort[0];
        private volatile int _lastIndexReturned = 0;

        //log polling state
        private volatile bool _continuePollingLogs = true;
        private volatile bool _pollingLogs = false;
        private readonly Timer _pollTimer;
        private ConcurrentBag<LogMessage[]> _log = new ConcurrentBag<LogMessage[]>();
        

        /// <summary>
        /// Simple notification that log messages have been retrieved via polling nodes.
        /// To sweep the log messages from the client and do something with them, call 
        /// </summary>
        public event EventHandler<LogMessageEventArgs> LogMessageReceived;
        
        /// <summary>
        /// The nodes that were successfully connected when Distributor.Connect tried to establish a connection.
        /// </summary>
        public IPEndPoint[] Servers { get { return _servers; } }

        /// <summary>
        /// Get nodes that failed, if any, when Distributor.Connect tried to establish a connection.
        /// </summary>
        public IPEndPoint[] FailedNodes { get { return _failedNodes; } }

        /// <summary>
        /// Get exceptions, if any, that occurred when Distribtor.Connect tried to connect to each node.
        /// </summary>
        public Exception[] FailedNodeExceptions { get { return _failedNodeExceptions; } }

        /// <summary>
        /// Get subscription rate for this instance of client. Is set using Distributor.Connect.
        /// </summary>
        public float SubscriptionRate { get { return _subscriptionRate; } }


        /// <summary>
        /// Execute func for each int from and including fromInclusive and up to but not including toExclusive. 
        /// Funcs will execute on distributed node proxies based on subscription rate.
        /// </summary>
        /// <typeparam name="TReturn">The return type of func.</typeparam>
        /// <param name="fromInclusive">The starting index value.</param>
        /// <param name="toExclusive">The ending index value plus one.</param>
        /// <param name="func">The Func that returns TReturn and takes the int index value and TInterface as inputs.</param>
        /// <returns>DistributedLoopResult of TReturn.</returns>
        public DistributedLoopResult<TReturn> For<TReturn>(int fromInclusive, int toExclusive, Func<int, TInterface, TReturn> func)
        {
            return For<TReturn>(fromInclusive, toExclusive, (index, state, proxy) => func(index, proxy));
        }


        /// <summary>
        /// Execute func for each int from and including fromInclusive and up to but not including toExclusive. 
        /// Funcs will execute on distributed node proxies based on subscription rate.
        /// </summary>
        /// <typeparam name="TReturn">The return type of func.</typeparam>
        /// <param name="fromInclusive">The starting index value.</param>
        /// <param name="toExclusive">The ending index value plus one.</param>
        /// <param name="func">The Func that returns TReturn and takes the int index value, the DistributedLoopState of TReturn and TInterface as inputs.</param>
        /// <returns>DistributedLoopResult of TReturn.</returns>
        public DistributedLoopResult<TReturn> For<TReturn>(int fromInclusive, int toExclusive, Func<int, DistributedLoopState<TReturn>, TInterface, TReturn> func) 
        {
            if (toExclusive <= fromInclusive)
            {
                return new DistributedLoopResult<TReturn>();
            }
            var state = new DistributedLoopState<TReturn>();
            for (int index = fromInclusive; index < toExclusive; index++)
            {
                if (!state.ContinueProcessing) break;
                var proxyIndex = GetNextBalancedProxyIndex();
                TInterface t = _intercepts[proxyIndex];
                int locIndex = index;
                state.Tasks.Add(Task.Factory.StartNew(() =>
                {
                    try
                    {
                        if (state.ContinueProcessing)
                        {
                            var funcResult = func(locIndex, state, t);
                            state.ReturnValues.Add(funcResult);
                        }
                    }
                    catch (Exception e)
                    {
                        var err = new DistributedExceptionInfo
                        {
                            Exception = e,
                            EndPoint = _interceptEndPoints[proxyIndex],
                            Index = locIndex,
                            Source = null,
                            TimeStamp = DateTime.Now
                        };
                        state.ExceptionInfos.Add(err);
                    }
                }, TaskCreationOptions.LongRunning)); 
            }
            Task.WaitAll(state.Tasks.ToArray());
            var result = new DistributedLoopResult<TReturn>()
            {
                Exceptions = state.ExceptionInfos.ToArray(),
                Results = state.ReturnValues.ToArray(),
                BreakIteration = state.BreakIteration
            };
            return result;
        }


        /// <summary>
        /// Execute action for each int from and including fromInclusive and up to but not including toExclusive. 
        /// Actions will execute on distributed node proxies based on subscription rate.
        /// </summary>
        /// <param name="fromInclusive">The starting index value.</param>
        /// <param name="toExclusive">The ending index value plus one.</param>
        /// <param name="action">The action to take for each index value in the range, taking the int index value and the TInterface node proxy as inputs.</param>
        /// <returns>DistributedLoopResult</returns>
        public DistributedLoopResult For(int fromInclusive, int toExclusive, Action<int, TInterface> action)
        {
            var tmp = For<object>(fromInclusive, toExclusive, (index, proxy) =>
            {
                action(index, proxy);
                return new object();
            });
            var result = new DistributedLoopResult()
            {
                Exceptions = tmp.Exceptions,
                BreakIteration = tmp.BreakIteration
            };
            return result;
        }

        /// <summary>
        /// Execute action for each int from and including fromInclusive and up to but not including toExclusive. 
        /// Actions will execute on distributed node proxies based on subscription rate.
        /// </summary>
        /// <param name="fromInclusive">The starting index value.</param>
        /// <param name="toExclusive">The ending index value plus one.</param>
        /// <param name="action">The action to take for each index value in the range, taking the int index value and the TInterface node proxy as inputs.</param>
        /// <returns>DistributedLoopResult</returns>
        public DistributedLoopResult For(int fromInclusive, int toExclusive, Action<int, DistributedLoopState, TInterface> action)
        {
            var tmp = For<object>(fromInclusive, toExclusive, (index, state, proxy) =>
            {
                action(index, state, proxy);
                return new object();
            });
            var result = new DistributedLoopResult()
            {
                Exceptions = tmp.Exceptions,
                BreakIteration = tmp.BreakIteration
            };
            return result;
        }


        /// <summary>
        /// Execute action for each object in source using distributed node proxies based on subscription rate.
        /// </summary>
        /// <typeparam name="TReturn"></typeparam>
        /// <typeparam name="TSource">The input source type for each execution of func.</typeparam>
        /// <param name="source">The IEnumerable of TSource. Each will be passed into the func.</param>
        /// <param name="func">The Func to take on inputs of TSource and TInterface and return a TReturn.</param>
        /// <returns>DistributedLoopResult of TReturn</returns>
        public DistributedLoopResult<TReturn> ForEach<TReturn, TSource>(IEnumerable<TSource> source, Func<TSource, TInterface, TReturn> func)
        {
            return ForEach<TReturn, TSource>(source, (src, state, proxy) => func(src, proxy));
        }


        /// <summary>
        /// Execute action for each object in source using distributed node proxies based on subscription rate.
        /// </summary>
        /// <typeparam name="TReturn"></typeparam>
        /// <typeparam name="TSource">The input source type for each execution of func.</typeparam>
        /// <param name="source">The IEnumerable of TSource. Each will be passed into the func.</param>
        /// <param name="func">The Func to take on inputs of TSource, DistributedLoopState of TReturn, and TInterface and return a TReturn.</param>
        /// <returns>DistributedLoopResult of TReturn</returns>
        public DistributedLoopResult<TReturn> ForEach<TReturn, TSource>(IEnumerable<TSource> source, Func<TSource, DistributedLoopState<TReturn>, TInterface, TReturn> func)
        {
            if (null == source) return new DistributedLoopResult<TReturn>();
            var sourceArray = source.ToArray();
            if (sourceArray.Length == 0) return new DistributedLoopResult<TReturn>();
            var result = For<TReturn>(0, sourceArray.Length, (index, state, proxy) =>
            {
                TSource locSource = sourceArray[index];
                return func(locSource, state, proxy);
            });
            return result;
        }


        /// <summary>
        /// Execute action for each object in source using distributed node proxies based on subscription rate.
        /// </summary>
        /// <typeparam name="TSource">The input source type for each execution of action.</typeparam>
        /// <param name="source">The IEnumerable of TSource. Each will be passed into the action.</param>
        /// <param name="action">The action to take on inputs of TSource and TInterface.</param>
        /// <returns>DistributedLoopResult</returns>
        public DistributedLoopResult ForEach<TSource>(IEnumerable<TSource> source, Action<TSource, TInterface> action)
        {
            if (null == source) return new DistributedLoopResult();
            var sourceArray = source.ToArray();
            if (sourceArray.Length == 0) return new DistributedLoopResult();
            var result = For(0, sourceArray.Length, (index, proxy) =>
            {
                TSource locSource = sourceArray[index];
                action(locSource, proxy);
            });
            return result;
        }


        /// <summary>
        /// Execute action for each object in source using distributed node proxies based on subscription rate.
        /// </summary>
        /// <typeparam name="TSource">The input source type for each execution of action.</typeparam>
        /// <param name="source">The IEnumerable of TSource. Each will be passed into the action.</param>
        /// <param name="action">The action to take on inputs of TSource, DistributedLoopState and TInterface.</param>
        /// <returns>DistributedLoopResult</returns>
        public DistributedLoopResult ForEach<TSource>(IEnumerable<TSource> source, Action<TSource, DistributedLoopState, TInterface> action)
        {
            if (null == source) return new DistributedLoopResult();
            var sourceArray = source.ToArray();
            if (sourceArray.Length == 0) return new DistributedLoopResult();
            var result = For(0, sourceArray.Length, (index, state, proxy) =>
            {
                TSource locSource = sourceArray[index];
                action(locSource, state, proxy);
            });
            return result;
        }

        /// <summary>
        /// Execute func once per server node. Used most commonly to set the state of the singleton of TInterface hosted on the remote node.
        /// </summary>
        /// <typeparam name="TReturn">The return type of the Func passed into this method.</typeparam>
        /// <typeparam name="TSource">The input source type for each action.</typeparam>
        /// <param name="source">The input source object to be used in each execution of action on each server node proxy.</param>
        /// <param name="func">The Func that takes the node index, source object and the TInterface proxy and returns the TReturn object.</param>
        /// <returns>Returns DistributedLoopResult of type TReturn, the return value of func.</returns>
        public DistributedLoopResult<TReturn> OncePerNode<TReturn, TSource>(TSource source, Func<TSource, TInterface, TReturn> func)
        {
            return OncePerNode<TReturn, TSource>(source, (index, src, state, proxy) => func(src, proxy));
        }


        /// <summary>
        /// Execute func once per server node. Used most commonly to set the state of the singleton of TInterface hosted on the remote node.
        /// </summary>
        /// <typeparam name="TReturn">The return type of the Func passed into this method.</typeparam>
        /// <typeparam name="TSource">The input source type for each action.</typeparam>
        /// <param name="source">The input source object to be used in each execution of action on each server node proxy.</param>
        /// <param name="func">The Func that takes the node index, source object, DistributedLoopState of TReturn and the TInterface proxy and returns the TReturn object.</param>
        /// <returns>Returns DistributedLoopResult of type TReturn, the return value of func.</returns>
        public DistributedLoopResult<TReturn> OncePerNode<TReturn, TSource>(TSource source, Func<int, TSource, DistributedLoopState<TReturn>, TInterface, TReturn> func)
        {
            var state = new DistributedLoopState<TReturn>();
            for (int i = 0; i < _proxiesOnePerNode.Length; i++)
            {
                if (!state.ContinueProcessing) break;
                TSource locSource = source;
                TInterface t = _proxiesOnePerNode[i];
                var locIndex = i;
                state.Tasks.Add(Task.Factory.StartNew(() =>
                {
                    try
                    {
                        if (state.ContinueProcessing)
                        {
                            var funcResult = func(locIndex, locSource, state, t);
                            state.ReturnValues.Add(funcResult);
                        }
                    }
                    catch (Exception e)
                    {
                        var err = new DistributedExceptionInfo
                        {
                            Exception = e,
                            EndPoint = _servers[locIndex],
                            Index = locIndex,
                            Source = locSource,
                            TimeStamp = DateTime.Now
                        };
                        state.ExceptionInfos.Add(err);
                    }
                }, TaskCreationOptions.LongRunning));
            }
            Task.WaitAll(state.Tasks.ToArray());
            var result = new DistributedLoopResult<TReturn>()
            {
                Exceptions = state.ExceptionInfos.ToArray(),
                Results = state.ReturnValues.ToArray(),
                BreakIteration = state.BreakIteration
            };
            return result;
        }


        /// <summary>
        /// Execute action once per server node. Used most commonly to set the state of the singleton of TInterface hosted on the remote node.
        /// </summary>
        /// <typeparam name="TSource">The input source type for each action.</typeparam>
        /// <param name="source">The input source object to be used in each execution of action on each server node proxy.</param>
        /// <param name="action">The action to take. Inputs are the node index, source and the TInterface proxy.</param>
        /// <returns>Returns DistributedLoopResult.</returns>
        public DistributedLoopResult OncePerNode<TSource>(TSource source, Action<TSource, TInterface> action)
        {
            var tmp = OncePerNode<object, TSource>(source, (src, proxy) =>
            {
                action(src, proxy);
                return new object();
            });
            var result = new DistributedLoopResult()
            {
                Exceptions = tmp.Exceptions,
                BreakIteration = tmp.BreakIteration
            };
            return result;
        }


        /// <summary>
        /// Execute action once per server node. Used most commonly to set the state of the singleton of TInterface hosted on the remote node.
        /// </summary>
        /// <typeparam name="TSource">The input source type for each action.</typeparam>
        /// <param name="source">The input source object to be used in each execution of action on each server node proxy.</param>
        /// <param name="action">The action to take. Inputs are the node index, source, DistributedLoopState and the TInterface proxy.</param>
        /// <returns>Returns DistributedLoopResult.</returns>
        public DistributedLoopResult OncePerNode<TSource>(TSource source, Action<int, TSource, DistributedLoopState, TInterface> action)
        {
            var tmp = OncePerNode<object, TSource>(source, (index, src, state, proxy) =>
            {
                action(index, src, state, proxy);
                return new object();
            });
            var result = new DistributedLoopResult()
            {
                Exceptions = tmp.Exceptions,
                BreakIteration = tmp.BreakIteration
            };
            return result;
        }


        /// <summary>
        /// Reads log messages using yield return. Order received is not guaranteed.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<LogMessage> ReadLogMessages()
        {
            if (!_log.IsEmpty)
            {
                foreach (var msgs in _log)
                {
                    foreach (var msg in msgs)
                    {
                        yield return msg;
                    }
                }
            }
        }

        /// <summary>
        /// Clears log messages. Current log messages will no longer be available.
        /// </summary>
        public void ClearLogMessages()
        {
            _log = new ConcurrentBag<LogMessage[]>();
        }


        /// <summary>
        /// Force polling of all nodes for log messages.
        /// </summary>
        public void SweepLogs()
        {
            //try to wait and poll only when not already polling
            int count = 0;
            while (_pollingLogs && count < 20)
            {
                count++;
                Thread.Sleep(50);
            }
            PollAllServersForLogs();
        }


        // Only the Distribuor class is allowed to create a client.
        internal DistributedClient(IPEndPoint[] servers, 
            DistributedSessionNode[] nodes,
            IPEndPoint[] failedNodes, 
            Exception[] failedNodeExceptionss,
            float subscriptionRate,
            int logPollingInterval)
        {
            _servers = servers;
            _nodes = nodes;
            _failedNodes = failedNodes;
            _failedNodeExceptions = failedNodeExceptionss;
            _subscriptionRate = subscriptionRate;
            _logPollingInterval = logPollingInterval;
            if (_logPollingInterval == 0) _continuePollingLogs = false;

            //create proxies for each node in _nodes, based on sub rate

            var allEndPoints = new List<IPEndPoint>();
            var allProxies = new List<TInterface>();
            var proxiesOnePerNode = new List<TInterface>();
            _nodeJobCounts = new ushort[_nodes.Length];
            for (int index = 0; index < _nodes.Length; index++)
            {
                var node = _nodes[index];
                //initialize job count for node
                _nodeJobCounts[index] = 0; 

                //add proxy to each subscription
                int subRate = Convert.ToInt32(node.LogicProcessorCount*_subscriptionRate);
                if (subRate < 1) subRate = 1;
                for (int i = 0; i < subRate; i++)
                {
                    var proxy = DistributedProxy.CreateProxy<TInterface>(new DistributedEndPoint()
                    {
                        EndPoint = node.EndPoint,
                        SessionId = node.SessionId
                    });
                    allProxies.Add(proxy);
                    allEndPoints.Add(node.EndPoint);
                    if (i == 0) proxiesOnePerNode.Add(proxy);
                }
            }

            _proxiesOnePerNode = proxiesOnePerNode.ToArray();

            var proxies = allProxies.ToArray();
            _interceptEndPoints = allEndPoints.ToArray();
            _interceptStatuses = new byte[proxies.Length];

            //shuffle these arrays randomly but assure their indexes are in sync
            var count = proxies.Length;
            var rand = new Random(DateTime.Now.Millisecond);
            for (int i = 0; i < count; i++) //O(n) shuffle
            {
                int index = rand.Next(i, count);

                var tmpProxy = proxies[i];
                proxies[i] = proxies[index];
                proxies[index] = tmpProxy;

                var tmpEndPoint = _interceptEndPoints[i];
                _interceptEndPoints[i] = _interceptEndPoints[index];
                _interceptEndPoints[index] = tmpEndPoint;

                _interceptStatuses[i] = 0; //initialization only
            }
        
            //create interception points to track usage
            var crossCuttingConcerns = new CrossCuttingConcerns()
                {
                    PreInvoke = OnProxyInvoke,
                    PostInvoke = OnProxyInvokeComplete
                };

            _intercepts = new TInterface[proxies.Length];
            for (int index = 0; index < proxies.Length; index++)
            {
                var proxy = proxies[index];
                _intercepts[index] = Interceptor.Intercept(index, proxy, crossCuttingConcerns);
            }

            _pollTimer = new Timer(PollLogs, 
                null, 
                new TimeSpan(0, 0, _logPollingInterval), 
                new TimeSpan(0, 0, _logPollingInterval));
        }


        /// <summary>
        /// Return next available proxy balanced per subscription rate and usage.
        /// </summary>
        /// <returns></returns>
        private int GetNextBalancedProxyIndex()
        {
            if (_intercepts.Length == 0) throw new NullReferenceException("Next");
            var index = -1;
            var looped = false;
            while (index < 0)
            {
                for (int i = 0; i < _intercepts.Length; i++)
                {
                    if (_interceptStatuses[i] == 0)
                    {
                        //before we return same as last, we loop at least once
                        if (i != _lastIndexReturned || looped)
                        {
                            index = i;
                            _lastIndexReturned = index;
                            break;
                        }
                    }
                }
                looped = true;
            }
            return index;
        }


        private void PollLogs(object state)
        {
            if (_continuePollingLogs)
            {
                if (!_pollingLogs)
                {
                    PollAllServersForLogs();
                }
            }
            else
            {
                _pollTimer.Dispose();
            }
        }

        private void PollAllServersForLogs()
        {
            try
            {
                _pollingLogs = true;
                bool logsRetrieved = false;
                Exception lastLogPollingException = null;
                DateTime lastLogPollingExceptionTime = DateTime.MinValue;
                IPEndPoint lastLogPollingExceptionServer = null;
                ConcurrentBag<LogMessage[]> localBag = new ConcurrentBag<LogMessage[]>();
                Parallel.ForEach(_servers, server =>
                {
                    try
                    {
                        using (var client = new ServiceClient(server))
                        {
                            var msgs = client.SweepLogMessages(_nodes[0].SessionId);
                            if (null != msgs && msgs.Length > 0)
                            {
                                _log.Add(msgs);
                                localBag.Add(msgs);
                                logsRetrieved = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lastLogPollingException = ex;
                        lastLogPollingExceptionTime = DateTime.Now;
                        lastLogPollingExceptionServer = server;
                    }
                });
                if (logsRetrieved && null != LogMessageReceived)
                {
                    LogMessageReceived(this, new LogMessageEventArgs()
                    {
                        LocalBag = localBag,
                        LastLogPollingException = lastLogPollingException,
                        LastLogPollingExceptionServer = lastLogPollingExceptionServer,
                        LastLogPollingExceptionTime = lastLogPollingExceptionTime
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error("LogPollingError: {0}", ex);
            }
            finally
            {
                _pollingLogs = false;
            }
        }


        private void OnProxyInvoke(int instanceId, string methodName, object[] parameters)
        {
            _interceptStatuses[instanceId] = 1; //busy
            var endPoint = _interceptEndPoints[instanceId];
            for (int i = 0; i < _nodes.Length; i++)
            {
                var nodeEndPoint = _nodes[i].EndPoint;
                if (endPoint.Equals(nodeEndPoint))
                {
                    _nodeJobCounts[i]++;
                    break;
                }
            }
        }

        private void OnProxyInvokeComplete(int instanceId, string methodName, object[] parameters)
        {
            _interceptStatuses[instanceId] = 0; //available
            var endPoint = _interceptEndPoints[instanceId];
            for (int i = 0; i < _nodes.Length; i++)
            {
                var nodeEndPoint = _nodes[i].EndPoint;
                if (endPoint.Equals(nodeEndPoint))
                {
                    _nodeJobCounts[i]--;
                    break;
                }
            }
        }



        #region IDisposable Members

        private bool _disposed = false;

        public void Dispose()
        {
            //MS recommended dispose pattern - prevents GC from disposing again
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true; //prevent second call to Dispose
                if (disposing)
                {
                    _pollTimer.Dispose();
                    for (int index = 0; index < _nodes.Length; index++)
                    {
                        var node = _nodes[index];
                        try
                        {
                            using (var client = new ServiceClient(node.EndPoint))
                            {
                                client.KillSession(node.SessionId);
                            }
                        }
                        catch (Exception ex)
                        {
                            //TODO log it?
                        }
                    }
                    //calling dispose on intercept calls it on intercepted object as well
                    for (int index = 0; index < _intercepts.Length; index++)
                    {
                        var proxy = _intercepts[index];
                        var d = proxy as IDisposable;
                        if (null != d) d.Dispose(); 
                    }
                }
            }
        }

        #endregion

    }
}
