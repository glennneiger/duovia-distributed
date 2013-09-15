using System.IO;
using DuoVia.Net.TcpIp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace DuoVia.Net.Distributed.Server
{
    public class Host : IDisposable
    {
        private ServiceHost _serviceHost;
        private TcpHost _tcpHost;

        public Host(IPEndPoint endPoint, 
            string packageStorageDirectory = null, string sessionExecutionDirectory = null, bool runLocalInProcess = false)
        {
            var rootDir = AppDomain.CurrentDomain.BaseDirectory;
            if (null == packageStorageDirectory)
            {
                packageStorageDirectory = Path.Combine(rootDir, @"_dpack");
                Directory.CreateDirectory(packageStorageDirectory);
            }
            if (null == sessionExecutionDirectory)
            {
                sessionExecutionDirectory = Path.Combine(rootDir, @"_dexec");
                Directory.CreateDirectory(sessionExecutionDirectory);
            }

            //must include some info about this host
            _serviceHost = new ServiceHost(endPoint, packageStorageDirectory, sessionExecutionDirectory, runLocalInProcess);
            _tcpHost = new TcpHost(_serviceHost, endPoint);
            _tcpHost.Open();
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
                    _tcpHost.Dispose(); //will dispose of singleton instance _serviceHost
                }
            }
        }

        #endregion
    }
}
