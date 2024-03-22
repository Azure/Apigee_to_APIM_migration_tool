using ApigeeToAzureApimMigrationTool.Core.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ApigeeToAzureApimMigrationTool.Service
{


    public class ExpressionTranslator : IExpressionTranslator
    {
        private readonly Dictionary<string, string> _translationTable;

        public ExpressionTranslator()
        {
            _translationTable = CreateTranslationTable();
        }

        /// <summary>
        /// Translates the whole string by replacing the keys in the translation table with their corresponding values.
        /// </summary>
        /// <param name="expression">The input expression to be translated.</param>
        /// <returns>The translated expression.</returns>
        public string TranslateWholeString(string expression)
        {
            foreach (var item in _translationTable)
                expression = expression.Replace(item.Key, item.Value);

            return expression;
        }

        /// <summary>
        /// Checks if the content has variables in it by using a regular expression pattern.
        /// </summary>
        /// <param name="content">The input content to be checked.</param>
        /// <returns>True if the content has variables, otherwise false.</returns>
        public bool ContentHasVariablesInIt(string content)
        {
            const string apigeeVariable = @"{(.*?)}";
            return Regex.Matches(content, apigeeVariable).Any();
        }

        /// <summary>
        /// Translates a single item by looking up its value in the translation table.
        /// </summary>
        /// <param name="expression">The input expression to be translated.</param>
        /// <param name="defaultValue">The default value to use in case variable wasn't found.</param>
        /// <returns>The translated expression if found in the translation table, otherwise the original expression.</returns>
        public string TranslateSingleItem(string expression, string defaultValue = "")
        {
            return _translationTable.ContainsKey(expression) ? _translationTable[expression] : $"context.Variables.GetValueOrDefault<string>(\"{expression}\",\"{defaultValue}\")";
        }

        /// <summary>
        /// Creates the translation table with the predefined key-value pairs.
        /// </summary>
        /// <returns>The translation table.</returns>
        private Dictionary<string, string> CreateTranslationTable()
        {
            var expressionList = new Dictionary<string, string>();
            expressionList.Add("request.verb", "context.Operation.Method");
            expressionList.Add("request.header.origin", "context.Request.Headers.GetValueOrDefault(\"origin\")");
            expressionList.Add("request.header.Access-Control-Request-Method", "context.Request.Headers.GetValueOrDefault(\"Access-Control-Request-Method\")");

            return expressionList;
        }

        /// <summary>
        /// Creates the translation table for conditions with the predefined key-value pairs.
        /// </summary>
        /// <returns>The translation table for conditions.</returns>
        private Dictionary<string, string> CreateTranslationTableForConditions()
        {
            var expressionList = new Dictionary<string, string>();
            expressionList.Add(" AND ", " && ");
            expressionList.Add(" and ", " && ");
            expressionList.Add(" or ", " || ");
            expressionList.Add(" OR ", " || ");
            expressionList.Add(" = ", " == ");

            return expressionList;
        }
    }
}
