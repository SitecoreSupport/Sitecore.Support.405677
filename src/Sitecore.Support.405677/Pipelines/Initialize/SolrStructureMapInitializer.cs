using Sitecore.Pipelines;
using Sitecore.Support.ContentSearch.SolrProvider.StructureMapIntegration;

namespace Sitecore.Support.ContentSearch.SolrProvider.Pipelines.Initialize
{
    public class SolrStructureMapInitializer
    {
        public void Process(PipelineArgs args)
        {
            new StructureMapSolrStartUp().Initialize();
        }
    }
}
