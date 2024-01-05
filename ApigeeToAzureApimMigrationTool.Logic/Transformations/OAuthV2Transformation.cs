using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Service.Transformations
{
    public class OAuthV2Transformation : IPolicyTransformation
    {
        public Task<IEnumerable<XElement>> Transform(XElement element, string apigeePolicyName)
        {
            var apimPolicies = new List<XElement>
            {
                OAuthV2(element, apigeePolicyName)
            };

            return Task.FromResult(apimPolicies.AsEnumerable());
        }
        private XElement OAuthV2(XElement element, string policyName)
        {
            var asyncAttr = element.Attribute("async")?.Value;
            var continueOnError = element.Attribute("continueOnError")?.Value;
            var enabled = element.Attribute("enabled")?.Value;
            var name = element.Attribute("name")?.Value;

            var displayName = element.Element("DisplayName")?.Value;
            var operation = element.Element("Operation")?.Value;

            var newPolicy = new XElement("validate-jwt",
                new XAttribute("header-name", "Authorization"),
                new XAttribute("failed-validation-httpcode", "401"),
                new XAttribute("failed-validation-error-message", "Unauthorized. Access token is missing or invalid.")
            // additional attributes can go here, like openid-config, issuers, required-claims when needed.
            );

            return newPolicy;
        }

    }
}
