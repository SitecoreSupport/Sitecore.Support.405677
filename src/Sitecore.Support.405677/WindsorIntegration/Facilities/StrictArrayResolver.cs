namespace Sitecore.Support.ContentSearch.SolrProvider.WindsorIntegration.Facilities
{
  using Castle.Core;
  using Castle.MicroKernel;
  using Castle.MicroKernel.Context;

  public class StrictArrayResolver : ISubDependencyResolver
  {
    // Fields
    private readonly IKernel kernel;

    // Methods
    public StrictArrayResolver(IKernel kernel)
    {
      this.kernel = kernel;
    }

    public bool CanResolve(CreationContext context, ISubDependencyResolver contextHandlerResolver, ComponentModel model, DependencyModel dependency)
    {
      return ((dependency.TargetType != null) && dependency.TargetType.IsArray);
    }

    public object Resolve(CreationContext context, ISubDependencyResolver contextHandlerResolver, ComponentModel model, DependencyModel dependency)
    {
      return this.kernel.ResolveAll(dependency.TargetType.GetElementType());
    }
  }
}
