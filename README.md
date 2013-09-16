duovia-distributed
==================

# DuoVia.Net.Distributed

### A Distributed Task Parallel Processing Library for .NET.

Get the [NuGet Package][3]

The [Task Paralel Library][2] (TPL) introduced in .NET 4.0 and [async and await][3] asynchronous programming introduced in .NET 4.5 make parallel programming on a single machine so easy.

But what if one machine is not enough? The current solutions for doing distributed parallel processing are too complicated.

Even our own [DuoVia.MpiVisor][4] solution can be too complicated. 

In DuoVia.Net.Distributed, we combined what we learned with DuoVia.MpiVisor with the concepts behind the Parallel class in the TPL.

Rather than telling you how this works, we prefer to show you here. There is no simpler way of doing distributed parallel programming while maintaining the ability to run and debug locally in Visual Studio.

First the node host. This can be a console app or windows service running on as many computers as you wish. In fact, for testing your code, you could host a single node in your own application.

```C#
	using System;
	using System.Configuration;
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
					Console.WriteLine("hosting started - press Enter to quit");
					Console.ReadLine();
				}
			}
		}
	}
```
Second, your code that needs to run in parallel across multiple machines.

```C#
	using System;
	using System.Globalization;
	using System.Linq;
	using System.Net;
	using System.Threading;
	using DuoVia.Net.Distributed;

	namespace DuoVia.Net.DistributedConsoleTest
	{
		class Program
		{
			static void Main(string[] args)
			{
				var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9096);
				float subscriptionRate = 0.5f; //create one proxy for every two logical processors on each node
				int logPollingIntervalSeconds = 1;
				using (DistributedClient<ITest> client = Distributor.Connect<ITest>(typeof(Test), 
					subscriptionRate, 
					logPollingIntervalSeconds, 
					LogLevel.Debug, 
					endpoint)) //last arg is params IPEndPoint[] so connect to all node servers you want
				{
					client.LogMessageReceived += ClientOnLogMessageReceived;

					//set data on each server node - one call per server node
					client.OncePerNode(new Tuple<int, int>(501, 3001), 
						(source, proxy) => proxy.SetFromTo(source.Item1, source.Item2));

					//similar to Parallel.For
					var distributedLoopResult = client.For(0, 10, 
						(index, proxy) => proxy.GetName(index.ToString(CultureInfo.InvariantCulture)));
						
					//in a real program, deal with distributedLoopResult.Exceptions if any

					var results = distributedLoopResult.Results.ToArray();
					foreach(var result in results) Console.WriteLine(result);
					client.SweepLogs(); //one last log sweep

					//now do it again with results from first run
					//similar to Parallel.ForEach
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

			//brute force time waster for illustration only
			private int CalculatePrimes(long from, long to)
			{
				var rand = new Random();
				var prime = rand.Next(1, 9);
				Thread.Sleep(prime * 1000);
				Log.Warning("log message " + prime);
				//test very long response time - try it
				//Thread.Sleep(600000); 
				return prime;
			}
		}
	}

Of course the code above is completely contrived but it serves to demonstrate just how easy it is to use the DuoVia.Net.Distribued libary.

[1]: http://nuget.org/packages/DuoVia.Net.Distributed/   "NuGet Package"
[2]: http://msdn.microsoft.com/en-us/library/dd460717.aspx   "Task Paralel Library"
[3]: http://msdn.microsoft.com/en-us/library/vstudio/hh191443.aspx    "async and await"
[4]: https://github.com/duovia/duovia-mpivisor    "DuoVia.MpiVisor"

