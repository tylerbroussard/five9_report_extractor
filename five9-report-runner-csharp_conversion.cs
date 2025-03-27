using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Collections.Generic;
using Renci.SshNet;  // https://url.us.m.mimecastprotect.com/s/8eulCJ6zYMCq8NxBMuVf2UyTGng?domain=ssh.net library for SFTP
using System.Xml;

namespace Five9ReportRunner
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var rootCommand = new RootCommand("Five9 Call Log Report Runner")
            {
                new Argument<string>("credentials", "Five9 credentials in format \"username:password\""),
                new Option<string>("--sftp-host", "SFTP server hostname"),
                new Option<int>("--sftp-port", "SFTP server port") { IsRequired = false, DefaultValue = 22 },
                new Option<string>("--sftp-username", "SFTP username"),
                new Option<string>("--sftp-password", "SFTP password"),
                new Option<string>("--sftp-path", "SFTP remote path") { IsRequired = false, DefaultValue = "/" }
            };

            rootCommand.Handler = CommandHandler.Create<string, string, int, string, string, string>(RunReports);
            await rootCommand.InvokeAsync(args);
        }

        static async Task RunReports(string credentials, string sftpHost, int sftpPort, string sftpUsername, string sftpPassword, string sftpPath)
        {
            DateTime startTime = DateTime.Now;
            var dateRanges = GetDateRanges();
            string outputDir = CreateOutputDirectory();

            // Define report list - only includes Call Log report
            var reports = new List<Report>
            {
                new Report
                {
                    Name = "Call Log",
                    Folder = "Shared Reports",
                    Start = dateRanges["last_week_start"],
                    End = dateRanges["last_week_end"]
                }
            };

            // Print summary information
            Console.WriteLine("\n=== Five9 Call Log Report Runner ===");
            Console.WriteLine($"Started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Output directory: {outputDir}");
            Console.WriteLine($"Total reports to run: {reports.Count}");
            Console.WriteLine("\n=== Date Ranges ===");
            Console.WriteLine($"Date Range: {dateRanges["last_week_start"]} to {dateRanges["last_week_end"]}");
            Console.WriteLine("\n=== Starting Report Execution ===");

            int successfulReports = 0;
            int failedReports = 0;
            var reportResults = new List<ReportResult>();

            SftpConfig sftpConfig = null;
            if (!string.IsNullOrEmpty(sftpHost) && !string.IsNullOrEmpty(sftpUsername) && !string.IsNullOrEmpty(sftpPassword))
            {
                sftpConfig = new SftpConfig
                {
                    Host = sftpHost,
                    Port = sftpPort,
                    Username = sftpUsername,
                    Password = sftpPassword,
                    Path = sftpPath
                };
            }

            // Run each report
            for (int i = 0; i < reports.Count; i++)
            {
                Console.WriteLine($"\n[{i + 1}/{reports.Count}] Processing Report:");
                var (success, result) = await RunSingleReport(reports[i], outputDir, credentials);

                if (success)
                {
                    successfulReports++;
                    reportResults.Add(new ReportResult
                    {
                        Name = reports[i].Name,
                        Status = "Success",
                        File = result,
                        Duration = (https://url.us.m.mimecastprotect.com/s/qdNoCKrzYNS2qVLDpfvhAU5eRqp?domain=datetime.now - startTime).TotalSeconds
                    });

                    // Upload to SFTP if configured
                    if (sftpConfig != null)
                    {
                        UploadToSftp(result, sftpConfig);
                    }
                }
                else
                {
                    failedReports++;
                    reportResults.Add(new ReportResult
                    {
                        Name = reports[i].Name,
                        Status = "Failed",
                        Error = result
                    });
                }

                Console.WriteLine($"\n  Progress: {i + 1}/{reports.Count} reports processed");
                Console.WriteLine($"  Running totals: {successfulReports} successful, {failedReports} failed");
            }

            // Final summary
            DateTime endTime = DateTime.Now;
            double totalDuration = (endTime - startTime).TotalSeconds;

            Console.WriteLine("\n=== Final Summary ===");
            Console.WriteLine($"Execution completed time: {endTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Total duration: {totalDuration:F1} seconds");
            Console.WriteLine($"Total reports: {reports.Count}");
            Console.WriteLine($"Successful: {successfulReports}");
            Console.WriteLine($"Failed: {failedReports}");
            Console.WriteLine($"Output directory: {outputDir}");

            Console.WriteLine("\n=== Detailed Report Results ===");
            foreach (var result in reportResults)
            {
                Console.WriteLine($"\nReport: {result.Name}");
                Console.WriteLine($"Status: {result.Status}");
                if (result.Status == "Success")
                {
                    Console.WriteLine($"Duration: {result.Duration:F1} seconds");
                    Console.WriteLine($"Output: {result.File}");
                }
                else
                {
                    Console.WriteLine($"Error: {result.Error}");
                }
            }
        }

        static Dictionary<string, string> GetDateRanges()
        {
            DateTime today = DateTime.Now;
            DateTime sevenDaysAgo = today.AddDays(-7);
            DateTime startOfWeek = today.AddDays(-(int)today.DayOfWeek);

            // Format dates in Five9's expected format (Eastern Time)
            // Note: In a real implementation, you would use TimeZoneInfo to handle Eastern Time properly
            var dateFormats = new Dictionary<string, string>
            {
                ["today_start"] = today.Date.ToString("yyyy-MM-ddTHH:mm:ss.000-05:00"),
                ["today_end"] = today.Date.AddHours(23).AddMinutes(59).AddSeconds(59).ToString("yyyy-MM-ddTHH:mm:ss.000-05:00"),
                ["this_week_start"] = startOfWeek.Date.ToString("yyyy-MM-ddTHH:mm:ss.000-05:00"),
                ["this_week_end"] = today.Date.AddHours(23).AddMinutes(59).AddSeconds(59).ToString("yyyy-MM-ddTHH:mm:ss.000-05:00"),
                ["last_week_start"] = sevenDaysAgo.Date.ToString("yyyy-MM-ddTHH:mm:ss.000-05:00"),
                ["last_week_end"] = today.Date.AddHours(23).AddMinutes(59).AddSeconds(59).ToString("yyyy-MM-ddTHH:mm:ss.000-05:00")
            };

            return dateFormats;
        }

        static string CreateOutputDirectory()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string outputDir = $"five9_reports_{timestamp}";
            Directory.CreateDirectory(outputDir);
            return outputDir;
        }

        static string GetCleanFilename(string reportName)
        {
            // Start with the report name
            string cleanName = reportName.ToLower();

            // Replace special characters
            var replacements = new Dictionary<string, string>
            {
                ["#"] = "num",
                ["/"] = "-",
                [" "] = "_",
                [" "] = "-",
                ["&"] = "and",
                ["%"] = "pct",
                ["("] = "",
                [")"] = ""
            };

            foreach (var replacement in replacements)
            {
                cleanName = cleanName.Replace(replacement.Key, replacement.Value);
            }

            // Remove any other special characters
            StringBuilder sb = new StringBuilder();
            foreach (char c in cleanName)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                {
                    sb.Append(c);
                }
            }
            cleanName = sb.ToString();

            // Add timestamp
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return $"{cleanName}_{timestamp}.csv";
        }

        static string SanitizeReportName(string name)
        {
            // Sanitize report name by replacing special characters with standard ones
            var replacements = new Dictionary<string, string>
            {
                [" "] = "-",  // Replace en-dash with hyphen
                [" "] = "-",  // Replace em-dash with hyphen
                ["""] = "\"", // Replace curly quotes
                ["""] = "\"",
                ["'"] = "'",
                ["'"] = "'"
            };

            foreach (var replacement in replacements)
            {
                name = name.Replace(replacement.Key, replacement.Value);
            }
            return name;
        }

        static async Task<string> RunReport(string credentials, string folder, string reportName, string startTime, string endTime)
        {
            // Sanitize the report name before using it in the API call
            reportName = SanitizeReportName(reportName);

            string soapRequest = $@"
            <soapenv:Envelope xmlns:soapenv=""https://url.us.m.mimecastprotect.com/s/Q-_hCL90YOIRP6Lk8sPilUyx2DP?domain=schemas.xmlsoap.org"" xmlns:ser=""https://url.us.m.mimecastprotect.com/s/LuhBCM8NEPCq5Nj2muWsGU8bwXB?domain=service.admin.ws.five9.com"">
               <soapenv:Header/>
               <soapenv:Body>
                  <ser:runReport>
                     <folderName>{folder}</folderName>
                     <reportName>{reportName}</reportName>
                     <criteria>
                        <time>
                           <start>{startTime}</start>
                           <end>{endTime}</end>
                        </time>
                     </criteria>
                  </ser:runReport>
               </soapenv:Body>
            </soapenv:Envelope>";

            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials)));

            var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
            HttpResponseMessage response = await client.PostAsync("https://url.us.m.mimecastprotect.com/s/HS00CNkXEgh0N4oZycrtvUy7AVN?domain=api.five9.com", content);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to run report: {await response.Content.ReadAsStringAsync()}");
            }

            // Extract identifier from response
            string responseContent = await response.Content.ReadAsStringAsync();
            XDocument doc = XDocument.Parse(responseContent);
            XNamespace ns = "https://url.us.m.mimecastprotect.com/s/LuhBCM8NEPCq5Nj2muWsGU8bwXB?domain=service.admin.ws.five9.com/";
            return doc.Descendants(ns + "return").First().Value;
        }

        static async Task<bool> CheckReportStatus(string credentials, string identifier)
        {
            string soapRequest = $@"
            <soapenv:Envelope xmlns:soapenv=""https://url.us.m.mimecastprotect.com/s/Q-_hCL90YOIRP6Lk8sPilUyx2DP?domain=schemas.xmlsoap.org/"" xmlns:ser=""https://url.us.m.mimecastprotect.com/s/LuhBCM8NEPCq5Nj2muWsGU8bwXB?domain=service.admin.ws.five9.com/"">
               <soapenv:Header/>
               <soapenv:Body>
                  <ser:isReportRunning>
                     <identifier>{identifier}</identifier>
                  </ser:isReportRunning>
               </soapenv:Body>
            </soapenv:Envelope>";

            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials)));

            var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
            HttpResponseMessage response = await client.PostAsync("https://url.us.m.mimecastprotect.com/s/HS00CNkXEgh0N4oZycrtvUy7AVN?domain=api.five9.com", content);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to check report status: {await response.Content.ReadAsStringAsync()}");
            }

            // Parse response to determine if report is still running
            string responseContent = await response.Content.ReadAsStringAsync();
            XDocument doc = XDocument.Parse(responseContent);
            XNamespace ns = "https://url.us.m.mimecastprotect.com/s/LuhBCM8NEPCq5Nj2muWsGU8bwXB?domain=service.admin.ws.five9.com/";
            string isRunning = doc.Descendants(ns + "return").First().Value;
            return isRunning.ToLower() == "true";
        }

        static async Task<string> GetReportResults(string credentials, string identifier)
        {
            string soapRequest = $@"
            <soapenv:Envelope xmlns:soapenv=""https://url.us.m.mimecastprotect.com/s/Q-_hCL90YOIRP6Lk8sPilUyx2DP?domain=schemas.xmlsoap.org/"" xmlns:ser=""https://url.us.m.mimecastprotect.com/s/LuhBCM8NEPCq5Nj2muWsGU8bwXB?domain=service.admin.ws.five9.com/"">
               <soapenv:Header/>
               <soapenv:Body>
                  <ser:getReportResultCsv>
                     <identifier>{identifier}</identifier>
                  </ser:getReportResultCsv>
               </soapenv:Body>
            </soapenv:Envelope>";

            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials)));

            var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
            HttpResponseMessage response = await client.PostAsync("https://url.us.m.mimecastprotect.com/s/HS00CNkXEgh0N4oZycrtvUy7AVN?domain=api.five9.com", content);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to get report results: {await response.Content.ReadAsStringAsync()}");
            }

            // Extract CSV data from response
            string responseContent = await response.Content.ReadAsStringAsync();
            XDocument doc = XDocument.Parse(responseContent);
            XNamespace ns = "https://url.us.m.mimecastprotect.com/s/LuhBCM8NEPCq5Nj2muWsGU8bwXB?domain=service.admin.ws.five9.com/";
            return doc.Descendants(ns + "return").First().Value;
        }

        static async Task<(bool success, string result)> RunSingleReport(Report report, string outputDir, string credentials)
        {
            try
            {
                Console.WriteLine($"  Name: {report.Name}");
                Console.WriteLine($"  Folder: {report.Folder}");
                Console.WriteLine($"  Time Range: {report.Start} to {report.End}");
                Console.WriteLine($"  Started at: {DateTime.Now:HH:mm:ss}");
                Console.WriteLine("  Executing report...");

                Console.WriteLine("  Calling run_report API...");
                string identifier = await RunReport(
                    credentials,
                    report.Folder,
                    https://url.us.m.mimecastprotect.com/s/yI4wCOYLEjipAW3NQI5ugUGEmyQ?domain=report.name,
                    report.Start,
                    report.End
                );
                Console.WriteLine($"  Got report identifier: {identifier}");

                Console.WriteLine("  Checking report status...");
                DateTime startTime = DateTime.Now;
                int timeout = 300; // seconds
                while (await CheckReportStatus(credentials, identifier))
                {
                    TimeSpan elapsed = https://url.us.m.mimecastprotect.com/s/qdNoCKrzYNS2qVLDpfvhAU5eRqp?domain=datetime.now - startTime;
                    if (elapsed.TotalSeconds > timeout)
                    {
                        throw new Exception($"Report timed out after {timeout} seconds");
                    }
                    Console.WriteLine($"  Still running... ({elapsed.TotalSeconds:F0} seconds elapsed)");
                    await Task.Delay(5000); // Wait 5 seconds before checking again
                }

                Console.WriteLine("  Report completed, fetching results...");
                // Get results
                string results = await GetReportResults(credentials, identifier);

                // Save to file
                string cleanName = GetCleanFilename(https://url.us.m.mimecastprotect.com/s/yI4wCOYLEjipAW3NQI5ugUGEmyQ?domain=report.name);
                string filepath = Path.Combine(outputDir, cleanName);
                await File.WriteAllTextAsync(filepath, results);

                Console.WriteLine($"  ? Success - Saved to {filepath}");
                return (true, filepath);
            }
            catch (Exception e)
            {
                Console.WriteLine($"  ? Error - {e.Message}");
                return (false, e.Message);
            }
        }

        static bool UploadToSftp(string localFile, SftpConfig sftpConfig)
        {
            try
            {
                Console.WriteLine($"\nUploading {localFile} to SFTP server {sftpConfig.Host}...");
                using var client = new SftpClient(https://url.us.m.mimecastprotect.com/s/UmJxCPNgMkfK4pQ0VhBCDUxvtu8?domain=sftpconfig.host, sftpConfig.Port, sftpConfig.Username, sftpConfig.Password);
                client.Connect();

                // Create remote path if it doesn't exist
                try
                {
                    client.GetAttributes(sftpConfig.Path);
                }
                catch (Exception)
                {
                    Console.WriteLine($"Remote directory {sftpConfig.Path} doesn't exist, creating it...");
                    client.CreateDirectory(sftpConfig.Path);
                }

                // Upload the file
                string remoteFile = $"{sftpConfig.Path}/{Path.GetFileName(localFile)}";
                using (var fileStream = new FileStream(localFile, FileMode.Open))
                {
                    client.UploadFile(fileStream, remoteFile);
                }

                Console.WriteLine($"? Successfully uploaded to {remoteFile}");
                client.Disconnect();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"? SFTP upload failed: {e.Message}");
                return false;
            }
        }
    }

    class Report
    {
        public string Name { get; set; }
        public string Folder { get; set; }
        public string Start { get; set; }
        public string End { get; set; }
    }

    class ReportResult
    {
        public string Name { get; set; }
        public string Status { get; set; }
        public string File { get; set; }
        public string Error { get; set; }
        public double Duration { get; set; }
    }

    class SftpConfig
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Path { get; set; }
    }
}
