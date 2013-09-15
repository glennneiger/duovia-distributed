using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace DuoVia.Net.Distributed
{
    public class LogMessageEventArgs : EventArgs
    {
        internal ConcurrentBag<LogMessage[]> LocalBag { get; set; }

        public Exception LastLogPollingException { get; internal set; }
        public DateTime LastLogPollingExceptionTime { get; internal set; }
        public IPEndPoint LastLogPollingExceptionServer { get; internal set; }

        public IEnumerable<LogMessage> ReadMessages()
        {
            if (null != LocalBag)
            {
                foreach (var msgs in LocalBag)
                {
                    foreach (var msg in msgs)
                    {
                        yield return msg;
                    }
                }
            }
        }
    }
}
