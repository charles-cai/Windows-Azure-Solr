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
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using System.IO;

namespace DeploySolzr
{
    public class PackageBlobHandler
    {
        private const string _cspkgBlobContainerName = "replsolzrcontainer";

        public string StoreDeploymentPackageInBlob(string csPkgFileLocation,
                                                   string blobUrl,
                                                   string storageAccountName,
                                                   string storageAccountKey)
        {
            string fileName;
            bool containerCreated = false;
            StorageCredentials storageCredentails;
            CloudBlobClient blobClient;
            CloudBlobContainer packageContainer;
            CloudBlob deploymentBlob;

            storageCredentails = new StorageCredentialsAccountAndKey(storageAccountName, storageAccountKey);
            blobClient = new CloudBlobClient(blobUrl, storageCredentails);

            packageContainer = blobClient.GetContainerReference(_cspkgBlobContainerName);

            try { containerCreated = packageContainer.CreateIfNotExist(); }
            catch { };

            if (containerCreated == true)
            {
                BlobContainerPermissions permissions = new BlobContainerPermissions();
                permissions.PublicAccess = BlobContainerPublicAccessType.Container;
                packageContainer.SetPermissions(permissions);
            }

            //Upload a file.
            Console.WriteLine("Uploading Deployment package - start");
            fileName = Path.GetFileName(csPkgFileLocation);
            deploymentBlob = packageContainer.GetBlobReference(fileName);

            using (FileStream fs = new FileStream(csPkgFileLocation, FileMode.Open))
            {
                deploymentBlob.UploadFromStream(fs);
            }
            Console.WriteLine("Uploading Deployment package - end");
            return String.Format(deploymentBlob.Uri.ToString());
        }


        public void DeletePackageBlob(string csPkgFileLocation,
                                      string blobUrl,
                                      string storageAccountName,
                                      string storageAccountKey)
        {
            StorageCredentials storageCredentails;
            CloudBlobClient blobClient;
            CloudBlobContainer packageContainer;

            storageCredentails = new StorageCredentialsAccountAndKey(storageAccountName, storageAccountKey);
            blobClient = new CloudBlobClient(blobUrl, storageCredentails);

            packageContainer = blobClient.GetContainerReference(_cspkgBlobContainerName);
            packageContainer.Delete();
        }
    }
}
