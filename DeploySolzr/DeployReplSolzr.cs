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
using System.Xml;
using System.Net;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Timers;

namespace DeploySolzr
{
    public class DeployReplSolzr
    {
        public string solrStorageAccName { get; set; }
        public string solrStorageAccKey { get; set; }
        public string hostedServiceName { get; set; }
        public string subscriptionId { get; set; }
        public string certThumbprint { get; set; }
        public string deploymentName { get; set; }
        public string masterWorkerRoleInstCount { get; set; }
        public string slaveWorkerRoleInstCount { get; set; }
        public string adminWebRoleInstCount { get; set; }
        public string cloudDriveSize { get; set; }
        public string deploymentPckgLoc { get; set; }
        public string masterWorkerRolePort { get; set; }
        public string slaveWorkerRolePort { get; set; }
        public string deploymentSlotName { get; set; }

        private static System.Timers.Timer deployAppWaitTimer;

        public void Deploy()
        {
            //Step 1 - Prepare configuration file.
            string configuration = UpdateConfiguration();

            //Step 2 - Deploy the package...wait untill completion.
            string requestToken = InitiateDeployment(configuration);

            WaitForDeploymentComplete(requestToken);
        }

        private string UpdateConfiguration()
        {
            XmlDocument configFile;
            XmlNamespaceManager namespaceMgr;

            configFile = new XmlDocument();
            configFile.Load("ReplSolzrServiceConfiguration.Cloud.cscfg");

            namespaceMgr = new XmlNamespaceManager(configFile.NameTable);
            namespaceMgr.AddNamespace("srccfg", "http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceConfiguration");

            // master worker role
            XmlNode masterWorkerRoleNode = configFile.SelectSingleNode("/srccfg:ServiceConfiguration/srccfg:Role[@name='SolrMasterHostWorkerRole']", namespaceMgr);

            XmlNode instCountMasterWorkerRole = masterWorkerRoleNode.SelectSingleNode("srccfg:Instances", namespaceMgr);
            instCountMasterWorkerRole.Attributes["count"].Value = masterWorkerRoleInstCount;

            XmlNode dataConnectionStr = masterWorkerRoleNode.SelectSingleNode("srccfg:ConfigurationSettings/srccfg:Setting[@name='DataConnectionString']", namespaceMgr);
            dataConnectionStr.Attributes["value"].Value = String.Format("DefaultEndpointsProtocol=http;AccountName={0};AccountKey={1}", solrStorageAccName, solrStorageAccKey);

            XmlNode cloudDriveSizeNode = masterWorkerRoleNode.SelectSingleNode("srccfg:ConfigurationSettings/srccfg:Setting[@name='CloudDriveSize']", namespaceMgr);
            cloudDriveSizeNode.Attributes["value"].Value = cloudDriveSize;

            // slave worker role
            XmlNode slaveWorkerRoleNode = configFile.SelectSingleNode("/srccfg:ServiceConfiguration/srccfg:Role[@name='SolrSlaveHostWorkerRole']", namespaceMgr);

            XmlNode instCountSlaveWorkerRole = slaveWorkerRoleNode.SelectSingleNode("srccfg:Instances", namespaceMgr);
            instCountSlaveWorkerRole.Attributes["count"].Value = slaveWorkerRoleInstCount;

            dataConnectionStr = slaveWorkerRoleNode.SelectSingleNode("srccfg:ConfigurationSettings/srccfg:Setting[@name='DataConnectionString']", namespaceMgr);
            dataConnectionStr.Attributes["value"].Value = String.Format("DefaultEndpointsProtocol=http;AccountName={0};AccountKey={1}", solrStorageAccName, solrStorageAccKey);

            cloudDriveSizeNode = slaveWorkerRoleNode.SelectSingleNode("srccfg:ConfigurationSettings/srccfg:Setting[@name='CloudDriveSize']", namespaceMgr);
            cloudDriveSizeNode.Attributes["value"].Value = cloudDriveSize;

            XmlNode masterWorkerRolePortNode = slaveWorkerRoleNode.SelectSingleNode("srccfg:ConfigurationSettings/srccfg:Setting[@name='SolrMasterHostWorkerRoleExternalEndpointPort']", namespaceMgr);
            masterWorkerRolePortNode.Attributes["value"].Value = masterWorkerRolePort;

            // admin web role
            XmlNode adminWebRoleNode = configFile.SelectSingleNode("/srccfg:ServiceConfiguration/srccfg:Role[@name='SolrAdminWebRole']", namespaceMgr);

            XmlNode instCountAdminWebRole = adminWebRoleNode.SelectSingleNode("srccfg:Instances", namespaceMgr);
            instCountAdminWebRole.Attributes["count"].Value = adminWebRoleInstCount;

            masterWorkerRolePortNode = adminWebRoleNode.SelectSingleNode("srccfg:ConfigurationSettings/srccfg:Setting[@name='SolrMasterHostWorkerRoleExternalEndpointPort']", namespaceMgr);
            masterWorkerRolePortNode.Attributes["value"].Value = masterWorkerRolePort;

            XmlNode slaveWorkerRolePortNode = adminWebRoleNode.SelectSingleNode("srccfg:ConfigurationSettings/srccfg:Setting[@name='SolrSlaveHostWorkerRoleExternalEndpointPort']", namespaceMgr);
            slaveWorkerRolePortNode.Attributes["value"].Value = slaveWorkerRolePort;

            return configFile.OuterXml;
        }

        private string InitiateDeployment(string configuration)
        {
            string requestUri;
            string requestXml;
            string deploymentError;

            HttpWebRequest webRequest;
            HttpWebResponse webResponse = null;
            WebResponse errorResponse;
            X509Certificate2 authCert;

            byte[] hostedServiceRequestXml;

            try
            {
                Console.WriteLine("Deploying the application -- start");

                authCert = GetAuthCertificate(certThumbprint);
                requestXml = CreateDeploymentXml(configuration);
                requestUri = string.Format("https://management.core.windows.net/{0}/services/hostedservices/{1}/deploymentslots/{2}", subscriptionId, hostedServiceName, deploymentSlotName);

                webRequest = (HttpWebRequest)WebRequest.Create(requestUri);
                webRequest.Method = "POST";
                webRequest.ContentType = "application/xml";
                webRequest.ClientCertificates.Add(authCert);
                webRequest.Headers.Add("x-ms-version", "2011-08-01");

                using (Stream requestStream = webRequest.GetRequestStream())
                {
                    hostedServiceRequestXml = UTF8Encoding.UTF8.GetBytes(requestXml);
                    requestStream.Write(hostedServiceRequestXml, 0, hostedServiceRequestXml.Length);
                }

                try
                {
                    webResponse = (HttpWebResponse)webRequest.GetResponse();
                }
                catch (WebException ex)
                {
                    errorResponse = ex.Response;
                    using (StreamReader sr = new StreamReader(errorResponse.GetResponseStream()))
                    {
                        deploymentError = sr.ReadToEnd();
                    }
                    Console.WriteLine("Error occured while sending deployment request. Error - " + deploymentError);
                    throw;
                }
                if (webResponse.StatusCode != HttpStatusCode.Accepted)
                {
                    throw new Exception(@"Error creating hosted service. Error code - " +
                                        webResponse.StatusCode.ToString() +
                                        " Description - " + webResponse.StatusDescription);
                }
                Console.WriteLine("Request for deploying the service submitted successfully. Request token - " + webResponse.Headers["x-ms-request-id"]);
                return webResponse.Headers["x-ms-request-id"];
            }
            finally
            {
                if (webResponse != null) webResponse.Close();
            }
        }

        private string CreateDeploymentXml(string configuration)
        {
            string deploymentXml;
            StringBuilder sb = new StringBuilder();

            using (MemoryStream ms = new MemoryStream())
            {
                XmlWriter deploymentXmlCreator = XmlTextWriter.Create(ms);
                deploymentXmlCreator.WriteStartDocument();
                deploymentXmlCreator.WriteStartElement("CreateDeployment", @"http://schemas.microsoft.com/windowsazure");
                deploymentXmlCreator.WriteElementString("Name", deploymentName);
                deploymentXmlCreator.WriteElementString("PackageUrl", deploymentPckgLoc);
                deploymentXmlCreator.WriteElementString("Label", Convert.ToBase64String(System.Text.UTF8Encoding.UTF8.GetBytes(deploymentName)));
                deploymentXmlCreator.WriteElementString("Configuration", Convert.ToBase64String(System.Text.UTF8Encoding.UTF8.GetBytes(configuration)));
                deploymentXmlCreator.WriteElementString("StartDeployment", "true");
                deploymentXmlCreator.WriteElementString("TreatWarningsAsError", "false");
                deploymentXmlCreator.WriteEndElement();
                deploymentXmlCreator.WriteEndDocument();

                deploymentXmlCreator.Flush();
                deploymentXmlCreator.Close();

                using (StreamReader sr = new StreamReader(ms))
                {
                    ms.Position = 0;
                    deploymentXml = sr.ReadToEnd();
                    sr.Close();
                }
            }
            return deploymentXml;
        }

        private X509Certificate2 GetAuthCertificate(string certThumbprint)
        {
            X509Store certStore;
            X509Certificate2Collection matchingCerts;

            certStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            certStore.Open(OpenFlags.ReadOnly);

            matchingCerts = certStore.Certificates.Find(X509FindType.FindByThumbprint,
                                                        certThumbprint,
                                                        false);
            certStore.Close();
            if (matchingCerts.Count == 0)
            {
                throw new Exception("Authorization certificate not found.");
            }
            return matchingCerts[0];
        }

        private void WaitForDeploymentComplete(string requestToken)
        {
            AutoResetEvent threadBlocker = null;
            try
            {
                threadBlocker = new AutoResetEvent(false);

                deployAppWaitTimer = new System.Timers.Timer(5000);
                deployAppWaitTimer.Elapsed += new ElapsedEventHandler(
                    delegate(object sender, ElapsedEventArgs e)
                    {
                        string requestUri;
                        string responseXml;
                        bool isError;

                        HttpWebRequest webRequest;
                        HttpWebResponse webResponse = null;
                        X509Certificate2 authCert;

                        try
                        {
                            Console.WriteLine("Getting deployment request status.");
                            deployAppWaitTimer.Stop();
                            authCert = GetAuthCertificate(certThumbprint);
                            requestUri = string.Format("https://management.core.windows.net/{0}/operations/{1}", subscriptionId, requestToken);

                            webRequest = (HttpWebRequest)WebRequest.Create(requestUri);
                            webRequest.Method = "GET";
                            webRequest.ClientCertificates.Add(authCert);
                            webRequest.Headers.Add("x-ms-version", "2009-10-01");

                            webResponse = (HttpWebResponse)webRequest.GetResponse();
                            if (webResponse.StatusCode != HttpStatusCode.OK)
                            {
                                throw new Exception(@"Error fetching status code for creating deployment. Error code - " +
                                                    webResponse.StatusCode.ToString() +
                                                    " Description - " + webResponse.StatusDescription);
                            }

                            using (Stream responseStream = webResponse.GetResponseStream())
                            using (StreamReader responseStreamReader = new StreamReader(responseStream))
                            {
                                responseXml = responseStreamReader.ReadToEnd();
                                if (IsDeploymentComplete(responseXml, out isError) == true)
                                {
                                    Console.WriteLine("Deployment successfull.");
                                    deployAppWaitTimer.Dispose();
                                    threadBlocker.Set();
                                }
                                else if (isError == true) //Give up.
                                {
                                    deployAppWaitTimer.Dispose();
                                    threadBlocker.Set();
                                }
                                else
                                {
                                    Console.WriteLine("Deployment not complete yet. System shall retry after 5 seconds.");
                                    deployAppWaitTimer.Start();
                                }
                            }
                        }
                        finally
                        {
                            if (webResponse != null) webResponse.Close();
                        }
                    });

                deployAppWaitTimer.Start();
                threadBlocker.WaitOne();
            }
            finally
            {
                if (threadBlocker != null) threadBlocker.Dispose();
            }
        }

        private bool IsDeploymentComplete(string responseXml, out bool isError)
        {
            bool isComplete;
            XmlDocument responseXmlDoc;
            XmlNode statusCode;
            XmlNamespaceManager namespaceManager;

            responseXmlDoc = new XmlDocument();
            responseXmlDoc.LoadXml(responseXml);

            namespaceManager = new XmlNamespaceManager(responseXmlDoc.NameTable);
            namespaceManager.AddNamespace("wa", "http://schemas.microsoft.com/windowsazure");
            namespaceManager.AddNamespace("i", "http://www.w3.org/2001/XMLSchema-instance");

            statusCode = responseXmlDoc.SelectSingleNode("/wa:Operation/wa:Status", namespaceManager);

            switch (statusCode.InnerText)
            {
                case "InProgress":
                    isComplete = false;
                    isError = false;
                    break;
                case "Succeeded":
                    isComplete = true;
                    isError = false;
                    break;
                default:
                case "Failed":
                    isComplete = false;
                    isError = true;
                    XmlNode errorNode = responseXmlDoc.SelectSingleNode("/wa:Operation/wa:Error", namespaceManager);
                    XmlNode errorCode = errorNode.SelectSingleNode("wa:Code", namespaceManager);
                    XmlNode errorMessage = errorNode.SelectSingleNode("wa:Message", namespaceManager);
                    Console.WriteLine(String.Format("Error during deployment - Error code is {0} and Error message is {1}", errorCode.InnerText, errorMessage.InnerText));
                    break;
            }
            return isComplete;
        }
    }
}
