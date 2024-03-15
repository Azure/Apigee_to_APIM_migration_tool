using ApigeeToApimMigrationTool.Core.Config;
using ApigeeToAzureApimMigrationTool.Core.Enum;
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
        private ApimConfiguration _apimConfiguration;
        public OAuthV2Transformation(ApimConfiguration apimConfiguration)
        {
            _apimConfiguration = apimConfiguration;
        }
        public Task<IEnumerable<XElement>> Transform(XElement element, string apigeePolicyName, PolicyDirection policyDirection = PolicyDirection.Inbound)
        {
            var apimPolicies = new List<XElement>
            {
                OAuthV2(element, apigeePolicyName)
            };

            return Task.FromResult(apimPolicies.AsEnumerable());
        }
        private XElement OAuthV2(XElement element, string policyName)
        {
            //Only VerifyAccessToken operation is supported
            var operation = element.Element("Operation")?.Value;
            var name = element.Attribute("name")?.Value;
            if (!operation.Equals("VerifyAccessToken", StringComparison.InvariantCultureIgnoreCase))
            {
                throw new NotSupportedException($"Only VerifyAccessToken operation is supported for OAuthV2 policy. Policy name: {name}");
            }

            var displayName = element.Element("DisplayName")?.Value;
            string accessTokenPrefix = element.Element("AccessTokenPrefix")?.Value;
            if (string.IsNullOrEmpty(accessTokenPrefix))
                accessTokenPrefix = "Bearer";


            //  query-parameter-name is not supported yet. JWT can only be included as a header
            string accessTokenHeaderName = element.Element("AccessToken")?.Value;
            if (string.IsNullOrEmpty(accessTokenHeaderName))
                accessTokenHeaderName = "Authorization";

            var newPolicy = new XElement("validate-jwt",
                new XAttribute("header-name", accessTokenHeaderName),
                new XAttribute("failed-validation-httpcode", "401"),
                new XAttribute("failed-validation-error-message", "Unauthorized. Access token is missing or invalid."),
                new XElement("openid-config", new XAttribute("url", $"https://login.microsoftonline.com/{_apimConfiguration.OAuthTenantId}/v2.0/.well-known/openid-configuration"))
            );

            if (!string.IsNullOrEmpty(_apimConfiguration.OAuthAudiences))
            {
                newPolicy.Element("validate-jwt").Add(new XElement("audiences"));

                var audienceArray = _apimConfiguration.OAuthAudiences.Split(',');
                foreach (string audience in audienceArray)
                {
                    newPolicy.Element("validate-jwt").Element("audiences").Add(new XElement("audience", audience));
                }
            }

            if (!string.IsNullOrEmpty(_apimConfiguration.OAuthIssuers))
            {
                newPolicy.Element("validate-jwt").Add(new XElement("issuers"));

                var issuerArray = _apimConfiguration.OAuthIssuers.Split(',');
                foreach (string issuer in issuerArray)
                {
                    newPolicy.Element("validate-jwt").Element("issuers").Add(new XElement("issuer", issuer));
                }
            }

            string scopeClaimName = "scope";
            if (!string.IsNullOrEmpty(_apimConfiguration.OAuthScopeClaimName))
                scopeClaimName = _apimConfiguration.OAuthScopeClaimName;

            var scope = element.Element("Scope")?.Value;
            if (!string.IsNullOrEmpty(scope))
            {
                newPolicy.Element("validate-jwt").Element("required-claims").Add(
                    new XElement("claim", scope,
                        new XAttribute("name", scopeClaimName),
                        new XAttribute("match", "any"),
                        new XAttribute("separator", " ")
                    )
                );
            }

            return newPolicy;
        }

    }
}
