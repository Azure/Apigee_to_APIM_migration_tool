using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Service
{
    public class ExpressionTranslator
    {
        private readonly Dictionary<string, string> _translationTable;

        public ExpressionTranslator()
        {
            _translationTable = CreateTranslationTable();
        }

        public string Translate(string expression)
        {
            foreach (var item in _translationTable)
            {
                expression = expression.Replace(item.Key, item.Value);
            }

            return expression;
        }

        private Dictionary<string, string> CreateTranslationTable()
        {
            var expressionList = new Dictionary<string, string>();
            expressionList.Add("request.verb", "context.Operation.Method");
            expressionList.Add("request.header.origin", "context.Request.Headers.GetValueOrDefault(\"origin\")");
            expressionList.Add("request.header.Access-Control-Request-Method", "context.Request.Headers.GetValueOrDefault(\"Access-Control-Request-Method\")");
            expressionList.Add(" AND ", " && ");
            expressionList.Add(" and ", " && ");
            expressionList.Add(" or ", " || ");
            expressionList.Add(" OR ", " || ");
            expressionList.Add(" = ", " == ");

            return expressionList;
        }

    }
}
