namespace Sitecore.Support.ContentSearch.SolrProvider.Pipelines.Initialize
{
    using Castle.Windsor;
    
    using Sitecore.Pipelines;
    using Sitecore.Support.ContentSearch.SolrProvider.WindsorIntegration;

    public class SolrWindsorInitializer
    {
        static SolrWindsorInitializer()
        {
            Container = new WindsorContainer();
        }

        public void Process(PipelineArgs args)
        {
            new WindsorSolrStartUp(Container).Initialize();
        }

        protected static IWindsorContainer Container { get; set; }
    }
}
