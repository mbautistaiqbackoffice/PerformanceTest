namespace JUST
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    internal class ExpressionHelper
    {
        private static readonly ConcurrentDictionary<string, string[]> _ArgumentCache = new ConcurrentDictionary<string, string[]>();

        internal static bool TryParseFunctionNameAndArguments(string input, out string functionName, out string arguments)
        {
            var openParenIndex = input?.IndexOf('(') ?? -1;
            if (input == null || (input.StartsWith("#") && input.EndsWith(")") && openParenIndex == -1))
            {
                functionName = input;
                arguments = null;
                return false;
            }

            functionName = input.Substring(1, openParenIndex - 1);
            arguments = input.Substring(openParenIndex + 1, input.Length - openParenIndex - 2);
            return true;
        }

        internal static string[] GetArguments(string functionString)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (_ArgumentCache.TryGetValue(functionString, out var cachedArguments))
                return cachedArguments;

            lock (_ArgumentCache)
            {
                if (_ArgumentCache.TryGetValue(functionString, out cachedArguments))
                    return cachedArguments;

                var arguments = new List<string>();
                var index = -1;

                var openParenCount = 0;
                var closeParenCount = 0;

                for (var i = 0; i < functionString.Length; i++)
                {
                    var currentChar = functionString[i];

                    if (currentChar == '(')
                        openParenCount++;
                    else if (currentChar == ')')
                        closeParenCount++;

                    var parensOpen = openParenCount != closeParenCount;
                    if (currentChar != ',' || parensOpen)
                        continue;

                    arguments.Add(functionString.Substring(index + 1, i - index - 1));
                    index = i;
                }

                arguments.Add(functionString.Substring(index + 1, functionString.Length - index - 1));

                _ArgumentCache.TryAdd(functionString, arguments.ToArray());
                return arguments.ToArray();
            }
        }
    }
}