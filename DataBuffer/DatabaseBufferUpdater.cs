using SynchronizerLibrary.Data;
using System.Text.Json;
using SynchronizerLibrary.EmailService;
using Microsoft.EntityFrameworkCore;




namespace SynchronizerLibrary.DataBuffer
{
    public class DatabaseSynchronizator
    {
        private GlobalInstance globalInstance;
        private Dictionary<string, RAP_ResourceStatus> databaseStatusUpdater;
        private Dictionary<string, bool> partialStatus;
        //private string username = "";
        //private string password = "";
        private EmailBuilder emailParser;
        public DatabaseSynchronizator()
        {
            globalInstance = GlobalInstance.Instance;
        }

        public void AverageGatewayReults()
        {
            databaseStatusUpdater = new Dictionary<string, RAP_ResourceStatus>();
            partialStatus = new Dictionary<string, bool>();

            foreach (var name in globalInstance.Names)
            {
                foreach (var obj in globalInstance.ObjectLists[name])
                {
                    var key = $"{obj.Value.ComputerName}-{obj.Value.GroupName}";
                    if (!databaseStatusUpdater.ContainsKey(key))
                    {
                        databaseStatusUpdater[key] = obj.Value;
                        partialStatus[key] = obj.Value.Status;

                        if (!obj.Value.Status)
                        {
                            if (!UnsuccessfulEmail.uncompletedMessages.Contains(obj.Value.StatusMessage) && !UnsuccessfulEmail.uncompletedMessages.Contains($"{obj.Value.StatusMessage};"))
                            {
                                databaseStatusUpdater[key].UnsynchronizedServers += name;
                                databaseStatusUpdater[key].UnsynchronizedServers += ";";
                            }
                            databaseStatusUpdater[key].FailedStatuses += obj.Value.StatusMessage;
                            databaseStatusUpdater[key].FailedStatuses += ";";
                        }
                    }
                    else
                    {
                        databaseStatusUpdater[key].Status &= obj.Value.Status;
                        partialStatus[key] |= obj.Value.Status;

                        if (!obj.Value.Status)
                        {
                            if (!UnsuccessfulEmail.uncompletedMessages.Contains(obj.Value.StatusMessage) && !UnsuccessfulEmail.uncompletedMessages.Contains($"{obj.Value.StatusMessage};"))
                            {
                                databaseStatusUpdater[key].UnsynchronizedServers += name;
                                databaseStatusUpdater[key].UnsynchronizedServers += ";";
                            }
                            databaseStatusUpdater[key].FailedStatuses += obj.Value.StatusMessage;
                            databaseStatusUpdater[key].FailedStatuses += ";";
                        }
                    }
                }
            }
        }

        public void UpdateDatabase()
        {
            bool sendEmail = true;
            using (var db = new RapContext())
            {
                foreach (var pair in databaseStatusUpdater.Zip(partialStatus, (item, partial) => (item, partial)))
                {
                    var obj = pair.item.Value;
                    if (!pair.item.Value.Status && !pair.partial.Value)
                    {
                        emailParser = new UnsuccessfulEmail();
                        emailParser.sendEmailFlag = sendEmail;
                        emailParser.CheckEmailTemplate(obj);

                        emailParser.PrepareEmail(obj, db);

                        _UpdateDatabase(obj, db);

                        emailParser.Send();
                        emailParser.cacheSpams();
                    }
                    else
                    {
                        Console.WriteLine($"  ComputerName: {obj.ComputerName}, GroupName: {obj.GroupName}");
                        emailParser = new SuccessfulEmail(obj, db);
                        emailParser.sendEmailFlag = sendEmail;
                        emailParser.PrepareEmail(obj, db);
                        emailParser.Send();
                        
                    }
                }
                db.SaveChanges();
            }
        }

        private void _UpdateDatabase(RAP_ResourceStatus? obj, RapContext db)
        {
            if (emailParser.UncompletedSync)
            {
                var matchingRapsUncompleted = db.raps
                    .Where(r => r.resourceGroupName == obj.GroupName)
                    .Include(r => r.rap_resource)  // Including related rap_resources
                    .ToList();

                // Filtering unsynchronized raps and their resources
                var unsynchronizedRapsUncompleted = matchingRapsUncompleted
                    .Where(r => r.rap_resource.Any(rr => !rr.synchronized && string.Equals(rr.resourceName, obj.ComputerName, StringComparison.OrdinalIgnoreCase) && !rr.toDelete))
                    .ToList();
                foreach (var unsynchronizedRap in unsynchronizedRapsUncompleted)
                {
                    foreach (var resource in unsynchronizedRap.rap_resource.Where(rr => string.Equals(rr.resourceName, obj.ComputerName, StringComparison.OrdinalIgnoreCase)))
                    {
                        resource.synchronized = true;
                        resource.exception = true;
                        resource.updateDate = DateTime.Now;
                    }
                }
                SpamFailureHandler.CleanCache(emailParser.remoteMachine, emailParser.users);
            }
            else
            {
                emailParser.cacheData = new SpamFailureHandler(emailParser.remoteMachine, emailParser.users);
            }
        }
    }

    public class SpamFailureHandler
    {
        public string deviceName { get; set; }
        public string userName { get; set; }
        public string cacheKey { get; set; }
        public int counter { get; set; }
        private const string CacheFilePath = "Cache";
        private static string path = $".\\{CacheFilePath}\\cached_spam_failures.json";

        public SpamFailureHandler(string deviceName, string userName)
        {
            this.deviceName = deviceName;
            this.userName = userName;
            this.cacheKey = $"{userName}-{deviceName}";
            this.counter = 1;
        }

        public static void CleanCache(string deviceName, string userName)
        {
            deviceName = GlobalInstance.ModifyComputerName(deviceName);

            string cacheKey = $"{userName}-{deviceName}";

            if (File.Exists(path))
            {
                var content = File.ReadAllText(path);
                var temp = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, SpamFailureHandler>>(content);

                if (temp.ContainsKey(cacheKey)) // kada sacuva json sjebe ga i nema imena itd
                {
                    temp.Remove(cacheKey);
                    File.WriteAllText(path, JsonSerializer.Serialize(temp));
                }

            }

        }

        public static bool CheckStatus(string deviceName, string userName, bool email = false)
        {
            deviceName = GlobalInstance.ModifyComputerName(deviceName);
            string cacheKey = $"{userName}-{deviceName}";

            if (File.Exists(path))
            {
                var content = File.ReadAllText(path);
                var temp = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, SpamFailureHandler>>(content);

                if (temp.ContainsKey(cacheKey)) // kada sacuva json sjebe ga i nema imena itd
                {
                    if (email) return true;

                    if (temp[cacheKey].counter >= 10)
                    {
                        return false;
                    }
                    else
                    {
                        temp[cacheKey].counter++;
                        var updatedJson = Newtonsoft.Json.JsonConvert.SerializeObject(temp, Newtonsoft.Json.Formatting.Indented);
                        System.IO.File.WriteAllText(path, updatedJson);
                        return true;
                    }
                }
                else
                    return false;
            }
            else
            {
                return false;
            }

        }

        public void CacheSpam()
        {
            if (File.Exists(path))
            {
                var content = File.ReadAllText(path);
                var temp = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, SpamFailureHandler>>(content);
                if (temp.ContainsKey(this.cacheKey))
                {
                    this.counter = temp[cacheKey].counter;
                    this.counter++;
                    if (this.counter > 10)
                    {
                        this.counter = 1;
                    }
                }
                else
                {
                    temp[cacheKey] = this;
                }    

                temp[cacheKey].counter = this.counter;
                File.WriteAllText(path, JsonSerializer.Serialize(temp));
            }
            else
            {
                var temp = new Dictionary<string, SpamFailureHandler>();
                temp[this.cacheKey] = this;
                File.WriteAllText(path, JsonSerializer.Serialize(temp));
            }
        }

    }

}