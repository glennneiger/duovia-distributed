using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using DuoVia.Net.Distributed.Server;
using System.Net;

namespace DuoVia.Net.DistributedConsoleHost
{
    class Program
    {
        static void Main(string[] args)
        {
            var port = 9096;
            //can host on specific IP or on Any
            var endpoint = new IPEndPoint(IPAddress.Any, port);
            using (var host = new Host(endpoint,
                packageStorageDirectory: @"C:\T\P",
                sessionExecutionDirectory: @"C:\T\E",
                runLocalInProcess: false))
            {
                Console.WriteLine("hosting started - press Enter to quit");
                Console.ReadLine();
            }
        }
    }
}
