// ReSharper disable UnusedMember.Global
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

namespace JUST
{
    using System;
    using System.Collections;
    using System.Linq;

    using Newtonsoft.Json.Linq;

    internal class Transformer
    {
        public static object valueof(string jsonPath, JUSTContext context)
        {
            var token = context.Input;
            var selectedToken = token.SelectToken(jsonPath);
            return GetValue(selectedToken);
        }

        public static bool exists(string jsonPath, JUSTContext context)
        {
            var token = context.Input;
            return token.SelectToken(jsonPath) != null;
        }

        public static bool existsandnotempty(string jsonPath, JUSTContext context)
        {
            var token = context.Input;
            var selectedToken = token.SelectToken(jsonPath);
            return selectedToken != null && selectedToken.ToString().Trim() != string.Empty;
        }

        public static object ifcondition(object condition, object value, object trueResult, object falseResult, JUSTContext context)
        {
            return string.Equals(condition.ToString(), value.ToString(), StringComparison.OrdinalIgnoreCase) ? trueResult : falseResult;
        }

        #region string functions

        public static string concat(string string1, string string2, JUSTContext context)
        {
            return string1 != null ? string1 + (string2 ?? string.Empty) : string2;
        }

        public static string substring(string stringRef, int startIndex, int length, JUSTContext context)
        {
            try
            {
                return stringRef.Substring(startIndex, length);
            }
            catch
            {
                return null;
            }
        }

        public static int firstindexof(string stringRef, string searchString, JUSTContext context) =>
            stringRef.IndexOf(searchString, 0, StringComparison.Ordinal);

        public static int lastindexof(string stringRef, string searchString, JUSTContext context) =>
            stringRef.LastIndexOf(searchString, StringComparison.Ordinal);

        public static string concatall(JArray parsedArray, JUSTContext context) =>
            parsedArray?.Children().Aggregate(string.Empty, (current, token) => current + token);

        public static string concatallatpath(JArray parsedArray, string jsonPath, JUSTContext context)
        {
            if (parsedArray == null)
                return null;

            var result = string.Empty;
            foreach (var token in parsedArray.Children())
            {
                result += token.SelectToken(jsonPath).ToString();
            }

            return result;
        }

        #endregion

        #region math functions

        public static object add(decimal num1, decimal num2, JUSTContext context) => TypedNumber(num1 + num2);

        public static object subtract(decimal num1, decimal num2, JUSTContext context) => TypedNumber(num1 - num2);

        public static object multiply(decimal num1, decimal num2, JUSTContext context) => TypedNumber(num1 * num2);

        public static object divide(decimal num1, decimal num2, JUSTContext context) => TypedNumber(num1 / num2);

        private static object TypedNumber(decimal number) => number * 10 % 10 == 0 ? (object)Convert.ToInt32(number) : number;

        #endregion

        #region aggregate functions

        public static object sum(JArray parsedArray, JUSTContext context) =>
            TypedNumber(parsedArray?.Children().Sum(token => Convert.ToDecimal(token.ToString())) ?? 0);

        public static object sumatpath(JArray parsedArray, string jsonPath, JUSTContext context) =>
            TypedNumber(parsedArray?.Children().Sum(token => Convert.ToDecimal(token.SelectToken(jsonPath).ToString())) ?? 0);

        public static object average(JArray parsedArray, JUSTContext context)
        {
            if (parsedArray == null || parsedArray.Count == 0)
                return TypedNumber(0);

            var result = parsedArray.Children().Sum(token => Convert.ToDecimal(token.ToString()));
            return TypedNumber(result / parsedArray.Count);
        }

        public static object averageatpath(JArray parsedArray, string jsonPath, JUSTContext context)
        {
            if (parsedArray == null || parsedArray.Count == 0)
                return TypedNumber(0);

            var result = parsedArray.Children().Sum(token => Convert.ToDecimal(token.SelectToken(jsonPath).ToString()));
            return TypedNumber(result / parsedArray.Count);
        }

        public static object max(JArray parsedArray, JUSTContext context) =>
            TypedNumber(parsedArray?.Children().Select(token => Convert.ToDecimal(token.ToString())).Concat(new decimal[] { 0 }).Max()
                        ?? 0);

        public static object maxatpath(JArray parsedArray, string jsonPath, JUSTContext context) =>
            TypedNumber(parsedArray?.Children().Select(token => Convert.ToDecimal(token.SelectToken(jsonPath).ToString()))
                                   .Concat(new decimal[] { 0 }).Max()
                        ?? 0);

        public static object min(JArray parsedArray, JUSTContext context) =>
            TypedNumber(parsedArray?.Children().Select(token => Convert.ToDecimal(token.ToString())).Concat(new[] { decimal.MaxValue })
                                   .Min()
                        ?? decimal.MaxValue);

        public static object minatpath(JArray parsedArray, string jsonPath, JUSTContext context)
        {
            return TypedNumber(parsedArray?.Children().Select(token => Convert.ToDecimal(token.SelectToken(jsonPath).ToString()))
                                          .Concat(new[] { decimal.MaxValue }).Min()
                               ?? decimal.MaxValue);
        }

        public static int arraylength(string array, JUSTContext context) => JArray.Parse(array).Count;

        #endregion

        #region arraylooping

        public static object currentvalue(JArray array, JToken currentElement) => GetValue(currentElement);

        public static int currentindex(JArray array, JToken currentElement) => array.IndexOf(currentElement);

        public static object lastvalue(JArray array, JToken currentElement) => GetValue(array.Last);

        public static int lastindex(JArray array, JToken currentElement) => array.Count - 1;

        public static object currentvalueatpath(JArray array, JToken currentElement, string jsonPath) =>
            GetValue(currentElement.SelectToken(jsonPath));

        public static object lastvalueatpath(JArray array, JToken currentElement, string jsonPath) =>
            GetValue(array.Last.SelectToken(jsonPath));

        #endregion

        #region Constants

        public static string constant_comma(string none, JUSTContext context) => ",";

        public static string constant_hash(string none, JUSTContext context) => "#";

        #endregion

        #region Variable parameter functions

        public static string xconcat(object[] list)
        {
            var result = string.Empty;
            foreach (var item in list)
            {
                if (item != null)
                    result += item;
            }

            return result;
        }

        public static object xadd(object[] list)
        {
            var context = list[list.Length - 1] as JUSTContext;
            var evaluationMode = context?.EvaluationMode ?? EvaluationMode.FallbackToDefault;
            decimal add = 0;
            foreach (var item in list)
            {
                if (item != null)
                    add += (decimal)ReflectionHelper.GetTypedValue(typeof(decimal), item, evaluationMode);
            }

            return TypedNumber(add);
        }

        #endregion

        public static object GetValue(JToken selectedToken)
        {
            if (selectedToken == null)
                return null;

            switch (selectedToken.Type)
            {
                case JTokenType.Object:
                    return selectedToken; // JsonConvert.SerializeObject(selectedToken);

                case JTokenType.Array:
                    return selectedToken.Values<object>().ToArray(); //selectedToken.ToString();

                case JTokenType.Integer:
                    return selectedToken.ToObject<long>();

                case JTokenType.Float:
                    return selectedToken.ToObject<float>();

                case JTokenType.String:
                    return selectedToken.ToString();

                case JTokenType.Boolean:
                    return selectedToken.ToObject<bool>();

                case JTokenType.Date:
                    var value = Convert.ToDateTime(selectedToken.Value<DateTime>());
                    return value.ToString("yyyy-MM-ddTHH:mm:ssZ");

                case JTokenType.Raw:
                case JTokenType.Bytes:
                case JTokenType.Guid:
                case JTokenType.Uri:
                case JTokenType.TimeSpan:
                case JTokenType.Undefined:
                case JTokenType.Constructor:
                case JTokenType.Property:
                case JTokenType.Comment:
                case JTokenType.Null:
                case JTokenType.None:
                    return null;

                default:
                    return null;
            }
        }

        #region grouparrayby

        public static JArray grouparrayby(string jsonPath, string groupingElement, string groupedElement, JUSTContext context)
        {
            var arr = (JArray)context.Input.SelectToken(jsonPath);
            if (!groupingElement.Contains(":"))
                return Utilities.CreateGroupArray(arr, groupingElement, groupedElement);

            var groupingElements = groupingElement.Split(':');
            return Utilities.GroupArrayMultipleProperties(arr, groupingElements, groupedElement);
        }

        #endregion

        #region operators

        public static bool stringequals(object[] list) => list.Length >= 2 && list[0].ToString().Equals(list[1].ToString());

        public static bool stringcontains(object[] list) => list.Length >= 2 && list[0].ToString().Contains(list[1].ToString());

        public static bool mathequals(object[] list)
        {
            var (lshDecimal, rhsDecimal, isValid) = GetOperands(list);
            return isValid && lshDecimal == rhsDecimal;
        }

        public static bool mathgreaterthan(object[] list)
        {
            var (lshDecimal, rhsDecimal, isValid) = GetOperands(list);
            return isValid && lshDecimal > rhsDecimal;
        }

        public static bool mathlessthan(object[] list)
        {
            var (lshDecimal, rhsDecimal, isValid) = GetOperands(list);
            return isValid && lshDecimal < rhsDecimal;
        }

        public static bool mathgreaterthanorequalto(object[] list)
        {
            var (lshDecimal, rhsDecimal, isValid) = GetOperands(list);
            return isValid && lshDecimal >= rhsDecimal;
        }

        public static bool mathlessthanorequalto(object[] list)
        {
            var (lshDecimal, rhsDecimal, isValid) = GetOperands(list);
            return isValid && lshDecimal <= rhsDecimal;
        }

        private static (decimal lshDecimal, decimal rhsDecimal, bool isValid) GetOperands(object[] list)
        {
            if (list.Length < 2)
                return (0, 0, false);

            var lhs = (decimal)ReflectionHelper.GetTypedValue(typeof(decimal), list[0], EvaluationMode.Strict);
            var rhs = (decimal)ReflectionHelper.GetTypedValue(typeof(decimal), list[1], EvaluationMode.Strict);

            return (lhs, rhs, true);
        }

        #endregion

        public static object tointeger(object val, JUSTContext context) =>
            ReflectionHelper.GetTypedValue(typeof(int), val, context.EvaluationMode);

        public static object tostring(object val, JUSTContext context) =>
            ReflectionHelper.GetTypedValue(typeof(string), val, context.EvaluationMode);

        public static object toboolean(object val, JUSTContext context) =>
            ReflectionHelper.GetTypedValue(typeof(bool), val, context.EvaluationMode);

        public static decimal todecimal(object val, JUSTContext context) =>
            decimal.Round((decimal)ReflectionHelper.GetTypedValue(typeof(decimal), val, context.EvaluationMode),
                          context.DefaultDecimalPlaces);

        public static decimal round(decimal val, int decimalPlaces, JUSTContext context) =>
            decimal.Round(val, decimalPlaces, MidpointRounding.AwayFromZero);
        
        public static int length(object val, JUSTContext context)
        {
            var result = 0;
            if (val is IEnumerable enumerable)
            {
                var enumerator = enumerable.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    result++;
                }
            }
            else
            {
                if (context.EvaluationMode == EvaluationMode.Strict)
                {
                    throw new ArgumentException($"Argument not eligible for #length: {val.ToString()}");
                }
            }
            
            return result;
        }

    }
}