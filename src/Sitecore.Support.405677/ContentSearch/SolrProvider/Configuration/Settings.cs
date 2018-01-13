namespace Sitecore.Support.ContentSearch.SolrProvider.Configuration
{
  public class Settings
  {
    public static int ConnectionTimeout => Sitecore.Configuration.Settings.GetIntSetting("ContentSearch.Solr.ConnectionTimeout", -1);
  }
}