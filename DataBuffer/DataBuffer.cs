using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace SynchronizerLibrary.DataBuffer
{ 
    public class GlobalInstance
    {
        private static GlobalInstance _instance;
        private static readonly object _lock = new object();

        public List<string> Names { get; private set; }
        public ConcurrentDictionary<string, ConcurrentDictionary<string, RAP_ResourceStatus>> ObjectLists { get; private set; }

        private GlobalInstance()
        {
            Names = new List<string>();
            ObjectLists = new ConcurrentDictionary<string, ConcurrentDictionary<string, RAP_ResourceStatus>>();
        }

        public static string ModifyComputerName(string computerName)
        {
            if (computerName[computerName.Length - 1] == '$')
                return computerName.Substring(0, computerName.Length - 1);
            else
                return computerName;
        }
        public void AddToObjectsList(string serverName, string computerName, string groupName, bool status, string statusMessage = null)
        {
            string configKey = GetConfigKey(computerName, groupName);
            string modifiedComputerName = ModifyComputerName(computerName);

            var serverObjects = ObjectLists.GetOrAdd(serverName, new ConcurrentDictionary<string, RAP_ResourceStatus>());
            var resourceStatus = serverObjects.GetOrAdd(configKey, new RAP_ResourceStatus
            {
                ComputerName = modifiedComputerName,
                GroupName = groupName,
                Status = status,
                StatusMessage = statusMessage
            });

            if (resourceStatus.Status)
            {
                resourceStatus.StatusMessage = statusMessage;
            }
            resourceStatus.Status &= status;
        }

        //public IEnumerable<RAP_ResourceStatus> GetValuesForUser(string serverName, string userPrefix)
        //{
        //    if (ObjectLists.TryGetValue(serverName, out var serverObjects))
        //    {
        //        var filteredValues = serverObjects.Where(kv => kv.Key.StartsWith(userPrefix)).Select(kv => kv.Value);

        //        return filteredValues;
        //    }

        //    return Enumerable.Empty<RAP_ResourceStatus>();
        //}

        public void UpdateObjectsStatus(string serverName, string groupName, bool status, string statusMessage = null)
        {
            if (ObjectLists.TryGetValue(serverName, out var serverObjects))
            {
                foreach (var entry in serverObjects)
                {
                    if (entry.Key.Contains(groupName))
                    {
                        var resourceStatus = entry.Value;
                        resourceStatus.Status = status;
                        if (statusMessage != null)
                        {
                            resourceStatus.StatusMessage = statusMessage;
                        }
                    }
                }
            }
        }


        //public void AddToObjectsList(string serverName, string computerName, string groupName, bool status, string statusMessage = null)
        //{
        //    Console.WriteLine(statusMessage);
        //    string configKey = GetConfigKey(computerName, groupName);

        //    string modifiedComputerName = ModifyComputerName(computerName);
        //    if (!ObjectLists.ContainsKey(serverName))
        //    {
        //        ObjectLists[serverName] = new Dictionary<string, RAP_ResourceStatus>();
        //    }
        //    if (!ObjectLists[serverName].ContainsKey(configKey))
        //    {
        //        ObjectLists[serverName][configKey] = new RAP_ResourceStatus
        //        {
        //            ComputerName = modifiedComputerName,
        //            GroupName = groupName,
        //            Status = status,
        //            StatusMessage = statusMessage
        //        };
        //    }
        //    else 
        //    {   if (ObjectLists[serverName][configKey].Status)
        //        {
        //            ObjectLists[serverName][configKey].StatusMessage = statusMessage;
        //        }
        //        ObjectLists[serverName][configKey].Status &= status;

        //    }

        //}
        public RAP_ResourceStatus GetObjectsList(string serverName, string computerName, string groupName)
        {
            string configKey = GetConfigKey(computerName, groupName);

            if (!ObjectLists.ContainsKey(serverName))
            {
               throw new Exception($"Unknown server: {serverName}!");
            }
            if (!ObjectLists[serverName].ContainsKey(configKey))
            {
                throw new Exception($"Unknown config:{configKey}!");
            }
            return ObjectLists[serverName][configKey];
        }

        private static string GetConfigKey(string computerName, string groupName)
        {
            string modifiedComputerName = ModifyComputerName(computerName);
            return $"{modifiedComputerName}-{groupName}";
        }

        public static GlobalInstance Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new GlobalInstance();
                    }
                    return _instance;
                }
            }
        }
    }

    public class RAP_ResourceStatus
    {
        public string ComputerName { get; set; }
        public string GroupName { get; set; }
        public bool Status { get; set; }
        public string UnsynchronizedServers { get; set; } = "";
        public string FailedStatuses { get; set; }
        public string StatusMessage { get; set; }
    }
}