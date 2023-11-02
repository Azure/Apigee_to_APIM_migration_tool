using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Core.Dto
{
    public class ApiProxyMetaData
    {
        public long createdAt { get; set; }
        public string createdBy { get; set; }
        public long lastModifiedAt { get; set; }
        public string lastModifiedBy { get; set; }
        public string subType { get; set; }
    }
}
