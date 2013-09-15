using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace DuoVia.Net.Distributed
{
    [Serializable]
    public class DistributedSessionRequest
    {
        public DistributedSessionRequest()
        {
            SessionId = Guid.NewGuid();
            HoursToLive = 48;
        }

        public Guid SessionId { get; private set; }
        public string PackageName { get; set; }
        public short HoursToLive { get; set; }
        public LogLevel LogLevel { get; set; }


        /// <summary>
        /// The type, assembly contract that will be hosted.
        /// </summary>
        public string ContractTypeName { get; set; }

        /// <summary>
        /// The type, assembly for the contract implementation.
        /// </summary>
        public string ImplementationTypeName { get; set; }
    }

    [Serializable]
    public class DistributedSessionNode
    {
        public Guid SessionId { get; set; }
        public string PackageName { get; set; }
        public IPEndPoint EndPoint { get; set; }
        public int LogicProcessorCount { get; set; }
        public MemoryDetail Memory { get; set; }
    }

    [Serializable]
    public class MemoryDetail
    {
        public ulong TotalVisibleMemorySize { get; set; }
        public ulong TotalVirtualMemorySize { get; set; }
        public ulong FreePhysicalMemory { get; set; }
        public ulong FreeVirtualMemory { get; set; }
    }
}
