// ReSharper disable UnusedMember.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable ForCanBeConvertedToForeach

namespace JUST
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public static class JsonTransformer
    {
        #region Public Fields

        public static readonly JUSTContext GlobalContext = new JUSTContext();

        #endregion Public Fields

        #region Public Constructors

        static JsonTransformer()
        {
            if (JsonConvert.DefaultSettings == null)
                JsonConvert.DefaultSettings = () => new JsonSerializerSettings
                                                    {
                                                        DateParseHandling = DateParseHandling.None
                                                    };
        }

        #endregion Public Constructors

        #region Public Methods

        public static string Transform(string transformerJson, string inputJson, JUSTContext localContext = null)
        {
            var transformerToken = JsonConvert.DeserializeObject<JToken>(transformerJson);
            return Transform(transformerToken, inputJson, localContext);
        }

        public static string Transform(JToken transformerToken, string inputJson, JUSTContext localContext = null)
        {
            JToken result;

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

                default:
                    throw new ArgumentOutOfRangeException();
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

        public static JObject Transform(JObject transformer, JToken input, JUSTContext localContext = null)
        {
            (localContext ?? GlobalContext).Input = input;
            var inputJson = JsonConvert.SerializeObject(input);
            RecursiveEvaluate(transformer, inputJson, null, null, localContext);
            return transformer;
        }

        public static JObject Transform(JObject transformerToken, string inputJson, JUSTContext localContext = null)
        {
            var transformerClone = transformerToken.DeepClone() as JObject;
            (localContext ?? GlobalContext).Input = JsonConvert.DeserializeObject<JToken>(inputJson);
            RecursiveEvaluate(transformerClone, inputJson, null, null, localContext);
            return transformerClone;
        }

        #endregion Public Methods

        #region Private Methods

        private static void AddTokens(JToken parentToken, List<JToken> tokensToAdd)
        {
            // Note: Optimized for performance (foreach is slower)
            for (var i = 0; i < tokensToAdd.Count; ++i)
            {
                if (!(tokensToAdd[i] is JProperty property))
                    continue;

                (parentToken as JObject)?.Add(property.Name, property.Value);
            }
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

        private static JToken Copy(string argument, string inputJson, JUSTContext localContext)
        {
            if (!(ParseArgument(inputJson, null, null, argument, localContext) is string jsonPath))
                throw new ArgumentException("Invalid jsonPath for #copy!");

            return GetInputToken(localContext).SelectToken(jsonPath);
        }

        private static void CopyTokens(JToken parentToken, List<JToken> tokensToCopy)
        {
            // Note: Optimized for performance (foreach is slower)
            for (var i = 0; i < tokensToCopy.Count; ++i)
            {
                if (tokensToCopy[i] == null)
                    continue;

                var copyChildren = tokensToCopy[i].Children().ToArray();

                // Note: Optimized for performance (foreach is slower)
                for (var j = 0; j < copyChildren.Length; ++j)
                {
                    if (!(copyChildren[j] is JProperty property))
                        continue;

                    (parentToken as JObject)?.Add(property.Name, property.Value);
                }
            }
        }

        private static string Delete(string argument, string inputJson, JUSTContext localContext)
        {
            if (!(ParseArgument(inputJson, null, null, argument, localContext) is string result))
                throw new ArgumentException("Invalid jsonPath for #delete!");

            return result;
        }

        private static void DeleteProperties(JToken parentToken, List<string> loopProperties)
        {
            // Note: Optimized for performance (foreach is slower)
            for (var i = 0; i < loopProperties.Count; i++)
            {
                var propertyToDelete = loopProperties[i];
                (parentToken as JObject)?.Remove(propertyToDelete);
            }
        }

        private static void DeleteTokens(JToken parentToken, List<JToken> tokensToDelete)
        {
            foreach (var selectedToken in tokensToDelete.Select(tokenToDelete => tokenToDelete.Path))
            {
                var tokenToRemove = parentToken.SelectToken(selectedToken);
                tokenToRemove?.Ancestors().First().Remove();
            }
        }

        private static void FormTokens(JToken parentToken, List<JToken> tokensToForm)
        {
            // Note: Optimized for performance (foreach is slower)
            for (var i = 0; i < tokensToForm.Count; ++i)
            {
                var childTokens = tokensToForm[i].Children().ToArray();
                for (var j = 0; j < childTokens.Length; ++j)
                {
                    if (!(childTokens[j] is JProperty property))
                        continue;

                    (parentToken as JObject)?.Add(property.Name, property.Value);
                }
            }
        }

        private static EvaluationMode GetEvaluationMode(JUSTContext localContext) =>
            localContext?.EvaluationMode ?? GlobalContext.EvaluationMode;

        private static int GetIndexOfFunctionEnd(string totalString)
        {
            var index = -1;
            var startIndex = totalString.IndexOf("#", StringComparison.Ordinal);
            var startParenCount = 0;
            var endParenCount = 0;
            for (var i = startIndex; i < totalString.Length; ++i)
            {
                if (totalString[i] == '(')
                    startParenCount++;
                else if (totalString[i] == ')')
                    endParenCount++;

                if (endParenCount != startParenCount || endParenCount == 0)
                    continue;

                index = i;
                break;
            }

            return index;
        }

        private static JToken GetInputToken(JUSTContext localContext) => localContext?.Input ?? GlobalContext.Input;

        private static JToken GetToken(object newValue, JUSTContext localContext)
        {
            if (newValue == null)
                return JValue.CreateNull();

            if (newValue is JToken token)
                return token;

            try
            {
                if (newValue is IEnumerable<object> newArray)
                    return new JArray(newArray);

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

        private static bool IsFallbackToDefault(JUSTContext localContext) =>
            GetEvaluationMode(localContext) == EvaluationMode.FallbackToDefault;

        private static bool IsStrictMode(JUSTContext localContext) => GetEvaluationMode(localContext) == EvaluationMode.Strict;

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
                                else if (functionName.IndexOf("::", StringComparison.Ordinal) != -1 && ReflectionHelper.ExternalAssemblyRegex.IsMatch(functionName))
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
                                        ((JUSTContext)parameters.Last()).Input = currentArrayElement;

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

        private static void ProcessArray(string inputJson,
                                         JUSTContext localContext,
                                         JProperty property,
                                         ref List<JToken> tokensToCopy,
                                         ref Dictionary<string, JToken> tokensToReplace,
                                         ref List<JToken> tokensToDelete)
        {
            if (!(property.Value is JArray values)) return;

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
                        if (tokensToCopy == null)
                            tokensToCopy = new List<JToken>();

                        tokensToCopy.Add(Copy(arguments, inputJson, localContext));
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

        private static void ProcessEval(string inputJson,
                                        JUSTContext localContext,
                                        JProperty property,
                                        ref List<string> loopProperties,
                                        ref List<JToken> tokensToAdd)
        {
            ExpressionHelper.TryParseFunctionNameAndArguments(property.Name, out var functionName, out var functionString);
            var functionResult = ParseFunction(functionString, inputJson, null, null, localContext);

            var clonedProperty = new JProperty(functionResult.ToString(), property.Value);

            if (loopProperties == null)
                loopProperties = new List<string>();

            loopProperties.Add(property.Name);

            if (tokensToAdd == null)
                tokensToAdd = new List<JToken> { clonedProperty };
        }

        private static void ProcessGroup(string inputJson,
                                         JArray parentArray,
                                         JToken currentArrayToken,
                                         JUSTContext localContext,
                                         JProperty property,
                                         ref List<string> loopProperties,
                                         JToken childToken,
                                         ref List<JToken> tokensToForm)
        {
            ExpressionHelper.TryParseFunctionNameAndArguments(property.Name, out var functionName, out var functionString);
            var functionResult = ParseFunction(functionString, inputJson, parentArray, currentArrayToken, localContext);
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
                if (tokensToForm == null)
                    tokensToForm = new List<JToken>();

                for (var j = 0; j < childTokensLength; j++)
                {
                    var grandChildToken = childTokens[j];
                    tokensToForm.Add(grandChildToken.DeepClone());
                }
            }
            else
            {
                loopProperties.Add(property.Name);
            }
        }

        private static void ProcessLoop(string inputJson,
                                        JArray parentArray,
                                        JToken currentArrayToken,
                                        JUSTContext localContext,
                                        JProperty property,
                                        ref JArray arrayToForm,
                                        JToken childToken,
                                        ref List<string> loopProperties)
        {
            ExpressionHelper.TryParseFunctionNameAndArguments(property.Name, out var functionName, out var arguments);
            var token = currentArrayToken != null && functionName == "loopwithincontext" ? currentArrayToken : JsonConvert.DeserializeObject<JToken>(inputJson);
            var arrayTokenString = ParseArgument(inputJson, parentArray, currentArrayToken, arguments, localContext) as string;

            JToken arrayToken;
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

                // Note: Optimized for performance (foreach is slower)
                for (var j = 0; j < elements.Length; ++j)
                {
                    var element = elements[j];
                    var clonedChildToken = childToken.DeepClone();
                    RecursiveEvaluate(clonedChildToken, inputJson, array, element, localContext);
                    var clonedChildChildren = clonedChildToken.Children();
                    var grandChildrenCount = clonedChildChildren.Count();
                    foreach (var grandChild in clonedChildChildren)
                    {
                        arrayToForm.Add(grandChild);
                    }
                }
            }

            if (loopProperties == null)
                loopProperties = new List<string>();

            loopProperties.Add(property.Name);
        }

        private static void RecursiveEvaluate(JToken parentToken, string inputJson,
                                              JArray parentArray,
                                              JToken currentArrayToken,
                                              JUSTContext localContext)
        {
            if (parentToken == null)
                return;
            
            List<JToken> tokensToAdd = null;
            List<JToken> tokensToCopy = null;
            List<JToken> tokensToDelete = null;
            List<JToken> tokensToForm = null;
            Dictionary<string, JToken> tokensToReplace = null;

            List<string> loopProperties = null;
            JArray arrayToForm = null;
            
            var tokens = parentToken.Children().ToArray();
            var tokensLength = tokens.Length;

            // Note: Optimized for performance (foreach is slower)
            for (var i = 0; i < tokensLength; i++)
            {
                var childToken = tokens[i];
                if (childToken.Type == JTokenType.Array && (parentToken as JProperty)?.Name.Trim() != "#")
                {
                    var arrayToken = childToken as JArray;
                    var itemsToAdd = new List<object>();
                    object itemToAdd;
                    var childTokens = childToken.Children().ToArray();
                    var childTokensLength = childTokens.Length;
                    for (var j = 0; j < childTokensLength; j++)
                    {
                        var arrEl = childTokens[j];
                        itemToAdd = arrEl.Value<JToken>();

                        if (arrEl.Type == JTokenType.String && arrEl.ToString().Trim().StartsWith("#"))
                            itemToAdd = ParseFunction(arrEl.ToString(), inputJson, parentArray, currentArrayToken, localContext);

                        itemsToAdd.Add(itemToAdd);
                    }

                    arrayToken.RemoveAll();
                    for (var j = 0; j < itemsToAdd.Count; ++j)
                    {
                        itemToAdd = itemsToAdd[j];
                        if (itemToAdd is Array items)
                        {
                            foreach (var item in items)
                            {
                                arrayToken.Add(Utilities.GetNestedData(item));
                            }
                        }
                        else
                        {
                            arrayToken.Add(JToken.FromObject(itemToAdd));
                        }
                    }
                }
                
                var isLoop = false;
                if (childToken.Type == JTokenType.Property)
                {
                    var property = childToken as JProperty;
                    if (property.Name != null)
                    {
                        if (property.Name == "#" && property.Value.Type == JTokenType.Array)
                            ProcessArray(inputJson, localContext, property, ref tokensToCopy, ref tokensToReplace,
                                         ref tokensToDelete);

                        if (property.Value.ToString().Trim().StartsWith("#")
                            && !property.Name.Contains("#eval")
                            && !property.Name.Contains("#ifgroup")
                            && !property.Name.Contains("#loop"))
                        {
                            var newValue = ParseFunction(property.Value.ToString(), inputJson, parentArray,
                                                         currentArrayToken, localContext);
                            property.Value = GetToken(newValue, localContext);
                        }

                        /* For looping*/
                        isLoop = false;

                        if (property.Name.Contains("#eval"))
                            ProcessEval(inputJson, localContext, property, ref loopProperties, ref tokensToAdd);

                        if (property.Name.Contains("#ifgroup"))
                        {
                            ProcessGroup(inputJson, parentArray, currentArrayToken, localContext, property,
                                         ref loopProperties, childToken, ref tokensToForm);
                            isLoop = true;
                        }

                        if (property.Name.Contains("#loop"))
                        {
                            ProcessLoop(inputJson, parentArray, currentArrayToken, localContext, property,
                                        ref arrayToForm, childToken, ref loopProperties);
                            isLoop = true;
                        }

                        /*End looping */
                    }
                }

                if (childToken.Type == JTokenType.String
                    && childToken.Value<string>().Trim().StartsWith("#")
                    && parentArray != null
                    && currentArrayToken != null)
                {
                    var newValue = ParseFunction(childToken.Value<string>(), inputJson, parentArray, currentArrayToken, localContext);
                    childToken.Replace(GetToken(newValue, localContext));
                }

                if (!isLoop)
                    RecursiveEvaluate(childToken, inputJson, parentArray, currentArrayToken, localContext);
            }

            if (tokensToCopy != null)
                CopyTokens(parentToken, tokensToCopy);

            if (tokensToReplace != null)
                ReplaceTokens(parentToken, tokensToReplace);

            if (tokensToDelete != null)
                DeleteTokens(parentToken, tokensToDelete);

            if (tokensToAdd != null)
                AddTokens(parentToken, tokensToAdd);

            if (tokensToForm != null)
                FormTokens(parentToken, tokensToForm);

            (parentToken as JObject)?.Remove("#");

            if (loopProperties != null)
                DeleteProperties(parentToken, loopProperties);

            if (arrayToForm != null)
                parentToken.Replace(arrayToForm);
        }
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

        private static void ReplaceTokens(JToken parentToken, Dictionary<string, JToken> tokensToReplace)
        {
            foreach (var tokenToReplace in tokensToReplace)
            {
                var selectedToken = (parentToken as JObject)?.SelectToken(tokenToReplace.Key);
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

        #endregion Private Methods
    }
}