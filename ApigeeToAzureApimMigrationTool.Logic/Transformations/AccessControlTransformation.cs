using ApigeeToAzureApimMigrationTool.Core.Enum;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using LukeSkywalker.IPNetwork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;


namespace ApigeeToAzureApimMigrationTool.Service.Transformations
{
    public class AccessControlTransformation : IPolicyTransformation
    {
        public Task<IEnumerable<XElement>> Transform(XElement element, string apigeePolicyName, PolicyDirection policyDirection = PolicyDirection.Inbound)
        {
            var policyList = new List<XElement>();
            var ipRules = element.Element("IPRules");
            var matchRules = ipRules.Elements("MatchRule");
            foreach (var matchRule in matchRules)
            {
                var newPolicy = new XElement("ip-filter");
                var action = matchRule.Attribute("action").Value.Equals("DENY", StringComparison.InvariantCultureIgnoreCase) ? "forbid" : "allow";
                newPolicy.Add(new XAttribute("action", action));
                foreach (var sourceAddress in matchRule.Elements("SourceAddress"))
                {
                    var address = sourceAddress.Value;
                    var mask = sourceAddress.Attribute("mask")?.Value;
                    if (mask == null)
                    {
                        if (address.StartsWith("{"))
                        {
                            newPolicy.Add(new XElement("address", $"@(context.Variables.GetValueOrDefault<string>(\"{address}\",\"\")"));
                        }
                        else
                            newPolicy.Add(new XElement("address", address));
                    }
                    else
                    {
                        //TODO: add support for variable used in mask
                        IPNetwork ipnetwork = IPNetwork.Parse($"{address}/{mask}");
                        var addressRangeElement = new XElement("address-range");
                        addressRangeElement.Add(new XAttribute("from", ipnetwork.FirstUsable), new XAttribute("to", ipnetwork.LastUsable));
                        newPolicy.Add(addressRangeElement);
                    }
                }
                policyList.Add(newPolicy);
            }
            return Task.FromResult<IEnumerable<XElement>>(policyList);
        }
    }
}
