using ApigeeToAzureApimMigrationTool.Core;
using ApigeeToAzureApimMigrationTool.Core.Enum;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Service.Transformations
{
    public class ServiceCalloutTransformation : AssignMessageTransformation, IPolicyTransformation
    {
        private readonly IApigeeManagementApiService _apigeeService;
        private readonly IApimProvider _apimProvider;
        private readonly IExpressionTranslator _expressionTranslator;

        public ServiceCalloutTransformation(IApigeeManagementApiService apigeeService, IApimProvider apimProvider, IExpressionTranslator expressionTranslator) : base(expressionTranslator)
        {
            _apigeeService = apigeeService;
            _apimProvider = apimProvider;
            _expressionTranslator = expressionTranslator;
        }

        public async Task<IEnumerable<XElement>> Transform(XElement element, string apigeePolicyName, PolicyDirection policyDirection = PolicyDirection.Inbound)
        {
            var apimPolicies = new List<XElement>();

            var apimSendRequestPolicy =  await SendRequest(element, _apimProvider.ApimUrl, _apigeeService.Environment);

            apimPolicies.Add(apimSendRequestPolicy);

            return apimPolicies.AsEnumerable();
        }
        private async Task<XElement> SendRequest(XElement element, string apimUrl, string? environment)
        {
            string requestVariable = element.Element("Request").Attribute("variable").Value;
            string responseVariable = element.Element("Response").Value;
            string continueOnError = element.Attribute("continueOnError").Value;
            string timeout = element.Element("Timeout")?.Value ?? "60";

            var newPolicy = new XElement("send-request", new XAttribute("mode", $"new"), new XAttribute("response-variable-name", responseVariable),
                new XAttribute("timeout", timeout), new XAttribute("ignore-error", $"{continueOnError}"));

            string url = string.Empty;
            string targetServerName = string.Empty;

            if (element.Element("LocalTargetConnection") != null && element.Element("LocalTargetConnection").Element("Path") != null)
                url = element.Element("LocalTargetConnection").Element("Path").Value;
            else if (element.Element("HTTPTargetConnection") != null && element.Element("HTTPTargetConnection").Element("URL") != null)
                url = element.Element("HTTPTargetConnection").Element("URL").Value;
            else if (element.Element("HTTPTargetConnection") != null && element.Element("HTTPTargetConnection").Element("LoadBalancer") != null
                && element.Element("HTTPTargetConnection").Element("LoadBalancer").Elements("Server").Any())
            {

                targetServerName = element.Element("HTTPTargetConnection").Element("LoadBalancer").Elements("Server").First().Attribute("name").Value;
                if (string.IsNullOrEmpty(environment))
                    throw new Exception($"service callout policy is using a load balancer as target connection. Environment input parameter must be provided in order to migrate target server {targetServerName}");
                var targetServerResponse = await _apigeeService.GetTargetServerByName(targetServerName, environment);
                if (targetServerResponse == null)
                    throw new Exception($"Can't read Target Server information for {targetServerName} in the {environment} env");
                string protocol = "http://";
                string port = string.Empty;
                if (targetServerResponse.SSLInfo.Enabled)
                    protocol = "https://";
                if (!(new int[] { 80, 443 }).Contains(targetServerResponse.Port))
                    port = $":{targetServerResponse.Port}";

                url = $"{protocol}{targetServerResponse.Host}{port}";
            }

            if (url.StartsWith('/'))
                url = apimUrl + url;

            if (element.Element("Request")?.Element("Set")?.Element("Path") != null)
            {
                string path = element.Element("Request").Element("Set").Element("Path").Value;
                if (path.StartsWith("{"))
                {
                    url = url + "/" + $"@((string)context.Variables[\"{path.Replace("{", "").Replace("}", "")}\"])";
                }
            }

            newPolicy.Add(new XElement("set-url", url));

            string verb = element.Element("Request")?.Element("Set")?.Element("Verb") != null ? element.Element("Request").Element("Set").Element("Verb").Value : "GET";
            newPolicy.Add(new XElement("set-method", verb));

            var apimPolicies = BuildApimPolicyCollection(element.Element("Request")).ToList();

            foreach (var policy in apimPolicies)
            {
                newPolicy.Add(policy);
            }

            return newPolicy;
        }

    }
}
