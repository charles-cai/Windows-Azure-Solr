/*
* Copyright 2011 Microsoft Corporation
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*   http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;
using System.IO;
using System.Xml.Linq;
using System.Xml;

namespace SolrSlaveHostWorkerRole
{
    public class WorkerRole : RoleEntryPoint
    {
        private static CloudDrive _solrStorageDrive = null;
        private static String _logFileLocation;
        private static Process _solrProcess = null;
        private static string _port = null;
        private static string _masterUrl = null;
        private static string _mySolrUrl = null;

        public override void Run()
        {
            Log("SolrSlaveHostWorkerRole Run() called", "Information");

            while (true)
            {
                Thread.Sleep(10000);

                string masterUrl = Util.GetMasterUrl();
                if (masterUrl != _masterUrl) // master changed?
                {
                    Log("Master Url changed, recycling slave role", "Information");
                    RoleEnvironment.RequestRecycle();
                    return;
                }

                if ((_solrProcess != null) && (_solrProcess.HasExited == true))
                {
                    Log("Solr Process Exited. Hence recycling slave role.", "Information");
                    RoleEnvironment.RequestRecycle();
                    return;
                }

                Log("Working", "Information");
            }
        }

        public override bool OnStart()
        {
            Log("SolrSlaveHostWorkerRole Start() called", "Information");

            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            RoleEnvironment.Changing += (sender, arg) =>
            {
                RoleEnvironment.RequestRecycle();
            };

            StartSolr();

            return base.OnStart();
        }

        public override void OnStop()
        {
            Log("SolrSlaveHostWorkerRole OnStop() called", "Information");

            if (_solrProcess != null)
            {
                try
                {
                    _solrProcess.Kill();
                    _solrProcess.WaitForExit(2000);
                }
                catch { }
            }

            if (_solrStorageDrive != null)
            {
                try
                {
                    _solrStorageDrive.Unmount();
                }
                catch { }
            }

            base.OnStop();
        }

        private void StartSolr()
        {
            try
            {
                // we use an Azure drive to store the solr index and conf data
                String vhdPath = CreateSolrStorageVhd();

                InitializeLogFile(vhdPath);

                InitRoleInfo();

                // Create the necessary directories in the Azure drive.
                CreateSolrStoragerDirs(vhdPath);

                //Set IP Endpoint and Port Address.
                //ConfigureIPEndPointAndPortAddress();

                // Copy solr files such as configuration and additional libraries etc.
                CopySolrFiles(vhdPath);

                Log("Done - Creating storage dirs and copying conf files", "Information");

                string cmdLineFormat = 
                    @"%RoleRoot%\approot\jre6\bin\java.exe -Dsolr.solr.home={0}SolrStorage -Djetty.port={1} -Denable.slave=true -DmasterUrl={2} -DdefaultCoreName=slaveCore -jar %RoleRoot%\approot\Solr\example\start.jar";

                _masterUrl = Util.GetMasterUrl();
                Log("GetMasterUrl: " + _masterUrl, "Information");

                string cmdLine = String.Format(cmdLineFormat, vhdPath, _port, _masterUrl + "replication");
                Log("Solr start command line: " + cmdLine, "Information");

                _solrProcess = ExecuteShellCommand(cmdLine, false, Path.Combine(Environment.GetEnvironmentVariable("RoleRoot") + @"\", @"approot\Solr\example\"));
                _solrProcess.Exited += new EventHandler(_solrProcess_Exited);

                Log("Done - Starting Solr", "Information");
            }
            catch (Exception ex)
            {
                Log("Exception occured in StartSolr " + ex.Message, "Error");
            }
        }

        void _solrProcess_Exited(object sender, EventArgs e)
        {
            Log("Solr Exited", "Information");
            RoleEnvironment.RequestRecycle();
        }

        private String CreateSolrStorageVhd()
        {
            CloudStorageAccount storageAccount;
            LocalResource localCache;
            CloudBlobClient client;
            CloudBlobContainer drives;

            localCache = RoleEnvironment.GetLocalResource("AzureDriveCache");
            Log(String.Format("AzureDriveCache {0} {1} MB", localCache.RootPath, localCache.MaximumSizeInMegabytes - 50), "Information");
            CloudDrive.InitializeCache(localCache.RootPath.TrimEnd('\\'), localCache.MaximumSizeInMegabytes - 50);

            storageAccount = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("DataConnectionString"));
            client = storageAccount.CreateCloudBlobClient();

            string roleId = RoleEnvironment.CurrentRoleInstance.Id;
            string containerAddress = ContainerNameFromRoleId(roleId);
            drives = client.GetContainerReference(containerAddress);

            try { drives.CreateIfNotExist(); }
            catch { };

            var vhdUrl = client.GetContainerReference(containerAddress).GetBlobReference("SolrStorage.vhd").Uri.ToString();
            Log(String.Format("SolrStorage.vhd {0}", vhdUrl), "Information");
            _solrStorageDrive = storageAccount.CreateCloudDrive(vhdUrl);

            int cloudDriveSizeInMB = int.Parse(RoleEnvironment.GetConfigurationSettingValue("CloudDriveSize"));
            try { _solrStorageDrive.Create(cloudDriveSizeInMB); }
            catch (CloudDriveException) { }

            Log(String.Format("CloudDriveSize {0} MB", cloudDriveSizeInMB), "Information");

            var dataPath = _solrStorageDrive.Mount(localCache.MaximumSizeInMegabytes - 50, DriveMountOptions.Force);
            Log(String.Format("Mounted as {0}", dataPath), "Information");

            return dataPath;
        }

        // follow container naming conventions to generate a unique container name
        private static string ContainerNameFromRoleId(string roleId)
        {
            return roleId.Replace('(', '-').Replace(").", "-").Replace('.', '-').Replace('_', '-').ToLower();
        }

        private void CreateSolrStoragerDirs(String vhdPath)
        {
            String solrStorageDir, solrConfDir, solrDataDir, solrLibDir;

            solrStorageDir = Path.Combine(vhdPath, "SolrStorage");
            solrConfDir = Path.Combine(solrStorageDir, "conf");
            solrDataDir = Path.Combine(solrStorageDir, "data");
            solrLibDir = Path.Combine(solrStorageDir, "lib");

            if (Directory.Exists(solrStorageDir) == false)
            {
                Directory.CreateDirectory(solrStorageDir);
            }
            if (Directory.Exists(solrConfDir) == false)
            {
                Directory.CreateDirectory(solrConfDir);
            }
            if (Directory.Exists(solrDataDir) == false)
            {
                Directory.CreateDirectory(solrDataDir);
            }
            if (Directory.Exists(solrLibDir) == false)
            {
                Directory.CreateDirectory(solrLibDir);
            }
        }

        private void InitializeLogFile(string vhdPath)
        {
            String logFileName;
            String logFileDirectoryLocation;

            logFileDirectoryLocation = Path.Combine(vhdPath, "LogFiles");
            if (Directory.Exists(logFileDirectoryLocation) == false)
            {
                Directory.CreateDirectory(logFileDirectoryLocation);
            }

            logFileName = String.Format("Log_{0}.txt", DateTime.Now.ToString("MM_dd_yyyy_HH_mm_ss"));
            using (FileStream logFileStream = File.Create(Path.Combine(logFileDirectoryLocation, logFileName)))
            {
                _logFileLocation = Path.Combine(logFileDirectoryLocation, logFileName);
            }
        }

        private void CopySolrFiles(String vhdPath)
        {
            // Copy solr conf files.
            IEnumerable<String> confFiles = Directory.EnumerateFiles(Path.Combine(Environment.GetEnvironmentVariable("RoleRoot") + @"\", @"approot\Solr\example\solr\conf"));
            foreach (String sourceFile in confFiles)
            {
                String confFileName = System.IO.Path.GetFileName(sourceFile);
                File.Copy(sourceFile, Path.Combine(vhdPath, "SolrStorage", "conf", confFileName), true);
            }

            // Overwrite original versions of SOLR files.
            string modifiedSolrFileSrc = Path.Combine(Environment.GetEnvironmentVariable("RoleRoot") + @"\", @"approot\SolrFiles\");
            string modifiedSolrFileDestination = Path.Combine(vhdPath, "SolrStorage", "conf");
            File.Copy(Path.Combine(modifiedSolrFileSrc, "data-config.xml"), Path.Combine(modifiedSolrFileDestination, "data-config.xml"), true);
            File.Copy(Path.Combine(modifiedSolrFileSrc, "schema.xml"), Path.Combine(modifiedSolrFileDestination, "schema.xml"), true);
            File.Copy(Path.Combine(modifiedSolrFileSrc, "solrconfig.xml"), Path.Combine(modifiedSolrFileDestination, "solrconfig.xml"), true);

            CopyLibFiles(Path.Combine(vhdPath, "SolrStorage"));
            CopyExtractionFiles(Path.Combine(vhdPath, "SolrStorage"));
        }

        private void CopyExtractionFiles(string solrStorage)
        {
            String libDir = Path.Combine(solrStorage, "lib");
            String sourceExtractionFilesDir = Path.Combine(Environment.GetEnvironmentVariable("RoleRoot") + @"\", @"approot\Solr\contrib\extraction\lib");
            ExecuteShellCommand(String.Format("XCOPY \"{0}\" \"{1}\"  /E /Y", sourceExtractionFilesDir, libDir), true);
        }

        private void CopyLibFiles(String solrStorage)
        {
            String libFileName, libFileLocation;
            IEnumerable<String> libFiles;

            libFiles = Directory.EnumerateFiles(Path.Combine(Environment.GetEnvironmentVariable("RoleRoot") + @"\", @"approot\Solr\dist"));
            libFileLocation = Path.Combine(solrStorage, "lib");
            foreach (String sourceFile in libFiles)
            {
                libFileName = System.IO.Path.GetFileName(sourceFile);
                File.Copy(sourceFile, Path.Combine(libFileLocation, libFileName), true);
            }
        }

        // figure out and set port, master / slave, master Url etc.
        private void InitRoleInfo()
        {
            _port = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["SolrSlaveEndpoint"].IPEndpoint.Port.ToString();
            _mySolrUrl = string.Format("http://{0}/solr/", RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["SolrSlaveEndpoint"].IPEndpoint);
            Log("My SolrURL: " + _mySolrUrl, "Information");
        }

        private Process ExecuteShellCommand(String command, bool waitForExit, String workingDir = null)
        {
            Process processToExecuteCommand = new Process();

            processToExecuteCommand.StartInfo.FileName = "cmd.exe";
            if (workingDir != null)
            {
                processToExecuteCommand.StartInfo.WorkingDirectory = workingDir;
            }
            
            processToExecuteCommand.StartInfo.Arguments = @"/C " + command;
            processToExecuteCommand.StartInfo.RedirectStandardInput = true;
            processToExecuteCommand.StartInfo.RedirectStandardError = true;
            processToExecuteCommand.StartInfo.RedirectStandardOutput = true;
            processToExecuteCommand.StartInfo.UseShellExecute = false;
            processToExecuteCommand.StartInfo.CreateNoWindow = true;
            processToExecuteCommand.EnableRaisingEvents = false;
            processToExecuteCommand.Start();

            processToExecuteCommand.OutputDataReceived += new DataReceivedEventHandler(processToExecuteCommand_OutputDataReceived);
            processToExecuteCommand.ErrorDataReceived += new DataReceivedEventHandler(processToExecuteCommand_ErrorDataReceived);
            processToExecuteCommand.BeginOutputReadLine();
            processToExecuteCommand.BeginErrorReadLine();
            
            if (waitForExit == true)
            {
                processToExecuteCommand.WaitForExit();
                processToExecuteCommand.Close();
                processToExecuteCommand.Dispose();
                processToExecuteCommand = null;
            }

            return processToExecuteCommand;
        }

        private void processToExecuteCommand_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Log(e.Data, "Message");
        }

        private void processToExecuteCommand_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Log(e.Data, "Message");
        }

        private void Log(string message, string category)
        {
            message = RoleEnvironment.CurrentRoleInstance.Id + "=> " + message;

            try
            {
                if (String.IsNullOrWhiteSpace(_logFileLocation) == false)
                {
                    File.AppendAllText(_logFileLocation, String.Concat(message, Environment.NewLine));
                }
            }
            catch
            { }
            
            Trace.WriteLine(message, category);
        }
    }
}
