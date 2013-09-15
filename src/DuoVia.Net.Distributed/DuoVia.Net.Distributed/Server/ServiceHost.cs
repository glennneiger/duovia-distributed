using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Management;
using System.Net;
using System.Runtime.Hosting;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using DuoVia.Net.Distributed.Agent;
using DuoVia.Net.NamedPipes;

namespace DuoVia.Net.Distributed.Server
{
    public interface IServiceHost
    {
        bool HasPackage(PackageHash hash);
        void AddUpdatePackage(PackageHash hash, byte[] package);
        DistributedSessionNode CreateSession(DistributedSessionRequest request);

        void KillSession(Guid sessionId);
        byte[] SyncInterface(Guid sessionId);
        object[] InvokeRemoteMethod(Guid sessionId, int methodHashCode, params object[] parameters);
        LogMessage[] SweepLogMessages(Guid sessionId);
    }

    public class ServiceHost : IServiceHost
    {
        private readonly string _packageStorageDirectory;
        private readonly string _sessionExecutionDirectory;
        private readonly bool _runLocalInProcess;
        private readonly IPEndPoint _endPoint;
        private readonly ConcurrentDictionary<Guid, string> _sessionExePaths = new ConcurrentDictionary<Guid, string>(); 

        public ServiceHost(IPEndPoint endPoint, string packageStorageDirectory, string sessionExecutionDirectory, bool runLocalInProcess)
        {
            _endPoint = endPoint;
            _packageStorageDirectory = packageStorageDirectory;
            _sessionExecutionDirectory = sessionExecutionDirectory;
            _runLocalInProcess = runLocalInProcess;
        }

        public bool HasPackage(PackageHash hash)
        {
            var pkgFile = Path.Combine(_packageStorageDirectory, hash.Name + ".pkg");
            if (!File.Exists(pkgFile)) return false;
            var formatter = new BinaryFormatter();
            PackageHash existingHash = null;
            using (var fs = File.OpenRead(pkgFile))
            {
                existingHash = (PackageHash)formatter.Deserialize(fs);
            }
            return hash.Equals(existingHash);
        }

        public void AddUpdatePackage(PackageHash hash, byte[] package)
        {
            var pkgFile = Path.Combine(_packageStorageDirectory, hash.Name + ".pkg");
            var pkgDir = Path.Combine(_packageStorageDirectory, hash.Name + "_pkg");
            if (Directory.Exists(pkgDir)) Directory.Delete(pkgDir, true);
            Directory.CreateDirectory(pkgDir);
            Packager.UnpackPackage(pkgDir, package);
            if (File.Exists(pkgFile)) File.Delete(pkgFile);
            var formatter = new BinaryFormatter();
            using (var fs = File.OpenWrite(pkgFile))
            {
                formatter.Serialize(fs, hash);
                fs.Flush();
            }
        }

        public DistributedSessionNode CreateSession(DistributedSessionRequest request)
        {
            var pkgDir = Path.Combine(_packageStorageDirectory, request.PackageName + "_pkg");
            if (!Directory.Exists(pkgDir)) throw new FileNotFoundException("Package does not exist.");
            
            //deploy files
            var instanceKey = DateTime.UtcNow.ToString("yyyyMMddhhmmssfff");
            var destDir = Path.Combine(_sessionExecutionDirectory, request.PackageName, instanceKey);
            Directory.CreateDirectory(destDir);
            _sessionExePaths.TryAdd(request.SessionId, destDir);

            var exeFiles = Directory.GetFiles(pkgDir, "*.exe", SearchOption.AllDirectories);
            var dllFiles = Directory.GetFiles(pkgDir, "*.dll", SearchOption.AllDirectories);
            var configFiles = Directory.GetFiles(pkgDir, "*.config", SearchOption.AllDirectories);

            var allFiles = exeFiles.Concat(dllFiles).Concat(configFiles).Where(n => !Path.GetFileName(n).Contains(".vshost.")).ToArray();
            foreach (var file in allFiles)
            {
                var dest = file.Replace(pkgDir, string.Empty).TrimStart(Path.DirectorySeparatorChar);
                var destFile = Path.Combine(destDir, dest);
                Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                File.Copy(file, destFile);
            }

            //deploy exe and run with specific evironment variables from session request
            var dppFile = Path.Combine(destDir, "dpp-" + instanceKey + ".exe");
            File.WriteAllBytes(dppFile, EmbeddedResources.dpp);

            //make any .exe.config file int dppFile.config
            var exeConfigs = Directory.GetFiles(destDir, "*.exe.config");
            if (exeConfigs.Length > 0)
            {
                File.Copy(exeConfigs[0], dppFile + ".config");
            }

            //0 = sessionId
            //1 = contractType
            //2 = implementationType
            //3 = hoursToLive

            if (_runLocalInProcess) //typically only used in debugging locally
            {
                //do app domain thing
                var args = new string[]
                {
                    request.SessionId.ToString(),
                    request.ContractTypeName,
                    request.ImplementationTypeName,
                    ((request.HoursToLive == 0) ? 48 : request.HoursToLive).ToString(),
                    ((int)request.LogLevel).ToString(),
                    _endPoint.ToString()
                };

                var setup = new AppDomainSetup()
                {
                    ApplicationBase = destDir
                };

                if (File.Exists(dppFile + ".config")) setup.ConfigurationFile = dppFile + ".config";
                var domain = AppDomain.CreateDomain(Path.GetFileNameWithoutExtension(dppFile), null, setup);
                Task.Factory.StartNew(() => domain.ExecuteAssembly(dppFile, args));
            }
            else
            {
                //do process start - this is most common - avoids having app domain congestion
                var argsCmd = request.SessionId
                    + " \"" + request.ContractTypeName
                    + "\" \"" + request.ImplementationTypeName
                    + "\" " + ((request.HoursToLive == 0) ? 48 : request.HoursToLive)
                    + " " + ((int)request.LogLevel)
                    + " " + _endPoint;
                var info = new ProcessStartInfo(dppFile, argsCmd);
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                Task.Factory.StartNew(() => Process.Start(info));
            }

            var result = new DistributedSessionNode()
            {
                PackageName = request.PackageName,
                SessionId = request.SessionId,
                EndPoint = _endPoint,
                LogicProcessorCount = Environment.ProcessorCount,
                Memory = GetSystemMemory()
            };
            return result;
        }

        private MemoryDetail GetSystemMemory()
        {
            var winQuery = new ObjectQuery("SELECT * FROM CIM_OperatingSystem");
            var searcher = new ManagementObjectSearcher(winQuery);
            foreach (ManagementObject item in searcher.Get())
            {
                var totalVisibleMemorySize = Convert.ToUInt64(item["TotalVisibleMemorySize"]);
                var totalVirtualMemorySize = Convert.ToUInt64(item["TotalVirtualMemorySize"]);
                var freePhysicalMemory = Convert.ToUInt64(item["FreePhysicalMemory"]);
                var freeVirtualMemory = Convert.ToUInt64(item["FreeVirtualMemory"]);
                var result = new MemoryDetail()
                {
                    TotalVisibleMemorySize = totalVisibleMemorySize,
                    TotalVirtualMemorySize = totalVirtualMemorySize,
                    FreePhysicalMemory = freePhysicalMemory,
                    FreeVirtualMemory = freeVirtualMemory
                };
                return result;
            }
            return new MemoryDetail();
        }

        public void KillSession(Guid sessionId)
        {
            var pipe = "dpp-" + sessionId;
            using (var client = new AgentClient(new NpEndPoint(pipe, 10000)))
            {
                client.KillSession();
            }
            //and delete directory
            string exePath;
            _sessionExePaths.TryRemove(sessionId, out exePath);
            //TODO - try to clean up old exe directories
        }

        public byte[] SyncInterface(Guid sessionId)
        {
            var pipe = "dpp-" + sessionId;
            using (var client = new AgentClient(new NpEndPoint(pipe, 10000)))
            {
                return client.SyncInterface();
            }
        }

        public object[] InvokeRemoteMethod(Guid sessionId, int methodHashCode, params object[] parameters)
        {
            var pipe = "dpp-" + sessionId;
            using (var client = new AgentClient(new NpEndPoint(pipe, 10000)))
            {
                return client.InvokeRemoteMethod(methodHashCode, parameters);
            }
        }

        public LogMessage[] SweepLogMessages(Guid sessionId)
        {
            var pipe = "dpp-" + sessionId;
            using (var client = new AgentClient(new NpEndPoint(pipe, 10000)))
            {
                return client.SweepLogMessages();
            }
        }
    }
}
