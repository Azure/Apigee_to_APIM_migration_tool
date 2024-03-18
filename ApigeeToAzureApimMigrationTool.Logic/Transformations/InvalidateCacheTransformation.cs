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

    public class InvalidateCacheTransformation : IPolicyTransformation
    {
        /// <summary>
        /// Transforms the Apigee cache policy to Azure APIM cache-remove-value policy.
        /// </summary>
        /// <param name="element">The XML element representing the Apigee cache policy.</param>
        /// <param name="apigeePolicyName">The name of the Apigee policy.</param>
        /// <param name="policyDirection">The direction of the policy (inbound or outbound).</param>
        /// <returns>A task that represents the asynchronous transformation operation. The task result contains the transformed Azure APIM policies as a collection of XML elements.</returns>
        public Task<IEnumerable<XElement>> Transform(XElement element, string apigeePolicyName, PolicyDirection policyDirection = PolicyDirection.Inbound)
        {
            var apimPolicies = new List<XElement>();
            if (element.Element("CacheKey") != null)
            {
                apimPolicies.Add(CacheRemoveValue(element, apigeePolicyName));
            }
            return Task.FromResult(apimPolicies.AsEnumerable());
        }

        /// <summary>
        /// Transforms the Apigee cache key to Azure APIM cache-remove-value key.
        /// </summary>
        /// <param name="element">The XML element representing the Apigee cache policy.</param>
        /// <param name="policyName">The name of the Apigee policy.</param>
        /// <returns>The transformed Azure APIM cache-remove-value policy as an XML element.</returns>
        private XElement CacheRemoveValue(XElement element, string policyName)
        {
            var cacheKey = element.Element("CacheKey");
            string prefix = cacheKey.Element("Prefix")?.Value;
            string keyFragment = cacheKey.Elements("KeyFragment").First(x => x.Attribute("ref") == null).Value;
            string keyFragmentRef = cacheKey.Elements("KeyFragment").First(x => x.Attribute("ref") != null).Attribute("ref").Value;

            string apimCacheKey;
            if (!string.IsNullOrEmpty(keyFragment))
                apimCacheKey = keyFragment;
            else
                apimCacheKey = $"(string)context.Variables[\"{keyFragmentRef}\"]";

            if (!string.IsNullOrEmpty(prefix))
                apimCacheKey = $"{prefix}__{apimCacheKey}";

            if (!string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(keyFragmentRef))
                apimCacheKey = $"@(\"{prefix}__\" + context.Variables.GetValueOrDefault<string>(\"{keyFragmentRef}\"))";

            var newPolicy = new XElement("cache-remove-value", new XAttribute("key", apimCacheKey));
            return newPolicy;
        }
    }
}
