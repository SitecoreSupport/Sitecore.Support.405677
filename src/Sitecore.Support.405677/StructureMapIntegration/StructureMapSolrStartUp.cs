namespace Sitecore.Support.ContentSearch.SolrProvider.StructureMapIntegration
{
    using System.Xml;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Practices.ServiceLocation;
    using Sitecore.ContentSearch.SolrProvider;
    using Sitecore.ContentSearch.SolrProvider.DocumentSerializers;
    using Sitecore.ContentSearch.SolrProvider.StructureMapIntegration;
    using Sitecore.Diagnostics;
    using SolrNet;
    using SolrNet.Impl;
    using StructureMap;
    using StructureMap.SolrNetIntegration;
    using StructureMap.SolrNetIntegration.Config;
    using Sitecore.ContentSearch.Diagnostics;
    using Sitecore.ContentSearch.Utilities;
    using Sitecore.Support.ContentSearch.SolrProvider.Configuration;

    public class StructureMapSolrStartUp : ISolrStartUp
    {
        #region Fields
        internal readonly SolrServers Cores;
        #endregion Fields

        public StructureMapSolrStartUp()
        {
            if (SolrContentSearchManager.IsEnabled)
            {
                this.Cores = new SolrServers();
            }
        }

        #region ISolrStartUp interface

        public void AddCore(string coreId, System.Type documentType, string coreUrl)
        {
            Assert.ArgumentNotNull(coreId, "coreId");
            Assert.ArgumentNotNull(documentType, "documentType");
            Assert.ArgumentNotNull(coreUrl, "coreUrl");
            SolrServerElement configurationElement = new SolrServerElement
            {
                Id = coreId,
                DocumentType = documentType.AssemblyQualifiedName,
                Url = coreUrl
            };
            this.Cores.Add(configurationElement);            
        }

        private ISolrCoreAdmin BuildCoreAdmin()
        {
            SolrConnection connection = new SolrConnection(SolrContentSearchManager.ServiceAddress);
            connection.Timeout = Settings.ConnectionTimeout;
            if (SolrContentSearchManager.EnableHttpCache)
            {
                connection.Cache = ObjectFactory.GetInstance<ISolrCache>() ?? new NullCache();
            }
            return new SolrCoreAdmin(connection, ObjectFactory.GetInstance<ISolrHeaderResponseParser>(), ObjectFactory.GetInstance<ISolrStatusResponseParser>());
        }

        public bool IsSetupValid()
        {
            if (!SolrContentSearchManager.IsEnabled)
            {
                return false;
            }
            ISolrCoreAdmin admin = this.BuildCoreAdmin();
            return (SolrContentSearchManager.Cores.Select(defaultIndex => admin.Status(defaultIndex).First<CoreResult>())).All<CoreResult>(status => (status.Name != null));
        }

        public void Initialize()
        {
            if (!SolrContentSearchManager.IsEnabled)
            {
                throw new InvalidOperationException("Solr configuration is not enabled. Please check your settings and include files.");
            }
            RegisterSolrServerUrls();

            // workaround for 405677
            SetSolrConnectionTimeout();
            // end of workaround 405677
            ObjectFactory.Configure(c => c.For(typeof(ISolrDocumentSerializer<Dictionary<string, object>>)).Use(typeof(SolrFieldBoostingDictionarySerializer)));
            if (SolrContentSearchManager.EnableHttpCache)
            {
                ConfigureHttpCache();
            }
            ServiceLocator.SetLocatorProvider(() => new StructureMapServiceLocator(ObjectFactory.Container));
            SolrContentSearchManager.SolrAdmin = this.BuildCoreAdmin();
            SolrContentSearchManager.Initialize();
        }

        #endregion ISolrStartUp interface

        /// <summary>
        /// Returns collection names by quering index configuration.
        /// </summary>
        protected virtual void ConfigureHttpCache()
        {
            ObjectFactory.Configure(cfg => cfg.For<ISolrCache>().Use<HttpRuntimeCache>());
            SolrConnection instance = ObjectFactory.GetInstance<ISolrConnection>() as SolrConnection;
            if (instance != null)
            {
                instance.Cache = ObjectFactory.GetInstance<ISolrCache>() ?? new NullCache();
            }
        }

        /// <summary>
        /// Sets timetou for SolrConnection implementation.
        /// </summary>
        protected void SetSolrConnectionTimeout()
        {
            var solrRegistry = new SolrNetRegistry(this.Cores);

            solrRegistry.For<ISolrConnection>().OnCreationForAll(c =>
            {
                if (c is SolrConnection)
                {
                    var conn = c as SolrConnection;
                    conn.Timeout = Settings.ConnectionTimeout;
                }
            });

            ObjectFactory.Initialize(c => c.IncludeRegistry(solrRegistry));
        }

        /// <summary>
        /// Registers Solr server cores/aliases and collections.
        /// </summary>
        protected void RegisterSolrServerUrls()
        {
            foreach (string core in SolrContentSearchManager.Cores)
            {
                CrawlingLog.Log.Debug($"Registring Solr core/alias: '{core}'");
                this.AddCore(core, typeof(Dictionary<string, object>), SolrContentSearchManager.ServiceAddress + "/" + core);
            }

            foreach (var collection in Collections)
            {
                if (!SolrContentSearchManager.Cores.Contains(collection))
                {
                    CrawlingLog.Log.Debug($"Registring Solr collection: '{collection}'");
                    this.AddCore(collection, typeof(Dictionary<string, object>), SolrContentSearchManager.ServiceAddress + "/" + collection);
                }
            }
        }

        protected static IEnumerable<string> Collections
        {
            get
            {
                var indexCtorColParams =
                    Sitecore.Configuration.Factory.GetConfigNodes(Settings.IndexConstructorParametersQuery);
                var valueList = new List<string>();
                if (indexCtorColParams.Count > 0)
                {
                    var enumerator = indexCtorColParams.GetEnumerator();
                    while (enumerator.MoveNext())
                    {
                        var node = (XmlNode)enumerator.Current;
                        var value = node.InnerText;
                        if (!string.IsNullOrEmpty(value) && !valueList.Contains(value))
                        {
                            valueList.Add(value);
                        }
                    }
                }

                var collections = valueList.ToHashSet();
                return collections;
            }
        }
    }
}
