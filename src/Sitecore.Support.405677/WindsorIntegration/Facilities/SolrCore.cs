namespace Sitecore.Support.ContentSearch.SolrProvider.WindsorIntegration.Facilities
{
    using System;

    public class SolrCore
    {
        // Methods
        public SolrCore(Type documentType, string url) : this(Guid.NewGuid().ToString(), documentType, url)
        {
            
        }

        public SolrCore(string id, Type documentType, string url)
        {
            Id = id;
            DocumentType = documentType;
            Url = url;
        }

        // Properties
        public Type DocumentType { get; private set; }
        public string Id { get; private set; }
        public string Url { get; private set; }
    }


}
