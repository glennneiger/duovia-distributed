using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace DuoVia.Net.Distributed
{
    public static class Distributor
    {
        /// <summary>
        /// Starts distributed session on each server and returns DistributedClient of TInterface 
        /// to provide access to TInterface on the distributed nodes. 
        /// </summary>
        /// <typeparam name="TInterface">The interface type to be hosted on the distributed nodes.</typeparam>
        /// <param name="implementationType">The concrete type that each distributed node will host.</param>
        /// <param name="servers">The IPEndPoint for each distritued node host to be used by the client.</param>
        /// <returns></returns>
        public static DistributedClient<TInterface> Connect<TInterface>(Type implementationType, params IPEndPoint[] servers) where TInterface : class
        {
            return Connect<TInterface>(implementationType, 1.0f, 5, LogLevel.Error, servers);
        }

        /// <summary>
        /// Starts distributed session on each server and returns DistributedClient of TInterface 
        /// to provide access to TInterface on the distributed nodes. 
        /// </summary>
        /// <typeparam name="TInterface">The interface type to be hosted on the distributed nodes.</typeparam>
        /// <param name="implementationType">The concrete type that each distributed node will host.</param>
        /// <param name="subscriptionRate">Produces N client proxy instances per logical processor on each distributed node (min of 0.0 which produces 1 proxy, max of 30 which produces 30 proxies per logical processor).</param>
        /// <param name="servers">The IPEndPoint for each distritued node host to be used by the client.</param>
        /// <returns></returns>
        public static DistributedClient<TInterface> Connect<TInterface>(Type implementationType,
            float subscriptionRate,
            params IPEndPoint[] servers) where TInterface : class
        {
            return Connect<TInterface>(implementationType, subscriptionRate, 5, LogLevel.Error, servers);
        }

        /// <summary>
        /// Starts distributed session on each server and returns DistributedClient of TInterface 
        /// to provide access to TInterface on the distributed nodes. 
        /// </summary>
        /// <typeparam name="TInterface">The interface type to be hosted on the distributed nodes.</typeparam>
        /// <param name="implementationType">The concrete type that each distributed node will host.</param>
        /// <param name="subscriptionRate">Produces N client proxy instances per logical processor on each distributed node (min of 0.0 which produces 1 proxy, max of 30 which produces 30 proxies per logical processor).</param>
        /// <param name="logPollingInterval">The polling interval for client to sweep logs from the distributed node. Value of 0 will prevent logs from being polled.</param>
        /// <param name="logLevel">Limit logging to this level or lower. Error = 0, Warning = 1, Info = 2, Debug = 3</param>
        /// <param name="servers">The IPEndPoint for each distritued node host to be used by the client.</param>
        /// <returns></returns>
        public static DistributedClient<TInterface> Connect<TInterface>(Type implementationType, 
            float subscriptionRate,
            int logPollingInterval,
            LogLevel logLevel,
            params IPEndPoint[] servers) where TInterface : class
        {
            if (null == servers || servers.Length == 0) throw new ArgumentNullException("servers");

            if (null == implementationType) throw new ArgumentNullException("implementationType");

            var interfaceType = typeof(TInterface);
            if (!interfaceType.IsInterface) throw new ArgumentException("TInterface not interface");

            if (subscriptionRate < 0.0f) subscriptionRate = 0.0f; //one per node, where 1.0 would be one per logical processor
            if (subscriptionRate > 30.0f) subscriptionRate = 30.0f; //prevent crazy over subscription

            if (logPollingInterval < 0) logPollingInterval = 0;
            if (logPollingInterval > 300) logPollingInterval = 300; //max of 5 minutes

            //get singleton package with hash
            var packager = Packager.Create();
            var request = new DistributedSessionRequest()
            {
                PackageName = packager.Hash.Name,
                ContractTypeName = interfaceType.ToConfigName(),
                ImplementationTypeName = implementationType.ToConfigName(),
                HoursToLive = 48,
                LogLevel = logLevel
            };

            var nodes = new ConcurrentDictionary<IPEndPoint, DistributedSessionNode>();
            var failedNodes = new ConcurrentDictionary<IPEndPoint, Exception>();

            Parallel.ForEach(servers, server =>
            {
                try
                {
                    using (var client = new ServiceClient(server))
                    {
                        if (!client.HasPackage(packager.Hash))
                        {
                            client.AddUpdatePackage(packager.Hash, packager.Package());
                        }
                        var node = client.CreateSession(request);
                        nodes.TryAdd(server, node);
                    }
                }
                catch (Exception ex)
                {
                    failedNodes.TryAdd(server, ex);
                }
            });

            var result = new DistributedClient<TInterface>(servers, 
                nodes.Values.ToArray(), 
                failedNodes.Keys.ToArray(), 
                failedNodes.Values.ToArray(), 
                subscriptionRate,
                logPollingInterval);
            return result;
        }
    }
}
