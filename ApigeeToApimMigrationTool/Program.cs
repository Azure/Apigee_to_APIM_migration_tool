// See https://aka.ms/new-console-template for more information
using ApigeeToApimMigrationTool.Core;
using ApigeeToApimMigrationTool.Core.Interface;
using ApigeeToApimMigrationTool.DataAccess;
using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.dto;
using ApigeeToAzureApimMigrationTool.Core.Dto;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using ApigeeToAzureApimMigrationTool.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Drawing.Text;
using System.Reflection;
using static System.Net.Mime.MediaTypeNames;

string apigeeOrganizationName = null, apigeeManagementApiBaseUrl = null, apigeeAuthenticationBaseUrl = null, username = null, password = null, proxyOrProduct, proxyOrProductName, azureAppId, azurePassword, azureTenantId, azureSubscriptionId,
    apimUrl, apimName, apimResourceGroupName, passcode = null, connectionString = null;
string oauthConfigName = null, backendAppId = null, azureAdTenantId = null;
bool usePasscode = false;

AzureCredentials azureCredentials = null;
ApimConfig apimConfig = null;


Console.WriteLine("Starting up ...");


if (Environment.GetCommandLineArgs().Count() == 1)
{
    Console.WriteLine("Enter Apigee's management API base URL");
    apigeeManagementApiBaseUrl = Console.ReadLine();
    Console.WriteLine("Enter Apigee's authentication base URL");
    apigeeAuthenticationBaseUrl = Console.ReadLine();
    Console.WriteLine("Enter Apigee organization name");
    apigeeOrganizationName = Console.ReadLine();
    Console.WriteLine("Migrating Product or Proxy? (enter \"Product\" or \"Proxy\" as answer)");
    proxyOrProduct = Console.ReadLine();
    Console.WriteLine("Enter Product or Proxy name");
    proxyOrProductName = Console.ReadLine();


    if (!File.Exists("appsettings.json"))
    {
        Console.WriteLine("Enter service principal App ID");
        azureAppId = Console.ReadLine();
        Console.WriteLine("Enter service principal Password");
        azurePassword = Console.ReadLine();
        Console.WriteLine("Enter Azure Tenant ID");
        azureTenantId = Console.ReadLine();
        Console.WriteLine("Enter Azure subscription ID");
        azureSubscriptionId = Console.ReadLine();
        Console.WriteLine("Enter Azure APIM URL");
        apimUrl = Console.ReadLine();
        Console.WriteLine("Enter Azure APIM name");
        apimName = Console.ReadLine();
        Console.WriteLine("Enter Azure APIMresource group name");
        apimResourceGroupName = Console.ReadLine();
    }
    else
    {
        var configuration = new ConfigurationBuilder()
     .AddJsonFile($"appsettings.json");

        var config = configuration.Build();
        connectionString = config.GetConnectionString("DataConnection");
        apimConfig = config.GetSection("apim").Get<ApimConfig>();
        azureCredentials = config.GetSection("azureServicePrincipal").Get<AzureCredentials>();

        azureAppId = azureCredentials.AppId.ToString();
        azurePassword = azureCredentials.Password;
        azureTenantId = azureCredentials.Tenant.ToString();
        azureSubscriptionId = azureCredentials.SubscriptionId;
        apimUrl = apimConfig.ApimUrl;
        apimName = apimConfig.ApimName;
        apimResourceGroupName = apimConfig.ApimResourceGroupName;
    }
    Console.WriteLine("Enter Oauth configuration name");
    oauthConfigName = Console.ReadLine();
    Console.WriteLine("Enter backend app registration ID");
    backendAppId = Console.ReadLine();

    Console.WriteLine("Use Passcode to authenticate to Apigee?");
    usePasscode = bool.Parse(Console.ReadLine());
    if (usePasscode)
    {
        Console.WriteLine("Enter Apigee passcode");
        passcode = Console.ReadLine();
    }
    else
    {
        Console.WriteLine("Enter Apigee username");
        username = Console.ReadLine();
        Console.WriteLine("Enter Apigee password");
        password = Console.ReadLine();
    }

}
else
{
    Console.WriteLine("values provided via arguments ...");


    apigeeManagementApiBaseUrl = Environment.GetCommandLineArgs()[1];
    apigeeAuthenticationBaseUrl = Environment.GetCommandLineArgs()[2];
    apigeeOrganizationName = Environment.GetCommandLineArgs()[3];
    proxyOrProduct = Environment.GetCommandLineArgs()[4];
    proxyOrProductName = Environment.GetCommandLineArgs()[5];
    username = Environment.GetCommandLineArgs()[6];
    password = Environment.GetCommandLineArgs()[7];
    //azure credentials
    azureAppId = Environment.GetCommandLineArgs()[8];
    azurePassword = Environment.GetCommandLineArgs()[9];
    azureTenantId = Environment.GetCommandLineArgs()[10];
    azureSubscriptionId = Environment.GetCommandLineArgs()[11];
    //apim info
    apimUrl = Environment.GetCommandLineArgs()[12];
    apimName = Environment.GetCommandLineArgs()[13];
    apimResourceGroupName = Environment.GetCommandLineArgs()[14];

    oauthConfigName = Environment.GetCommandLineArgs().Count() >= 16 ? Environment.GetCommandLineArgs()[15] : string.Empty;
    backendAppId = Environment.GetCommandLineArgs().Count() >= 17 ? Environment.GetCommandLineArgs()[16] : string.Empty;
    azureAdTenantId = Environment.GetCommandLineArgs().Count() == 18 ? Environment.GetCommandLineArgs()[17] : string.Empty;

}


#region dependency injection
Console.WriteLine("registring dependencies ...");

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IProxyMetaDataDataAccess>(new ProxyMetaDataDataAccess(connectionString));

builder.Services.AddSingleton<IApigeeManagementApiService, ApigeeManagementApiService>(
    serviceProvider => new ApigeeManagementApiService(
            proxyMetaDataDataAccess: serviceProvider.GetRequiredService<IProxyMetaDataDataAccess>(),
            organizationName: apigeeOrganizationName,
            apigeeManagementApiBaseUrl: apigeeManagementApiBaseUrl));

builder.Services.AddSingleton<IAzureApimService, AzureApimService>(
    serviceProvider => new AzureApimService(
     apiService: serviceProvider.GetRequiredService<IApigeeManagementApiService>(),
     apimProvider: serviceProvider.GetRequiredService<IApimProvider>(),
     apimUrl: apimUrl));

builder.Services.AddSingleton<IApimProvider, AzureApimProvider>(
    serviceProvider => new AzureApimProvider(
             subscriptionId: azureSubscriptionId,
                    tenantId: azureTenantId,
                           clientId: azureAppId,
                                  clientSecret: azurePassword,
                                         apimUrl: apimUrl));

using IHost host = builder.Build();

await Migrate(host.Services, username, password, proxyOrProduct, proxyOrProductName, oauthConfigName, backendAppId);

await host.RunAsync();

#endregion

async Task Migrate(IServiceProvider hostProvider, string username, string password, string proxyOrProduct, string proxyOrProductName, string oauthConfigName, string backendAppId)
{
    if (string.IsNullOrEmpty(proxyOrProduct) || (!proxyOrProduct.ToLower().Equals("product") && !proxyOrProduct.ToLower().Equals("proxy")))
        throw new Exception("only supported values are Product and Proxy");

    using IServiceScope serviceScope = hostProvider.CreateScope();
    IServiceProvider provider = serviceScope.ServiceProvider;

    var apigeeManagementApiService = provider.GetRequiredService<IApigeeManagementApiService>();
    var azureApimService = provider.GetRequiredService<IAzureApimService>();
    var azureApimProvider = provider.GetRequiredService<IApimProvider>();

    Console.WriteLine("get the bearer token for Apigee management API ...");
    string bearerToken;
    if (usePasscode)
    {
        //get the token using passcode
        bearerToken = await apigeeManagementApiService.GetAuthenticationToken(passcode, apigeeAuthenticationBaseUrl);
    }
    else
    {
        //get the token using username and password
        bearerToken = await apigeeManagementApiService.GetAuthenticationToken(username, password, apigeeAuthenticationBaseUrl);
    }

    if (proxyOrProduct.ToLower().Equals("product"))
    {
        var apigeeProduct = await apigeeManagementApiService.GetApiProductByName(proxyOrProductName, bearerToken);
        var apigeeProductName = apigeeProduct.Name.Trim().Replace(" ", "-").ToLower();
        var apimApiProduct = await azureApimProvider.CreateProduct(apigeeProductName, apigeeProduct.DisplayName, apigeeProduct.Description, apimResourceGroupName, apimName);
        foreach (var proxy in apigeeProduct.Proxies)
        {
            await MigrateApiProxy(hostProvider, bearerToken, proxy, oauthConfigName, backendAppId, azureAdTenantId);
            await azureApimService.AddApiToProduct(apimApiProduct, proxy);
        }
        Console.WriteLine($"API product {proxyOrProductName} and all API proxies belonging to this product are successfully migrated to Azure APIM!");
    }
    else
    {
        await MigrateApiProxy(hostProvider, bearerToken, proxyOrProductName, oauthConfigName, backendAppId, azureAdTenantId);
        Console.WriteLine($"API proxy {proxyOrProductName} is successfully migrated to Azure APIM!");
    }
    Environment.Exit(0);
}

async Task MigrateApiProxy(IServiceProvider hostProvider, string bearerToken, string proxyOrProductName, string oauthConfigName, string backendAppId, string azureAdTenentId)
{
    using IServiceScope serviceScope = hostProvider.CreateScope();
    IServiceProvider provider = serviceScope.ServiceProvider;
    var _apigeeManagementApiService = provider.GetRequiredService<IApigeeManagementApiService>();
    var _azureApimService = provider.GetRequiredService<IAzureApimService>();

    //get api metadata
    Console.WriteLine("Downloading the proxy api bundle...");
    var apiProxyMetadata = await _apigeeManagementApiService.GetApiProxyByName(proxyOrProductName, bearerToken);
    //get the latest revision
    int maxRevision = apiProxyMetadata.revision.Select(x => int.Parse(x)).Max();
    //download api proxy bundle 
    string bundlePath = await _apigeeManagementApiService.DownloadApiProxyBundle(proxyOrProductName, maxRevision, bearerToken);
    // import the proxy into Azure APO,
    Console.WriteLine($"Migrating API proxy {proxyOrProductName} to Azure APIM");
    await _azureApimService.ImportApi(apimName, apimUrl, apimResourceGroupName, bundlePath, proxyOrProductName, bearerToken, oauthConfigName, backendAppId, azureAdTenentId);
}










