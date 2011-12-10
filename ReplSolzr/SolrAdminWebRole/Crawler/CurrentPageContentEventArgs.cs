using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Samples.NetCF;

namespace SolrAdminWebRole
{
    public class CurrentPageContentEventArgs
    {
        public string Url { get; set; }
        public string Content { get; set; }
    }
}