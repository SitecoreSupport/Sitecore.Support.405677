# Sitecore.Support.405677  
Patch allows to configure SolrConnection timeout and moves container initialization into Sitecore pipeline.  

## Main  
This repo contains patch 405677 that allows Solr connection to be set thru a configuration setting. 
The patch comes in 3 flavors tailored to 3 different IOC containers. 
Use appropriate release/branch for the IOC container you use with your Solr integration.  

## Building the project
The project uses SIM (currently QA version 1.4.0.422) generated nuget packages to reference Sitecore specific assemblies. 
Inspect `packages.config` file to understand how SIM configures local nuget repo.  

## Deployment  
> This patch should be applied on each Sitecore instance that works with Solr indexes.  

To deploy the patch:  
* Copy `/App_Config/Include/Sitecore.Support.405677` folder into the `/App_Config/Include` folder.  
* Copy `/Sitecore.Support.405677.<IocName>.dll` into the `/bin` folder.


## Content  
The patch provides 2 configuration files located in `/App_Config/Include/Sitecore.Support.405677` folder.
### Castle.Windsor  
* `/App_Config/Include/Sitecore.Support.405677/Sitecore.Support.405677.config`  
* `/App_Config/Include/Sitecore.Support.405677/Sitecore.Support.405677.WindsorInitializer.config`  
* `/bin/Sitecore.Support.405677.Windsor.dll`   

### StructureMap  
* `/App_Config/Include/Sitecore.Support.405677/Sitecore.Support.405677.config`  
* `/App_Config/Include/Sitecore.Support.405677/Sitecore.Support.405677.StructureMapInitializer.config`  
* `/bin/Sitecore.Support.405677.StructureMap.dll`  

### Unity  
* `/App_Config/Include/Sitecore.Support.405677/Sitecore.Support.405677.config`  
* `/App_Config/Include/Sitecore.Support.405677/Sitecore.Support.405677.UnityInitializer.config`  
* `/bin/Sitecore.Support.405677.Unity.dll`  

## License  
This patch is licensed under the [Sitecore Corporation A/S License](https://github.com/SitecoreSupport/Sitecore.Support.405677/blob/master/LICENSE).  

## Download  
Downloads are available via [GitHub Releases](https://github.com/SitecoreSupport/Sitecore.Support.405677/releases).  