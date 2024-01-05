using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToAzureApimMigrationTool.Service.Transformations
{
    public class NullTransformation : IPolicyTransformation
    {
        public Task<IEnumerable<XElement>> Transform(XElement element, string apigeePolicyName)
        {
            // Return empty list
            return Task.FromResult(new List<XElement>().AsEnumerable());
        }
    }
}
