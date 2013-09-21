using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DuoVia.Net.NamedPipes;

namespace DuoVia.Net.Distributed.Agent
{
    internal class AgentClient : NpClient<IAgentService>, IAgentService
    {
        public AgentClient(NpEndPoint npAddress) : base(npAddress)
        {
            
        }

        public void KillSession()
        {
            Proxy.KillSession();
        }

        public byte[] SyncInterface()
        {
            return Proxy.SyncInterface();
        }

        public byte[] InvokeRemoteMethod(int methodHashCode, byte[] parameters, out bool exceptionThrown)
        {
            bool exInner;
            var result = Proxy.InvokeRemoteMethod(methodHashCode, parameters, out exInner);
            exceptionThrown = exInner;
            return result;
        }


        public LogMessage[] SweepLogMessages()
        {
            return Proxy.SweepLogMessages();
        }
    }
}
