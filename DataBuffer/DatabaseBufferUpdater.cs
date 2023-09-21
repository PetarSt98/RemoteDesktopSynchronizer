﻿using Microsoft.EntityFrameworkCore;
using SynchronizerLibrary.Data;
using SynchronizerLibrary.Loggers;
using SynchronizerLibrary.Loggers;
using System.Net;
using System.Net.Mail;
using SynchronizerLibrary.SOAPservices;
using System.Text.Json;

namespace SynchronizerLibrary.DataBuffer
{
    public class DatabaseSynchronizator
    {
        private GlobalInstance globalInstance;
        private Dictionary<string, RAP_ResourceStatus> databaseStatusUpdater;
        private Dictionary<string, bool> partialStatus;
        private string username = "pstojkov";
        private string password = "GeForce9800GT.";
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
                    }
                    else
                    {
                        databaseStatusUpdater[key].Status &= obj.Value.Status;
                        partialStatus[key] |= obj.Value.Status;
                    }
                }
            }
        }

        public void UpdateDatabase()
        {
            bool sendEmail;
            sendEmail = true;
            using (var db = new RapContext())
            {
                foreach (var pair in databaseStatusUpdater.Zip(partialStatus, (item, partial) => (item, partial)))
                {
                    var obj = pair.item.Value;
                    if (!pair.item.Value.Status)
                    {
                        if (sendEmail)
                        {
                            bool uncompletedSync = false;
                            try
                            {
                                Console.WriteLine("Sending email");
                                string logMessage = $"Unsuccessfully synchronized Local Group: {obj.GroupName} and Device: {obj.ComputerName}";
                                LoggerSingleton.Raps.Warn(logMessage);
                                //string body = logMessage;

                                Dictionary<string, string> deviceInfo = Task.Run(() => SOAPMethods.ExecutePowerShellSOAPScript(obj.ComputerName, obj.GroupName.Replace("LG-", ""), username, password)).Result;
                                if (deviceInfo == null) break;
                                Console.WriteLine(deviceInfo["UserGivenName"]);
                                string firstName = deviceInfo["UserGivenName"]; // Dodaj ime
                                                                                //firstName = firstName.ToLower(); // Convert the entire string to lowercase first
                                                                                //char firstLetter = char.ToUpper(firstName[0]); // Convert the first character to uppercase

                                //firstName = firstLetter + firstName.Substring(1);
                                string users = obj.GroupName.Replace("LG-", "");
                                string remoteMachine = obj.ComputerName;

                                // Load the HTML template from a file or from a string, 
                                // then replace the placeholders with the actual values

                                Console.WriteLine(remoteMachine);
                                Console.WriteLine(users);
                                string template;
                                string subject;
                                if (obj.StatusMessage != null)
                                {
                                    Console.WriteLine("Status");
                                    Console.WriteLine(obj.StatusMessage);

                                    if (obj.StatusMessage == "Access is denied.")
                                    {
                                        subject = "no-reply Remote Desktop Service device synchronization Uncompleted";
                                        template = System.IO.File.ReadAllText(@".\DataBuffer\EmailTemplates\User_RequestUncompleted.htm");  // Replace with actual path
                                        template = template.Replace("$reason", "TS Gateway does not have access to your device.");
                                        template = template.Replace("$raison", "TS Gateway n'a pas accès à votre appareil.");
                                        uncompletedSync = true;
                                    }
                                    else if (obj.StatusMessage == "There is no such object on the server.")
                                    {
                                        subject = "no-reply Remote Desktop Service device synchronization Uncompleted";
                                        template = System.IO.File.ReadAllText(@".\DataBuffer\EmailTemplates\User_RequestUncompleted.htm");  // Replace with actual path
                                        template = template.Replace("$reason", $"The device {remoteMachine} cannot be found on the server.");
                                        template = template.Replace("$raison", "L'appareil n'est pas trouvé sur le serveur.");
                                        uncompletedSync = true;
                                    }
                                    else if (obj.StatusMessage.Contains("disabled"))
                                    {
                                        subject = "no-reply Remote Desktop Service device synchronization Uncompleted";
                                        template = System.IO.File.ReadAllText(@".\DataBuffer\EmailTemplates\User_RequestUncompleted.htm");  // Replace with actual path
                                        template = template.Replace("$reason", "Administrator user in Local User and Groups on your machine is disabled.");
                                        template = template.Replace("$raison", "L'utilisateur administrateur est désactivé dans le groupe local d'utilisateurs et de groupes de votre machine.");
                                        uncompletedSync = true;
                                    }
                                    else if (obj.StatusMessage == "Device is unreachable.")
                                    {
                                        subject = "no-reply Remote Desktop Service device synchronization Uncompleted";
                                        template = System.IO.File.ReadAllText(@".\DataBuffer\EmailTemplates\User_RequestUncompleted.htm");  // Replace with actual path
                                        template = template.Replace("$reason", "Your device is not reachable by TS Gateway.");
                                        template = template.Replace("$raison", "Votre appareil n'est pas accessible par TS Gateway.");
                                        uncompletedSync = true;
                                    }
                                    else if (obj.StatusMessage == "The network path was not found.")
                                    {
                                        subject = "no-reply Remote Desktop Service device synchronization Uncompleted";
                                        template = System.IO.File.ReadAllText(@".\DataBuffer\EmailTemplates\User_RequestUncompleted.htm");  // Replace with actual path
                                        template = template.Replace("$reason", "Your device was offline when TS Gateway tried to reach it.");
                                        template = template.Replace("$raison", "Votre appareil était hors ligne lorsque TS Gateway a essayé de le joindre.");
                                        uncompletedSync = true;
                                    }
                                    else
                                    {
                                        subject = "no-reply Remote Desktop Service device synchronization Failed";
                                        template = System.IO.File.ReadAllText(@".\DataBuffer\EmailTemplates\User_RequestFailed.htm");  // Replace with actual path
                                    }
                                }
                                else
                                {
                                    subject = "no-reply Remote Desktop Service device synchronization Failed";
                                    Console.WriteLine("No Status");
                                    template = System.IO.File.ReadAllText(@".\DataBuffer\EmailTemplates\User_RequestFailed.htm");
                                }
                                template = template.Replace("$firstName", firstName);
                                template = template.Replace("$users", users);
                                template = template.Replace("$RemoteMachine", remoteMachine);

                                // Now use the template as the body of your email
                                string toAddress = obj.GroupName.Replace("LG-", "") + "@cern.ch";
                                //string toAddressCC = resource.resourceOwner.Replace(@"CERN\", "") + "@cern.ch";
                                string toAddressCC = null;
                                if (deviceInfo["ResponsiblePersonUsername"].Length != 0)
                                {
                                    toAddressCC = deviceInfo["ResponsiblePersonUsername"] + "@cern.ch";
                                }
                                string body = template;

                                Console.WriteLine(subject);
                                if (!SpamFailureHandler.CheckStatus(remoteMachine, users, true))
                                {
                                    
                                    Console.WriteLine(subject);
                                    SendEmail(toAddress, toAddressCC, subject, body);
                                    Console.WriteLine("Email sent");
                                }


                                if (uncompletedSync)
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
                                        }
                                    }
                                    SpamFailureHandler.CleanCache(remoteMachine, users);
                                }
                                else
                                {
                                    var cacheData = new SpamFailureHandler(remoteMachine, users);
                                    cacheData.CacheSpam();
                                }

                                //if (!pair.partial.Value)
                                //{
                                //    var rapResourcesToDelete = db.rap_resource.Where(rr => (rr.RAPName == obj.GroupName.Replace("LG-", "RAP_") && string.Equals(rr.resourceName, obj.ComputerName, StringComparison.OrdinalIgnoreCase))).ToList();
                                //    db.rap_resource.RemoveRange(rapResourcesToDelete);

                                //    LoggerSingleton.General.Warn($"Deleting unsynchronized RAP_Resource RAP_Name: {obj.GroupName.Replace("LG-", "RAP_")} resourceName: {obj.ComputerName} from MySQL database");
                                //    LoggerSingleton.Raps.Warn($"Deleting unsynchronized RAP_Resource RAP_Name: {obj.GroupName.Replace("LG-", "RAP_")} resourceName: {obj.ComputerName} from MySQL database");
                                //    Console.WriteLine($"Deleting unsynchronized RAP_Resource RAP_Name: {obj.GroupName.Replace("LG-", "RAP_")} resourceName: {obj.ComputerName} from MySQL database");

                                //    db.SaveChanges();
                                //}
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("No Status");
                                Console.WriteLine(ex.Message);
                            }

                        }
                        continue;
                    }
                    Console.WriteLine($"  ComputerName: {obj.ComputerName}, GroupName: {obj.GroupName}");

                    // Selecting matching raps
                    var matchingRaps = db.raps
                        .Where(r => r.resourceGroupName == obj.GroupName)
                        .Include(r => r.rap_resource)  // Including related rap_resources
                        .ToList();

                    // Filtering unsynchronized raps and their resources
                    var unsynchronizedRaps = matchingRaps
                        .Where(r => r.rap_resource.Any(rr => !rr.synchronized && string.Equals(rr.resourceName, obj.ComputerName, StringComparison.OrdinalIgnoreCase) && !rr.toDelete))
                        .ToList();

                    foreach (var unsynchronizedRap in unsynchronizedRaps)
                    {
                        foreach (var resource in unsynchronizedRap.rap_resource.Where(rr => string.Equals(rr.resourceName, obj.ComputerName, StringComparison.OrdinalIgnoreCase)))
                        {
                            resource.synchronized = true;
                            string logMessage = $"Successfully synchronized User: {resource.RAPName.Replace("RAP_", "")} and Device: {resource.resourceName}";
                            LoggerSingleton.Raps.Info(logMessage);
                            if (sendEmail)
                            {
                                // Prepare the email
                                string toAddress = resource.RAPName.Replace("RAP_", "") + "@cern.ch";
                                string toAddressCC = resource.resourceOwner.Replace(@"CERN\", "") + "@cern.ch";
                                string subject = "no-reply Remote Desktop Service device synchronization Success";
                                //string body = logMessage;
                                Dictionary<string, string> deviceInfo = Task.Run(() => SOAPMethods.ExecutePowerShellSOAPScript(obj.ComputerName, resource.RAPName.Replace("RAP_", ""), username, password)).Result;

                                string firstName = deviceInfo["UserGivenName"]; // Dodaj ime
                                //firstName = firstName.ToLower(); // Convert the entire string to lowercase first
                                //char firstLetter = char.ToUpper(firstName[0]); // Convert the first character to uppercase
                                //firstName = firstLetter + firstName.Substring(1);
                                string users = obj.GroupName.Replace("LG-", "");
                                string remoteMachine = obj.ComputerName;
                                // Load the HTML template from a file or from a string, 
                                // then replace the placeholders with the actual values
                                string template = System.IO.File.ReadAllText(@".\DataBuffer\EmailTemplates\User_RequestSucceeded.htm");  // Replace with actual path
                                template = template.Replace("$firstName", firstName);
                                template = template.Replace("$users", users);
                                template = template.Replace("$RemoteMachine", remoteMachine);
                                string body = template;

                                Console.WriteLine(subject);
                                SendEmail(toAddress, toAddressCC, subject, body);

                                SpamFailureHandler.CleanCache(remoteMachine, users);
                            }
                        }
                    }
                }
                db.SaveChanges();
            }
        }

        public void SendEmail(string toAddress,string toAddressCC, string subject, string body)
        {

            MailMessage message = new MailMessage();
            message.From = new MailAddress("noreply@cern.ch");

            message.To.Add(new MailAddress(toAddress));
            if (toAddressCC != null)
                message.CC.Add(new MailAddress(toAddressCC));
            message.Bcc.Add(new MailAddress("cernts-tsgateway-admin@cern.ch"));

            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            SmtpClient client = new SmtpClient("cernmx.cern.ch");
            client.Send(message);
            Console.WriteLine($"Send and email to {toAddress}");
        }

    }

    public class SpamFailureHandler
    {
        public string deviceName { get; set; }
        public string userName { get; set; }
        public string cacheKey { get; set; }
        public int counter { get; set; }

        private static string path = "./cached_spam_failures.json";

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

                    if (temp[cacheKey].counter == 255)
                        return false;
                    else
                        return true;
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
                    if (this.counter > 255)
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


            //if (this.counter == 1)
            //{
            //    return true;
            //}
            //else
            //{
            //    return false;
            //}

        }

    }

}