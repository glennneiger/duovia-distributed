using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DuoVia.Net.Distributed
{
    internal class Packager
    {
        private static object _syncRoot = new object();
        private static volatile Packager _singleton = null;

        private PackageHash _hash;
        public PackageHash Hash {
            get { return _hash; }
            private set { _hash = value; }
        }

        private Packager(PackageHash hash)
        {
            this.Hash = hash;
        }

        /// <summary>
        /// Returns singleton of Packager with PackageHash created and set. Hashes current app domain's DLL, EXE and CONFIG files.
        /// </summary>
        /// <returns></returns>
        internal static Packager Create()
        {
            if (null != _singleton) return _singleton;
            lock (_syncRoot)
            {
                if (null != _singleton) return _singleton;
                var rootDir = AppDomain.CurrentDomain.BaseDirectory;
                var exeFiles = Directory.GetFiles(rootDir, "*.exe", SearchOption.AllDirectories);
                var dllFiles = Directory.GetFiles(rootDir, "*.dll", SearchOption.AllDirectories);
                var configFiles = Directory.GetFiles(rootDir, "*.config", SearchOption.AllDirectories);

                var list = exeFiles.Concat(dllFiles).Concat(configFiles).Where(n => !Path.GetFileName(n).Contains(".vshost.")).ToList();
                list.Sort();

                //get hash codes for each file
                var fileHashes = new ConcurrentDictionary<string, string>();
                Parallel.ForEach(list, file =>
                {
                    using (RIPEMD160 rip = RIPEMD160Managed.Create())
                    {
                        byte[] hashValue;
                        using (var fileStream = File.OpenRead(file))
                        {
                            fileStream.Position = 0;
                            hashValue = rip.ComputeHash(fileStream);
                        }
                        fileHashes.TryAdd(file, Convert.ToBase64String(hashValue));
                    }
                });

                var hash = new PackageHash()
                {
                    Name = AppDomain.CurrentDomain.FriendlyName,
                    Files = fileHashes.Keys.ToArray(),
                    Hashes = fileHashes.Values.ToArray()
                };

                _singleton = new Packager(hash);
                return _singleton;
            }
        }

        internal byte[] Package()
        {
            var rootDir = AppDomain.CurrentDomain.BaseDirectory;
            byte[] compressedObjectBytes = CompressFiles(rootDir, _hash);
            return compressedObjectBytes;
        }

        internal static byte[] CompressFiles(string rootDir, PackageHash hash)
        {
            rootDir = rootDir.TrimEnd(Path.DirectorySeparatorChar);
            var lengthOfRootDir = rootDir.Length;
            BinaryFormatter formatter = new BinaryFormatter();
            var fileBag = new Dictionary<string, byte[]>();
            foreach (var file in hash.Files)
            {
                var pathName = file.Substring(lengthOfRootDir).TrimStart(Path.DirectorySeparatorChar);
                byte[] bytes = File.ReadAllBytes(file);
                fileBag.Add(pathName, bytes);
            }

            byte[] compressedObjectBytes;
            using (var msCompressed = new MemoryStream())
            {
                using (var msObj = new MemoryStream())
                {
                    formatter.Serialize(msObj, fileBag);
                    msObj.Seek(0, SeekOrigin.Begin);

                    using (GZipStream gzs = new GZipStream(msCompressed, CompressionMode.Compress))
                    {
                        msObj.CopyTo(gzs);
                    }
                }
                compressedObjectBytes = msCompressed.ToArray();
            }
            return compressedObjectBytes;
        }

        internal static void UnpackPackage(string targetDir, byte[] package)
        {
            var uncompressedFiles = UncompressFiles(package);
            foreach (var kvp in uncompressedFiles)
            {
                var fileName = Path.Combine(targetDir, kvp.Key);
                var dirName = Path.GetDirectoryName(fileName);
                if (!Directory.Exists(dirName)) Directory.CreateDirectory(dirName); //probably overkill
                File.WriteAllBytes(fileName, kvp.Value);
            }
        }

        internal static Dictionary<string, byte[]> UncompressFiles(byte[] compressedBytes)
        {
            using (var msObj = new MemoryStream())
            {
                using (var msCompressed = new MemoryStream(compressedBytes))
                using (var gzs = new GZipStream(msCompressed, CompressionMode.Decompress))
                {
                    gzs.CopyTo(msObj);
                }
                msObj.Seek(0, SeekOrigin.Begin);
                BinaryFormatter formatter = new BinaryFormatter();
                var dsObj = (Dictionary<string, byte[]>)formatter.Deserialize(msObj);
                return dsObj;
            }
        }


    }

    [Serializable]
    public class PackageHash
    {
        public string Name { get; set; }
        public string[] Files { get; set; }
        public string[] Hashes { get; set; }

        public override bool Equals(object obj)
        {
            var hash = obj as PackageHash;
            if (null == obj) return false;
            if (this.Name != hash.Name) return false;
            if (null == this.Files || null == hash.Files) return false;
            if (null == this.Hashes || null == hash.Hashes) return false;
            if (this.Files.Length != hash.Files.Length || this.Hashes.Length != hash.Hashes.Length) return false;
            if (this.Files.Length != this.Hashes.Length || hash.Files.Length != hash.Hashes.Length) return false;
            for (int i = 0; i < this.Files.Length; i++)
            {
                if (this.Files[i] != hash.Files[i]) return false;
                if (this.Hashes[i] != hash.Hashes[i]) return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("{0}", Name ?? "Unknown");
        }
    }
}
