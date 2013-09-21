using System.Net;
using DuoVia.Net.Distributed.Server;
using DuoVia.Net.TcpIp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DuoVia.Net.Distributed
{
    internal class ServiceClient : TcpClient<IServiceHost>, IServiceHost
    {
        public ServiceClient(IPEndPoint endpoint) : base(endpoint)
        {
            
        }

        public bool HasPackage(PackageHash hash)
        {
            return Proxy.HasPackage(hash);
        }

        public void AddUpdatePackage(PackageHash hash, byte[] package)
        {
            Proxy.AddUpdatePackage(hash, package);
        }


        public DistributedSessionNode CreateSession(DistributedSessionRequest request)
        {
            return Proxy.CreateSession(request);
        }

        public void KillSession(Guid sessionId)
        {
            Proxy.KillSession(sessionId);
        }

        public byte[] SyncInterface(Guid sessionId)
        {
            return Proxy.SyncInterface(sessionId);
        }

        public byte[] InvokeRemoteMethod(Guid sessionId, int methodHashCode, byte[] parameters, out bool exceptionThrown)
        {
            bool ex;
            var result = Proxy.InvokeRemoteMethod(sessionId, methodHashCode, parameters, out ex);
            exceptionThrown = ex;
            return result;
        }

        public LogMessage[] SweepLogMessages(Guid sessionId)
        {
            return Proxy.SweepLogMessages(sessionId);
        }
    }
}
