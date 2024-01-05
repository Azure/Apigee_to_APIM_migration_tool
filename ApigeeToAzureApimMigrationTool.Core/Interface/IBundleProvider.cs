using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Core.Interface
{
    public interface IBundleProvider
    {
        Task LoadBundle(string proxyOrProductName);
        string GetBundlePath();
    }
}
