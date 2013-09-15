using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace DuoVia.Net.Distributed
{
    public class DistributedProxy
    {
        public static TInterface CreateProxy<TInterface>(DistributedEndPoint endpoint) where TInterface : class
        {
            return ProxyFactory.CreateProxy<TInterface>(typeof(DistributedChannel), typeof(DistributedEndPoint), endpoint);
        }
    }

    public class DistributedEndPoint
    {
        public Guid SessionId { get; set; }
        public IPEndPoint EndPoint { get; set; }
    }
}
