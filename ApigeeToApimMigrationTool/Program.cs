﻿// See https://aka.ms/new-console-template for more information
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
using System;
using ApigeeToAzureApimMigrationTool.Service.Transformations;

string apigeeOrganizationName = null, apigeeManagementApiBaseUrl = null, apigeeAuthenticationBaseUrl = null, username = null, password = null, proxyOrProduct, proxyOrProductName, azureAppId, azurePassword, azureTenantId, azureSubscriptionId,
    apimUrl, apimName, apimResourceGroupName, passcode = null, connectionString = null, environment = null, keyVaultName = null;
string oauthConfigName = null, backendAppId = null, azureAdTenentId = null;
bool usePasscode = false;

AzureCredentials azureCredentials = null;
ApimConfig apimConfig = null;


Console.WriteLine("Starting up ...");


if (Environment.GetCommandLineArgs().Count() == 1)
{
    Console.WriteLine("Enter Apigee's management API base URL (ex: https://api.enterprise.apigee.com");
    apigeeManagementApiBaseUrl = Console.ReadLine();
    Console.WriteLine("Enter Apigee's authentication base URL (ex: https://yourcompanyname.login.apigee.com");
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
        Console.WriteLine("Enter Azure APIM resource group name");
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
    Console.WriteLine("Enter Oauth configuration name (optional)");
    oauthConfigName = Console.ReadLine();
    Console.WriteLine("Enter backend app registration ID (optional)");
    backendAppId = Console.ReadLine();

    Console.WriteLine("Enter Apigee environment name");
    environment = Console.ReadLine();

    Console.WriteLine("Enter Azure Key Vault name you'd like to use for named values in APIM (optional)");
    keyVaultName = Console.ReadLine();

    Console.WriteLine("Use Passcode to authenticate to Apigee?(Y[yes], N[no]");
    usePasscode = Console.ReadLine().Equals("y", StringComparison.OrdinalIgnoreCase) || Console.ReadLine().Equals("yes", StringComparison.OrdinalIgnoreCase);
    if (usePasscode)
    {
        Console.WriteLine("Enter Apigee passcode (can be retrieved from this URL: https://yourcompanyname.login.apigee.com/passcode");
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
    azureAdTenentId = Environment.GetCommandLineArgs().Count() == 18 ? Environment.GetCommandLineArgs()[17] : string.Empty;
    environment = Environment.GetCommandLineArgs().Count() >= 17 ? Environment.GetCommandLineArgs()[16] : string.Empty;
    keyVaultName = Environment.GetCommandLineArgs().Count() >= 18 ? Environment.GetCommandLineArgs()[17] : string.Empty;

}


#region dependency injection
Console.WriteLine("registring dependencies ...");

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IProxyMetaDataDataAccess>(new ProxyMetaDataDataAccess(connectionString));

builder.Services.AddSingleton<IApigeeXmlLoader, ApigeeXmlFileLoader>();

builder.Services.AddSingleton<IApigeeManagementApiService, ApigeeManagementApiService>(
    serviceProvider => new ApigeeManagementApiService(
        proxyMetaDataDataAccess: serviceProvider.GetRequiredService<IProxyMetaDataDataAccess>(),
        organizationName: apigeeOrganizationName,
        apigeeManagementApiBaseUrl: apigeeManagementApiBaseUrl,
        proxyName: proxyOrProduct,
        environmentName: environment,
        authenticationBaseUrl: apigeeAuthenticationBaseUrl));

builder.Services.AddSingleton<IAzureApimService, AzureApimService>(
    serviceProvider => new AzureApimService(
        apigeeXmlLoader: serviceProvider.GetRequiredService<IApigeeXmlLoader>(),
        apimProvider: serviceProvider.GetRequiredService<IApimProvider>(),
        policyTransformer: serviceProvider.GetRequiredService<IApimPolicyTransformer>()
        ));

builder.Services.AddSingleton<IApimProvider, AzureApimProvider>(
    serviceProvider => new AzureApimProvider(
             subscriptionId: azureSubscriptionId,
                tenantId: azureTenantId,
                clientId: azureAppId,
                clientSecret: azurePassword,
                resourceGroupName: apimResourceGroupName,
                apimName: apimName,   
                apimUrl: apimUrl,
                keyVaultName: keyVaultName));

// TODO: Load with local file bundle provider based on config
builder.Services.AddSingleton<IBundleProvider, ApigeeOnlineBundleProvider>();

builder.Services.AddSingleton<IPolicyTransformationFactory, PolicyTransformationFactory>();
builder.Services.AddSingleton<IApimPolicyTransformer, ApigeeToApimPolicyTransformer>();

using IHost host = builder.Build();

await Migrate(host.Services, username, password, proxyOrProduct, proxyOrProductName, oauthConfigName, backendAppId, environment, keyVaultName);

await host.RunAsync();

#endregion

async Task Migrate(IServiceProvider hostProvider, string username, string password, string proxyOrProduct, string proxyOrProductName,
    string oauthConfigName, string backendAppId, string environment, string keyVaultName)
{
    if (string.IsNullOrEmpty(proxyOrProduct) || (!proxyOrProduct.ToLower().Equals("product") && !proxyOrProduct.ToLower().Equals("proxy")))
        throw new Exception("only supported values are Product and Proxy");

    using IServiceScope serviceScope = hostProvider.CreateScope();
    IServiceProvider provider = serviceScope.ServiceProvider;

    var apigeeManagementApiService = provider.GetRequiredService<IApigeeManagementApiService>();
    var azureApimService = provider.GetRequiredService<IAzureApimService>();
    var azureApimProvider = provider.GetRequiredService<IApimProvider>();

    Console.WriteLine("get the bearer token for Apigee management API ...");
    if (usePasscode)
    {
        apigeeManagementApiService.AuthenticationToken = passcode;
    }
    else
    {
        apigeeManagementApiService.Username = username;
        apigeeManagementApiService.Password = password;
    }

    if (proxyOrProduct.ToLower().Equals("product"))
    {
        var apigeeProduct = await apigeeManagementApiService.GetApiProductByName(proxyOrProductName);
        var apigeeProductName = apigeeProduct.Name.Trim().Replace(" ", "-").ToLower();
        var apimApiProduct = await azureApimProvider.CreateProduct(apigeeProductName, apigeeProduct.DisplayName, apigeeProduct.Description, apimName);
        foreach (var proxy in apigeeProduct.Proxies)
        {
            await MigrateApiProxy(hostProvider, proxy, oauthConfigName, backendAppId, azureAdTenentId, environment, keyVaultName);
            await azureApimProvider.AddApiToProduct(proxy);
        }
        Console.WriteLine($"API product {proxyOrProductName} and all API proxies belonging to this product are successfully migrated to Azure APIM!");
    }
    else
    {
        await MigrateApiProxy(hostProvider, proxyOrProductName, oauthConfigName, backendAppId, azureAdTenentId, environment, keyVaultName);
        Console.WriteLine($"API proxy {proxyOrProductName} is successfully migrated to Azure APIM!");
    }
    Environment.Exit(0);
}

async Task MigrateApiProxy(IServiceProvider hostProvider, string proxyOrProductName, string oauthConfigName,
    string backendAppId, string azureAdTenentId, string environment, string keyVaultName)
{
    using IServiceScope serviceScope = hostProvider.CreateScope();
    IServiceProvider provider = serviceScope.ServiceProvider;
    var _apigeeManagementApiService = provider.GetRequiredService<IApigeeManagementApiService>();
    var _azureApimService = provider.GetRequiredService<IAzureApimService>();

    var bundleProvider = provider.GetRequiredService<IBundleProvider>();

    //get api metadata
    // TODO - use IBundleProvider ********
    Console.WriteLine("Downloading the proxy api bundle...");
    await bundleProvider.LoadBundle(proxyOrProductName);
    Console.WriteLine($"Migrating API proxy {proxyOrProductName} to Azure APIM");
    await _azureApimService.ImportApi(apimName, proxyOrProductName, oauthConfigName, environment, keyVaultName);
}










