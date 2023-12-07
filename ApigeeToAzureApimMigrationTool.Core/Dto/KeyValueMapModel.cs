using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Core.dto
{
    public class KeyValueMapModel
    {
        public bool Encrypted { get; set; }
        public string Name { get; set; }
        public ICollection<KeyValueMapItemModel> Entry { get; set; }
    }

    public class KeyValueMapItemModel
    {
        public string Name  { get; set; }
        public string Value { get; set; }
    }
}

