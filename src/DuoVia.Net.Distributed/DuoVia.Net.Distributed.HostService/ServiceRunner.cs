using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Diagnostics;
using System.Configuration;
using DuoVia.Net.Distributed.Server;

namespace DuoVia.Net.Distributed.HostService
{
    internal class ServiceRunner
    {
        private Host _host = null;

        public void Start(string[] args)
        {
            try
            {
                var port = 9096;
                var endpoint = new IPEndPoint(IPAddress.Any, port);
                var packageStorageDirectory = @"C:\T\P";
                var sessionExecutionDirectory = @"C:\T\E";
                _host = new Host(endpoint, packageStorageDirectory, sessionExecutionDirectory);
            }
            catch (Exception e)
            {
                //do your exception handling thing
                e.ProcessUnhandledException("DuoVia.Net.Distributed.ServiceHost");
            }
        }

        public void Stop()
        {
            try
            {
                if (null != _host)
                {
                    _host.Dispose();
                }
            }
            catch (Exception e)
            {
                //do your exception handling thing
                e.ProcessUnhandledException("DuoVia.Net.Distributed.ServiceHost");
            }
        }
    }

    public static class GeneralExtensions
    {
        public static void ProcessUnhandledException(this Exception ex, string appName)
        {
            //log to Windows EventLog
            try
            {
                string sSource = System.Reflection.Assembly.GetEntryAssembly().FullName;
                string sLog = "Application";
                string sEvent = string.Format("Unhandled exception in {0}: {1}", appName, ex.ToString());
                if (!EventLog.SourceExists(sSource))
                    EventLog.CreateEventSource(sSource, sLog);

                EventLog.WriteEntry(sSource, sEvent);
                EventLog.WriteEntry(sSource, sEvent, EventLogEntryType.Error, 999);
            }
            catch
            {
                //do nothing if this one fails
            }
        }

        public static string Flatten(this string src)
        {
            return src.Replace("\r", ":").Replace("\n", ":");
        }
    }

}

