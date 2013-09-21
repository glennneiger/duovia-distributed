using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using DuoVia.Net;

namespace DuoVia.Net.Distributed
{
    public class DistributedChannel : Channel
    {
        private DistributedEndPoint _endpoint = null;
        private MethodSyncInfo[] _syncInfos;

        public DistributedEndPoint DistributedEndPoint { get { return _endpoint; } }

        /// <summary>
        /// Creates a connection to the concrete object handling method calls on the server side
        /// </summary>
        /// <param name="endpoint"></param>
        public DistributedChannel(DistributedEndPoint endpoint)
        {
            _endpoint = endpoint;
            SyncInterface();
        }

        /// <summary>
        /// This method asks the server for a list of identifiers paired with method
        /// names and -parameter types. This is used when invoking methods server side.
        /// </summary>
        protected override void SyncInterface()
        {
            byte[] syncBytes;
            using (var client = new ServiceClient(_endpoint.EndPoint))
            {
                syncBytes = client.SyncInterface(_endpoint.SessionId);
            }
            _syncInfos = ((List<MethodSyncInfo>)syncBytes.ToDeserializedObject()).ToArray();
        }

        /// <summary>
        /// Invokes the method with the specified parameters.
        /// </summary>
        /// <param name="parameters">Parameters for the method call</param>
        /// <returns>An array of objects containing the return value (index 0) and the parameters used to call
        /// the method, including any marked as "ref" or "out"</returns>
        protected override object[] InvokeMethod(params object[] parameters)
        {
            //not thread safe
            var callingMethod = (new StackFrame(1)).GetMethod();
            var methodName = callingMethod.Name;
            var methodParams = callingMethod.GetParameters();
            var medthodId = -1;
            for (int index = 0; index < _syncInfos.Length; index++)
            {
                var si = _syncInfos[index];
                //first of all the method names must match
                if (si.MethodName == methodName)
                {
                    //second of all the parameter types and -count must match
                    if (methodParams.Length == si.ParameterTypes.Length)
                    {
                        var matchingParameterTypes = true;
                        for (int i = 0; i < methodParams.Length; i++)
                            if (!methodParams[i].ParameterType.FullName.Equals(si.ParameterTypes[i].FullName))
                            {
                                matchingParameterTypes = false;
                                break;
                            }
                        if (matchingParameterTypes)
                        {
                            medthodId = si.MethodIdent;
                            break;
                        }
                    }
                }
            }

            if (medthodId < 0)
                throw new Exception(string.Format("Cannot match method '{0}' to its server side equivalent", callingMethod.Name));

            byte[] bytes = new byte[0];
            bool exceptionThrown;
            using (var client = new ServiceClient(_endpoint.EndPoint))
            {
                bytes = client.InvokeRemoteMethod(_endpoint.SessionId, medthodId, parameters.ToSerializedBytes(), out exceptionThrown);
            }

            var results = (object[])bytes.ToDeserializedObject();
            if (exceptionThrown)
                throw (Exception)results[0];

            return results;
        }


        #region IDisposable override

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true; //prevent second call to Dispose
                if (disposing)
                {
                    //dispose of disposables - none at the moment
                }
            }
        }

        #endregion
    }
}