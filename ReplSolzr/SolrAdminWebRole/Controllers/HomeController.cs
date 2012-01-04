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
using System.Web.Mvc;
using Microsoft.WindowsAzure.ServiceRuntime;
using System.Net;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Samples.NetCF;
using System.Threading;

namespace SolrAdminWebRole.Controllers
{
    public class SearchResult
    {
        public string Url { get; set; }
        public string Title { get; set; }
    }

    public class SolrInstanceStatus
    {
        public string InstanceName { get; set; }
        public string Url { get; set; }
        public bool IsReady { get; set; }
        public bool IsMaster { get; set; }
        public string IndexVersion { get; set; }
        public string Generation { get; set; }
        public string IndexSize { get; set; }
        public string NumTimesReplicated { get; set; }
        public string LastReplicationTime { get; set; }
        public string NextReplicationTime { get; set; }
        public string PollInterval { get; set; }
    }

    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Message = "Welcome to Solzr (Solr-On-Azure) Admin!";

            return View();
        }

        public ActionResult Search()
        {
            ViewBag.SearchText = "<Enter search term>";

            string solrUrl = SolrAdminWebRole.Shared.Util.GetSolrUrl(false);

            if (solrUrl == null)
            {
                ViewBag.Message = "Search using Solr Slave [Not ready]";
                return View();
            }

            ViewBag.Message = "Search using Solr Slave [Ready]";

            return View();
        }

        public ActionResult SolrAdmin(string role)
        {
            SolrInstanceStatus status = new SolrInstanceStatus();
            List<SolrInstanceStatus> statuses = new List<SolrInstanceStatus>();

            if (role == "master")
            {
                status = GetSolrInstanceStatus(true, -1);
                statuses.Add(status);
            }
            else
            {
                int numInstances = Shared.Util.GetNumInstances(false);
                for (int iInstance = 0; iInstance < numInstances; iInstance++)
                {
                    status = GetSolrInstanceStatus(false, iInstance);
                    statuses.Add(status);
                }
            }

            ViewBag.Statuses = statuses;

            return View();
        }

        private dynamic GetSolrInstanceStatus(bool isMaster, int iInstance)
        {
            SolrInstanceStatus status = new SolrInstanceStatus() { IsReady = false };

            if (isMaster)
            {
                status.InstanceName = "Solr Master";
                status.IsMaster = true;
            }
            else
            {
                status.InstanceName = "Solr Slave #" + iInstance;
                status.IsMaster = false;
            }

            string solrUrl = Shared.Util.GetSolrUrl(isMaster, iInstance);

            if (solrUrl == null)
                return status;

            status.Url = solrUrl;

            try
            {
                string solrCommandUrl = solrUrl + "replication?command=details";
                string result = ExecSolrCommand(solrCommandUrl);

                XmlDocument docResult = new XmlDocument();
                docResult.LoadXml(result);

                XmlNode node;

                node = docResult.SelectSingleNode("response/lst[@name='details']/long[@name='indexVersion']");
                if (node != null)
                    status.IndexVersion = node.InnerText;

                node = docResult.SelectSingleNode("response/lst[@name='details']/str[@name='indexSize']");
                if (node != null)
                    status.IndexSize = node.InnerText;

                node = docResult.SelectSingleNode("response/lst[@name='details']/long[@name='generation']");
                if (node != null)
                    status.Generation = node.InnerText;

                if (!isMaster)
                {
                    node = docResult.SelectSingleNode("response/lst[@name='details']/lst[@name='slave']/str[@name='timesIndexReplicated']");
                    if (node != null)
                        status.NumTimesReplicated = node.InnerText;

                    node = docResult.SelectSingleNode("response/lst[@name='details']/lst[@name='slave']/str[@name='indexReplicatedAt']");
                    if (node != null)
                        status.LastReplicationTime = node.InnerText;

                    node = docResult.SelectSingleNode("response/lst[@name='details']/lst[@name='slave']/str[@name='nextExecutionAt']");
                    if (node != null)
                        status.NextReplicationTime = node.InnerText;

                    node = docResult.SelectSingleNode("response/lst[@name='details']/lst[@name='slave']/str[@name='pollInterval']");
                    if (node != null)
                        status.PollInterval = node.InnerText;
                }

                status.IsReady = true;
            }
            catch
            {
            }

            return status;
        }

        [HttpPost]
        public ActionResult Search(FormCollection forms)
        {
            string searchText = forms["searchText"];
            ViewBag.SearchStatus = null;

            if (string.IsNullOrEmpty(searchText))
                ViewBag.SearchText = "<Enter search term>";
            else
                ViewBag.SearchText = searchText;

            string solrUrl = SolrAdminWebRole.Shared.Util.GetSolrUrl(false);

            if (solrUrl == null)
            {
                ViewBag.Message = "Search using Solr Slave [Not ready]";
                return View();
            }

            ViewBag.Message = "Search using Solr Slave [Ready]";

            try
            {
                HttpWebRequest request = WebRequest.Create(solrUrl + "select/?start=0&rows=10&indent=on&q=" +
                    HttpUtility.UrlEncode(searchText)) as HttpWebRequest;
                request.Timeout = 10000;

                // Get response  
                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    // Get the response stream  
                    StreamReader reader = new StreamReader(response.GetResponseStream());

                    string result = reader.ReadToEnd();
                    ViewBag.SearchResults = GetSearchResults(result);

                    if ((ViewBag.SearchResults == null) || (ViewBag.SearchResults.Count == 0))
                        ViewBag.SearchStatus = "No results returned.";
                    else
                        ViewBag.SearchStatus = ViewBag.SearchResults.Count + " results returned.";
                }
            }
            catch (Exception exc)
            {
                ViewBag.SearchStatus = "Exception during search request: " + exc.ToString();
            }

            return View();
        }

        [HttpPost]
        public ActionResult ImportWikipediaData(FormCollection forms)
        {
            string solrUrl = SolrAdminWebRole.Shared.Util.GetSolrUrl(true);

            if (solrUrl == null)
                return Json(false);

            try
            {
                HttpWebRequest request = WebRequest.Create(solrUrl + "dataimport?command=full-import") as HttpWebRequest;
                request.Timeout = 10000;

                // Get response  
                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    // Get the response stream  
                    StreamReader reader = new StreamReader(response.GetResponseStream());
                    string result = reader.ReadToEnd();
                }
            }
            catch
            {
                return Json(false);
            }

            return Json(true);
        }

        public ActionResult Import()
        {
            string solrUrl = SolrAdminWebRole.Shared.Util.GetSolrUrl(true);

            if (solrUrl == null)
                ViewBag.Message = "Import data into Solr Master [Not ready]";
            else
                ViewBag.Message = "Import data into Solr Master [Ready]";

            return View();
        }

        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult UploadFile(HttpPostedFileBase postedFile)
        {
            string solrUrl = SolrAdminWebRole.Shared.Util.GetSolrUrl(true);

            if (solrUrl == null)
            {
                ViewBag.Message = "Upload file data into Solr Master [Not ready]";
                return View("Import");
            }

            ViewBag.Message = "Upload file data into Solr Master [Ready]";

            if (postedFile != null)
            {
                try
                {
                    Uri address = new Uri(solrUrl + "update?commit=true");

                    // Create the web request  
                    HttpWebRequest request = WebRequest.Create(address) as HttpWebRequest;
                    request.Timeout = 10000;

                    // Set type to POST  
                    request.Method = "POST";
                    request.ContentType = postedFile.ContentType;
                    request.ContentLength = postedFile.ContentLength;

                    // Write data  
                    using (Stream postStream = request.GetRequestStream())
                    {
                        postedFile.InputStream.CopyTo(postStream);
                        postedFile.InputStream.Flush();
                    }

                    // Get response  
                    using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                    {
                        // Get the response stream  
                        StreamReader reader = new StreamReader(response.GetResponseStream());

                        ViewBag.Result = reader.ReadToEnd();
                    }
                }
                catch (Exception exc)
                {
                    ViewBag.Result = "Exception during update request: " + exc.ToString();
                }
            }

            return View("Import");
        }

        #region CRAWLER

        private static Crawler _crawler;
        private static CrawlerHistory _history;
        private const int SolrCommitAfterUrls = 5;

        [HttpGet]
        public ActionResult GetCrawlStatus(int crawlUrlIndex)
        {
            if (_crawler == null)
            {
                return new ContentResult() { Content = "Complete," + _history.UrlCount + "," + String.Join(",", _history.Get(crawlUrlIndex)) };
            }
            return new ContentResult() { Content = "InProgress," + _history.UrlCount + "," + String.Join(",", _history.Get(crawlUrlIndex)) };
        }

        [HttpGet]
        public ActionResult StopCrawler(int crawlUrlIndex)
        {
            if (_crawler == null)
            {
                return new ContentResult() { Content = "NoCrawlperationInProgress" };
            }

            _crawler.Stop();
            return new ContentResult()
            {
                Content = "Stopped," + _history.UrlCount + "," + String.Join(",", _history.Get(crawlUrlIndex))
            };
        }

        public ActionResult Crawl()
        {
            if (_crawler == null)
            {
                ViewBag.IsCrawlingInProgess = false;
                ViewBag.SiteUrl = "<Enter website to crawl and index>";
            }
            else
            {
                ViewBag.IsCrawlingInProgess = true;
                ViewBag.SiteUrl = _crawler.StartingUri;
            }

            string solrUrl = SolrAdminWebRole.Shared.Util.GetSolrUrl(true);

            if (solrUrl == null)
            {
                ViewBag.Message = "Crawl and index website [Not ready]";
                return View();
            }

            ViewBag.Message = "Crawl and index website [Ready]";
            return View();
        }

        [HttpPost]
        public ActionResult Crawl(FormCollection forms)
        {
            string siteUrl = forms["siteUrl"];

            if (string.IsNullOrEmpty(siteUrl))
                ViewBag.SiteUrl = "<Enter website to crawl and index>";
            else
                ViewBag.SiteUrl = siteUrl;

            string solrUrl = SolrAdminWebRole.Shared.Util.GetSolrUrl(true);

            if (solrUrl == null)
            {
                ViewBag.Message = "Crawl and index website [Not ready]";
                return View();
            }

            ViewBag.Message = "Crawl and index website [Ready]";

            // start crawler
            UriBuilder enteredUrl = new UriBuilder(siteUrl);
            _crawler = new Crawler(enteredUrl.Uri.ToString(), true);
            _history = new CrawlerHistory();
            _crawler.CrawlFinishedEvent += new EventHandler(crawler_CrawlFinishedEvent);
            _crawler.CurrentPageContentEvent += new Crawler.CurrentPageContentEventHandler(crawler_CurrentPageContentEvent);
            _crawler.Start();

            ViewBag.IsCrawlingInProgess = true;
            return View();
        }

        public void crawler_CurrentPageContentEvent(object sender, CurrentPageContentEventArgs e)
        {
            HttpWebRequest webRequest;
            HttpWebResponse webResponse = null;
            String postUrl;

            try
            {
                _history.Add(e.Url);

                postUrl = String.Format("{0}update/extract?literal.id={1}&prefix=attr_&fmap.content=body&commit=true", SolrAdminWebRole.Shared.Util.GetSolrUrl(true), HttpUtility.UrlEncode(e.Url));
                webRequest = (HttpWebRequest)WebRequest.Create(postUrl);
                webRequest.Method = "POST";

                using (Stream requestStream = webRequest.GetRequestStream())
                {
                    byte[] fileContent = Encoding.UTF8.GetBytes(e.Content);
                    requestStream.Write(fileContent, 0, fileContent.Length);
                }
                webResponse = (HttpWebResponse)webRequest.GetResponse();

                if (_history.UrlCount % SolrCommitAfterUrls == 0)
                {
                    CommitToSolrIndex();
                }
            }
            finally
            {
                if (webResponse != null)
                {
                    webResponse.Close();
                }
            }

            //using (Stream responseStream = webResponse.GetResponseStream())
            //using (StreamReader responseReader = new StreamReader(responseStream))
            //{
            //    Console.WriteLine(responseReader.ReadToEnd());
            //}
        }

        public void crawler_CrawlFinishedEvent(object sender, EventArgs e)
        {
            _crawler = null;
            CommitToSolrIndex();
        }

        private void CommitToSolrIndex()
        {
            HttpWebRequest webRequest;
            HttpWebResponse webResponse = null;
            String postUrl;
            try
            {
                postUrl = String.Format("{0}update", SolrAdminWebRole.Shared.Util.GetSolrUrl(true));
                webRequest = (HttpWebRequest)WebRequest.Create(postUrl);
                webRequest.Method = "POST";

                using (Stream requestStream = webRequest.GetRequestStream())
                {
                    byte[] fileContent = Encoding.UTF8.GetBytes("<commit />");
                    requestStream.Write(fileContent, 0, fileContent.Length);
                }
                webResponse = (HttpWebResponse)webRequest.GetResponse();
            }
            finally
            {
                if (webResponse != null)
                {
                    webResponse.Close();
                }
            }
        }

        #endregion

        [HttpPost]
        public ActionResult DeleteAll(FormCollection forms)
        {
            string solrUrl = SolrAdminWebRole.Shared.Util.GetSolrUrl(true);

            if (solrUrl == null)
                return Json(false);

            try
            {
                string result = ExecSolrCommand(solrUrl + "update?commit=true&optimize=true&expungeDeletes=true", true, "<delete><query>*:*</query></delete>");

                // TODO: looks like this has to be done twice to force optimize?
                result = ExecSolrCommand(solrUrl + "update?commit=true&optimize=true&expungeDeletes=true", true, "<delete><query>*:*</query></delete>");
            }
            catch
            {
                return Json(false);
            }

            return Json(true);
        }

        [HttpPost]
        public ActionResult EnableRepl(FormCollection forms)
        {
            string solrUrl = SolrAdminWebRole.Shared.Util.GetSolrUrl(true);

            if (solrUrl == null)
                return Json(false);

            try
            {
                string result = ExecSolrCommand(solrUrl + "replication?command=enablereplication");

                // force replication on all slaves
                int cInstances = Shared.Util.GetNumInstances(false);
                for (int iInstance = 0; iInstance < cInstances; iInstance++)
                {
                    solrUrl = SolrAdminWebRole.Shared.Util.GetSolrUrl(false, iInstance);
                    result = ExecSolrCommand(solrUrl + "replication?command=fetchindex");
                }
            }
            catch
            {
                return Json(false);
            }

            return Json(true);
        }

        [HttpPost]
        public ActionResult DisableRepl(FormCollection forms)
        {
            string solrUrl = SolrAdminWebRole.Shared.Util.GetSolrUrl(true);

            if (solrUrl == null)
                return Json(false);

            try
            {
                string result = ExecSolrCommand(solrUrl + "replication?command=disablereplication");
            }
            catch
            {
                return Json(false);
            }

            return Json(true);
        }

        public ActionResult About()
        {
            return View();
        }

        private List<SearchResult> GetSearchResults(string resultXml)
        {
            List<SearchResult> results = new List<SearchResult>();
            XmlDocument docSearchResult = new XmlDocument();

            docSearchResult.LoadXml(resultXml);

            XmlNodeList resultDocs = docSearchResult.SelectNodes("/response/result/doc");

            foreach (XmlNode resultDoc in resultDocs)
            {
                XmlNode node = resultDoc.SelectSingleNode("*[@name='titleText']");

                if (node == null)
                    continue;

                SearchResult result = new SearchResult();
                result.Title = node.InnerText;

                node = resultDoc.SelectSingleNode("*[@name='id']");
                if (node == null)
                    continue;

                Uri uri;
                int id;

                // crawler data uses url as the id. Wikipedia uses a numeric id, and we have to consturct a url from the revision id and the title text.
                if (Uri.TryCreate(node.InnerText, UriKind.Absolute, out uri))
                    result.Url = uri.ToString();
                else if (int.TryParse(node.InnerText, out id))
                {
                    node = resultDoc.SelectSingleNode("*[@name='revision']");
                    if (node == null)
                        continue;

                    string revisionId = node.InnerText;
                    string titleText = result.Title.Replace(' ', '_');
                    result.Url = string.Format("http://en.wikipedia.org/w/index.php?title={0}&oldid={1}", titleText, revisionId);
                }

                results.Add(result);
            }

            return results;
        }

        private string ExecSolrCommand(string solrCommandUrl, bool bPost = false, string solrCommand = null)
        {
            HttpWebRequest request = WebRequest.Create(solrCommandUrl) as HttpWebRequest;
            request.Timeout = 10000;

            if (bPost)
            {
                // Set type to POST  
                request.Method = "POST";
                request.ContentType = "text/xml";

                byte[] bytes = Encoding.ASCII.GetBytes(solrCommand);
                request.ContentLength = bytes.Length;

                using (MemoryStream ms = new MemoryStream(bytes))
                using (Stream postStream = request.GetRequestStream())
                {
                    ms.CopyTo(postStream);
                    ms.Flush();
                }
            }
            else
                request.Method = "GET";

            // Get response  
            using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
            {
                // Get the response stream  
                StreamReader reader = new StreamReader(response.GetResponseStream());

                return reader.ReadToEnd();
            }
        }

    }
}
