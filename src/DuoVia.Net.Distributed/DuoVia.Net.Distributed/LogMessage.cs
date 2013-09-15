using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Net;
using System.Text;

namespace DuoVia.Net.Distributed
{
    public enum LogLevel
    {
        Error = 0,
        Warning = 1,
        Info = 2,
        Debug = 3
    }

    [Serializable]
    public class LogMessage
    {
        public DateTime TimeStamp { get; set; }
        public DateTime TimeStampUtc { get; set; }
        public string ServerName { get; set; }
        public IPEndPoint EndPoint { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; }

        public override string ToString()
        {
            return string.Format("{0}\t{1}\t{2}\t{3}", 
                TimeStamp.ToString("yyyyMMdd_hhmmss.fff"),
                Level, 
                ServerName, 
                Message);
        }
    }
}
