namespace Sitecore.Support.ContentSearch.SolrProvider.Pipelines.Initialize
{
  using Ninject;
  using Sitecore.Pipelines;
  using NinjectIntegration;

  public class NinjectInitializer
  {
    public void Process(PipelineArgs args)
    {
      var container = new StandardKernel();

      var startup = new NinjectSolrStartUp(container);

      startup.Initialize();
    }
  }
}