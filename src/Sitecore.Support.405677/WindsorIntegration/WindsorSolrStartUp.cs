namespace Sitecore.Support.ContentSearch.SolrProvider.WindsorIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using Castle.MicroKernel.Registration;
    using Castle.Windsor;
    using Microsoft.Practices.ServiceLocation;
    using Sitecore.ContentSearch.SolrProvider;
    using Sitecore.ContentSearch.SolrProvider.DocumentSerializers;
    using Sitecore.ContentSearch.Utilities;
    using Sitecore.Diagnostics;
    using Sitecore.Support.ContentSearch.SolrProvider.Configuration;
    using Sitecore.Support.ContentSearch.SolrProvider.WindsorIntegration.Facilities;
    using SolrNet;
    using SolrNet.Impl;
    using Sitecore.ContentSearch.Diagnostics;

    public class WindsorSolrStartUp : ISolrStartUp
    {
        #region Fields
        internal readonly IWindsorContainer Container;
        internal readonly SolrNetFacility SolrFacility;
        #endregion Fields

        public WindsorSolrStartUp(IWindsorContainer container)
        {
            Assert.ArgumentNotNull(container, "container");
            if (SolrContentSearchManager.IsEnabled)
            {
                this.Container = container;
                this.SolrFacility = new SolrNetFacility(SolrContentSearchManager.ServiceAddress);
            }
        }

        #region ISolrStartUp interface

        public void AddCore(string coreId, Type documentType, string coreUrl)
        {
            Assert.ArgumentNotNull(coreId, "coreId");
            Assert.ArgumentNotNull(documentType, "documentType");
            Assert.ArgumentNotNull(coreUrl, "coreUrl");
            this.SolrFacility.AddCore(coreId, documentType, coreUrl);            
        }

        private ISolrCoreAdmin BuildCoreAdmin()
        {
            SolrConnection connection = new SolrConnection(SolrContentSearchManager.ServiceAddress);
            connection.Timeout = Settings.ConnectionTimeout;
            if (SolrContentSearchManager.EnableHttpCache)
            {
                connection.Cache = this.Container.Resolve<ISolrCache>() ?? new NullCache();
            }
            return this.SolrFacility.BuildCoreAdmin(connection);
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
            // Register collections and their aliases
            RegisterSolrServerUrls();

            this.Container.AddFacility(this.SolrFacility);
            this.Container.Register(new IRegistration[]
            {
                Component.For<ISolrDocumentSerializer<Dictionary<string, object>>>()
                    .ImplementedBy<SolrFieldBoostingDictionarySerializer>()
                    .OverridesExistingRegistration<ISolrDocumentSerializer<Dictionary<string, object>>>()
            });
            if (SolrContentSearchManager.EnableHttpCache)
            {
                ConfigureHttpCache();
            }
            ServiceLocator.SetLocatorProvider(() => new WindsorServiceLocator(this.Container));
            SolrContentSearchManager.SolrAdmin = this.BuildCoreAdmin();
            SolrContentSearchManager.Initialize();
        }

        #endregion ISolrStartUp interface

        protected virtual void ConfigureHttpCache()
        {
            this.Container.Register(new[] { Component.For<ISolrCache>().ImplementedBy<HttpRuntimeCache>() });
            var connection = this.Container.Resolve<ISolrConnection>() as SolrConnection;
            if (connection != null)
            {
                connection.Cache = this.Container.Resolve<ISolrCache>() ?? new NullCache();
            }
        }

        protected void RegisterSolrServerUrls()
        {
            foreach (string alias        in SolrContentSearchManager.Cores)
            {
                CrawlingLog.Log.Debug($"Registring Solr alias: '{alias}'");
                this.AddCore(alias, typeof(Dictionary<string, object>), SolrContentSearchManager.ServiceAddress + "/" + alias);
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
                    if (null != enumerator)
                    {
                        while (enumerator.MoveNext())
                        {
                            var node = (XmlNode) enumerator.Current;
                            var value = node.InnerText;
                            if (!string.IsNullOrEmpty(value) && !valueList.Contains(value))
                            {
                                valueList.Add(value);
                            }
                        }
                    }
                }
                
                var collections = valueList.ToHashSet();
                return collections;
                }
        }
    }
}
