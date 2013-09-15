using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DuoVia.Net.Distributed;
using DuoVia.Net.Distributed.Server;

namespace DuoVia.Net.DistributedConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {
            //wait for other host to start
            Thread.Sleep(3000);

            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9096);
            float subscriptionRate = 0.5f;
            int logPollingIntervalSeconds = 1;
            using (DistributedClient<ITest> client = Distributor.Connect<ITest>(typeof(Test), 
                subscriptionRate, 
                logPollingIntervalSeconds, 
                LogLevel.Debug, 
                endpoint))
            {
                client.LogMessageReceived += ClientOnLogMessageReceived;

                //set data on each server node - one call per server node
                client.OncePerNode(new Tuple<int, int>(501, 3001), 
                    (source, proxy) => proxy.SetFromTo(source.Item1, source.Item2));

                var distributedLoopResult = client.For(0, 10, 
                    (index, proxy) => proxy.GetName(index.ToString(CultureInfo.InvariantCulture)));
                    
                //in a real program, deal with distributedLoopResult.Exceptions if any

                var results = distributedLoopResult.Results.ToArray();
                foreach(var result in results) Console.WriteLine(result);
                client.SweepLogs(); //one last log sweep

                //now do it again with results from first using ForEach
                var distributedLoopResult2 = client.ForEach(results, 
                    (source, proxy) => proxy.GetName(source));

                //in a real program, deal with distributedLoopResult2.Exceptions if any

                var results2 = distributedLoopResult2.Results.ToArray();
                foreach (var result in results2) Console.WriteLine(result);
                client.SweepLogs(); //one last log sweep
            }

            Console.WriteLine("");
            Console.WriteLine("done - hit enter to quit");
            Console.ReadLine();
        }

        //print out logs - could also be done after all is said and done, ignoring this event
        private static void ClientOnLogMessageReceived(object sender, LogMessageEventArgs eventArgs)
        {
            Console.WriteLine("");
            Console.WriteLine("Log messages:");
            foreach (var msg in eventArgs.ReadMessages())
            {
                Console.WriteLine(msg);    
            }
            Console.WriteLine("");
            if (null != eventArgs.LastLogPollingException)
            {
                Console.WriteLine(@"Error at {0} on {1} as: {2}", 
                    eventArgs.LastLogPollingExceptionTime, 
                    eventArgs.LastLogPollingExceptionServer, 
                    eventArgs.LastLogPollingException);
                Console.WriteLine("");
            }
        }
    }

    public interface ITest
    {
        void SetFromTo(long from, long to);
        string GetName(string query);
    }

    public class Test : ITest
    {
        private long _from = 1;
        private long _to = 1000;

        public void SetFromTo(long from, long to)
        {
            _from = from;
            _to = to;
            Log.Info("from {0} and to {1} set", from, to);
        }

        public string GetName(string query)
        {
            var primes = CalculatePrimes(_from, _to);
            return "name:" + query + primes;
        }

        //brute force prime finder - consumes more and more time
        private int CalculatePrimes(long from, long to)
        {
            var rand = new Random();
            var prime = rand.Next(1, 9);
            Thread.Sleep(prime * 1000);
            Log.Warning("log message " + prime);
            //Thread.Sleep(600000); //test very long response time
            return prime;
        }
    }
}
