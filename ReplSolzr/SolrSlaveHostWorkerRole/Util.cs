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
using System.Threading;

namespace SolrSlaveHostWorkerRole
{
	public class Util
	{
        public static string GetMasterUrl()
        {
            int numTries = 100;

            while (--numTries > 0) // try multiple times since master may be initializing
            {
                try
                {
                    string masterUrl = "http://" + GetMasterEndpoint().ToString() + "/solr/";
                    return masterUrl;
                }
                catch
                {
                    Thread.Sleep(10000);
                }
            }

            throw new ApplicationException("Master not ready");
        }

        /// <summary>
        /// Get the url associated with the master worker instance. If running with a warm standby instance
        /// it returns the address on which solr is actually listening.
        /// </summary>
        private static IPEndPoint GetMasterEndpoint()
        {
            var instance = RoleEnvironment.Roles["SolrMasterHostWorkerRole"].Instances[0];

            if (instance.InstanceEndpoints["SolrMasterServiceEndpoint"] != null)
            {
                // Various approaches are possible for obtaining the external endpoint, depending upon your situation and requirements.
                // Possible approaches include writing a small WCF service in the worker role that returns its external endpoint, or having some sort of reporting mechanism to a well-known
                // location (such as a common blob where all roles report their status). We may implement one of these approaches ourselves in a future release.
                IPEndPoint solrEndpoint = instance.InstanceEndpoints["SolrMasterServiceEndpoint"].IPEndpoint;
                int port = int.Parse(RoleEnvironment.GetConfigurationSettingValue("SolrMasterHostWorkerRoleExternalEndpointPort"));
                int cAttempts = 10;

                while (--cAttempts > 0)
                {
                    solrEndpoint = new IPEndPoint(solrEndpoint.Address, port++);

                    if (CheckEndpoint(solrEndpoint))
                        return solrEndpoint;
                }
            }

            throw new ApplicationException("Master not ready");
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