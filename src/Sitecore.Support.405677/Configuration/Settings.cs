namespace Sitecore.Support.ContentSearch.SolrProvider.Configuration
{
    public class Settings
    {
        public static int ConnectionTimeout => Sitecore.Configuration.Settings.GetIntSetting("ContentSearch.Solr.ConnectionTimeout", 60000);

        public static string IndexConstructorParametersQuery => Sitecore.Configuration.Settings.GetSetting(
            "ContentSearch.Solr.IndexConstractorParameters.Query",
            "contentSearch/configuration/indexes/index/param[@desc='collection' or @desc='rebuildcollection']");
    }
}
