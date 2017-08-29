namespace Sitecore.Support.ContentSearch.SolrProvider.Pipelines.Initialize
{
    using Autofac;
    using Sitecore.Pipelines;
    using AutoFacIntegration;
    public class AutofacInitializer
    {
        public void Process(PipelineArgs args)
        {
            ContainerBuilder builder = new ContainerBuilder();
            AutoFacSolrStartUp autoFacSolrStartUp = new AutoFacSolrStartUp(builder);
            autoFacSolrStartUp.Initialize();
        }
    }
}