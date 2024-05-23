using Microsoft.EntityFrameworkCore;
using SynchronizerLibrary.Data;
using SynchronizerLibrary.Loggers;
using SynchronizerLibrary.Loggers;
using System.Net;
using System.Net.Mail;
using SynchronizerLibrary.SOAPservices;
using System.Text.Json;
using SynchronizerLibrary.DataBuffer;
using SynchronizerLibrary.RDPFile;


namespace SynchronizerLibrary.EmailService
{
    public class EmailBuilder
    {
        protected string subject { get; set; }
        protected string template { get; set; }
        public string remoteMachine { get; set; }
        public string users { get; set; }
        protected string body { get; set; }
        protected string toAddress { get; set; }
        protected string toAddressCC { get; set; }
        public bool sendEmailFlag { get; set; } = true;
        //protected string username = "";
        //protected string password = ".";
        protected string rdpContent = null;
        public SpamFailureHandler cacheData;
        public virtual void CheckEmailTemplate(RAP_ResourceStatus? obj)
        {
        }
        public virtual bool UncompletedSync { get; set; } = false;
        public virtual void PrepareEmail(RAP_ResourceStatus? obj, RapContext db) { }
        public EmailBuilder()
        {

        }

        public void Send()
        {
            Console.WriteLine("Email Subject");
            Console.WriteLine(subject);
            if (!sendEmailFlag) return;
  
            if (!SpamFailureHandler.CheckStatus(remoteMachine, users, true))
            {
                Console.WriteLine(subject);
                _SendEmail(toAddress, toAddressCC, subject, body, remoteMachine, rdpContent);

            }
        }

        private void _SendEmail(string toAddress, string toAddressCC, string subject, string body, string deviceName, string rdpContent)
        {
            try
            {
                MailMessage message = new MailMessage();
                message.From = new MailAddress("noreply@cern.ch");

                message.To.Add(new MailAddress(toAddress));
                if (toAddressCC != null && toAddress.Length > 0)
                    message.CC.Add(new MailAddress(toAddressCC));
                message.Bcc.Add(new MailAddress("cernts-tsgateway-admin@cern.ch"));

                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = true;

                if (rdpContent != null)
                {
                    using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rdpContent)))
                    {
                        Attachment attachment = new Attachment(stream, $"{deviceName}.rdp", "application/octet-stream");
                        message.Attachments.Add(attachment);
                        SmtpClient client = new SmtpClient("cernmx.cern.ch");
                        client.Send(message);
                        Console.WriteLine($"Send an email to {toAddress}");
                    }
                }
                else
                {
                    SmtpClient client = new SmtpClient("cernmx.cern.ch");
                    client.Send(message);
                    Console.WriteLine($"Send an email to {toAddress}");
                    cacheSpams();

                }
            } catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        public void cacheSpams()
        {
            if (!UncompletedSync)
                cacheData.CacheSpam();
        }
    }

    public class UnsuccessfulEmail : EmailBuilder
    {
        public override bool UncompletedSync { get; set; } = false;
        public static string[] uncompletedMessages { get; } = { "Access is denied.", "There is no such object on the server.", "disabled", "Device is unreachable.", "The network path was not found.", "Unreachable password" };

        public UnsuccessfulEmail()
        {
        }
        public override void PrepareEmail(RAP_ResourceStatus? obj, RapContext db)
        {
            Console.WriteLine($"{obj.GroupName}, {obj.ComputerName}, {obj.UnsynchronizedServers}");

            var resource = db.rap_resource
                .Where(r =>
                    ((r.RAPName == obj.GroupName.Replace("LG-", "RAP_")) && (r.resourceName == obj.ComputerName)))
                    .ToList()[0];

            if (resource.unsynchronizedGateways.Length != 0 && obj.UnsynchronizedServers.Length != 0)
            {
                sendEmailFlag = false;
            }
            resource.unsynchronizedGateways = obj.UnsynchronizedServers;
            if (!sendEmailFlag) return;

            Console.WriteLine("Sending email");
            string logMessage = $"Unsuccessfully synchronized Local Group: {obj.GroupName} and Device: {obj.ComputerName}";
            LoggerSingleton.Raps.Warn(logMessage);
            //string body = logMessage;

            Dictionary<string, string> deviceInfo = Task.Run(() => SOAPMethods.ExecutePowerShellSOAPScript(obj.ComputerName, obj.GroupName.Replace("LG-", ""))).Result;
            if (deviceInfo != null)
            {
                Console.WriteLine(deviceInfo["UserGivenName"]);
                string firstName = deviceInfo["UserGivenName"];
                                                               

                //firstName = firstLetter + firstName.Substring(1);



                Console.WriteLine(remoteMachine);
                Console.WriteLine(users);


                template = template.Replace("$firstName", firstName);
                template = template.Replace("$users", users);
                template = template.Replace("$RemoteMachine", remoteMachine);

                // Now use the template as the body of your email
                toAddress = obj.GroupName.Replace("LG-", "") + "@cern.ch";

                toAddressCC = null;
                if (deviceInfo["ResponsiblePersonUsername"] != null && deviceInfo["ResponsiblePersonUsername"].Length != 0)
                {
                    toAddressCC = deviceInfo["ResponsiblePersonUsername"] + "@cern.ch";
                }
                else if(deviceInfo["ResponsiblePersonEmail"] != null && deviceInfo["ResponsiblePersonEmail"].Length != 0)
                {
                    toAddressCC = deviceInfo["ResponsiblePersonEmail"];
                }
                else
                {
                    toAddressCC = "";
                }

                body = template;

                
                if (UncompletedSync)
                {
                    rdpContent = RdpFile.CustomizeRdpFile(remoteMachine);
                }
            }
        }
        public override void CheckEmailTemplate(RAP_ResourceStatus? obj)
        {
            users = obj.GroupName.Replace("LG-", "");
            remoteMachine = obj.ComputerName;

            CheckUnsyncEmailTemplate(obj);
        }

        public void CheckUnsyncEmailTemplate(RAP_ResourceStatus? obj)
        {
            if (obj.StatusMessage != null)
            {
                Console.WriteLine("Status");
                Console.WriteLine(obj.StatusMessage);

                if (obj.UnsynchronizedServers.Length > 0)
                {
                    subject = "Remote Desktop Service device synchronization Failed";
                    template = System.IO.File.ReadAllText(@".\DataBuffer\EmailTemplates\User_RequestFailed.htm");
                }
                else
                {
                    if (obj.StatusMessage == uncompletedMessages[0] || obj.FailedStatuses.Contains(uncompletedMessages[0]))
                    {
                        subject = "Remote Desktop Service device synchronization Uncompleted";
                        template = System.IO.File.ReadAllText(@".\DataBuffer\EmailTemplates\User_RequestUncompleted.htm");  // Replace with actual path
                        template = template.Replace("$reason", "TS Gateway does not have access to your device.");
                        template = template.Replace("$raison", "TS Gateway n'a pas accès à votre appareil.");
                        UncompletedSync = true;
                    }
                    else if (obj.StatusMessage == uncompletedMessages[1] || obj.FailedStatuses.Contains(uncompletedMessages[1]))
                    {
                        subject = "Remote Desktop Service device synchronization Uncompleted";
                        template = System.IO.File.ReadAllText(@".\DataBuffer\EmailTemplates\User_RequestUncompleted.htm");  // Replace with actual path
                        template = template.Replace("$reason", $"The device {remoteMachine} cannot be found on the server.");
                        template = template.Replace("$raison", "L'appareil n'est pas trouvé sur le serveur.");
                        UncompletedSync = true;
                    }
                    else if (obj.StatusMessage.Contains(uncompletedMessages[2]) || obj.FailedStatuses.Contains(uncompletedMessages[2]))
                    {
                        subject = "Remote Desktop Service device synchronization Uncompleted";
                        template = System.IO.File.ReadAllText(@".\DataBuffer\EmailTemplates\User_RequestUncompleted.htm");  // Replace with actual path
                        template = template.Replace("$reason", "Administrator user in Local User and Groups on your machine is disabled.");
                        template = template.Replace("$raison", "L'utilisateur administrateur est désactivé dans le groupe local d'utilisateurs et de groupes de votre machine.");
                        UncompletedSync = true;
                    }
                    else if (obj.StatusMessage == uncompletedMessages[3] || obj.FailedStatuses.Contains(uncompletedMessages[3]))
                    {
                        subject = "Remote Desktop Service device synchronization Uncompleted";
                        template = System.IO.File.ReadAllText(@".\DataBuffer\EmailTemplates\User_RequestUncompleted.htm");  // Replace with actual path
                        template = template.Replace("$reason", "Your device is not reachable by TS Gateway.");
                        template = template.Replace("$raison", "Votre appareil n'est pas accessible par TS Gateway.");
                        UncompletedSync = true;
                    }
                    else if (obj.StatusMessage == uncompletedMessages[4] || obj.FailedStatuses.Contains(uncompletedMessages[4]))
                    {
                        subject = "Remote Desktop Service device synchronization Uncompleted";
                        template = System.IO.File.ReadAllText(@".\DataBuffer\EmailTemplates\User_RequestUncompleted.htm");  // Replace with actual path
                        template = template.Replace("$reason", "Your device was offline when TS Gateway tried to reach it.");
                        template = template.Replace("$raison", "Votre appareil était hors ligne lorsque TS Gateway a essayé de le joindre.");
                        UncompletedSync = true;
                    }
                    else if (obj.StatusMessage.Contains(uncompletedMessages[5]) || obj.FailedStatuses.Contains(uncompletedMessages[5]))
                    {
                        subject = "Remote Desktop Service device synchronization Uncompleted";
                        template = System.IO.File.ReadAllText(@".\DataBuffer\EmailTemplates\User_RequestUncompleted.htm");  // Replace with actual path
                        template = template.Replace("$reason", "Your device is not reachable by TS Gateway.");
                        template = template.Replace("$raison", "Votre appareil n'est pas accessible par TS Gateway.");
                        UncompletedSync = true;
                    }
                    else
                    {
                        subject = "Remote Desktop Service device synchronization Failed";
                        template = System.IO.File.ReadAllText(@".\DataBuffer\EmailTemplates\User_RequestFailed.htm");  // Replace with actual path
                    }
                }
            }
            else
            {
                subject = "Remote Desktop Service device synchronization Failed";
                Console.WriteLine("No Status");
                template = System.IO.File.ReadAllText(@".\DataBuffer\EmailTemplates\User_RequestFailed.htm");
            }
        }
    }

    public class SuccessfulEmail : EmailBuilder
    {
        public SuccessfulEmail(RAP_ResourceStatus? obj, RapContext db)
        { }
        public override void PrepareEmail(RAP_ResourceStatus? obj, RapContext db)
        {
            Console.WriteLine($"  ComputerName: {obj.ComputerName}, GroupName: {obj.GroupName}");
            // Selecting matching raps
            var matchingRaps = db.raps
                .Where(r => r.resourceGroupName == obj.GroupName)
                .Include(r => r.rap_resource)  // Including related rap_resources
                .ToList();

            // Filtering unsynchronized raps and their resources
            var unsynchronizedRaps = matchingRaps
                .Where(r => r.rap_resource.Any(rr => (!rr.synchronized || rr.unsynchronizedGateways.Length != 0) && string.Equals(rr.resourceName, obj.ComputerName, StringComparison.OrdinalIgnoreCase) && !rr.toDelete))
                .ToList();

            foreach (var unsynchronizedRap in unsynchronizedRaps)
            {
                foreach (var resource in unsynchronizedRap.rap_resource.Where(rr => string.Equals(rr.resourceName, obj.ComputerName, StringComparison.OrdinalIgnoreCase)))
                {
                    resource.synchronized = true;
                    resource.updateDate = DateTime.Now;

                    if(obj.UnsynchronizedServers.Length != 0)
                    {
                        sendEmailFlag = false;
                    }

                    resource.unsynchronizedGateways = obj.UnsynchronizedServers;
                    
                    db.SaveChanges();

                    if (!sendEmailFlag) return;

                    string logMessage = $"Successfully synchronized User: {resource.RAPName.Replace("RAP_", "")} and Device: {resource.resourceName}";
                    LoggerSingleton.Raps.Info(logMessage);

                     // Prepare the email
                    toAddress = resource.RAPName.Replace("RAP_", "") + "@cern.ch";
                    toAddressCC = resource.resourceOwner.Replace(@"CERN\", "") + "@cern.ch";
                    subject = "Remote Desktop Service device synchronization Success";
                    //string body = logMessage;
                    Dictionary<string, string> deviceInfo = Task.Run(() => SOAPMethods.ExecutePowerShellSOAPScript(obj.ComputerName, resource.RAPName.Replace("RAP_", ""))).Result;

                    string firstName = deviceInfo["UserGivenName"]; // Dodaj ime
                                                                    //firstName = firstName.ToLower(); // Convert the entire string to lowercase first
                                                                    //char firstLetter = char.ToUpper(firstName[0]); // Convert the first character to uppercase
                                                                    //firstName = firstLetter + firstName.Substring(1);
                    users = obj.GroupName.Replace("LG-", "");
                    remoteMachine = obj.ComputerName;
                    // Load the HTML template from a file or from a string, 
                    // then replace the placeholders with the actual values
                    string template = System.IO.File.ReadAllText(@".\DataBuffer\EmailTemplates\User_RequestSucceeded.htm");  // Replace with actual path
                    template = template.Replace("$firstName", firstName);
                    template = template.Replace("$users", users);
                    template = template.Replace("$RemoteMachine", remoteMachine);
                    body = template;

                    Console.WriteLine(subject);
                    rdpContent = RdpFile.CustomizeRdpFile(remoteMachine);
                    SpamFailureHandler.CleanCache(remoteMachine, users);
                }
            }
        }
    }
}
