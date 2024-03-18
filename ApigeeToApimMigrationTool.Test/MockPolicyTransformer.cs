using ApigeeToApimMigrationTool.Core.Config;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToApimMigrationTool.Test
{
    public class MockPolicyTransformer : IApimPolicyTransformer
    {
        public Task TransformPoliciesInCollection(IEnumerable<XElement>? elements, XElement azureApimPolicySection, Func<string, string, XDocument> xmlLoader,
            string apimName, string proxyName, ApigeeConfiguration apigeeConfiguration, ApimConfiguration apimConfig)
        {
            throw new NotImplementedException();
        }
    }
}
