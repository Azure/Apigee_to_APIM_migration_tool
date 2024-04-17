using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Core.Interface
{
    public interface IExpressionTranslator
    {
        bool ContentHasVariablesInIt(string content);
        string TranslateSingleItem(string expression, string defaultValue = "");
        string TranslateWholeString(string expression);
        string TranslateConditionOperator(string expression);
    }
}
