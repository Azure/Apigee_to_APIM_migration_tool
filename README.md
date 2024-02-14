# Project

Apigee to Azure APIM Migration Tool is a cross platform application developed with C# and .Net 7. This application can run on any operating system.
The main purpose of this application is to migrate Apigee API Products, API Proxies and their policies to Azure APIM. It does that by reading metadata from Apigee using its management API, transforming entities and creating them in Azure APIM using .Net SDK and REST API. 

![image](https://github.com/Azure/Apigee_to_APIM_migration_tool/assets/106823811/8df21f97-503a-4af0-97ae-9f7675dcc8c8)

Migration tool requires input parameters listed below. Alternatively it can use a JSON file to store information related to Azure Service Principal and Azure APIM.

Parameter Name |	Parameter Description|Notes|
---------------|-------------------------|-----|
--configFile|Config File Name|JSON configuration file so you don't need to specify all of these command line options (see apigeeToApimConfig.SAMPLE.json)
--apigeeConfigDir|Config Directory Name|A directory containing pre-downloaded Apigee policy XML files, useful for testing if you don't want to continually download your policies from Apigee with every run.
--apigeeManagementApiBaseUrl |	Apigee Management Api Base Url |	Base URL for Apigee's management API (ex: https://api.enterprise.apigee.com)
--apigeeAuthenticationBaseUrl|	Apigee Authentication BaseUrl |	Base URL for authentication against Apigee (ex: https://yourcompany.login.apigee.com)
--apigeeOrganizationName |	Apigee Organization Name |	Name of the Apigee organization that the API Proxy is hosted in
--proxyOrProduct| Proxy or Product |	Define what you want to migrate. Accepted vales are "product" and "proxy"
--proxyOrProductName| API Product or Proxy Name |	Name of the Apigee API Product or Proxy to migrate
--apigeeUsername|	Apigee username | Apigee username to use to authenticate against Apigee
--apigeePassword|	Apigee password | Apigee password to use to authenticate against Apigee
--azureAppId|	App ID| App ID of the Service Principal created in Azure
--azurePassword| App Password |	Password of the Service Principal created in Azure
--azureTenantId|Tenant ID |	Tenant ID of the Azure Active Directory
--azureSubscriptionId|	Subscription ID |	Azure Subscription ID where Azure APIM is deployed in
--apimUrl| APIM URL| URL of the target Azure APIM instance
--apimName|	APIM Name |	Name of the target Azure APIM instance
--apimResourceGroup|	Resource Group Name |	Name of the Resource Group that Azure APIM is deployed in
--apimOauthConfigName|	OAuth configuration name | Name of the OAuth config created in Azure APIM. This must exist before executing the tool <code style="color : red">(optional)</code>
--apimOauthBackendAppId| Backend App ID | App registration ID to use for OAuth <code style="color : red">(optional)</code>
--apimOauthTenantId| Azure AD Tenant ID | Azure AD Tenent ID ito use in OAuth configuration <code style="color : red">(optional)</code>
--apigeeEnvironmentName|	Apigee environment Name |	Name of the Apigee environment the proxy is being migrated from
--keyVaultName|	Azure Key Vault Name |	Name of the Azure Key Vault to be used with Named Values. Note that if you provide this value, all encrypted Key Value Maps from Apigee will be moved to APIM Named Values that point to a secret in Key Vault. Those secrets must be created beforhand with the following naming convention: mapIdentifier-keyName and Azure APIM must be able to access the Key Vault instance <code style="color : red">(optional)</code>

## Entity Mapping

Apigee and Azure APIM have a lot in common and offer same features and capabilities. However, in many cases, in completely different ways. In this section we’ll map entities and features from Apigee to Azure APIM.

First, we’ll start with Proxy Endpoints. In Apigee it is possible to add multiple proxy endpoints to a single API proxy and each proxy endpoint can have multiple Flows. This feature doesn’t exist in APIM therefore if an API Proxy has multiple proxy endpoints, they need to be consolidated into one set of API Operations in Azure APIM.

Apigee has the concept of Pre-Flows and Post-Flows. It is possible to define policies for requests and responses as part of each and it’s also possible to have Pre-Flows and Post-Flows for Target Endpoints which are the equivalent of Endpoints in Azure APIM. In Azure APIM however, we can add Inbound Policies and Outbound Policies at different levels. Global (policies will be applies to all APIs), API (policies will be applied to all operations within an API), Operation (policies will be applies to a specific operation or endpoint) and Product (policies will be applied to all the APIs and their operations that belong to a specific product).

Apigee to APIM Migration tool will consolidate all the pre-flow and post-flow policies for both proxy endpoint and target endpoint and transform them to Azure APIM as API level policies.

All request and response policies at the flow level will be migrated as policies at the operation level.
Shared Flows in Apigee are a reusable collection of policies in a specific order that can be used in multiple places. All Shared Flows are transformed into and migrated as Policy Fragments.

Apigee Entity |	Azure APIM Entity
--------------|------------------
Api Proxy |	API
Proxy Endpoints |	N/A
Target Endpoints |	Endpoints
Flows |	API Operations
Proxy endpoint pre-flow and post-flow policies |	API level inbound and outbound policies
Target endpoint pre-flow and post-flow policies |	API level inbound and outbound policies
Shared Flows | 	Policy Fragments
API Product | API Product
Unencrypted Key Value Maps | Named Values
Encrypted Key Value Maps | Named Values marked as secret (can point to a secret in an Azure Key Vault instance)

## Policy Mapping

The most challenging part of this tool and where it adds real value is Policy Transformation. Apigee To APIM Migration Tool reads all Apigee policies and transforms them into Azure APIM policies. It includes all variable and secret references and complex conditions that use Apigee expressions. As of now, 10 types of policies are supported.

Apigee Policy	| Azure APIM Policy |	Description
--------------|-------------------|------------
Assign Message |	Set Header & Set Body |	Add or remove request headers or body
Lookup Cache |	Cache Lookup Value |	Get a cached value from cache store
Populate Cache |	Cache Store Value |	Store new value in the cache store
KeyValue Map Operations |	Set Variable |	Read variables from named values and assign to a variable
Verify JWT |	Validate Jwt |	Validate a JWT token using encryption key
Service Callout |	Send Request |	Call an HTTP endpoint
Extract Variables |	Set Variable| Read JSON object, extract values and assign to variables. Requires custom expression in Azure APIM
OAuthV2	Validate | Jwt	Validate | JWT policy in APIM can be configured to validate OAuth
Flow Callout |	Include Fragment |	Call Policy Fragment

## Azure APIM Configuration

Azure APIM must be configured properly before migrating API Proxies otherwise APIs may not work. Additionally, there are some entities that can’t be migrated automatically and need to be created manually in Azure APIM. 
- Azure APIM must be provisioned with access to all the back-end services Apigee organization has access to.
- OAuth must be configured on APIM.
- All Key Values must be moved manually from Apigee to APIM. In Apigee it’s possible to create a group of Key Values (it’s called Map Identifier). The equivalent entity in Azure APIM is Named Values. Named Values don’t support groups, so we need to create them in a way that prevents conflicts. All Named Values must follow the naming convention used by the migration tool which is {Map Identifier} – {Key}. For example, before migrating below policy we need to create two Named Values in APIM called APIDoubleProxyConfig-KVM-payment-hk-v1-key and APIDoubleProxyConfig-KVM-payment-hk-v1-secret. APIM doesn’t allow use of underscores in Named Value names so all underscores must be replaced with hyphen.

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
