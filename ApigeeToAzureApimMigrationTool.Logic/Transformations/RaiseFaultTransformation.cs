using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Service.Transformations
{
    public class RaiseFaultTransformation : IPolicyTransformation
    {
        public Task<IEnumerable<XElement>> Transform(XElement apigeePolicy, string apigeePolicyName)
        {
            throw new NotImplementedException();
        }
    }
}
