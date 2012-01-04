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
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace DeploySolzr
{
    class Program
    {
        private static string _solrStorageAccName;
        private static string _solrStorageAccKey;
        private static string _hostedServiceName;
        private static string _subscriptionId;
        private static string _certThumbprint;
        private static string _deploymentName;
        private static string _masterWorkerRoleInstCount;
        private static string _slaveWorkerRoleInstCount;
        private static string _adminWebRoleInstCount;
        private static string _cloudDriveSize;
        private static string _deploymentPckgLoc;
        private static string _masterWorkerRolePort;
        private static string _slaveWorkerRolePort;
        private static string _blobBaseUrl;
        private static string _deploymentSlotName;

        static void Main(string[] args)
        {
            //Step 1 - Extract the name of config file from command line arg and parse xml to get required params.
            if (ParseArgs(args) == false)
            {
                Console.WriteLine("DeploySolzr: Deploys Solzr (Solr implementation on Windows Azure).\n\n" +
                     "Usage:\n\n" +
                     "DeploySolzr -configFilePath=<Path to file containing configuration parameters.>"
                     );
                Console.ReadLine();
                return;
            }

            PackageBlobHandler blobHandler = new PackageBlobHandler();
            String blobWithPackageLocation = blobHandler.StoreDeploymentPackageInBlob(_deploymentPckgLoc, _blobBaseUrl, _solrStorageAccName, _solrStorageAccKey);

            DeployReplSolzr deploymentHandler = new DeployReplSolzr();
            deploymentHandler.adminWebRoleInstCount = _adminWebRoleInstCount;
            deploymentHandler.certThumbprint = _certThumbprint;
            deploymentHandler.cloudDriveSize = _cloudDriveSize;
            deploymentHandler.deploymentName = _deploymentName;
            deploymentHandler.deploymentPckgLoc = blobWithPackageLocation;
            deploymentHandler.hostedServiceName = _hostedServiceName;
            deploymentHandler.masterWorkerRoleInstCount = _masterWorkerRoleInstCount;
            deploymentHandler.masterWorkerRolePort = _masterWorkerRolePort;
            deploymentHandler.slaveWorkerRoleInstCount = _slaveWorkerRoleInstCount;
            deploymentHandler.slaveWorkerRolePort = _slaveWorkerRolePort;
            deploymentHandler.solrStorageAccKey = _solrStorageAccKey;
            deploymentHandler.solrStorageAccName = _solrStorageAccName;
            deploymentHandler.subscriptionId = _subscriptionId;
            deploymentHandler.deploymentSlotName = _deploymentSlotName;

            deploymentHandler.Deploy();
            blobHandler.DeletePackageBlob(_deploymentPckgLoc, _blobBaseUrl, _solrStorageAccName, _solrStorageAccKey);
            Console.Write("Deployment Complete. Press any key to exit.");
            Console.ReadKey();
        }

        private static bool ParseArgs(string[] args)
        {
            string configFilePath;
            Arguments CommandLine;
            XDocument configFile;

            CommandLine = new Arguments(args);

            if (String.IsNullOrWhiteSpace(CommandLine["configFilePath"]) == false)
                configFilePath = CommandLine["configFilePath"];
            else
                return false;

            configFile = XDocument.Load(configFilePath);

            _solrStorageAccName = (from eachNode in configFile.Descendants("SolrStorageAccName") select eachNode.Value).FirstOrDefault();
            _solrStorageAccKey = (from eachNode in configFile.Descendants("SolrStorageAccKey") select eachNode.Value).FirstOrDefault();
            _hostedServiceName = (from eachNode in configFile.Descendants("HostedServiceName") select eachNode.Value).FirstOrDefault();
            _subscriptionId = (from eachNode in configFile.Descendants("SubscriptionId") select eachNode.Value).FirstOrDefault();
            _certThumbprint = (from eachNode in configFile.Descendants("CertThumbprint") select eachNode.Value).FirstOrDefault();
            _deploymentName = (from eachNode in configFile.Descendants("DeploymentName") select eachNode.Value).FirstOrDefault();
            _masterWorkerRoleInstCount = (from eachNode in configFile.Descendants("SolrMasterHostWorkerRoleInstCount") select eachNode.Value).FirstOrDefault();
            _slaveWorkerRoleInstCount = (from eachNode in configFile.Descendants("SolrSlaveHostWorkerRoleInstCount") select eachNode.Value).FirstOrDefault();
            _adminWebRoleInstCount = (from eachNode in configFile.Descendants("AdminWebRoleInstCount") select eachNode.Value).FirstOrDefault();
            _deploymentPckgLoc = (from eachNode in configFile.Descendants("DeploymentPckgLoc") select eachNode.Value).FirstOrDefault();
            _cloudDriveSize = (from eachNode in configFile.Descendants("CloudDriveSize") select eachNode.Value).FirstOrDefault();
            _masterWorkerRolePort = (from eachNode in configFile.Descendants("SolrMasterHostWorkerRoleExternalEndpointPort") select eachNode.Value).FirstOrDefault();
            _slaveWorkerRolePort = (from eachNode in configFile.Descendants("SolrSlaveHostWorkerRoleExternalEndpointPort") select eachNode.Value).FirstOrDefault();
            _blobBaseUrl = (from eachNode in configFile.Descendants("BlobBaseUrl") select eachNode.Value).FirstOrDefault();
            _deploymentSlotName = (from eachNode in configFile.Descendants("DeploymentSlotName") select eachNode.Value).FirstOrDefault().ToLower();

            return true;
        }
    }
}
