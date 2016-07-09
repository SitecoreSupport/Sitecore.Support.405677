namespace Sitecore.Support.ContentSearch.SolrProvider.Pipelines.Initialize
{
    using Microsoft.Practices.Unity;
    using Sitecore.Pipelines;
    using Sitecore.Support.ContentSearch.SolrProvider.UnityIntegration;

    public class SolrUnityInitializer
    {
        static SolrUnityInitializer()
        {
            Container = new UnityContainer();
        }

        public void Process(PipelineArgs args)
        {
            new UnitySolrStartUp(Container).Initialize();
        }

        protected static IUnityContainer Container { get; set; }
    }
}
