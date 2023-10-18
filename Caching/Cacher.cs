﻿using System.Text.Json;
using SynchronizerLibrary.CommonServices.LocalGroups;

namespace SynchronizerLibrary.Caching
{
    public class Cacher
    {
        private const string CacheFilePolicyPrefix = "rapNamesCache";
        private const string CacheFileLocalGroupPrefix = "localGroupsCache";
        private const string CacheFileExtension = ".json";

        static public void SavePolicyCacheToFile(List<string> _rapNamesCache)
        {
            string dateTime = DateTime.Now.ToString("yyyyMMddHHmmss");
            string newCacheFilePath = $"{CacheFilePolicyPrefix}{dateTime}{CacheFileExtension}";

            File.WriteAllText(newCacheFilePath, JsonSerializer.Serialize(_rapNamesCache));
        }

        static public void SaveLocalGroupCacheToFile(List<LocalGroup> _policyNamesCache)
        {
            string dateTime = DateTime.Now.ToString("yyyyMMddHHmmss");
            string newCacheFilePath = $"{CacheFileLocalGroupPrefix}{dateTime}{CacheFileExtension}";

            File.WriteAllText(newCacheFilePath, JsonSerializer.Serialize(_policyNamesCache));
        }

        static public List<string> LoadPolicyCacheFromFile()
        {
            var directoryInfo = new DirectoryInfo(Directory.GetCurrentDirectory());
            var newestFile = directoryInfo.GetFiles($"{CacheFilePolicyPrefix}*{CacheFileExtension}")
                                            .OrderByDescending(f => f.LastWriteTime)
                                            .FirstOrDefault();

            if (newestFile != null)
            {
                var content = File.ReadAllText(newestFile.FullName);
                return JsonSerializer.Deserialize<List<string>>(content);
            }

            return null;
        }

        static public List<LocalGroup> LoadLocalGroupCacheFromFile()
        {
            var directoryInfo = new DirectoryInfo(Directory.GetCurrentDirectory());
            var newestFile = directoryInfo.GetFiles($"{CacheFileLocalGroupPrefix}*{CacheFileExtension}")
                                            .OrderByDescending(f => f.LastWriteTime)
                                            .FirstOrDefault();

            if (newestFile != null)
            {
                var content = File.ReadAllText(newestFile.FullName);
                return JsonSerializer.Deserialize<List<LocalGroup>>(content);
            }

            return null;
        }
    }
}
