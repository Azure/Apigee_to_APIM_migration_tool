using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Core.Dto
{
    public class ApiProxyRevisionMetadata
    {
        public int Id { get; set; }
        public string[] BasePaths { get; set; }
        public string Name { get; set; }
        public int Revision { get; set; }
        public string Description { get; set; }
        public string[] Policies { get; set; }
        public string[] ProxyEndpoints { get; set; }
        public Dictionary<string,string> ProxyEndpointPaths { get; set; }

    }
}
