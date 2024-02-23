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
        public Task<IEnumerable<XElement>> Transform(XElement element, string apigeePolicyName, PolicyDirection policyDirection = PolicyDirection.Inbound)
        {
            var apimPolicies = new List<XElement>();
            if (element.Element("CacheKey") != null)
            {
                apimPolicies.Add(CacheLookupValue(element, apigeePolicyName));
            }
            return Task.FromResult(apimPolicies.AsEnumerable());
        }
        private XElement CacheLookupValue(XElement element, string policyName)
        {
            var cacheKey = element.Element("CacheKey");
            string keyFragment = cacheKey.Elements("KeyFragment").First(x => x.Attribute("ref") == null).Value;
            string keyFragmentRef = cacheKey.Elements("KeyFragment").First(x => x.Attribute("ref") != null).Attribute("ref").Value;
            string variableName = element.Element("AssignTo").Value;
            string apimCacheKey = $"{keyFragment}__{keyFragmentRef}";

            var newPolicy = new XElement("cache-lookup-value", new XAttribute("key", apimCacheKey), new XAttribute("variable-name", variableName), new XAttribute("caching-type", "internal"));
            _policyVariables.Add(new KeyValuePair<string, string>(policyName, variableName));
            return newPolicy;
        }

    }
}
