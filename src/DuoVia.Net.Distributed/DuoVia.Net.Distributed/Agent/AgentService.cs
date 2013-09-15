using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace DuoVia.Net.Distributed.Agent
{
    public interface IAgentService
    {
        void KillSession();
        byte[] SyncInterface();
        object[] InvokeRemoteMethod(int methodHashCode, params object[] parameters);
        LogMessage[] SweepLogMessages();
    }

    public class AgentService : IAgentService
    {
        private Session _session = null;
        private IPEndPoint _serverEndPoint = null;

        public AgentService(Session session, IPEndPoint endPoint)
        {
            if (null == session) throw new ArgumentNullException("session");
            _session = session;
            _serverEndPoint = endPoint;

            //set logging info for this session
            Log.LogLevel = session.LogLevel;
            Log.EndPoint = endPoint;

            _singletonInstance = Activator.CreateInstance(_session.ImplementationType);
            CreateMethodMap();
        }

        public LogMessage[] SweepLogMessages()
        {
            var result = Log.SweepMessages();
            return result;
        }

        public void KillSession()
        {
            _session.KillSession();
        }

        private object _singletonInstance;
        private Dictionary<int, MethodInfo> _interfaceMethods;
        private Dictionary<int, bool[]> _methodParametersByRef;
        private ParameterTransferHelper _parameterTransferHelper = new ParameterTransferHelper();

        public byte[] SyncInterface()
        {
            //adapted from TcpHost
            var syncInfos = new List<MethodSyncInfo>();
            foreach (var kvp in _interfaceMethods)
            {
                var parameters = kvp.Value.GetParameters();
                var parameterTypes = new Type[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                    parameterTypes[i] = parameters[i].ParameterType;
                syncInfos.Add(new MethodSyncInfo
                {
                    MethodIdent = kvp.Key, 
                    MethodName = kvp.Value.Name, 
                    ParameterTypes = parameterTypes
                });
            }

            //send the sync data back to the client
            var formatter = new BinaryFormatter();
            var ms = new MemoryStream();
            formatter.Serialize(ms, syncInfos);
            ms.Seek(0, SeekOrigin.Begin);
            return ms.ToArray();
        }

        public object[] InvokeRemoteMethod(int methodHashCode, params object[] parameters)
        {
            //adapted from TcpHost
            if (_interfaceMethods.ContainsKey(methodHashCode))
            {
                var method = _interfaceMethods[methodHashCode];
                var isByRef = _methodParametersByRef[methodHashCode];

                //invoke the method
                object[] returnParameters;
                try
                {
                    object returnValue = method.Invoke(_singletonInstance, parameters);
                    //the result to the client is the return value (null if void) and the input parameters
                    returnParameters = new object[1 + parameters.Length];
                    returnParameters[0] = returnValue;
                    for (int i = 0; i < parameters.Length; i++)
                        returnParameters[i + 1] = isByRef[i] ? parameters[i] : null;
                }
                catch (Exception ex)
                {
                    //an exception was caught. Rethrow it client side
                    returnParameters = new object[] { ex };
                }
                return returnParameters; //TODO assure exception parameter handled on client channel side
            }
            return new object[] { new MethodAccessException("unknown method") };
        }

        private void CreateMethodMap()
        {
            //adapted from TcpHost
            var interfaces = _singletonInstance.GetType().GetInterfaces();
            _interfaceMethods = new Dictionary<int, MethodInfo>();
            _methodParametersByRef = new Dictionary<int, bool[]>();
            var currentMethodIdent = 0;
            foreach (var interfaceType in interfaces)
            {
                var methodInfos = interfaceType.GetMethods();
                foreach (var mi in methodInfos)
                {
                    _interfaceMethods.Add(currentMethodIdent, mi);
                    var parameterInfos = mi.GetParameters();
                    var isByRef = new bool[parameterInfos.Length];
                    for (int i = 0; i < isByRef.Length; i++)
                        isByRef[i] = parameterInfos[i].ParameterType.IsByRef;
                    _methodParametersByRef.Add(currentMethodIdent, isByRef);
                    currentMethodIdent++;
                }
            }
        }
    }
}
