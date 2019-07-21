// ReSharper disable UnusedMember.Global
// ReSharper disable StringLiteralTypo

namespace JUST
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public static class JsonTransformer
    {
        public static readonly JUSTContext GlobalContext = new JUSTContext();

        static JsonTransformer()
        {
            if (JsonConvert.DefaultSettings == null)
                JsonConvert.DefaultSettings = () => new JsonSerializerSettings
                                                    {
                                                        DateParseHandling = DateParseHandling.None
                                                    };
        }

        public static string Transform(string transformerJson, string inputJson, JUSTContext localContext = null)
        {
            JToken result = null;

            var transformerToken = JsonConvert.DeserializeObject<JToken>(transformerJson);
            switch (transformerToken.Type)
            {
                case JTokenType.Object:
                    result = Transform(transformerToken as JObject, inputJson, localContext);
                    break;

                case JTokenType.Array:
                    result = Transform(transformerToken as JArray, inputJson, localContext);
                    break;

                case JTokenType.None:
                case JTokenType.Constructor:
                case JTokenType.Property:
                case JTokenType.Comment:
                case JTokenType.Integer:
                case JTokenType.Float:
                case JTokenType.String:
                case JTokenType.Boolean:
                case JTokenType.Null:
                case JTokenType.Undefined:
                case JTokenType.Date:
                case JTokenType.Raw:
                case JTokenType.Bytes:
                case JTokenType.Guid:
                case JTokenType.Uri:
                case JTokenType.TimeSpan:
                    throw new NotSupportedException($"Transformer of type '{transformerToken.Type}' not supported!");
            }

            return JsonConvert.SerializeObject(result);
        }

        public static JArray Transform(JArray transformerArray, string input, JUSTContext localContext = null)
        {
            var result = new JArray();
            var items = transformerArray.ToArray();

            var itemsLength = items.Length;
            for (var i = 0; i < itemsLength; i++)
            {
                var transformer = items[i];
                if (transformer.Type != JTokenType.Object)
                    throw new NotSupportedException($"Transformer of type '{transformer.Type}' not supported!");

                Transform(transformer as JObject, input, localContext);
                result.Add(transformer);
            }

            return result;
        }

        public static JObject Transform(JObject transformer, JObject input, JUSTContext localContext = null)
        {
            var inputJson = JsonConvert.SerializeObject(input);
            return Transform(transformer, inputJson, localContext);
        }

        public static JObject Transform(JObject transformer, string input, JUSTContext localContext = null)
        {
            (localContext ?? GlobalContext).Input = JsonConvert.DeserializeObject<JToken>(input);
            RecursiveEvaluate(transformer, input, null, null, localContext);
            return transformer;
        }

        #region RecursiveEvaluate

        private static void RecursiveEvaluate(JToken parentToken,
                                              string inputJson,
                                              JArray parentArray,
                                              JToken currentArrayToken,
                                              JUSTContext localContext)
        {
            if (parentToken == null)
                return;

            var tokens = parentToken.Children().ToArray();

            List<JToken> selectedTokens = null;
            Dictionary<string, JToken> tokensToReplace = null;
            List<JToken> tokensToDelete = null;
            List<string> loopProperties = null;
            JArray arrayToForm = null;
            List<JToken> tokenToForm = null;
            List<JToken> tokensToAdd = null;
            var tokensLength = tokens.Length;

            for (var i = 0; i < tokensLength; i++)
            {
                var childToken = tokens[i];
                if (childToken.Type == JTokenType.Array && (parentToken as JProperty).Name.Trim() != "#")
                {
                    var itemsToAdd = new List<object>();
                    var childTokens = childToken.Children().ToArray();
                    var childTokensLength = childTokens.Length;
                    for (var j = 0; j < childTokensLength; j++)
                    {
                        var arrEl = childTokens[j];
                        object itemToAdd = arrEl.Value<JToken>();

                        if (arrEl.Type == JTokenType.String && arrEl.ToString().Trim().StartsWith("#"))
                            itemToAdd = ParseFunction(arrEl.ToString(), inputJson, parentArray, currentArrayToken, localContext);

                        itemsToAdd.Add(itemToAdd);
                    }

                    var arrayToken = new JArray(itemsToAdd);
                }

                var isLoop = false;
                if (childToken.Type == JTokenType.Property)
                {
                    var property = childToken as JProperty;
                    if (property.Name != null && property.Name == "#" && property.Value.Type == JTokenType.Array)
                    {
                        var values = property.Value as JArray;
                        var arrayValues = values.Children().ToArray();

                        var arrayValuesLength = arrayValues.Length;
                        for (var j = 0; j < arrayValuesLength; j++)
                        {
                            var arrayValue = arrayValues[j];

                            if (arrayValue.Type != JTokenType.String
                                || !ExpressionHelper.TryParseFunctionNameAndArguments(arrayValue.Value<string>().Trim(),
                                                                                      out var functionName,
                                                                                      out var arguments))
                            {
                                continue;
                            }

                            switch (functionName)
                            {
                                case "copy":
                                    if (selectedTokens == null)
                                        selectedTokens = new List<JToken>();

                                    selectedTokens.Add(Copy(arguments, inputJson, localContext));
                                    break;

                                case "replace":
                                    if (tokensToReplace == null)
                                        tokensToReplace = new Dictionary<string, JToken>();

                                    var replaceResult = Replace(arguments, inputJson, localContext);
                                    tokensToReplace.Add(replaceResult.Key, replaceResult.Value);
                                    break;

                                case "delete":
                                    if (tokensToDelete == null)
                                        tokensToDelete = new List<JToken>();

                                    tokensToDelete.Add(Delete(arguments, inputJson, localContext));
                                    break;
                            }
                        }
                    }

                    if (property.Name != null
                        && property.Value.ToString().Trim().StartsWith("#")
                        && !property.Name.Contains("#eval")
                        && !property.Name.Contains("#ifgroup")
                        && !property.Name.Contains("#loop"))
                    {
                        var newValue = ParseFunction(property.Value.ToString(), inputJson, parentArray, currentArrayToken, localContext);
                        property.Value = GetToken(newValue, localContext);
                    }

                    /* For looping*/
                    isLoop = false;

                    if (property.Name != null && property.Name.Contains("#eval"))
                    {
                        ExpressionHelper.TryParseFunctionNameAndArguments(property.Name, out var functionName, out var functionString);
                        var functionResult = ParseFunction(functionString, inputJson, null, null, localContext);

                        var clonedProperty = new JProperty(functionResult.ToString(), property.Value);

                        if (loopProperties == null)
                            loopProperties = new List<string>();

                        loopProperties.Add(property.Name);

                        if (tokensToAdd == null)
                            tokensToAdd = new List<JToken>
                                          {
                                              clonedProperty
                                          };
                    }

                    if (property.Name != null && property.Name.Contains("#ifgroup"))
                    {
                        ExpressionHelper.TryParseFunctionNameAndArguments(property.Name, out var functionName, out var functionString);
                        var functionResult = ParseFunction(functionString, inputJson, null, null, localContext);
                        bool result;

                        try
                        {
                            result = (bool)ReflectionHelper.GetTypedValue(typeof(bool), functionResult, GetEvaluationMode(localContext));
                        }
                        catch
                        {
                            if (IsStrictMode(localContext))
                                throw;

                            result = false;
                        }

                        if (loopProperties == null)
                            loopProperties = new List<string>();

                        if (result)
                        {
                            loopProperties.Add(property.Name);

                            RecursiveEvaluate(childToken, inputJson, parentArray, currentArrayToken, localContext);
                            var childTokens = childToken.Children().ToArray();
                            var childTokensLength = childTokens.Length;
                            if (tokenToForm == null)
                                tokenToForm = new List<JToken>();

                            for (var j = 0; j < childTokensLength; j++)
                            {
                                var grandChildToken = childTokens[j];
                                tokenToForm.Add(grandChildToken.DeepClone());
                            }
                        }
                        else
                        {
                            loopProperties.Add(property.Name);
                        }

                        isLoop = true;
                    }

                    if (property.Name != null && property.Name.Contains("#loop"))
                    {
                        var arrayTokenString = property.Name.Substring(6, property.Name.Length - 7);

                        var jsonToLoad = inputJson;
                        if (currentArrayToken != null && property.Name.Contains("#loopwithincontext"))
                        {
                            arrayTokenString = property.Name.Substring(19, property.Name.Length - 20);
                            jsonToLoad = JsonConvert.SerializeObject(currentArrayToken);
                        }

                        JToken token = JsonConvert.DeserializeObject<JObject>(jsonToLoad);
                        JToken arrayToken;
                        if (arrayTokenString.Contains("#"))
                        {
                            var hashIndex = arrayTokenString.IndexOf("#", StringComparison.Ordinal);
                            var preHashString = arrayTokenString.Substring(0, hashIndex);
                            var indexOfEndFunction = GetIndexOfFunctionEnd(arrayTokenString);
                            if (indexOfEndFunction > hashIndex && hashIndex > 0)
                            {
                                var sub2 = arrayTokenString.Substring(indexOfEndFunction + 1, arrayTokenString.Length - indexOfEndFunction - 1);
                                var functionResult = ParseFunction(arrayTokenString.Substring(hashIndex, indexOfEndFunction - hashIndex + 1),
                                                                   inputJson,
                                                                   parentArray,
                                                                   currentArrayToken,
                                                                   localContext).ToString();
                                arrayTokenString = preHashString + functionResult + sub2;
                            }
                        }

                        try
                        {
                            arrayToken = token.SelectToken(arrayTokenString);
                            if (arrayToken is JObject)
                                arrayToken = new JArray(arrayToken);
                        }
                        catch
                        {
                            var multipleTokens = token.SelectTokens(arrayTokenString);
                            arrayToken = new JArray(multipleTokens);
                        }

                        if (arrayToken == null)
                        {
                            arrayToForm = new JArray();
                        }
                        else
                        {
                            var array = (JArray)arrayToken;
                            var elements = array.ToArray();
                            if (arrayToForm == null)
                                arrayToForm = new JArray();

                            foreach (var element in elements)
                            {
                                var clonedToken = childToken.DeepClone();
                                RecursiveEvaluate(clonedToken, inputJson, array, element, localContext);
                                var clonedTokens = clonedToken.Children().ToArray();
                                foreach (var clonedTkn in clonedTokens)
                                {
                                    arrayToForm.Add(clonedTkn);
                                }
                            }
                        }

                        if (loopProperties == null)
                            loopProperties = new List<string>();

                        loopProperties.Add(property.Name);
                        isLoop = true;
                    }

                    /*End looping */
                }

                if (childToken.Type == JTokenType.String
                    && childToken.Value<string>().Trim().StartsWith("#")
                    && parentArray != null
                    && currentArrayToken != null)
                {
                    var newValue = ParseFunction(childToken.Value<string>(), inputJson, parentArray, currentArrayToken, localContext);
                    var replaceToken = GetToken(newValue, localContext);
                    childToken.Replace(replaceToken);
                }

                if (!isLoop)
                    RecursiveEvaluate(childToken, inputJson, parentArray, currentArrayToken, localContext);
            }

            if (selectedTokens != null)
            {
                foreach (var selectedToken in selectedTokens.Where(t => t != null))
                {
                    var copyChildren = selectedToken.Children().ToArray();

                    // Note: Optimized for performance (foreach is slower)
                    for (var i = 0; i < copyChildren.Length; ++i)
                    {
                        if (!(copyChildren[i] is JProperty property))
                            continue;

                        (parentToken as JObject).Add(property.Name, property.Value);
                    }
                }
            }

            if (tokensToReplace != null)
            {
                foreach (var tokenToReplace in tokensToReplace)
                {
                    var selectedToken = (parentToken as JObject).SelectToken(tokenToReplace.Key);
                    if (selectedToken != null && selectedToken is JObject jObj)
                    {
                        jObj.RemoveAll();
                        var copyChildren = tokenToReplace.Value.Children().ToArray();

                        // Note: Optimized for performance (foreach is slower)
                        for (var i = 0; i < copyChildren.Length; ++i)
                        {
                            if (!(copyChildren[i] is JProperty property))
                                continue;

                            jObj.Add(property.Name, property.Value);
                        }
                    }

                    if (!(selectedToken is JValue))
                        continue;

                    var selectedObject = selectedToken as JValue;
                    selectedObject.Value = tokenToReplace.Value.ToString();
                }
            }

            if (tokensToDelete != null)
            {
                foreach (var selectedToken in tokensToDelete.Select(tokenToDelete => tokenToDelete.Path))
                {
                    var tokenToRemove = parentToken.SelectToken(selectedToken);
                    tokenToRemove?.Ancestors().First().Remove();
                }
            }

            if (tokensToAdd != null)
            {
                // Note: Optimized for performance (foreach is slower)
                for (var i = 0; i < tokensToAdd.Count; ++i)
                {
                    if (!(tokensToAdd[i] is JProperty property))
                        continue;

                    (parentToken as JObject).Add(property.Name, property.Value);
                }
            }

            if (tokenToForm != null)
            {
                foreach (var token in tokenToForm)
                {
                    var childTokens = token.Children().ToArray();

                    // Note: Optimized for performance (foreach is slower)
                    for (var i = 0; i < childTokens.Length; ++i)
                    {
                        if (!(childTokens[i] is JProperty property))
                            continue;

                        (parentToken as JObject).Add(property.Name, property.Value);
                    }
                }
            }

            (parentToken as JObject)?.Remove("#");

            if (loopProperties != null)
            {
                for (var i = 0; i < loopProperties.Count; i++)
                {
                    var propertyToDelete = loopProperties[i];
                    (parentToken as JObject).Remove(propertyToDelete);
                }
            }

            if (arrayToForm != null)
                parentToken.Replace(arrayToForm);
        }

        #endregion

        private static JToken GetToken(object newValue, JUSTContext localContext)
        {
            if (newValue == null)
                return JValue.CreateNull();

            if (newValue is JToken token)
                return token;

            try
            {
                if (newValue is IEnumerable<object> newArrayay)
                    return new JArray(newArrayay);

                return new JValue(newValue);
            }
            catch
            {
                if (IsStrictMode(localContext))
                    throw;

                if (IsFallbackToDefault(localContext))
                    return JValue.CreateNull();
            }

            return null;
        }

        #region Copy

        private static JToken Copy(string argument, string inputJson, JUSTContext localContext)
        {
            if (!(ParseArgument(inputJson, null, null, argument, localContext) is string jsonPath))
                throw new ArgumentException("Invalid jsonPath for #copy!");

            return GetInputToken(localContext).SelectToken(jsonPath);
        }

        #endregion

        #region Replace

        private static KeyValuePair<string, JToken> Replace(string arguments, string inputJson, JUSTContext localContext)
        {
            var argumentArr = ExpressionHelper.GetArguments(arguments);
            if (argumentArr.Length < 2)
                throw new Exception("Function #replace needs two arguments - 1. jsonPath to be replaced, 2. token to replace with.");

            if (!(ParseArgument(inputJson, null, null, argumentArr[0], localContext) is string key))
                throw new ArgumentException("Invalid jsonPath for #replace!");

            var str = ParseArgument(inputJson, null, null, argumentArr[1], localContext);
            var newToken = GetToken(str, localContext);
            return new KeyValuePair<string, JToken>(key, newToken);
        }

        #endregion

        #region Delete

        private static string Delete(string argument, string inputJson, JUSTContext localContext)
        {
            if (!(ParseArgument(inputJson, null, null, argument, localContext) is string result))
                throw new ArgumentException("Invalid jsonPath for #delete!");

            return result;
        }

        #endregion

        #region ParseFunction

        private static object ParseFunction(string functionString,
                                            string inputJson,
                                            JArray array,
                                            JToken currentArrayElement,
                                            JUSTContext localContext)
        {
            try
            {
                object output;

                if (!ExpressionHelper.TryParseFunctionNameAndArguments(functionString, out var functionName, out var argumentString))
                    return functionName;

                var arguments = ExpressionHelper.GetArguments(argumentString);
                var listParameters = new List<object>();

                if (functionName == "ifcondition")
                {
                    var condition = ParseArgument(inputJson, array, currentArrayElement, arguments[0], localContext);
                    var value = ParseArgument(inputJson, array, currentArrayElement, arguments[1], localContext);
                    var index = string.Equals(condition.ToString(), value.ToString(), StringComparison.CurrentCultureIgnoreCase) ? 2 : 3;
                    output = ParseArgument(inputJson, array, currentArrayElement, arguments[index], localContext);
                }
                else
                {
                    listParameters.AddRange(arguments.Select(argument => ParseArgument(inputJson,
                                                                                       array,
                                                                                       currentArrayElement,
                                                                                       argument,
                                                                                       localContext)));
                    listParameters.Add(localContext ?? GlobalContext);
                    var parameters = listParameters.ToArray();

                    switch (functionName)
                    {
                        case "currentvalue":
                        case "currentindex":
                        case "lastindex":
                        case "lastvalue":
                            output = ReflectionHelper.caller(null,
                                                             "JUST.Transformer",
                                                             functionName,
                                                             new object[] { array, currentArrayElement },
                                                             true,
                                                             localContext ?? GlobalContext);
                            break;

                        case "currentvalueatpath":
                        case "lastvalueatpath":
                            output = ReflectionHelper.caller(null,
                                                             "JUST.Transformer",
                                                             functionName,
                                                             new object[] { array, currentArrayElement, arguments[0] },
                                                             true,
                                                             localContext ?? GlobalContext);
                            break;

                        case "customfunction":
                            output = CallCustomFunction(parameters, localContext);
                            break;

                        default:
                        {
                            if (localContext?.IsRegisteredCustomFunction(functionName) ?? false)
                            {
                                var methodInfo = localContext.GetCustomMethod(functionName);
                                output = ReflectionHelper.InvokeCustomMethod(methodInfo, parameters, true, localContext);
                            }
                            else if (GlobalContext.IsRegisteredCustomFunction(functionName))
                            {
                                var methodInfo = GlobalContext.GetCustomMethod(functionName);
                                output = ReflectionHelper.InvokeCustomMethod(methodInfo, parameters, true, localContext ?? GlobalContext);
                            }
                            else if (ReflectionHelper.ExternalAssemblyRegex.IsMatch(functionName))
                            {
                                output = ReflectionHelper.CallExternalAssembly(functionName, parameters, localContext ?? GlobalContext);
                            }
                            else if (functionName == "xconcat"
                                     || functionName == "xadd"
                                     || functionName == "mathequals"
                                     || functionName == "mathgreaterthan"
                                     || functionName == "mathlessthan"
                                     || functionName == "mathgreaterthanorequalto"
                                     || functionName == "mathlessthanorequalto"
                                     || functionName == "stringcontains"
                                     || functionName == "stringequals")
                            {
                                var oParams = new object[1];
                                oParams[0] = parameters;
                                output = ReflectionHelper.caller(null,
                                                                 "JUST.Transformer",
                                                                 functionName,
                                                                 oParams,
                                                                 true,
                                                                 localContext ?? GlobalContext);
                            }
                            else
                            {
                                var input = ((JUSTContext)parameters.Last()).Input;
                                if (currentArrayElement != null && functionName != "valueof")
                                    ((JUSTContext)parameters.Last()).Input = JsonConvert.SerializeObject(currentArrayElement);

                                output = ReflectionHelper.caller(null,
                                                                 "JUST.Transformer",
                                                                 functionName,
                                                                 parameters,
                                                                 true,
                                                                 localContext ?? GlobalContext);
                                ((JUSTContext)parameters.Last()).Input = input;
                            }

                            break;
                        }
                    }
                }

                return output;
            }
            catch (Exception ex)
            {
                throw new Exception("Error while calling function : " + functionString + " - " + ex.Message, ex);
            }
        }

        private static object ParseArgument(string inputJson,
                                            JArray array,
                                            JToken currentArrayElement,
                                            string argument,
                                            JUSTContext localContext)
        {
            var trimmedArgument = argument;

            if (argument.Contains("#"))
                trimmedArgument = argument.Trim();

            return trimmedArgument.StartsWith("#")
                       ? ParseFunction(trimmedArgument, inputJson, array, currentArrayElement, localContext)
                       : trimmedArgument;
        }

        private static object CallCustomFunction(object[] parameters, JUSTContext localContext)
        {
            var customParameters = new object[parameters.Length - 3];
            var functionString = string.Empty;
            var dllName = string.Empty;
            var i = 0;
            var parametersLength = parameters.Length;
            for (var j = 0; j < parametersLength; j++)
            {
                var parameter = parameters[j];

                if (i == 0)
                    dllName = parameter.ToString();
                else if (i == 1)
                    functionString = parameter.ToString();
                else if (i != parameters.Length - 1)
                    customParameters[i - 2] = parameter;

                i++;
            }

            var index = functionString.LastIndexOf(".", StringComparison.Ordinal);
            var className = functionString.Substring(0, index) + "," + dllName;
            var functionName = functionString.Substring(index + 1, functionString.Length - index - 1);

            return ReflectionHelper.caller(null, className, functionName, customParameters, true, localContext ?? GlobalContext);
        }

        #endregion

        private static int GetIndexOfFunctionEnd(string totalString)
        {
            var index = -1;
            var startIndex = totalString.IndexOf("#", StringComparison.Ordinal);
            var startBracketCount = 0;
            var endBracketCount = 0;
            for (var i = startIndex; i < totalString.Length; ++i)
            {
                if (totalString[i] == '(')
                    startBracketCount++;
                else if (totalString[i] == ')')
                    endBracketCount++;

                if (endBracketCount != startBracketCount || endBracketCount == 0)
                    continue;

                index = i;
                break;
            }

            return index;
        }

        private static JToken GetInputToken(JUSTContext localContext) => localContext?.Input ?? GlobalContext.Input;

        private static EvaluationMode GetEvaluationMode(JUSTContext localContext) =>
            localContext?.EvaluationMode ?? GlobalContext.EvaluationMode;

        private static bool IsStrictMode(JUSTContext localContext) => GetEvaluationMode(localContext) == EvaluationMode.Strict;

        private static bool IsFallbackToDefault(JUSTContext localContext) =>
            GetEvaluationMode(localContext) == EvaluationMode.FallbackToDefault;
    }
}