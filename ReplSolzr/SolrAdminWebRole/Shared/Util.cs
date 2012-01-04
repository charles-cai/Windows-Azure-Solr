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
using System.Web;
using Microsoft.WindowsAzure.ServiceRuntime;
using System.Net;
using System.Net.Sockets;

namespace SolrAdminWebRole.Shared
{
    public class Util
    {
        public static string GetSolrUrl(bool bMaster, int iInstance = -1)
        {
            string url = null;

            try
            {
                // Worker role access:
                url = "http://" + GetSolrEndpoint(bMaster, iInstance).ToString() + "/solr/";
            }
            catch { }

            return url;
        }

        /// <summary>
        /// Get the url associated with the worker instance. If running with a warm standby instance
        /// it returns the address on which solr is actually listening.
        /// Specify bMaster = true to get master instance, false to get slave instance.
        /// Specify iInstance = -1 to get the endpoint of any instance of that type that may be actively listening.
        /// </summary>
        private static IPEndPoint GetSolrEndpoint(bool bMaster, int iInstance)
        {
            var roleInstances = RoleEnvironment.Roles[bMaster ? "SolrMasterHostWorkerRole" : "SolrSlaveHostWorkerRole"].Instances;
            IPEndPoint solrEndpoint = null;

            if (iInstance >= 0)
                solrEndpoint = GetEndpoint(roleInstances[iInstance], bMaster);
            else
            {
                foreach (var instance in roleInstances)
                {
                    solrEndpoint = GetEndpoint(instance, bMaster);
                    if (solrEndpoint == null)
                        continue;
                    
                    break;
                }
            }

            if (solrEndpoint == null)
                return null;

            return solrEndpoint;
        }

        private static IPEndPoint GetEndpoint(RoleInstance instance, bool bMaster)
        {
            string internalEndpointName = bMaster ? "SolrMasterServiceEndpoint" : "SolrSlaveServiceEndpoint";
            string externalEndpointSettingName = bMaster ? "SolrMasterHostWorkerRoleExternalEndpointPort" : "SolrSlaveHostWorkerRoleExternalEndpointPort";

            if (instance.InstanceEndpoints[internalEndpointName] == null)
                return null;

            // Various approaches are possible for obtaining the external endpoint, depending upon your situation and requirements.
            // Possible approaches include writing a small WCF service in the worker role that returns its external endpoint, or having some sort of reporting mechanism to a well-known
            // location (such as a common blob where all roles report their status). We may implement one of these approaches ourselves in a future release.
            IPEndPoint solrEndpoint = instance.InstanceEndpoints[internalEndpointName].IPEndpoint;
            int port = int.Parse(RoleEnvironment.GetConfigurationSettingValue(externalEndpointSettingName));
            int cAttempts = 10;

            while (--cAttempts > 0)
            {
                solrEndpoint = new IPEndPoint(solrEndpoint.Address, port++);

                if (CheckEndpoint(solrEndpoint))
                    return solrEndpoint;
            }

            throw new ApplicationException("Master not ready");
        }

        public static int GetNumInstances(bool bMaster)
        {
            var roleInstances = RoleEnvironment.Roles[bMaster ? "SolrMasterHostWorkerRole" : "SolrSlaveHostWorkerRole"].Instances;
            return roleInstances.Count();
        }

        private static bool CheckEndpoint(IPEndPoint solrEndpoint)
        {
            var valid = false;
            using (var s = new Socket(solrEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                try
                {
                    s.Connect(solrEndpoint);
                    if (s.Connected)
                    {
                        valid = true;
                        s.Disconnect(true);
                    }
                    else
                    {
                        valid = false;
                    }
                }
                catch
                {
                    valid = false;
                }
            }

            return valid;
        }
    }
}