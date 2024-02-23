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
    public class PopulateCacheTransformation : IPolicyTransformation
    {
        private readonly IList<KeyValuePair<string, string>> _policyVariables;

        public PopulateCacheTransformation(IList<KeyValuePair<string, string>> policyVariables)
        {
            _policyVariables = policyVariables;
        }
        public Task<IEnumerable<XElement>> Transform(XElement element, string apigeePolicyName, PolicyDirection policyDirection = PolicyDirection.Inbound)
        {
            var apimPolicies = new List<XElement>();

            apimPolicies.Add(CacheStoreValue(element, apigeePolicyName));

            return Task.FromResult(apimPolicies.AsEnumerable());
        }
        private XElement CacheStoreValue(XElement element, string policyName)
        {
            var cacheKey = element.Element("CacheKey");
            string keyFragment = cacheKey.Elements("KeyFragment").First(x => x.Attribute("ref") == null).Value;
            string keyFragmentRef = cacheKey.Elements("KeyFragment").First(x => x.Attribute("ref") != null).Attribute("ref").Value;
            string expiry = element.Element("ExpirySettings").Element("TimeoutInSec").Value;
            string variableName = element.Element("Source").Value;
            string apimCacheKey = $"{keyFragment}__{keyFragmentRef}";

            var newPolicy = new XElement("cache-store-value", new XAttribute("key", apimCacheKey), new XAttribute("value", $"@((string)context.Variables[\"{variableName}\"])"), new XAttribute("caching-type", "internal"), new XAttribute("duration", expiry));
            _policyVariables.Add(new KeyValuePair<string, string>(policyName, variableName));
            return newPolicy;
        }

    }
}
