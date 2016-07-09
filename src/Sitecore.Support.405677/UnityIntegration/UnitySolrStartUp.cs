namespace Sitecore.Support.ContentSearch.SolrProvider.UnityIntegration
{
    using Microsoft.Practices.ServiceLocation;
    using Microsoft.Practices.Unity;
    using Sitecore.ContentSearch.SolrProvider;
    using Sitecore.ContentSearch.SolrProvider.DocumentSerializers;
    using Sitecore.Diagnostics;
    using SolrNet;
    using SolrNet.Impl;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using Configuration;
    using Sitecore.ContentSearch.Diagnostics;
    using Sitecore.ContentSearch.Utilities;
    using Unity.SolrNetIntegration;
    using Unity.SolrNetIntegration.Config;

    public class UnitySolrStartUp : ISolrStartUp
    {
        // Fields
        internal IUnityContainer Container;
        internal readonly SolrServers Cores;

        // Methods
        public UnitySolrStartUp(IUnityContainer container)
        {
            Assert.ArgumentNotNull(container, "container");
            if (SolrContentSearchManager.IsEnabled)
            {
                this.Container = container;
                this.Cores = new SolrServers();
            }
        }

        public void AddCore(string coreId, Type documentType, string coreUrl)
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
            SolrConnection connection = new SolrConnection(SolrContentSearchManager.ServiceAddress)
            {
                Timeout = Settings.ConnectionTimeout
            };
            if (SolrContentSearchManager.EnableHttpCache)
            {
                connection.Cache = this.Container.Resolve<ISolrCache>(new ResolverOverride[0]) ?? new NullCache();
            }
            return new SolrCoreAdmin(connection, this.Container.Resolve<ISolrHeaderResponseParser>(new ResolverOverride[0]), this.Container.Resolve<ISolrStatusResponseParser>(new ResolverOverride[0]));
        }

        public void Initialize()
        {
            if (!SolrContentSearchManager.IsEnabled)
            {
                throw new InvalidOperationException("Solr configuration is not enabled. Please check your settings and include files.");
            }
            RegisterSolrServerUrls();
            this.Container = new SolrNetContainerConfiguration().ConfigureContainer(this.Cores, this.Container);
            //workaround to set the timeout
            SetSolrConnectionTimeout();
            //end of workaround
            this.Container.RegisterType(typeof(ISolrDocumentSerializer<Dictionary<string, object>>), typeof(SolrFieldBoostingDictionarySerializer), new InjectionMember[0]);
            if (SolrContentSearchManager.EnableHttpCache)
            {
                ConfigureHttpCache();
            }
            ServiceLocator.SetLocatorProvider(() => new UnityServiceLocator(this.Container));
            //workaround to set the timeout
            SolrContentSearchManager.SolrAdmin = this.BuildCoreAdmin();
            //end of workaround
            SolrContentSearchManager.Initialize();
        }

        protected virtual void ConfigureHttpCache()
        {
            this.Container.RegisterType(typeof(ISolrCache), typeof(HttpRuntimeCache), new InjectionMember[0]);
            List<ContainerRegistration> list = (from r in this.Container.Registrations
                                                where r.RegisteredType == typeof(ISolrConnection)
                                                select r).ToList<ContainerRegistration>();
            if (list.Count > 0)
            {
                foreach (var connectionRegistration in list)
                {
                    Func<SolrServerElement, bool> predicate = null;
                    if (predicate == null)
                    {
                        predicate =
                            core => connectionRegistration.Name == (core.Id + connectionRegistration.MappedToType.FullName);
                    }
                    SolrServerElement element = this.Cores.FirstOrDefault<SolrServerElement>(predicate);
                    if (element == null)
                    {
                        Log.Error(
                            "The Solr Core configuration for the '" + connectionRegistration.Name +
                            "' Unity registration could not be found. The HTTP cache for the Solr connection to the Core cannot be configured.",
                            this);
                    }
                    else
                    {
                        InjectionMember[] injectionMembers = new InjectionMember[]
                        {
                                new InjectionConstructor(new object[] {element.Url}),
                                new InjectionProperty("Cache", new ResolvedParameter<ISolrCache>())
                        };
                        this.Container.RegisterType(typeof(ISolrConnection), typeof(SolrConnection),
                            connectionRegistration.Name, null, injectionMembers);
                    }
                }
            }
        }

        /// <summary>
        /// Sets timetou for SolrConnection implementation.
        /// </summary>
        protected void SetSolrConnectionTimeout()
        {
            foreach (SolrServerElement solrServer in this.Cores)
            {
                var coreConnectionId = solrServer.Id + (object)typeof(SolrConnection);
                this.Container.RegisterType<ISolrConnection, SolrConnection>(coreConnectionId, new InjectionMember[2]
                {
                    (InjectionMember) new InjectionConstructor(new object[1]
                    {
                        (object) solrServer.Url
                    }),
                    (InjectionMember) new InjectionProperty("Timeout", Settings.ConnectionTimeout)
                });
            }
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

        public bool IsSetupValid()
        {
            if (!SolrContentSearchManager.IsEnabled)
            {
                return false;
            }
            ISolrCoreAdmin admin = this.BuildCoreAdmin();
            return (from defaultIndex in SolrContentSearchManager.Cores select admin.Status(defaultIndex).First<CoreResult>()).All<CoreResult>(status => (status.Name != null));
        }

        /// <summary>
        /// Returns collection names by quering index configuration.
        /// </summary>
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