using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;

namespace DuoVia.Net.Distributed
{
    public class DistributedLoopResult
    {
        internal DistributedLoopResult()
        {
            Exceptions = new DistributedExceptionInfo[0];
        }

        /// <summary>
        /// The index, original source object (if any), and the Exception thrown when executing the action of function passed to the For or ForEach;
        /// </summary>
        public IEnumerable<DistributedExceptionInfo> Exceptions { get; internal set; }

        /// <summary>
        /// The index upon which the loop was broken.
        /// </summary>
        public int BreakIteration { get; internal set; }
    }

    public class DistributedLoopResult<TReturn> : DistributedLoopResult
    {
        internal DistributedLoopResult() : base()
        {
            Results = new TReturn[0];
        } 

        public IEnumerable<TReturn> Results { get; internal set; }
    }

    public class DistributedExceptionInfo
    {
        /// <summary>
        /// The index in the loop through the For or the ForEach source.
        /// </summary>
        public int Index { get; internal set; }

        /// <summary>
        /// The source parameter object on which the exception occurred when executing the action or function.
        /// </summary>
        public object Source { get; internal set; }

        /// <summary>
        /// The endpoint on which the exception occurred.
        /// </summary>
        public IPEndPoint EndPoint { get; internal set; }

        /// <summary>
        /// The exception that was raised by the action or function executed.
        /// </summary>
        public Exception Exception { get; internal set; }

        /// <summary>
        /// Time the exception was raised.
        /// </summary>
        public DateTime TimeStamp { get; internal set; }
    }
}
