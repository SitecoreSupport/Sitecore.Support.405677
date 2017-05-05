namespace Sitecore.Support.ContentSearch.SolrProvider.WindsorIntegration
{
  using System;
  using Castle.MicroKernel.Registration;

  public static class WindsorExtensions
  {
    public static ComponentRegistration<T> OverridesExistingRegistration<T>(
        this ComponentRegistration<T> componentRegistration, string withName) where T : class
    {
      return componentRegistration.Named(withName).IsDefault();
    }

    public static ComponentRegistration<T> OverridesExistingRegistration<T>(this ComponentRegistration<T> componentRegistration) where T : class
    {
      return componentRegistration.Named(Guid.NewGuid().ToString()).IsDefault();
    }
  }
}
