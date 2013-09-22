using DuoVia.Net.Distributed;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace spidey
{
    class Program
    {
        static void Main(string[] args)
        {
            DoWarmUp();
            DoOneUrl();
            DoTenUrlsInSequence();
            DoTenUrlsInParallel();

            //TODO - flip the boolean to run on other servers
            DoTenUrlsThreeTimesEachAroundTheWorldInParallel(runLocal: false);
            Console.WriteLine("Hit Enter to exit.");
            Console.ReadLine();
        }

        private static void DoWarmUp()
        {
            var testUrl = @"http://www.tsjensen.com/blog/post/2013/04/22/Distributed-Parallel-Processing-in-Simple-NET-Console-Application.aspx";
            ISpeedTest test = new SpeedTest();
            var result = test.GetSpeed(testUrl);
        }

        private static void DoOneUrl()
        {
            var testUrl = @"http://www.tsjensen.com/blog";
            Console.WriteLine("Do one url: {0}", testUrl);
            ISpeedTest test = new SpeedTest();
            var result = test.GetSpeed(testUrl);
            Console.WriteLine("r:{0}, s:{1}, b:{2}", 
                result.ResponseTimeMs, result.ReadStreamTimeMs, result.ResponseLength);
            Console.WriteLine(string.Empty);
        }

        private static string[] TestUrls = new string[]
            {
                @"http://www.tsjensen.com/blog",
                @"http://www.google.com",
                @"http://www.yahoo.com",
                @"http://www.linkedin.com",
                @"http://www.ancestry.com",
                @"http://www.cnn.com",
                @"http://www.stackoverflow.com",
                @"http://www.whitehouse.gov",
                @"http://www.byu.edu",
                @"http://www.utah.edu"
            };

        private static void DoTenUrlsInSequence()
        {
            Console.WriteLine("Do 10 urls in sequence");
            var sw = Stopwatch.StartNew();
            ISpeedTest test = new SpeedTest();
            foreach (var url in TestUrls)
            {
                var result = test.GetSpeed(url);
                Console.WriteLine("r:{0}, s:{1}, b:{2}, u:{3}", 
                    result.ResponseTimeMs, result.ReadStreamTimeMs, result.ResponseLength, result.Url);
            }
            sw.Stop();
            Console.WriteLine("Total elapsed time: {0}", sw.ElapsedMilliseconds);
            Console.WriteLine(string.Empty);
        }

        private static void DoTenUrlsInParallel()
        {
            Console.WriteLine("Do 10 urls in parallel");
            var sw = Stopwatch.StartNew();
            ISpeedTest test = new SpeedTest();
            Parallel.ForEach(TestUrls, (url) =>
            {
                var result = test.GetSpeed(url);
                Console.WriteLine("r:{0}, s:{1}, b:{2}, u:{3}",
                    result.ResponseTimeMs, result.ReadStreamTimeMs, result.ResponseLength, result.Url);
            });
            sw.Stop();
            Console.WriteLine("Total elapsed time: {0}", sw.ElapsedMilliseconds);
            Console.WriteLine(string.Empty);
        }

        private static void DoTenUrlsThreeTimesEachAroundTheWorldInParallel(bool runLocal = false)
        {
            var serverEndpoints = new IPEndPoint[0];
            if (runLocal)
            {
                serverEndpoints = new IPEndPoint[] { new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9096) };
            }
            else
            {
                //these server names are temporary - to run this test use your own
                var servers = new string[]
                {
                    "t5westus.cloudapp.net",
                    "t5eastus.cloudapp.net",
                    "t5northeu.cloudapp.net",
                    "t5westeu.cloudapp.net",
                    "t5soeastasia.cloudapp.net",
                    "t5eastasia.cloudapp.net"
                };

                serverEndpoints = new IPEndPoint[servers.Length];
                for (int i = 0; i < servers.Length; i++)
                {
                    var host = Dns.GetHostAddresses(servers[i]);
                    var ip = (from n in host where n.AddressFamily == AddressFamily.InterNetwork select n).First();
                    serverEndpoints[i] = new IPEndPoint(ip, 9096);
                }
            }

            float subscriptionRate = 2.0f; //oversubscribed 
            int logPollingIntervalSeconds = 2;
            using (DistributedClient<ISpeedTest> client = Distributor.Connect<ISpeedTest>(typeof(SpeedTest),
                subscriptionRate,
                logPollingIntervalSeconds,
                LogLevel.Debug,
                serverEndpoints))
            {
                for (int i = 0; i < 3; i++)
                {
                    var sw = Stopwatch.StartNew();
                    Console.WriteLine(@"round:{0}", i + 1);
                    var loopResult = client.ForEach(TestUrls, (url, proxy) => proxy.GetSpeed(url));
                    foreach (var result in loopResult.Results)
                    {
                        Console.WriteLine(@"r:{0}, s:{1}, b:{2}, on: {3}, u:{4}",
                            result.ResponseTimeMs, result.ReadStreamTimeMs, result.ResponseLength, result.MachineName, result.Url);
                    }
                    sw.Stop();
                    Console.WriteLine("Total elapsed time: {0}", sw.ElapsedMilliseconds);
                    Console.WriteLine(string.Empty);
                }
            }
        }
    }

    public interface ISpeedTest
    {
        SpeedResults GetSpeed(string url);
    }

    public class SpeedTest : ISpeedTest
    {
        public SpeedResults GetSpeed(string url)
        {
            // Set a default policy level for the "http:" and "https" schemes.
            var policy = new HttpRequestCachePolicy(HttpRequestCacheLevel.Default);
            HttpWebRequest.DefaultCachePolicy = policy;

            var html = string.Empty;
            var request = (HttpWebRequest)WebRequest.Create(url);

            // Define a cache policy for this request only. 
            var noCachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);
            request.CachePolicy = noCachePolicy;

            var sw = Stopwatch.StartNew();
            var response = request.GetResponse();
            sw.Stop();
            var responseTime = sw.ElapsedMilliseconds;
            sw.Restart();
            using (var stream = response.GetResponseStream())
            {
                if (null != stream)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        html = reader.ReadToEnd();
                    }
                }
            }
            sw.Stop();
            var readTime = sw.ElapsedMilliseconds;
            return new SpeedResults()
            {
                Url = url,
                ResponseTimeMs = responseTime,
                ReadStreamTimeMs = readTime,
                ResponseLength = html.Length,
                MachineName = Environment.MachineName
            };
        }
    }

    [Serializable]
    public class SpeedResults
    {
        public string Url { get; set; }
        public long ResponseTimeMs { get; set; }
        public long ReadStreamTimeMs { get; set; }
        public int ResponseLength { get; set; }
        public string MachineName { get; set; }
    }
}
