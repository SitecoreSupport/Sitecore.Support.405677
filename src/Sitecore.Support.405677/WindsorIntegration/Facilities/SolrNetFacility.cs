namespace Sitecore.Support.ContentSearch.SolrProvider.WindsorIntegration.Facilities
{
    using Castle.Core.Configuration;
    using Castle.MicroKernel;
    using Castle.MicroKernel.Facilities;
    using Castle.MicroKernel.Registration;
    using Sitecore.Support.ContentSearch.SolrProvider.Configuration;
    using SolrNet;
    using SolrNet.Exceptions;
    using SolrNet.Impl;
    using SolrNet.Impl.DocumentPropertyVisitors;
    using SolrNet.Impl.FacetQuerySerializers;
    using SolrNet.Impl.FieldParsers;
    using SolrNet.Impl.FieldSerializers;
    using SolrNet.Impl.QuerySerializers;
    using SolrNet.Impl.ResponseParsers;
    using SolrNet.Mapping;
    using SolrNet.Mapping.Validation;
    using SolrNet.Mapping.Validation.Rules;
    using SolrNet.Schema;
    using SolrNet.Utils;
    using System;
    using System.Collections.Generic;

    public class SolrNetFacility : AbstractFacility
    {
        private readonly List<SolrCore> cores;
        private readonly string solrURL;

        public SolrNetFacility()
        {
            this.cores = new List<SolrCore>();
        }

        public SolrNetFacility(string solrURL)
        {
            this.cores = new List<SolrCore>();
            ValidateUrl(solrURL);
            this.solrURL = solrURL;
        }

        public void AddCore(Type documentType, string coreUrl)
        {
            this.AddCore(Guid.NewGuid().ToString(), documentType, coreUrl);
        }

        public void AddCore(string coreId, Type documentType, string coreUrl)
        {
            ValidateUrl(coreUrl);
            this.cores.Add(new SolrCore(coreId, documentType, coreUrl));
        }

        private void AddCoresFromConfig()
        {
            if (base.FacilityConfig != null)
            {
                IConfiguration coresConfig = base.FacilityConfig.Children["cores"];
                if (coresConfig != null)
                {
                    foreach (IConfiguration coreConfig in coresConfig.Children)
                    {
                        string coreId = coreConfig.Attributes["id"] ?? Guid.NewGuid().ToString();
                        Type coreDocumentType = this.GetCoreDocumentType(coreConfig);
                        string coreUrl = this.GetCoreUrl(coreConfig);
                        this.AddCore(coreId, coreDocumentType, coreUrl);
                    }
                }
            }
        }

        public ISolrCoreAdmin BuildCoreAdmin(ISolrConnection conn)
        {
            return new SolrCoreAdmin(conn, base.Kernel.Resolve<ISolrHeaderResponseParser>(),
                base.Kernel.Resolve<ISolrStatusResponseParser>());
        }

        public ISolrCoreAdmin BuildCoreAdmin(string url)
        {
            //SolrConnection conn = new SolrConnection(url);
            var conn = base.Kernel.Resolve<ISolrConnection>(new[] {"serverUrl", url});
            return this.BuildCoreAdmin(conn);
        }

        private Type GetCoreDocumentType(IConfiguration coreConfig)
        {
            Type type;
            IConfiguration coreSettings = coreConfig.Children["documentType"];
            if (coreSettings == null)
            {
                throw new FacilityException("Document type missing in SolrNet core configuration");
            }
            try
            {
                type = Type.GetType(coreSettings.Value);
            }
            catch (Exception exception)
            {
                throw new FacilityException(string.Format("Error getting document type '{0}'", coreSettings.Value),
                    exception);
            }
            return type;
        }

        private string GetCoreUrl(IConfiguration coreConfig)
        {
            IConfiguration coreSettings = coreConfig.Children["url"];
            if (coreSettings == null)
            {
                throw new FacilityException("Core url missing in SolrNet core configuration");
            }
            return coreSettings.Value;
        }

        private string GetSolrUrl()
        {
            if (this.solrURL != null)
            {
                return this.solrURL;
            }
            if (base.FacilityConfig == null)
            {
                throw new FacilityException("Please add solrURL to the SolrNetFacility configuration");
            }
            IConfiguration configuration = base.FacilityConfig.Children["solrURL"];
            if (configuration == null)
            {
                throw new FacilityException("Please add solrURL to the SolrNetFacility configuration");
            }
            string s = configuration.Value;
            ValidateUrl(s);
            return s;
        }

        protected override void Init()
        {
            IReadOnlyMappingManager instance = this.Mapper ??
                                               new MemoizingMappingManager(new AttributesMappingManager());
            base.Kernel.Register(new IRegistration[] {Component.For<IReadOnlyMappingManager>().Instance(instance)});
            base.Kernel.Register(new IRegistration[]
            {
                Component.For<ISolrConnection>().ImplementedBy<SolrConnection>()
                    .DependsOn(new {serverURL = this.GetSolrUrl()})
                    .DependsOn(new Arguments(new {Timeout = Settings.ConnectionTimeout}))
            });
            //.Parameters(new Parameter[] { Parameter.ForKey("serverURL").Eq(this.GetSolrUrl()) }) });
            base.Kernel.Register(new IRegistration[]
            {Component.For(typeof (ISolrDocumentActivator<>)).ImplementedBy(typeof (SolrDocumentActivator<>))});
            base.Kernel.Register(new IRegistration[]
            {Component.For(typeof (ISolrDocumentResponseParser<>)).ImplementedBy(typeof (SolrDocumentResponseParser<>))});
            base.Kernel.Register(new IRegistration[]
            {
                Component.For<ISolrDocumentResponseParser<Dictionary<string, object>>>()
                    .ImplementedBy<SolrDictionaryDocumentResponseParser>()
            });
            base.Kernel.Register(new IRegistration[]
            {Component.For(typeof (ISolrAbstractResponseParser<>)).ImplementedBy(typeof (DefaultResponseParser<>))});
            base.Kernel.Register(new IRegistration[]
            {Component.For<ISolrHeaderResponseParser>().ImplementedBy<HeaderResponseParser<string>>()});
            base.Kernel.Register(new IRegistration[]
            {Component.For<ISolrExtractResponseParser>().ImplementedBy<ExtractResponseParser>()});
            foreach (Type type in new Type[]
            {
                typeof (MappedPropertiesIsInSolrSchemaRule), typeof (RequiredFieldsAreMappedRule),
                typeof (UniqueKeyMatchesMappingRule), typeof (MultivaluedMappedToCollectionRule)
            })
            {
                base.Kernel.Register(new IRegistration[] {Component.For<IValidationRule>().ImplementedBy(type)});
            }
            base.Kernel.Resolver.AddSubResolver(new StrictArrayResolver(base.Kernel));
            base.Kernel.Register(new IRegistration[]
            {
                Component.For(typeof (ISolrMoreLikeThisHandlerQueryResultsParser<>))
                    .ImplementedBy(typeof (SolrMoreLikeThisHandlerQueryResultsParser<>))
            });
            base.Kernel.Register(new IRegistration[]
            {Component.For(typeof (ISolrQueryExecuter<>)).ImplementedBy(typeof (SolrQueryExecuter<>))});
            base.Kernel.Register(new IRegistration[]
            {Component.For(typeof (ISolrDocumentSerializer<>)).ImplementedBy(typeof (SolrDocumentSerializer<>))});
            base.Kernel.Register(new IRegistration[]
            {
                Component.For<ISolrDocumentSerializer<Dictionary<string, object>>>()
                    .ImplementedBy<SolrDictionarySerializer>()
            });
            base.Kernel.Register(new IRegistration[]
            {
                Component.For(new Type[] {typeof (ISolrBasicOperations<>), typeof (ISolrBasicReadOnlyOperations<>)})
                    .ImplementedBy(typeof (SolrBasicServer<>))
            });
            base.Kernel.Register(new IRegistration[]
            {
                Component.For(new Type[] {typeof (ISolrOperations<>), typeof (ISolrReadOnlyOperations<>)})
                    .ImplementedBy(typeof (SolrServer<>))
            });
            base.Kernel.Register(new IRegistration[]
            {Component.For<ISolrFieldParser>().ImplementedBy<DefaultFieldParser>()});
            base.Kernel.Register(new IRegistration[]
            {Component.For<ISolrFieldSerializer>().ImplementedBy<DefaultFieldSerializer>()});
            base.Kernel.Register(new IRegistration[]
            {Component.For<ISolrQuerySerializer>().ImplementedBy<DefaultQuerySerializer>()});
            base.Kernel.Register(new IRegistration[]
            {Component.For<ISolrFacetQuerySerializer>().ImplementedBy<DefaultFacetQuerySerializer>()});
            base.Kernel.Register(new IRegistration[]
            {Component.For<ISolrDocumentPropertyVisitor>().ImplementedBy<DefaultDocumentVisitor>()});
            base.Kernel.Register(new IRegistration[]
            {Component.For<ISolrSchemaParser>().ImplementedBy<SolrSchemaParser>()});
            base.Kernel.Register(new IRegistration[]
            {Component.For<ISolrDIHStatusParser>().ImplementedBy<SolrDIHStatusParser>()});
            base.Kernel.Register(new IRegistration[]
            {Component.For<IMappingValidator>().ImplementedBy<MappingValidator>()});
            base.Kernel.Register(new IRegistration[]
            {Component.For<ISolrStatusResponseParser>().ImplementedBy<SolrStatusResponseParser>()});
            base.Kernel.Register(new IRegistration[] {Component.For<ISolrCoreAdmin>().ImplementedBy<SolrCoreAdmin>()});
            this.AddCoresFromConfig();
            foreach (SolrCore core in this.cores)
            {
                this.RegisterCore(core);
            }
        }

        protected virtual void RegisterCore(SolrCore core)
        {
            string connKey = core.Id + typeof(SolrConnection);
            base.Kernel.Register(new IRegistration[]
            {
                Component.For<ISolrConnection>()
                    .ImplementedBy<SolrConnection>().Named(connKey)
                    .DependsOn(new { serverURL = core.Url })
                    .DependsOn(new Arguments(new {Timeout = Settings.ConnectionTimeout}))
            });
            Type serviceSolrQueryExec = typeof (ISolrQueryExecuter<>).MakeGenericType(new Type[] {core.DocumentType});
            Type solrQueryExec = typeof (SolrQueryExecuter<>).MakeGenericType(new Type[] {core.DocumentType});
            var queryExecKey = core.Id + solrQueryExec;
            base.Kernel.Register(new IRegistration[]
            {
                Component.For(serviceSolrQueryExec)
                    .ImplementedBy(solrQueryExec).Named(queryExecKey)
                    .DependsOn(Dependency.OnComponent("connection", connKey))
            });
            Type solrBasicOps = typeof (ISolrBasicOperations<>).MakeGenericType(new Type[] {core.DocumentType});
            Type solrBasicReadOnlyOps =
                typeof (ISolrBasicReadOnlyOperations<>).MakeGenericType(new Type[] {core.DocumentType});
            Type solrBasicServer = typeof (SolrBasicServer<>).MakeGenericType(new Type[] {core.DocumentType});
            var basicServerKey = core.Id + solrBasicServer;
            base.Kernel.Register(new IRegistration[]
            {
                Component.For(new Type[] {solrBasicOps, solrBasicReadOnlyOps})
                    .ImplementedBy(solrBasicServer).Named(basicServerKey)
                    .DependsOn(
                        Dependency.OnComponent("connection", connKey),
                        Dependency.OnComponent("queryExecuter", queryExecKey)
                    )
            });
            Type serviceSolrOps = typeof (ISolrOperations<>).MakeGenericType(new Type[] {core.DocumentType});
            Type solrServer = typeof (SolrServer<>).MakeGenericType(new Type[] {core.DocumentType});
            base.Kernel.Register(new IRegistration[]
            {
                Component.For(serviceSolrOps)
                    .ImplementedBy(solrServer).Named(core.Id)
                    .DependsOn(Dependency.OnComponent("basicServer", basicServerKey))
            });
        }

        protected static void ValidateUrl(string s)
        {
            try
            {
                UriValidator.ValidateHTTP(s);
            }
            catch (InvalidURLException exception)
            {
                throw new FacilityException("", exception);
            }
        }

        public IReadOnlyMappingManager Mapper { get; set; }
    }
}