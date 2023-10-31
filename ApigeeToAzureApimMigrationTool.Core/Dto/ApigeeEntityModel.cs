using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Core.Dto
{
    public class ApigeeEntityModel
    {
        public string name { get; set; }
        public string[] revision { get; set; }
        public ApiProxyMetaData metaData { get; set; }
    }
}
