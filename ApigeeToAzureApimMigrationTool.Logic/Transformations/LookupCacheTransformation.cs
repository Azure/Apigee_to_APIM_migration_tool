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
    public class LookupCacheTransformation : IPolicyTransformation
    {
        private readonly IList<KeyValuePair<string, string>> _policyVariables;

        public LookupCacheTransformation(IList<KeyValuePair<string, string>> policyVariables)
        {
            _policyVariables = policyVariables;
        }

        /// <summary>
        /// Transforms the Apigee LookupCache policy to Azure APIM cache-lookup-value policy.
        /// </summary>
        /// <param name="element">The XML element representing the Apigee LookupCache policy.</param>
        /// <param name="apigeePolicyName">The name of the Apigee policy.</param>
        /// <returns>A task that represents the asynchronous transformation operation. The task result contains the transformed Azure APIM policies.</returns>
        public Task<IEnumerable<XElement>> Transform(XElement element, string apigeePolicyName, PolicyDirection policyDirection = PolicyDirection.Inbound)
        {
            var apimPolicies = new List<XElement>();
            if (element.Element("CacheKey") != null)
            {
                apimPolicies.Add(CacheLookupValue(element, apigeePolicyName));
            }
            return Task.FromResult(apimPolicies.AsEnumerable());
        }

        /// <summary>
        /// Transforms the CacheLookupValue element of the Apigee LookupCache policy to Azure APIM cache-lookup-value policy.
        /// </summary>
        /// <param name="element">The XML element representing the CacheLookupValue element of the Apigee LookupCache policy.</param>
        /// <param name="policyName">The name of the Apigee policy.</param>
        /// <returns>The transformed Azure APIM cache-lookup-value policy.</returns>
        private XElement CacheLookupValue(XElement element, string policyName)
        {
            var cacheKey = element.Element("CacheKey");
            string prefix = cacheKey.Element("Prefix")?.Value;
            string keyFragment = cacheKey.Elements("KeyFragment").First(x => x.Attribute("ref") == null).Value;
            string keyFragmentRef = cacheKey.Elements("KeyFragment").First(x => x.Attribute("ref") != null).Attribute("ref").Value;
            string variableName = element.Element("AssignTo").Value;

            string apimCacheKey;
            if (!string.IsNullOrEmpty(keyFragment))
                apimCacheKey = keyFragment;
            else
                apimCacheKey = $"(string)context.Variables[\"{keyFragmentRef}\"]";

            if (!string.IsNullOrEmpty(prefix))
                apimCacheKey = $"{prefix}__{apimCacheKey}";

            if (!string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(keyFragmentRef))
                apimCacheKey = $"@(\"{prefix}__\" + context.Variables.GetValueOrDefault<string>(\"{keyFragmentRef}\"))";

            var newPolicy = new XElement("cache-lookup-value", new XAttribute("key", apimCacheKey), new XAttribute("variable-name", variableName));
            _policyVariables.Add(new KeyValuePair<string, string>(policyName, variableName));
            return newPolicy;
        }
    }
}
