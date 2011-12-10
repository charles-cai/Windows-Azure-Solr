using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace PackSolzr
{
    public class Program
    {
        private static string _solrHostMasterWorkerRoleBinLocation; // = @"C:\DevpSolr\Src\ReplSolzr\SolrMasterHostWorkerRole\bin\Debug";
        private static string _solrHostSlaveWorkerRoleBinLocation; // = @"C:\DevpSolr\Src\ReplSolzr\SolrSlaveHostWorkerRole\bin\Debug";

        private static string _webRoleBinLocation; // = @"C:\DevpSolr\Src\ReplSolzr\SolrAdminWebRole\PublishedVersion";
        private static string _jreLocation; // = @"C:\Program Files\Java\jre6";
        private static string _solrLocation; // = @"C:\RamWork1\apache-solr-3.4.0";
        private static string _csdefLocation; // = @"C:\DevpSolr\Src\ReplSolzr\ReplSolzr\ServiceDefinition.csdef";
        private static string _cspackOutputLocation; // = @"C:\temp\CSPACKAGE";
        private static string _azureSdkBinLocation; // = @"C:\Program Files\Windows Azure SDK\v1.5\bin";

        private static string _webRoleVMSize;
        private static string _masterWorkerRoleVMSize;
        private static string _slaveWorkerRoleVMSize;
        private static bool _forEmulator;

        private static Dictionary<String, String> _possibleVMSizes;

        static void Main(string[] args)
        {
            _possibleVMSizes = new Dictionary<String, String>();
            _possibleVMSizes.Add("extrasmall", "ExtraSmall");
            _possibleVMSizes.Add("small", "Small");
            _possibleVMSizes.Add("medium", "Medium");
            _possibleVMSizes.Add("large", "Large");
            _possibleVMSizes.Add("extralarge", "ExtraLarge");

            if (ParseArgs(args) == false)
            {
                Console.WriteLine("Invalid args list.");
                Console.Read();
                return;
            }

            //Clean up temp location by removing the old directories this tool might have created. 
            RemoveOldDirectories();

            String tempfolderLocation = Path.Combine(Path.GetTempPath(), String.Concat(Guid.NewGuid().ToString(), "_", "ReplSolrz"));

            //Create Directory in temporary location.
            Directory.CreateDirectory(tempfolderLocation);

            //Set up configuration.
            _csdefLocation = SetUpCSDEFFile(tempfolderLocation);

            //Init master and slave worker role.
            String masterWorkerRoleFolder = PrepareWorkerRoleFolder(tempfolderLocation, _solrHostMasterWorkerRoleBinLocation, "SolrMasterHostWorkerRole");
            String slaveWorkerRoleFolder = PrepareWorkerRoleFolder(tempfolderLocation, _solrHostSlaveWorkerRoleBinLocation, "SolrSlaveHostWorkerRole");

            //Init web role.
            String webRoleFolder = PrepareWebRoleFolder(tempfolderLocation);

            //Copy role property file.
            ExecuteShellCommand.Execute(String.Format("XCOPY \"roleproperties.txt\" \"{0}\" /E  /Y", tempfolderLocation), true);

            //Create the package.
            CreatePackage(tempfolderLocation, masterWorkerRoleFolder, slaveWorkerRoleFolder, webRoleFolder);

            //Delete the temp folder.
            if (_forEmulator == false)
            {
                /*
                 * I am not able to delete the folder structure created in temp folder because I can see the when cspack tool creates
                 * the folder strucutre for Emulator.. The roles/SolrAdminWebRole/approot contains a file named RoleModel.xml. Now this
                 * file has path to the temporary folder structure for getting website contents like JS, CSS..
                 * I am not able to find any way to solve this as of now. But on the other hand I hope this is not the case when it is 
                 * not run for emulator.
                 */
                Directory.Delete(tempfolderLocation, true);
            }

            Console.WriteLine("Packaging completed. Press any key to exit.");
            Console.ReadKey();
        }

        private static void RemoveOldDirectories()
        {
            String[] oldReplSolzrDirs = Directory.GetDirectories(Path.GetTempPath(), "*_ReplSolrz");
            foreach (String eachOldDir in oldReplSolzrDirs)
            {
                try
                {
                    Directory.Delete(eachOldDir, true);
                }
                catch { }
            }
        }

        private static String SetUpCSDEFFile(String tempfolderLocation)
        {
            String csdefFileName = Path.GetFileName(_csdefLocation);

            //Copy CSDEF file to temporary location before making changes.
            DirectoryInfo csdefFileDir = Directory.CreateDirectory(Path.Combine(tempfolderLocation, "configuration"));
            String newCSDEFFileLoc = Path.Combine(csdefFileDir.FullName, csdefFileName);
            File.Copy(_csdefLocation, newCSDEFFileLoc, true);

            //Update VM Sizes.
            XmlDocument csdefFileContent = new XmlDocument();

            XmlNamespaceManager xmlNsMgr = new XmlNamespaceManager(csdefFileContent.NameTable);
            xmlNsMgr.AddNamespace("csdefns", "http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition");

            csdefFileContent.Load(newCSDEFFileLoc);

            XmlNode adminWebRoleNode = csdefFileContent.SelectSingleNode("/csdefns:ServiceDefinition/csdefns:WebRole[@name='SolrAdminWebRole']", xmlNsMgr);
            adminWebRoleNode.Attributes["vmsize"].Value = _webRoleVMSize;

            XmlNode masterWorkerRoleNode = csdefFileContent.SelectSingleNode("/csdefns:ServiceDefinition/csdefns:WorkerRole[@name='SolrMasterHostWorkerRole']", xmlNsMgr);
            masterWorkerRoleNode.Attributes["vmsize"].Value = _masterWorkerRoleVMSize;

            XmlNode slaveWorkerRoleNode = csdefFileContent.SelectSingleNode("/csdefns:ServiceDefinition/csdefns:WorkerRole[@name='SolrSlaveHostWorkerRole']", xmlNsMgr);
            slaveWorkerRoleNode.Attributes["vmsize"].Value = _slaveWorkerRoleVMSize;

            csdefFileContent.Save(newCSDEFFileLoc);

            return newCSDEFFileLoc;
        }

        private static String PrepareWorkerRoleFolder(String tempfolderLocation, String workerRoleBinLocation, String workerRoleName)
        {
            //Create Worker role directory.
            String tempWorkerRoleDirectory = Path.Combine(tempfolderLocation, workerRoleName);
            Directory.CreateDirectory(tempWorkerRoleDirectory);

            //Copy worker role binaries. 
            ExecuteShellCommand.Execute(String.Format("XCOPY \"{0}\" \"{1}\" /E  /Y", workerRoleBinLocation, tempWorkerRoleDirectory), true);

            //Copy JRE.
            String tempJREDirectory = Path.Combine(tempWorkerRoleDirectory, "JRE6");
            Directory.CreateDirectory(tempJREDirectory);
            ExecuteShellCommand.Execute(String.Format("XCOPY \"{0}\" \"{1}\" /E  /Y", _jreLocation, tempJREDirectory), true);

            //Copy SOLR.
            String tempSolrDirectory = Path.Combine(tempWorkerRoleDirectory, "Solr");
            Directory.CreateDirectory(tempSolrDirectory);
            ExecuteShellCommand.Execute(String.Format("XCOPY \"{0}\" \"{1}\" /E  /Y", _solrLocation, tempSolrDirectory), true);

            return tempWorkerRoleDirectory;
        }

        private static String PrepareWebRoleFolder(String tempfolderLocation)
        {
            //Create Worker role directory.
            String tempWebRoleDirectory = Path.Combine(tempfolderLocation, "SolrAdminWebRole");
            Directory.CreateDirectory(tempWebRoleDirectory);

            //Copy web role binaries and contents.
            ExecuteShellCommand.Execute(String.Format("XCOPY \"{0}\" \"{1}\" /E  /Y", _webRoleBinLocation, tempWebRoleDirectory), true);

            return tempWebRoleDirectory;
        }

        private static void CreatePackage(string tempFolderLocation, string masterWorkerRoleFolder, string slaveWorkerRoleFolder, string webRoleFolder)
        {
            String createPackageCommand;
            if (_forEmulator == true)
            {
                createPackageCommand = String.Format(@"cspack ""{0}"" /role:SolrMasterHostWorkerRole;""{1}"";SolrMasterHostWorkerRole.dll /rolePropertiesFile:SolrMasterHostWorkerRole;""{3}\roleproperties.txt"" /role:SolrSlaveHostWorkerRole;""{2}"";SolrSlaveHostWorkerRole.dll /rolePropertiesFile:SolrSlaveHostWorkerRole;""{3}\roleproperties.txt"" /role:SolrAdminWebRole;""{4}"" /sites:SolrAdminWebRole;Web;""{4}"" /rolePropertiesFile:SolrAdminWebRole;""{3}\roleproperties.txt"" /out:""{5}"" /Copyonly ",
                                                             _csdefLocation, masterWorkerRoleFolder, slaveWorkerRoleFolder, tempFolderLocation, webRoleFolder, _cspackOutputLocation);
            }
            else
            {
                createPackageCommand = String.Format(@"cspack ""{0}"" /role:SolrMasterHostWorkerRole;""{1}"";SolrMasterHostWorkerRole.dll /rolePropertiesFile:SolrMasterHostWorkerRole;""{3}\roleproperties.txt"" /role:SolrSlaveHostWorkerRole;""{2}"";SolrSlaveHostWorkerRole.dll /rolePropertiesFile:SolrSlaveHostWorkerRole;""{3}\roleproperties.txt"" /role:SolrAdminWebRole;""{4}"" /sites:SolrAdminWebRole;Web;""{4}"" /rolePropertiesFile:SolrAdminWebRole;""{3}\roleproperties.txt"" /out:""{5}\ReplSolzr.cspkg"" ",
                                                                            _csdefLocation, masterWorkerRoleFolder, slaveWorkerRoleFolder, tempFolderLocation, webRoleFolder, _cspackOutputLocation);
            }
            ExecuteShellCommand.Execute(createPackageCommand, true, _azureSdkBinLocation);
        }

        private static bool ParseArgs(string[] args)
        {
            Arguments CommandLine = new Arguments(args);

            String configFilePath;

            if (String.IsNullOrWhiteSpace(CommandLine["configFilePath"]) == false)
            {
                configFilePath = CommandLine["configFilePath"];
            }
            else
            {
                return false;
            }

            XDocument configFile = XDocument.Load(configFilePath);

            String replSolzrLocation = (from eachNode in configFile.Descendants("ReplSolzrLocation") select eachNode.Value).FirstOrDefault();

            _solrHostMasterWorkerRoleBinLocation = Path.Combine(replSolzrLocation, "SolrMasterHostWorkerRole");
            _solrHostSlaveWorkerRoleBinLocation = Path.Combine(replSolzrLocation, "SolrSlaveHostWorkerRole");
            _webRoleBinLocation = Path.Combine(replSolzrLocation, "SolrAdminWebRole");
            
            _jreLocation = (from eachNode in configFile.Descendants("JreLocation") select eachNode.Value).FirstOrDefault();
            _solrLocation = (from eachNode in configFile.Descendants("SolrLocation") select eachNode.Value).FirstOrDefault();
            _csdefLocation = (from eachNode in configFile.Descendants("CsdefLocation") select eachNode.Value).FirstOrDefault();
            _cspackOutputLocation = (from eachNode in configFile.Descendants("CspackOutputLocation") select eachNode.Value).FirstOrDefault();
            _azureSdkBinLocation = (from eachNode in configFile.Descendants("AzureSdkBinLocation") select eachNode.Value).FirstOrDefault();

            String forEmulatorValInXml = (from eachNode in configFile.Descendants("ForEmulator") select eachNode.Value).FirstOrDefault();
            if (bool.TryParse(forEmulatorValInXml, out _forEmulator) == false)
            {
                return false;
            }

            String webRoleVMSizeValInXml = (from eachNode in configFile.Descendants("AdminWebRoleVMSize") select eachNode.Value).FirstOrDefault();
            if (TryParseVMSize(webRoleVMSizeValInXml, out _webRoleVMSize) == false)
            {
                return false;
            }

            String masterWorkerRoleVMSizeInXml = (from eachNode in configFile.Descendants("SolrMasterHostWorkerRoleVMSize") select eachNode.Value).FirstOrDefault();
            if (TryParseVMSize(masterWorkerRoleVMSizeInXml, out _masterWorkerRoleVMSize) == false)
            {
                return false;
            }

            String slaveWorkerRoleVMSizeInXml = (from eachNode in configFile.Descendants("SolrSlaveHostWorkerRoleVMSize") select eachNode.Value).FirstOrDefault();
            if (TryParseVMSize(slaveWorkerRoleVMSizeInXml, out _slaveWorkerRoleVMSize) == false)
            {
                return false;
            }
            return true;
        }

        private static bool TryParseVMSize(string size, out string parsedVMSize)
        {
            parsedVMSize = null;
            string formattedInputSize = size.Trim().ToLower();
            if (_possibleVMSizes.ContainsKey(formattedInputSize) == false)
            {
                return false;
            }
            parsedVMSize = _possibleVMSizes[formattedInputSize];
            return true;
        }
    }
}
