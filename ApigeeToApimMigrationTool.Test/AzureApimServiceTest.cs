using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.Dto;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using ApigeeToAzureApimMigrationTool.Service;
using Newtonsoft.Json;
using System.Reflection;

namespace ApigeeToApimMigrationTool.Test
{
    public class AzureApimServiceTest
    {
        [Fact]
        public async void TestCreateApi()
        {
            //var azureCredentials = JsonConvert.DeserializeObject<AzureCredentials>(File.ReadAllText($"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\credentials.json"));
            //IAzureApimService apimInstance = new AzureApimService(azureCredentials.Tenant.ToString(), azureCredentials.AppId.ToString(), azureCredentials.Password);
            //await apimInstance.CreateApi("Test API", "ApigeeToApimMigrationDemo", "ApigeeToApimMigrationDemo", "1", "/v1/test", "http://www.google.com");
        }
    }
}