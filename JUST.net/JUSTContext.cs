// ReSharper disable IdentifierTypo
// ReSharper disable UnusedMember.Global
namespace JUST
{
    using System;
    using System.Collections.Concurrent;
    using System.Reflection;

    using Newtonsoft.Json.Linq;

    public enum EvaluationMode
    {
        FallbackToDefault,
        Strict
    }

    public class JUSTContext
    {
        private readonly ConcurrentDictionary<string, MethodInfo> _customFunctions = new ConcurrentDictionary<string, MethodInfo>();
        private int _defaultDecimalPlaces = 28;

        internal JToken Input;

        public EvaluationMode EvaluationMode = EvaluationMode.FallbackToDefault;

        public int DefaultDecimalPlaces
        {
            get => _defaultDecimalPlaces;
            set
            {
                if (value < 0 || value > 28)
                    throw new ArgumentException("Value must be between 0 and 28");

                _defaultDecimalPlaces = value;
            }
        }

        public JUSTContext()
        {
        }

        internal JUSTContext(string inputJson) => Input = JToken.Parse(inputJson);

        public void RegisterCustomFunction(string assemblyName, string namespc, string methodName, string methodAlias = null)
        {
            var methodInfo = ReflectionHelper.SearchCustomFunction(assemblyName, namespc, methodName);
            if (methodInfo == null)
                throw new Exception("Unable to find specified method!");

            if (!_customFunctions.TryAdd(methodAlias ?? methodName, methodInfo))
                throw new Exception("Error while registering custom method!");
        }

        public bool UnregisterCustomFunction(string name) => _customFunctions.TryRemove(name, out _);

        public void ClearCustomFunctionRegistrations() => _customFunctions.Clear();

        internal MethodInfo GetCustomMethod(string key)
        {
            if (!_customFunctions.TryGetValue(key, out var result))
                throw new Exception($"Custom function {key} is not registered!");
            return result;
        }

        internal bool IsRegisteredCustomFunction(string name) => _customFunctions.ContainsKey(name);
    }
}