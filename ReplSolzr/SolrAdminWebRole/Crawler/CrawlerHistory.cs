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