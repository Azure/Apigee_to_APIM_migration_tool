﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Core.Interface
{
    public interface IBundleProvider
    {
        IBundle GetApiProxyBundle(string proxyOrProductName);
        IBundle GetSharedFlowBundle(string sharedFlowName);
    }
}
