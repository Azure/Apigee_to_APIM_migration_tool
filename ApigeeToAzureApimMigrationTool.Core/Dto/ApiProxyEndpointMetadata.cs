using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Core.dto
{
    public class ApiProxyEndpointMetadata
    {
        public ApiProxyEndpointConnection Connection { get; set; }
    }

    public class ApiProxyEndpointConnection
    {
        public string BasePath { get; set; }
        public string ConnectionType { get; set; }
        public string[] VirtualHost { get; set; }
    }
}
