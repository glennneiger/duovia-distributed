using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using DuoVia.Net.Distributed.Server;
using System.Net;

namespace DuoVia.Net.DistributedConsoleHost
{
    class Program
    {
        static void Main(string[] args)
        {
            var ip = ConfigurationManager.AppSettings["hostIP"];
            var port = Convert.ToInt32(ConfigurationManager.AppSettings["hostPort"]);
            var endpoint = new IPEndPoint(IPAddress.Parse(ip), port);

            using (var host = new Host(endpoint,
                packageStorageDirectory: @"C:\T\P",
                sessionExecutionDirectory: @"C:\T\E",
                runLocalInProcess: false))
            {
                

                Console.WriteLine("hosting starte - press Enter to quit");
                Console.ReadLine();
            }
        }
    }
}
