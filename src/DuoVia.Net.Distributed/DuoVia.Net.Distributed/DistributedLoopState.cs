using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DuoVia.Net.Distributed
{
    public class DistributedLoopState
    {
        private volatile bool _continueProcessing = true;
        private volatile int _breakIteration = -1;

        internal DistributedLoopState()
        {
            ExceptionInfos = new ConcurrentBag<DistributedExceptionInfo>();
            Tasks = new List<Task>();
        }

        internal ConcurrentBag<DistributedExceptionInfo> ExceptionInfos { get; set; }
        internal List<Task> Tasks { get; set; }

        public void Break(int iteration)
        {
            if (_continueProcessing)
            {
                _continueProcessing = false;
                _breakIteration = iteration;
            }
        }

        internal bool ContinueProcessing { get { return _continueProcessing; } }
        internal int BreakIteration { get { return _breakIteration; } }
    }

    public class DistributedLoopState<TReturn> : DistributedLoopState
    {
        internal DistributedLoopState() : base()
        {
            ReturnValues = new ConcurrentBag<TReturn>();
        } 

        public ConcurrentBag<TReturn> ReturnValues { get; internal set; }
    }
}
