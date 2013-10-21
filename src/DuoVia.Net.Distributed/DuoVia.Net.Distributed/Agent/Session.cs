using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using DuoVia.Net.NamedPipes;

namespace DuoVia.Net.Distributed.Agent
{
    public class Session
    {
        private DateTime _created;
        private Guid _sessionId;
        private string _contractTypeDef;
        private string _implementationTypeDef;
        private ManualResetEvent _exitWaitHandle = new ManualResetEvent(false);
        private short _hoursToLive = 48; //will exit in two days if nothing is changed
        private Type _contractType = null;
        private Type _implementationType = null;
        private Assembly _contractAssembly = null;
        private Assembly _implementationAssembly = null;
        private AgentService _agentService = null;
        private NpHost _agentServiceHost = null;
        private LogLevel _logLevel = LogLevel.Error;
        private IPEndPoint _endPoint = null;

        private Session(Guid sessionId, string contractType, string implementionType, short hoursToLive, LogLevel logLevel, IPEndPoint endPoint)
        {
            _created = DateTime.UtcNow;
            _sessionId = sessionId;
            _contractTypeDef = contractType;
            _implementationTypeDef = implementionType;
            _hoursToLive = hoursToLive;
            _logLevel = logLevel;
            _endPoint = endPoint;
        }

        public DateTime Created { get { return _created; } }
        public Guid SessionId { get { return _sessionId; } }
        public Type ContractType { get { return _contractType; } }
        public Type ImplementationType { get { return _implementationType; } }
        public LogLevel LogLevel { get { return _logLevel; } }

        public void WaitUntilExit()
        {
            _exitWaitHandle.WaitOne(new TimeSpan(_hoursToLive, 0, 0));
        }

        public void KillSession()
        {
            //clean up
            if (null != _agentServiceHost) _agentServiceHost.Close();
            _exitWaitHandle.Set(); //allows WaitUntilExit to return
        }

        private void Initialize()
        {
            _contractType = _contractTypeDef.ToType();
            _implementationType = _implementationTypeDef.ToType();

            if (!_implementationType.GetInterfaces().Contains(_contractType))
            {
                throw new TypeAccessException("Contract and implementation mismatch.");
            }

            _agentService = new AgentService(this, _endPoint);
            _agentServiceHost = new NpHost(string.Format("dpp-{0}", _sessionId));
            _agentServiceHost.AddService<IAgentService>(_agentService);
            _agentServiceHost.Open();
        }

        public static Session Create(string[] args)
        {
            //0 = sessionId
            //1 = contractType
            //2 = implementationType
            //3 = hoursToLive
            //4 = logLevel
            //5 = endpoint string

            if (null == args || args.Length != 6) return null;
            Guid sessionId;
            if (!Guid.TryParse(args[0].Trim(' ', '"'), out sessionId)) return null;
            short hoursToLive;
            if (!short.TryParse(args[3].Trim(' ', '"'), out hoursToLive)) return null;
            LogLevel logLevel = LogLevel.Error;
            if (!Enum.TryParse(args[4], out logLevel)) logLevel = LogLevel.Error;

            var lastColon = args[5].LastIndexOf(':');
            if (lastColon < 1) return null;
            var addr = args[5].Substring(0, lastColon);
            var port = Convert.ToInt32(args[5].Substring(lastColon + 1, args[5].Length - lastColon - 1));
            var endPoint = new IPEndPoint(IPAddress.Parse(addr), port);

            var session = new Session(sessionId, args[1].Trim(' ', '"'), args[2].Trim(' ', '"'), hoursToLive, logLevel, endPoint);
            try
            {
                session.Initialize();
            }
            catch (Exception e)
            {
                //log it
                session = null;
            }
            return session;
        }
    }
}
