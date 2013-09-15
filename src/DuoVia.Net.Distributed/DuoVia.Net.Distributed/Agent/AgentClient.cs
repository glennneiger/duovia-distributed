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

        public object[] InvokeRemoteMethod(int methodHashCode, params object[] parameters)
        {
            return Proxy.InvokeRemoteMethod(methodHashCode, parameters);
        }


        public LogMessage[] SweepLogMessages()
        {
            return Proxy.SweepLogMessages();
        }
    }
}
