using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace DuoVia.Net.Distributed
{
    public static class Log
    {
        private volatile static LogLevel _logLevel = LogLevel.Info;
        private static volatile IPEndPoint _endPoint;

        private static ConcurrentBag<LogMessage> _messages = new ConcurrentBag<LogMessage>();

        public static LogMessage[] SweepMessages()
        {
            LogMessage[] result = new LogMessage[0];
            if (!_messages.IsEmpty)
            {
                LogMessage[] msgs = null;
                lock (_messages)
                {
                    msgs = _messages.ToArray();
                    _messages = new ConcurrentBag<LogMessage>();
                }
                if (null != msgs && msgs.Length > 0)
                {
                    result = msgs.OrderBy(x => x.TimeStampUtc).ToArray();
                }
            }
            return result;
        }

        public static IPEndPoint EndPoint
        {
            get
            {
                return _endPoint;
            }
            internal set
            {
                _endPoint = value;
            }
        }

        public static LogLevel LogLevel 
        {
            get
            {
                return _logLevel;
            }
            internal set
            {
                _logLevel = value;
            } 
        }

        public static void Info(string formattedMessage, params object[] args)
        {
            if (_logLevel >= LogLevel.Info)
            {
                WriteMessage(LogLevel.Info, formattedMessage, args);
            }
        }

        public static void Warning(string formattedMessage, params object[] args)
        {
            if (_logLevel >= LogLevel.Warning)
            {
                WriteMessage(LogLevel.Warning, formattedMessage, args);
            }
        }

        public static void Error(string formattedMessage, params object[] args)
        {
            WriteMessage(LogLevel.Error, formattedMessage, args);
        }

        public static void Debug(string formattedMessage, params object[] args)
        {
            if (_logLevel >= LogLevel.Debug)
            {
                WriteMessage(LogLevel.Debug, formattedMessage, args);
            }
        }

        private static void WriteMessage(LogLevel logLevel, string formattedMessage, params object[] args)
        {
            var msg = new LogMessage
            {
                TimeStamp = DateTime.Now,
                TimeStampUtc = DateTime.UtcNow,
                Level = logLevel,
                ServerName = Environment.MachineName,
                Message = string.Format(formattedMessage, args).Flatten()
            };
            _messages.Add(msg);
        }

        public static string Flatten(this string src)
        {
            return src.Replace("\r", ":").Replace("\n", ":");
        }

    }
}
