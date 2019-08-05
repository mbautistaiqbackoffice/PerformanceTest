// ReSharper disable UnusedMember.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable UnusedMember.Local
namespace JUST
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class DataTransformer
    {
        public static string Transform(string transformer, string inputJson)
        {
            return Parse(transformer, inputJson, null, null);
        }

        private static string Parse(string transformer, string inputJson, JArray array, JToken currentArrayElement)
        {
            var startIndex = 0;
            int index;

            while (startIndex < transformer.Length && (index = transformer.IndexOf('#', startIndex)) != -1)
            {
                var functionString = GetFunctionString(transformer, index);
                if (functionString == null)
                    break;

                if (functionString.Contains("loop"))
                {
                    var loopArgs = GetLoopArguments(index, transformer, false);
                    var loopArgsInclusive = GetLoopArguments(index, transformer, true);
                    var builder = new StringBuilder(transformer);
                    builder.Remove(index - 1, loopArgsInclusive.Length + 1);

                    var evaluatedFunction = EvaluateFunction(functionString, inputJson, array, currentArrayElement, loopArgs).ToString();
                    builder.Insert(index - 1, evaluatedFunction);
                    transformer = builder.ToString();
                    startIndex = index + evaluatedFunction.Length;
                }
                else
                {
                    var builder = new StringBuilder(transformer);
                    builder.Remove(index, functionString.Length);

                    var evaluatedFunction = EvaluateFunction(functionString, inputJson, array, currentArrayElement, null).ToString();
                    if (string.IsNullOrEmpty(evaluatedFunction))
                        continue;

                    startIndex = index + evaluatedFunction.Length;

                    builder.Insert(index, evaluatedFunction);
                    transformer = builder.ToString();
                }
            }

            return transformer;
        }

        private static string GetLoopArguments(int startIndex, string input, bool inclusive)
        {
            var loopArgs = string.Empty;

            var openBraceCount = 0;
            var closeBraceCount = 0;

            var braceStartIndex = 0;
            var braceEndIndex = 0;
            for (var i = startIndex; i < input.Length; i++)
            {
                var currentChar = input[i];

                if (currentChar == '{')
                {
                    if (openBraceCount == 0)
                        braceStartIndex = i;

                    openBraceCount++;
                }

                if (currentChar == '}')
                {
                    braceEndIndex = i;
                    closeBraceCount++;
                }

                if (openBraceCount <= 0 || openBraceCount != closeBraceCount)
                    continue;

                var fromIndex = inclusive ? startIndex : braceStartIndex;
                loopArgs = input.Substring(fromIndex, braceEndIndex - fromIndex + 1);
                break;
            }

            if (inclusive && loopArgs == string.Empty)
                return "#loop";

            return loopArgs;
        }

        private static object EvaluateFunction(string functionString,
                                               string inputJson,
                                               JToken array,
                                               JToken currentArrayElement,
                                               string loopArgumentString)
        {
            functionString = functionString.Trim().Substring(1);
            var indexOfStart = functionString.IndexOf("(", StringComparison.Ordinal);
            if (indexOfStart == -1)
                return null;

            var functionName = functionString.Substring(0, indexOfStart);
            var argumentString = functionString.Substring(indexOfStart + 1, functionString.Length - indexOfStart - 2);
            var arguments = ExpressionHelper.GetArguments(argumentString);
            var listParameters = new List<object>();
            if (arguments != null && arguments.Length > 0)
            {
                foreach (var argument in arguments)
                {
                    var trimmedArgument = argument;
                    if (argument.Contains("#"))
                        trimmedArgument = argument.Trim();

                    if (trimmedArgument.StartsWith("#"))
                        listParameters.Add(EvaluateFunction(trimmedArgument,
                                                                 inputJson,
                                                                 array,
                                                                 currentArrayElement,
                                                                 loopArgumentString));
                    else
                        listParameters.Add(trimmedArgument);
                }
            }

            listParameters.Add(new JUSTContext(inputJson));
            var parameters = listParameters.ToArray();

            object output;
            switch (functionName)
            {
                case "loop":
                    output = GetLoopResult(parameters, loopArgumentString);
                    break;

                case "currentvalue":
                case "currentindex":
                case "lastindex":
                case "lastvalue":
                    output = CallMethod("JUST.Transformer", functionName, new object[] { array, currentArrayElement });
                    break;

                case "currentvalueatpath":
                case "lastvalueatpath":
                    output = CallMethod("JUST.Transformer", functionName, new object[] { array, currentArrayElement, arguments?[0] });
                    break;

                case "customfunction":
                    output = CallCustomFunction(parameters);
                    break;

                case "xconcat":
                case "xadd":
                case "mathequals":
                case "mathgreaterthan":
                case "mathlessthan":
                case "mathgreaterthanorequalto":
                case "mathlessthanorequalto":
                case "stringcontains":
                case "stringequals":
                    var oParams = new object[1];
                    oParams[0] = parameters;
                    output = CallMethod("JUST.Transformer", functionName, oParams);
                    break;

                default:
                    output = CallMethod("JUST.Transformer", functionName, parameters);
                    break;
            }

            return output;
        }

        private static string GetStringValue(object obj)
        {
            if (obj is JToken token)
                return token.ToString(Formatting.None);

            if (obj is IEnumerable<object> list)
                return string.Join(",", list.Select(el => el.ToString()));

            return obj.ToString();
        }

        private static string GetLoopResult(object[] parameters, string loopArgumentString)
        {
            if (parameters.Length < 2)
                throw new Exception("Incorrect number of parameters for function #Loop");

            var token = (parameters[parameters.Length - 1] as JUSTContext)?.Input;
            var selectedToken = token?.SelectToken(parameters[0].ToString());
            if (selectedToken == null)
                return string.Empty;

            if (selectedToken.Type != JTokenType.Array)
                throw new Exception("The JSONPath argument inside a #loop function must be an Array");

            var separator = parameters.Length == 3 ? parameters[1].ToString() : Environment.NewLine;
            var returnString = string.Empty;

            foreach (var arrToken in selectedToken.Children())
            {
                var selectedArray = selectedToken as JArray;
                var parsedRecord = Parse(loopArgumentString, token.ToString(Formatting.None), selectedArray, arrToken);

                returnString += parsedRecord.Substring(1, parsedRecord.Length - 2);
                returnString += separator;
            }

            return returnString;
        }

        private static string GetFunctionString(string input, int startIndex)
        {
            var functionString = string.Empty;
            var parenOpenCount = 0;
            var parenClosedCount = 0;

            for (var i = startIndex; i < input.Length; i++)
            {
                if (input[i] == '(')
                    parenOpenCount++;

                if (input[i] == ')')
                    parenClosedCount++;

                if (parenClosedCount <= 0 || parenClosedCount != parenOpenCount)
                    continue;

                functionString = input.Substring(startIndex, i - startIndex + 1);
                break;
            }

            return functionString;
        }

        private static object CallCustomFunction(object[] parameters)
        {
            var customParameters = new object[parameters.Length - 3];
            var functionString = string.Empty;
            var dllName = string.Empty;
            for (var i = 0; i < parameters.Length; ++i)
            {
                switch (i)
                {
                    case 0:
                        dllName = parameters[i].ToString();
                        break;

                    case 1:
                        functionString = parameters[i].ToString();
                        break;

                    default:
                        if (i != parameters.Length - 1)
                            customParameters[i - 2] = parameters[i];
                        break;
                }
            }

            var index = functionString.LastIndexOf(".", StringComparison.Ordinal);
            var className = functionString.Substring(0, index) + "," + dllName;
            var functionName = functionString.Substring(index + 1, functionString.Length - index - 1);

            return CallMethod(className, functionName, customParameters);
        }

        private static object CallMethod(string theClass, string theMethod, object[] parameters) =>
            ReflectionHelper.caller(null, theClass, theMethod, parameters, true, new JUSTContext());
    }
}