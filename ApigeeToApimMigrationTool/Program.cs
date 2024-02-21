// See https://aka.ms/new-console-template for more information
using ApigeeToApimMigrationTool.Core.Config;
using ApigeeToApimMigrationTool.Core.Interface;
using ApigeeToApimMigrationTool.DataAccess;
using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using ApigeeToAzureApimMigrationTool.Service;
using ApigeeToAzureApimMigrationTool.Service.Bundles;
using ApigeeToAzureApimMigrationTool.Service.Transformations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using System.Configuration;

Console.WriteLine("Starting up ...");

// Apigee Options
var apigeeOrgNameOption = new Option<string>(
    name: "--apigeeOrganizationName",
    description: "Apigee organization name");

var apigeeAuthenticationBaseUrlOption = new Option<string>(
    name: "--apigeeAuthenticationBaseUrl",
    description: "Apigee authentication base URL (ex: https://yourcompanyname.login.apigee.com");

var apigeeManagementApiBaseUrlOption = new Option<string>(
    name: "--apigeeManagementApiBaseUrl",
    description: "Apigee management API base URL (ex: https://api.enterprise.apigee.com");

var apigeeUsernameOption = new Option<string>(
    name: "--apigeeUsername",
    description: "Apigee username");

var apigeePasswordOption = new Option<string>(
    name: "--apigeePassword",
    description: "Apigee password");

var apigeePasscodeOption = new Option<string>(
    name: "--apigeePasscode",
    description: "Apigee passcode (can be retrieved from this URL: https://yourcompanyname.login.apigee.com/passcode");

var proxyOrProductOption = new Option<string>(
    name: "--proxyOrProduct",
    description: "Migrating Product or Proxy? (enter \"Product\" or \"Proxy\" as answer)")
    .FromAmong("Product", "Proxy");

var proxyOrProductNameOption = new Option<string>(
    name: "--proxyOrProductName",
    description: "Name of the proxy or product you'd like to migrate");

var apigeeEnvironmentNameOption = new Option<string>(
    name: "--apigeeEnvironmentName",
    description: "Apigee environment name");

var apigeeConfigDirOption = new Option<string>(
    name: "--apigeeConfigDir",
    description: "Specify a local directory to load the Apigee configuration bundle from (optional)");

// Entra (Azure AD) Options
var azureAppIdOption = new Option<string>(
    name: "--azureAppId",
    description: "Azure service principal App ID");

var azurePasswordOption = new Option<string>(
    name: "--azurePassword",
    description: "Azure service principal Password");

var azureTenantIdOption = new Option<string>(
    name: "--azureTenantId",
    description: "Azure Tenant ID");

var azureSubscriptionIdOption = new Option<string>(
    name: "--azureSubscriptionId",
    description: "Azure subscription ID");

// Azure APIM Options
var apimUrlOption = new Option<string>(
    name: "--apimUrl",
    description: "Azure APIM URL");

var apimNameOption = new Option<string>(
    name: "--apimName",
    description: "Azure APIM name");

var apimResourceGroupOption = new Option<string>(
    name: "--apimResourceGroup",
    description: "Azure APIM resource group name");

var apimOauthConfigNameOption = new Option<string>(
    name: "--apimOauthConfigName",
    description: "Oauth configuration name (optional)");

var apimOauthBackendAppIdOption = new Option<string>(
    name: "--apimOauthBackendAppId",
    description: "Backend app registration ID (optional)");

var apimOauthTenantIdOption = new Option<string>(
    name: "--apimOauthTenantId",
    description: "Azure AD tenant ID (optional)");

// Other Azure Options
var keyVaultNameOption = new Option<string>(
    name: "--keyVaultName",
    description: "Azure Key Vault name you'd like to use for named values in APIM (optional)");

// Misc Options
var databaseConnectionStringOption = new Option<string>(
    name: "--databaseConnectionString",
    description: "Database connection string")
{ IsHidden = true };

var configFileOption = new Option<string>(
    name: "--configFile",
    description: "Path to the configuration file (optional)");

var rootCommand = new RootCommand("Apigee to Azure APIM migration tool")
{
    apigeeOrgNameOption,
    apigeeAuthenticationBaseUrlOption,
    apigeeManagementApiBaseUrlOption,
    apigeeUsernameOption,
    apigeePasswordOption,
    apigeePasscodeOption,
    proxyOrProductOption,
    proxyOrProductNameOption,
    apigeeEnvironmentNameOption,
    apigeeConfigDirOption,
    azureAppIdOption,
    azurePasswordOption,
    azureTenantIdOption,
    azureSubscriptionIdOption,
    apimUrlOption,
    apimNameOption,
    apimResourceGroupOption,
    apimOauthConfigNameOption,
    apimOauthBackendAppIdOption,
    apimOauthTenantIdOption,
    keyVaultNameOption,
    databaseConnectionStringOption,
    configFileOption
};

rootCommand.SetHandler(async (apigeeConfig, entraConfig, apimConfig, keyVaultName, databaseConnectionString, configFile) =>
{

    await RunMigration(apigeeConfig, entraConfig, apimConfig, keyVaultName, databaseConnectionString, configFile);
}, 
new ApigeeConfigurationBinder(apigeeOrgNameOption, apigeeAuthenticationBaseUrlOption, apigeeManagementApiBaseUrlOption, apigeePasscodeOption, apigeeUsernameOption, apigeePasswordOption, proxyOrProductOption, proxyOrProductNameOption, apigeeEnvironmentNameOption, apigeeConfigDirOption),
new EntraConfigurationBinder(azureAppIdOption, azurePasswordOption, azureTenantIdOption, azureSubscriptionIdOption), 
new ApimConfigurationBinder(apimUrlOption, apimNameOption, apimResourceGroupOption, apimOauthConfigNameOption, apimOauthBackendAppIdOption, apimOauthTenantIdOption), 
keyVaultNameOption, databaseConnectionStringOption, configFileOption);

await rootCommand.InvokeAsync(args);

#region dependency injection
async Task RunMigration(ApigeeConfiguration apigeeConfiguration, EntraConfiguration entraConfiguration, ApimConfiguration apimConfiguration, 
    string keyVaultName, 
    string databaseConnectionString, string configFile)
{
    if (!string.IsNullOrEmpty(configFile))
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(configFile)
            .Build();

        databaseConnectionString = config.GetConnectionString("DataConnection");
        keyVaultName = config["KeyVaultName"];

        config.Bind("Apigee", apigeeConfiguration);
        config.Bind("Entra", entraConfiguration);
        config.Bind("Apim", apimConfiguration);
    }

    Console.WriteLine("registring dependencies ...");

    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSingleton<IProxyMetaDataDataAccess>(new ProxyMetaDataDataAccess(databaseConnectionString));

    builder.Services.AddSingleton<IApigeeXmlLoader, ApigeeXmlFileLoader>();

    if (apigeeConfiguration.ConfigDir != null)
    {
        builder.Services.AddSingleton<IApigeeManagementApiService, ApigeeManagementApiTestFileService>(
            serviceProvider => new ApigeeManagementApiTestFileService(
                apigeeBundleProvider: serviceProvider.GetRequiredService<IBundleProvider>(),
                apigeeXmlLoader: serviceProvider.GetRequiredService<IApigeeXmlLoader>(),
                localConfigPath: apigeeConfiguration.ConfigDir));
    }
    else
    {
        builder.Services.AddSingleton<IApigeeManagementApiService, ApigeeManagementApiService>(
            serviceProvider => new ApigeeManagementApiService(
            proxyMetaDataDataAccess: serviceProvider.GetRequiredService<IProxyMetaDataDataAccess>(),
            apigeeConfiguration: apigeeConfiguration));
    }

    builder.Services.AddSingleton<IAzureApimService, AzureApimService>();

    builder.Services.AddSingleton<IApimProvider, AzureApimProvider>(
        serviceProvider => new AzureApimProvider(apimConfiguration, entraConfiguration, keyVaultName));

    if (apigeeConfiguration.ConfigDir != null)
    {
        builder.Services.AddSingleton<IBundleProvider, ApigeeFileBundleProvider>(
            serviceProvider => new ApigeeFileBundleProvider(apigeeConfiguration.ConfigDir));
    }
    else
    {
        var dir = Directory.GetCurrentDirectory();
        builder.Services.AddSingleton<IBundleProvider, ApigeeOnlineBundleProvider>(
            serviceProvider => new ApigeeOnlineBundleProvider(dir, 
            serviceProvider.GetRequiredService<IApigeeManagementApiService>()));
    }

    builder.Services.AddSingleton<IPolicyTransformationFactory, PolicyTransformationFactory>();
    builder.Services.AddSingleton<IApimPolicyTransformer, ApigeeToApimPolicyTransformer>();
    builder.Services.AddSingleton<IExpressionTranslator, ExpressionTranslator>();

    using IHost host = builder.Build();

    await Migrate(host.Services, apigeeConfiguration, apimConfiguration, keyVaultName);

    await host.RunAsync();
}

#endregion

async Task Migrate(IServiceProvider hostProvider, ApigeeConfiguration apigeeConfiguration, ApimConfiguration apimConfiguration, string keyVaultName)
{
    if (string.IsNullOrEmpty(apigeeConfiguration.ProxyOrProduct) || (!apigeeConfiguration.ProxyOrProduct.ToLower().Equals("product") && !apigeeConfiguration.ProxyOrProduct.ToLower().Equals("proxy")))
        throw new Exception("only supported values are Product and Proxy");

    using IServiceScope serviceScope = hostProvider.CreateScope();
    IServiceProvider provider = serviceScope.ServiceProvider;

    var apigeeManagementApiService = provider.GetRequiredService<IApigeeManagementApiService>();
    var azureApimService = provider.GetRequiredService<IAzureApimService>();
    var azureApimProvider = provider.GetRequiredService<IApimProvider>();

    Console.WriteLine("get the bearer token for Apigee management API ...");
    if (apigeeConfiguration.Passcode != null)
    {
        apigeeManagementApiService.AuthenticationToken = apigeeConfiguration.Passcode;
    }
    else
    {
        apigeeManagementApiService.Username = apigeeConfiguration.Username;
        apigeeManagementApiService.Password = apigeeConfiguration.Password;
    }

    if (apigeeConfiguration.ProxyOrProduct.ToLower().Equals("product"))
    {
        var apigeeProduct = await apigeeManagementApiService.GetApiProductByName(apigeeConfiguration.ProxyOrProductName);
        var apigeeProductName = apigeeProduct.Name.Trim().Replace(" ", "-").ToLower();
        var apimApiProduct = await azureApimProvider.CreateProduct(apigeeProductName, apigeeProduct.DisplayName, apigeeProduct.Description, apimConfiguration.Name);
        foreach (var proxy in apigeeProduct.Proxies)
        {
            await MigrateApiProxy(hostProvider, proxy, apigeeConfiguration, apimConfiguration, keyVaultName);
            await azureApimProvider.AddApiToProduct(proxy);
        }
        Console.WriteLine($"API product {apigeeConfiguration.ProxyOrProductName} and all API proxies belonging to this product are successfully migrated to Azure APIM!");
    }
    else
    {
        await MigrateApiProxy(hostProvider, apigeeConfiguration.ProxyOrProductName, apigeeConfiguration, apimConfiguration, keyVaultName);
        Console.WriteLine($"API proxy {apigeeConfiguration.ProxyOrProductName} is successfully migrated to Azure APIM!");
    }
    Environment.Exit(0);
}

async Task MigrateApiProxy(IServiceProvider hostProvider, string proxyOrProductName, ApigeeConfiguration apigeeConfiguration, ApimConfiguration apimConfig, string keyVaultName)
{
    using IServiceScope serviceScope = hostProvider.CreateScope();
    IServiceProvider provider = serviceScope.ServiceProvider;
    var _apigeeManagementApiService = provider.GetRequiredService<IApigeeManagementApiService>();
    var _azureApimService = provider.GetRequiredService<IAzureApimService>();

    var bundleProvider = provider.GetRequiredService<IBundleProvider>();

    //get api metadata
    Console.WriteLine("Downloading the proxy api bundle...");
    var apiProxyBundle = bundleProvider.GetApiProxyBundle(proxyOrProductName);
    await apiProxyBundle.LoadBundle();

    Console.WriteLine($"Migrating API proxy {proxyOrProductName} to Azure APIM");
    await _azureApimService.ImportApi(apimConfig.Name, proxyOrProductName, apimConfig.OAuthConfigName, apigeeConfiguration.EnvironmentName, keyVaultName);
}










