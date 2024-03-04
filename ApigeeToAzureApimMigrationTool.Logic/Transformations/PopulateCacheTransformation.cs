using ApigeeToAzureApimMigrationTool.Core.Enum;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Globalization;
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

        /// <summary>
        /// Transforms the Apigee policy to Azure API Management policies.
        /// </summary>
        /// <param name="element">The Apigee policy XML element.</param>
        /// <param name="apigeePolicyName">The name of the Apigee policy.</param>
        /// <returns>The transformed Azure API Management policies as a collection of XML elements.</returns>
        public Task<IEnumerable<XElement>> Transform(XElement element, string apigeePolicyName, PolicyDirection policyDirection = PolicyDirection.Inbound)
        {
            var apimPolicies = new List<XElement>();

            apimPolicies.Add(CacheStoreValue(element, apigeePolicyName));

            return Task.FromResult(apimPolicies.AsEnumerable());
        }

        /// <summary>
        /// Transforms the CacheStoreValue policy.
        /// </summary>
        /// <param name="element">The Apigee policy XML element.</param>
        /// <param name="policyName">The name of the Apigee policy.</param>
        /// <returns>The transformed Azure API Management policy as an XML element.</returns>
        private XElement CacheStoreValue(XElement element, string policyName)
        {
            var cacheKey = element.Element("CacheKey");
            string prefix = cacheKey.Element("Prefix")?.Value;
            string keyFragment = cacheKey.Elements("KeyFragment").First(x => x.Attribute("ref") == null).Value;
            string keyFragmentRef = cacheKey.Elements("KeyFragment").First(x => x.Attribute("ref") != null).Attribute("ref").Value;

            string timeout = element.Element("ExpirySettings").Element("TimeoutInSec").Value;
            string timeoutRef = element.Element("ExpirySettings").Element("TimeoutInSec").Attribute("ref").Value;
            string expiryDate = element.Element("ExpirySettings").Element("ExpiryDate").Value;
            string expiryDateRef = element.Element("ExpirySettings").Element("ExpiryDate").Attribute("ref").Value;
            string timeOfDay = element.Element("ExpirySettings").Element("TimeOfDay").Value;
            string timeOfDayRef = element.Element("ExpirySettings").Element("TimeOfDay").Attribute("ref").Value;

            string timeOutSeconds = "30";
            if (!string.IsNullOrEmpty(expiryDate))
            {
                string[] formats = { "MM-dd-yyyy" };
                DateTime cacheExpiryDate = DateTime.ParseExact(expiryDate, formats, new CultureInfo("en-US"), DateTimeStyles.None);
                timeOutSeconds = (cacheExpiryDate - DateTime.Now).Seconds.ToString();
            }
            else if (!string.IsNullOrEmpty(expiryDateRef))
            {
                timeOutSeconds = $"@((DateTime.Parse(context.Variables.GetValueOrDefault<string>(\"{expiryDateRef}\")) - DateTime.Now).Seconds.ToString())";
            }
            else if (!string.IsNullOrEmpty(timeOfDay))
            {
                var time = TimeSpan.Parse(timeOfDay);
                var cacheExpiryDate = DateTime.Today.Add(time);
                timeOutSeconds = (cacheExpiryDate - DateTime.Now).Seconds.ToString();
            }
            else if (!string.IsNullOrEmpty(timeOfDayRef))
            {
                timeOutSeconds = $"@{{ var time = TimeSpan.Parse(context.Variables.GetValueOrDefault<string>(\"{timeOfDayRef}\"));  var cacheExpiryDate = DateTime.Today.Add(time); return (cacheExpiryDate - DateTime.Now).Seconds.ToString(); }}";
            }
            else if (!string.IsNullOrEmpty(timeout))
            {
                timeOutSeconds = timeout;
            }
            else if (!string.IsNullOrEmpty(timeoutRef))
            {
                timeOutSeconds = $"@(context.Variables.GetValueOrDefault<string>(\"{timeoutRef}\"))";
            }
            else
            {
                throw new NotSupportedException("At least one of the following elements need to be defined within the ExpirySettings element of PopulateCache policy. TimeoutInSec, ExpiryDate or TimeOfDay");
            }

            string variableName = element.Element("Source").Value;

            string apimCacheKey;
            if (!string.IsNullOrEmpty(keyFragment))
                apimCacheKey = keyFragment;
            else
                apimCacheKey = $"(string)context.Variables[\"{keyFragmentRef}\"]";

            if (!string.IsNullOrEmpty(prefix))
                apimCacheKey = $"{prefix}__{apimCacheKey}";

            if(!string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(keyFragmentRef))
                apimCacheKey = $"@(\"{prefix}__\" + context.Variables.GetValueOrDefault<string>(\"{keyFragmentRef}\"))";

            var newPolicy = new XElement("cache-store-value", new XAttribute("key", apimCacheKey), new XAttribute("value", $"@((string)context.Variables[\"{variableName}\"])"), new XAttribute("duration", timeOutSeconds));
            _policyVariables.Add(new KeyValuePair<string, string>(policyName, variableName));
            return newPolicy;
        }

    }
}
