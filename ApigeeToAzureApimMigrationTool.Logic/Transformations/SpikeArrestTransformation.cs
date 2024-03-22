using ApigeeToAzureApimMigrationTool.Core.Enum;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Service.Transformations
{
    public class SpikeArrestTransformation : IPolicyTransformation
    {
        private readonly IExpressionTranslator _expressionTranslator;

        public SpikeArrestTransformation(IExpressionTranslator expressionTranslator)
        {
            _expressionTranslator = expressionTranslator;
        }

        public Task<IEnumerable<XElement>> Transform(XElement element, string apigeePolicyName, PolicyDirection policyDirection = PolicyDirection.Inbound)
        {
            var policyList = new List<XElement>();

            string rateExpression;
            string renewalPeriodExpression = "1";

            string rate = element.Element("Rate")?.Value;
            if (string.IsNullOrEmpty(rate))
            {
                rate = element.Element("Rate").Attribute("ref").Value;
                rateExpression = $"@{{ var value = {_expressionTranslator.TranslateSingleItem(rate)}; return value.Contains(\"pm\") ? int.Parse(value.Replace(\"pm\", \"\")) / 60 :  value.Replace(\"ps\", \"\"); }}";
                renewalPeriodExpression = $"@{{ var value = {_expressionTranslator.TranslateSingleItem(rate)}; return value.Contains(\"pm\") ? 60 : 1 }}";
            }
            else
            {
                if (rate.Contains("pm"))
                {
                    renewalPeriodExpression = "60";
                    rateExpression = (int.Parse(rate.Replace("pm", "")) / 60).ToString();
                }
                else
                    rateExpression = (int.Parse(rate.Replace("ps", ""))).ToString();
            }

            string counterKey = element.Element("Identifier").Attribute("ref").Value;

            var rateLimitPolicy = new XElement("rate-limit-by-key", new XAttribute("calls", rateExpression), new XAttribute("renewal-period", renewalPeriodExpression),
                new XAttribute("counter-key", $"@({_expressionTranslator.TranslateSingleItem(counterKey)})"));

            policyList.Add(rateLimitPolicy);
            return Task.FromResult<IEnumerable<XElement>>(policyList);

        }
    }
}
