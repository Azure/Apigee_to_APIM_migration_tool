using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToApimMigrationTool.Test
{
    public class MockBundleProvider : IBundleProvider
    {
        public IBundle GetApiProxyBundle(string proxyOrProductName)
        {
            throw new NotImplementedException();
        }

        public IBundle GetSharedFlowBundle(string sharedFlowName)
        {
            throw new NotImplementedException();
        }
    }
}
