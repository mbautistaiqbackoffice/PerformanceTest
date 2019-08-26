// ReSharper disable UnusedMember.Global

namespace JUST
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Newtonsoft.Json.Linq;

    //using Newtonsoft.Json.Linq;

    /// <summary>
    ///     Utilities for the JUST.NET package.
    /// </summary>
    public static class Utilities
    {
        /// <summary>
        ///     Flatten a JSON string to the corresponding paths and values.
        /// </summary>
        /// <param name="inputJson">
        ///     The JSON to flatten.
        /// </param>
        /// <returns>
        ///     An IDictionary&lt;string,string&gt; mapping all paths to values.
        /// </returns>
        public static IDictionary<string, string> FlattenJson(string inputJson) => FlattenRecursively(JToken.Parse(inputJson), null);

        /// <summary>
        ///     Flattens a JSON string to the corresponding paths and values recursively.
        /// </summary>
        /// <param name="parent">
        ///     The parent JToken.
        /// </param>
        /// <param name="result">
        ///     The result (modified by this method).
        /// </param>
        /// <returns>
        ///     The result parameter.
        /// </returns>
        private static Dictionary<string, string> FlattenRecursively(JToken parent, Dictionary<string, string> result)
        {
            if (!parent.HasValues)
                return result;

            if (result == null)
                result = new Dictionary<string, string>();

            // Note: Optimized for performance (foreach is slower)
            var count = parent.Children().Count();
            for (var i = 0; i < count; ++i)
            {
                if (!(parent.Children()[i] is JProperty property))
                    continue;

                if (property.Value.HasValues)
                    FlattenRecursively(property.Value, result);
                else
                    result.Add(property.Path, property.Value.ToString());
            }

            return result;
        }

        /// <summary>
        ///     Create an array for group array.
        /// </summary>
        /// <param name="array">
        ///     The array.
        /// </param>
        /// <param name="groupPropertyName">
        ///     Name of the grouping property.
        /// </param>
        /// <param name="groupedPropertyName">
        ///     Name of the grouped property.
        /// </param>
        /// <returns>
        ///     A JArray.
        /// </returns>
        public static JArray CreateGroupArray(JArray array, string groupPropertyName, string groupedPropertyName)
        {
            var groupedPairs = new Dictionary<string, JArray>();
            if (array != null)
            {
                var children = array.Children();
                for (var i = 0; i < children.Count(); ++i)
                {
                    var childObject = (JObject)children[i];
                    var groupToken = childObject.SelectToken("$." + groupPropertyName);
                    if (groupToken == null)
                        continue;

                    var valueOfGroupToken = Transformer.GetValue(groupToken);
                    var childObjectClone = (JObject)childObject.DeepClone();
                    childObjectClone.Remove(groupPropertyName);

                    JArray newArray;
                    if (groupedPairs.ContainsKey(valueOfGroupToken.ToString()))
                    {
                        newArray = groupedPairs[valueOfGroupToken.ToString()];
                        newArray.Add(childObjectClone);
                    }
                    else
                    {
                        newArray = new JArray { childObjectClone };
                    }

                    groupedPairs[valueOfGroupToken.ToString()] = newArray;
                }
            }

            JArray result = null;
            foreach (var pair in groupedPairs)
            {
                var groupObj = new JObject { { groupPropertyName, pair.Key }, { groupedPropertyName, pair.Value } };

                if (result == null)
                    result = new JArray();

                result.Add(groupObj);
            }

            return result;
        }

        /// <summary>
        ///     Group array multiple properties.
        /// </summary>
        /// <param name="array">
        ///     The array.
        /// </param>
        /// <param name="groupingPropertyNames">
        ///     List of names of the grouping properties.
        /// </param>
        /// <param name="groupedPropertyName">
        ///     Name of the grouped property.
        /// </param>
        /// <returns>
        ///     A JArray.
        /// </returns>
        public static JArray GroupArrayMultipleProperties(JArray array, string[] groupingPropertyNames, string groupedPropertyName)
        {
            var groupedPair = new Dictionary<string, JArray>();

            if (array != null)
            {
                foreach (var jToken in array.Children())
                {
                    var eachObj = (JObject)jToken;
                    var groupTokens = new List<JToken>();

                    foreach (var groupPropertyName in groupingPropertyNames)
                        groupTokens.Add(eachObj.SelectToken("$." + groupPropertyName));

                    if (groupTokens.Count == 0)
                        continue;

                    var key = string.Empty;

                    foreach (var valueOfToken in groupTokens.Select(Transformer.GetValue))
                    {
                        key += (key == string.Empty ? key : ":") + valueOfToken;
                    }

                    var clonedObj = (JObject)eachObj.DeepClone();
                    foreach (var groupPropertyName in groupingPropertyNames)
                        clonedObj.Remove(groupPropertyName);

                    JArray arr;
                    if (groupedPair.ContainsKey(key))
                    {
                        arr = groupedPair[key];
                        arr.Add(clonedObj);
                    }
                    else
                    {
                        arr = new JArray { clonedObj };
                    }

                    groupedPair[key] = arr;
                }
            }

            JArray resultObj = null;
            foreach (var pair in groupedPair)
            {
                if (resultObj == null)
                    resultObj = new JArray();

                var groupObj = new JObject();
                var keys = pair.Key.Split(':');

                for (var i = 0; i < groupingPropertyNames.Length; ++i)
                {
                    groupObj.Add(groupingPropertyNames[i], keys[i]);
                }

                groupObj.Add(groupedPropertyName, pair.Value);
                resultObj.Add(groupObj);
            }

            return resultObj;
        }
        
        public static JToken GetNestedData(object item)
        {
            var result = new JArray();
            if (item is Array items)
            {
                foreach (var innerItem in items)
                {
                    result.Add(GetNestedData(innerItem));
                }
            }
            else
            {
                return JToken.FromObject(item);
            }
            
            return result;
        }

    }
}