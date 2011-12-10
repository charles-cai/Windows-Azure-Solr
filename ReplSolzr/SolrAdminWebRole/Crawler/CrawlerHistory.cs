using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SolrAdminWebRole
{
    public class CrawlerHistory
    {
        private List<String> _crawledUrls;

        public CrawlerHistory()
        {
            _crawledUrls = new List<string>();
        }

        public void Add(String url)
        {
            _crawledUrls.Add(url);
        }

        public int UrlCount
        {
            get 
            {
                return _crawledUrls.Count;
            }
        }

        public List<String> Get(int rootIndex)
        {
            if (_crawledUrls.Count < rootIndex) 
            {
                return null;
            }
            return _crawledUrls.GetRange(rootIndex, _crawledUrls.Count - rootIndex);
        }
    }
}