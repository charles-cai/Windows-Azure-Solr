Solr/Lucene on Azure
===
In this project we showcase how to configure and host Solr/Lucene in Windows Azure using multi-instance replication for index-serving and single-instance for index generation with a persistent index mounted in Azure storage. Typical scenarios we address with this sample are commercial and publisher sites that need to scale the traffic with increasing query volume and need to index maximum 16 TB of data and require couple of index updates per day.

## Prerequisites:

Windows 64 bit Operating System for creating, packing and deploying to Windows Azure.

1. Install the Windows Azure SDK from http://www.microsoft.com/windowsazure/sdk/.  This will install the SDK at `C:\Program Files\Windows Azure SDK\`
2. Create a Windows Azure subscription. A free 90 day trial can be obtained at http://www.microsoft.com/windowsazure/free-trial/.  
3. Create a Windows Azure Storage Account
    - Login to your Windows Azure Portal: http://windows.azure.com
    - After logging in, click New Storage Account on the toolbar 
    - Fill the details about your new storage account.We recommend creating an affinity group that  groups your services into one location and help optimize for speed.


4. Create a Windows Azure Hosted Service Account
    - Login to your Windows Azure Portal or http://windows.azure.com click on Home if already signed in 
    - Click New Hosted Service on the toolbar 
    - Fill the details about your subscription, service and use the same affinity group from Step 3.
   

5. Create Windows Azure Management Certificate
    - Create a self-signed certificate using IIS Manager: click on yout computer name on the left, choose "Server Certificates", and then "Create Self-Signed Certificate on the right".
    - Ctart/Run certmgr.msc . Find your Truster Root Certification Authorities/Certificates on tye left, by friendly name you have it on the previous setp.
    - Right click/All Tasks/Export it as .cer and upload to Azure as management certificate 
    - Install it to Personal certificate store: copy/Paste from "Truster Root Certification Authorities/Certificates" to "Personal/Certificates"


6. Install the Java JRE
    - Download the 64bit version of JRE from http://www.java.com/en/download/manual.jsp
      Note: Compatible JRE version can be found on the Solr/Lucene manual at http://lucene.apache.org/solr/tutorial.html#Requirements
      In this tutorial we use `C:\Program Files\Java\jre6`


7. Install Solr/Lucene
    - We tested this project with Solr/Lucene 3.4 and Solr/Lucene 3.5. Solr can be downloaded from: http://www.apache.org/dyn/closer.cgi/lucene/solr/
      In this tutorial we used 3.5 and unzip it in the root `C:\apache-solr-3.5.0`


8. Download the Windows Azure Solr/Lucene project from this Github location
    - In this tutorial we unzip it in the `C:\temp\Solr`


9. Package the code 
    - Create output folder `C:\temp\CSPACKAGE`
    - `cd C:\temp\solr\PackSolzr`
    - Review the `C:\Temp\solr\PackSolzr\PackSolzrConfig.xml` and update with your path if different than the one in the tutorial
    - Run 

            PackSolzr.exe /configFilePath=PackSolzrConfig.xml

10. Deploy to Azure 
    - `cd C:\temp\solr\DeploySolzr`
    - Review the `C:\temp\solr\DeploySolzr\DeploySolzrConfig.xml`, add your Windows Azure credentials and update the paths if different than the one in the tutorial
    Note: <HostedServiceName> tag expects the hosted service DNS prefix
    - Run

            DeploySolzr.exe /configFilePath=DeploySolzrConfig.xml

    - After deployment finish successfully the deployment status will indicate ready.

11. Administering Solr/Lucene
    - In the panel for your deployment from step 10 you will find the DNS name `http://<Deployment_Endpoint>.cloudapp.net`
    - Start in a browser `http://<Deployment_Endpoint>.cloudapp.net` to the typical tasks for Solr
        - `Crawl`: used to get public web content
        - `Import`: used to  index and replicate the data across SolrSlave  instances. 
          Note: An import finished successfully when Solr Slaves and the Solr Master will have the same generation index and index size 
        - `Search`: once the replication of the index finished for both Solr slave instances
