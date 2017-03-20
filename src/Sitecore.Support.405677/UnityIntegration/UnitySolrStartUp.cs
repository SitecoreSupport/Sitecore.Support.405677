namespace Sitecore.Support.ContentSearch.SolrProvider.UnityIntegration
{

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
    using Sitecore.ContentSearch;
    using Microsoft.Practices.ServiceLocation;

    public class UnitySolrStartUp : ISolrStartUp, IProviderStartUp
    {
        internal readonly SolrServers Cores;
        internal IUnityContainer Container;

        public UnitySolrStartUp(IUnityContainer container)
        {
            Assert.ArgumentNotNull((object)container, "container");
            if (!SolrContentSearchManager.IsEnabled)
                return;
            this.Container = container;
            this.Cores = new SolrServers();
        }

        public void AddCore(string coreId, Type documentType, string coreUrl)
        {
            Assert.ArgumentNotNull((object)coreId, "coreId");
            Assert.ArgumentNotNull((object)documentType, "documentType");
            Assert.ArgumentNotNull((object)coreUrl, "coreUrl");
            this.Cores.Add(new SolrServerElement()
            {
                Id = coreId,
                DocumentType = documentType.AssemblyQualifiedName,
                Url = coreUrl
            });
        }

        public void Initialize()
        {
            if (!SolrContentSearchManager.IsEnabled)
                throw new InvalidOperationException("Solr configuration is not enabled. Please check your settings and include files.");
            foreach (string core in SolrContentSearchManager.Cores)
                this.AddCore(core, typeof(Dictionary<string, object>), SolrContentSearchManager.ServiceAddress + "/" + core);
            this.Container = new SolrNetContainerConfiguration().ConfigureContainer(this.Cores, this.Container);
            this.SetSolrConnectionTimeout();
            this.Container.RegisterType(typeof(ISolrDocumentSerializer<Dictionary<string, object>>), typeof(SolrFieldBoostingDictionarySerializer), new InjectionMember[0]);
            if (SolrContentSearchManager.EnableHttpCache)
            {
                this.Container.RegisterType(typeof(ISolrCache), typeof(HttpRuntimeCache), new InjectionMember[0]);
                List<ContainerRegistration> list = this.Container.Registrations.Where<ContainerRegistration>((Func<ContainerRegistration, bool>)(r => r.RegisteredType == typeof(ISolrConnection))).ToList<ContainerRegistration>();
                if (list.Count > 0)
                {
                    foreach (ContainerRegistration containerRegistration in list)
                    {
                        ContainerRegistration registration = containerRegistration;
                        SolrServerElement solrServerElement = this.Cores.FirstOrDefault<SolrServerElement>((Func<SolrServerElement, bool>)(core => registration.Name == core.Id + registration.MappedToType.FullName));
                        if (solrServerElement == null)
                        {
                            Log.Error("The Solr Core configuration for the '" + registration.Name + "' Unity registration could not be found. The HTTP cache for the Solr connection to the Core cannot be configured.", (object)this);
                        }
                        else
                        {
                            InjectionMember[] injectionMemberArray = new InjectionMember[2]
                            {
                (InjectionMember) new InjectionConstructor(new object[1]
                {
                  (object) solrServerElement.Url
                }),
                (InjectionMember) new InjectionProperty("Cache", (object) new ResolvedParameter<ISolrCache>())
                            };
                            this.Container.RegisterType(typeof(ISolrConnection), typeof(SolrConnection), registration.Name, (LifetimeManager)null, injectionMemberArray);
                        }
                    }
                }
            }
            ServiceLocator.SetLocatorProvider((ServiceLocatorProvider)(() => (IServiceLocator)new UnityServiceLocator(this.Container)));
            SolrContentSearchManager.SolrAdmin = this.BuildCoreAdmin();
            SolrContentSearchManager.Initialize();
        }

        public bool IsSetupValid()
        {
            if (!SolrContentSearchManager.IsEnabled)
                return false;
            ISolrCoreAdmin admin = this.BuildCoreAdmin();
            return SolrContentSearchManager.Cores.Select<string, CoreResult>((Func<string, CoreResult>)(defaultIndex => admin.Status(defaultIndex).First<CoreResult>())).All<CoreResult>((Func<CoreResult, bool>)(status => status.Name != null));
        }

        private ISolrCoreAdmin BuildCoreAdmin()
        {
            SolrConnection solrConnection = new SolrConnection(SolrContentSearchManager.ServiceAddress);
            if (SolrContentSearchManager.EnableHttpCache)
                solrConnection.Cache = this.Container.Resolve<ISolrCache>() ?? (ISolrCache)new NullCache();
            return (ISolrCoreAdmin)new SolrCoreAdmin((ISolrConnection)solrConnection, this.Container.Resolve<ISolrHeaderResponseParser>(), this.Container.Resolve<ISolrStatusResponseParser>());
        }
        protected void SetSolrConnectionTimeout()
        {
            foreach (SolrServerElement element in this.Cores)
            {
                string name = element.Id + typeof(SolrConnection);
                InjectionMember[] injectionMembers = new InjectionMember[2];
                object[] parameterValues = new object[] { element.Url };
                injectionMembers[0] = new InjectionConstructor(parameterValues);
                injectionMembers[1] = new InjectionProperty("Timeout", Settings.ConnectionTimeout);
                this.Container.RegisterType<ISolrConnection, SolrConnection>(name, injectionMembers);
            }
        }
    }

}