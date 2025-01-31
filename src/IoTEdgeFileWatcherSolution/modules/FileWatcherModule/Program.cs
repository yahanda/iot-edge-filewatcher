namespace FileWatcherModule
{
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.IO;
    using System.Runtime.Loader;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Shared;
    //using Newtonsoft.Json;

    class Program
    {
        private const int DefaultInterval = 10000;
        private const string DefaultSearchPattern = "*.txt";
        private const string DefaultRenameExtension = ".old";

        private static string _moduleId;

        private static string _deviceId;

        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            Console.WriteLine("  _       _                  _                    __ _ _                    _       _               ");
            Console.WriteLine(" (_)     | |                | |                  / _(_) |                  | |     | |              ");
            Console.WriteLine("  _  ___ | |_ ______ ___  __| | __ _  ___ ______| |_ _| | _____      ____ _| |_ ___| |__   ___ _ __ ");
            Console.WriteLine(" | |/ _ \\| __|______/ _ \\/ _` |/ _` |/ _ \\______|  _| | |/ _ \\ \\ /\\ / / _` | __/ __| '_ \\ / _ \\ '__|");
            Console.WriteLine(" | | (_) | |_      |  __/ (_| | (_| |  __/      | | | | |  __/\\ V  V / (_| | || (__| | | |  __/ |   ");
            Console.WriteLine(" |_|\\___/ \\__|      \\___|\\__,_|\\__, |\\___|      |_| |_|_|\\___| \\_/\\_/ \\__,_|\\__\\___|_| |_|\\___|_|   ");
            Console.WriteLine("                                __/ |                                                               ");
            Console.WriteLine("                               |___/                                                                ");
            Console.WriteLine("");
            Console.WriteLine("   Copyright © 2020 - IoT Edge Foundation");
            Console.WriteLine(" ");

            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);

            // Execute callback method for Twin desired properties updates
            var twin = await ioTHubModuleClient.GetTwinAsync();
            await OnDesiredPropertiesUpdate(twin.Properties.Desired, ioTHubModuleClient);

            // Attach a callback for updates to the module twin's desired properties.
            await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, ioTHubModuleClient);

            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            _deviceId = System.Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID");
            _moduleId = Environment.GetEnvironmentVariable("IOTEDGE_MODULEID");

            var directory = Directory.GetCurrentDirectory();
            System.Console.WriteLine($"Current Directory {directory}");

            if (!Directory.Exists("exchange"))
            {
                System.Console.WriteLine($"No sub folder 'exchange' found");
            }
            else
            {
                System.Console.WriteLine($"Found sub folder 'exchange'");
            }

            var thread = new Thread(() => ThreadBody(ioTHubModuleClient));
            thread.Start();
        }

         private static async void ThreadBody(object userContext)
        {
            var client = userContext as ModuleClient;

            if (client == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            while (true)
            {
                try
                {                    
                    var files = Directory.GetFiles("exchange", SearchPattern);

                    System.Console.WriteLine($"{DateTime.UtcNow} - Seen {files.Length} files with pattern '{SearchPattern}'" );

                    if (files.Length > 0)
                    {
                        foreach(var fileName in files)
                        {
                            try
                            {
                                var canRead = false;
                                var canWrite = false;

                                using (var fs = new FileStream(fileName, FileMode.Open))
                                {
                                    canRead = fs.CanRead;
                                    canWrite = fs.CanWrite;
                                }

                                if (!canRead
                                        && !canWrite)
                                {
                                    System.Console.WriteLine($"File {fileName}: {(canRead? "is readable": "is not readable")}; {(canWrite? "is writable": "is not writable")}");                                                
                                    continue;   
                                }
                            }
                            catch
                            {
                                System.Console.WriteLine($"{fileName} cannot be opened");
                                continue;
                            }

                            var fi = new FileInfo(fileName);

                            System.Console.WriteLine($"File found: '{fi.FullName}' - Size: {fi.Length} bytes.");

                            // Create JSON message
                            string messageBody = JsonSerializer.Serialize(
                                new
                                {
                                    filename = fi.FullName,
                                    filesize = fi.Length,
                                });
                            using var message = new Message(Encoding.ASCII.GetBytes(messageBody))
                            {
                                ContentType = "application/json",
                                ContentEncoding = "utf-8",
                            };

                            // Send the message
                            await client.SendEventAsync("output1", message);
                            System.Console.WriteLine($"Sending message: {messageBody}");

                            var targetFullFilename = fi.FullName + RenameExtension;
                            
                            File.Move(fi.FullName, targetFullFilename);
                            System.Console.WriteLine($"Renamed '{fi.FullName}' to '{targetFullFilename}'");
                        }
                    }
                }
                catch (System.Exception ex)
                {  
                    System.Console.WriteLine($"Exception: {ex.Message}");
                }

                var fileMessage = new FileMessage
                {
                    counter = DateTime.Now.Millisecond
                };

                Thread.Sleep(Interval);
            }
        }

        public static int Interval {get; set;} = DefaultInterval;

        public static string RenameExtension {get; set;} = DefaultRenameExtension;

        public static string SearchPattern {get; set;} = DefaultSearchPattern;

        /// <summary>
        /// Call back function for updating the desired properties
        /// </summary>
        static async Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            Console.WriteLine("OnDesiredPropertiesUpdate started");

            var client = userContext as ModuleClient;

            if (desiredProperties == null)
            {
                Console.WriteLine("Empty desired properties ignored.");

                return;
            }

            try
            {
                var reportedProperties = new TwinCollection();

                if (desiredProperties.Contains("interval")) 
                {
                    if (desiredProperties["interval"] != null)
                    {
                        Interval = desiredProperties["interval"];
                    }
                    else
                    {
                        Interval = DefaultInterval;
                    }

                    Console.WriteLine($"Interval changed to '{Interval}'");

                    reportedProperties["interval"] = Interval;
                } 
                else
                {
                    Console.WriteLine($"Interval ignored");
                }

                if (desiredProperties.Contains("renameExtension")) 
                {
                    if (desiredProperties["renameExtension"] != null)
                    {
                        RenameExtension = desiredProperties["renameExtension"];
                    }
                    else
                    {
                        RenameExtension = DefaultRenameExtension;
                    }

                    Console.WriteLine($"RenameExtension changed to '{RenameExtension}'");

                    reportedProperties["renameExtension"] = RenameExtension;
                } 
                else
                {
                    Console.WriteLine($"RenameExtension ignored");
                }

                if (desiredProperties.Contains("searchPattern")) 
                {
                    if (desiredProperties["searchPattern"] != null)
                    {
                        SearchPattern = desiredProperties["searchPattern"];
                    }
                    else
                    {
                        SearchPattern = DefaultSearchPattern;
                    }

                    Console.WriteLine($"SearchPattern changed to '{SearchPattern}'");

                    reportedProperties["searchPattern"] = SearchPattern;
                } 
                else
                {
                    Console.WriteLine($"SearchPattern ignored");
                }

                if (reportedProperties.Count > 0)
                {
                    await client.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);
                }
            }
            catch (AggregateException ex)
            {
                Console.WriteLine($"Desired properties change error: {ex.Message}");
                
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine($"Error when receiving desired properties: {exception}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error when receiving desired properties: {ex.Message}");
            }
        }


    }

    public class FileMessage
    {
        public int counter { get; set; }
    }
}
