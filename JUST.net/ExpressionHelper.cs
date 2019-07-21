namespace JUST
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    internal class ExpressionHelper
    {
        private const string FUNCTION_AND_ARGUMENTS_REGEX = "^#(.+?)[(](.*)[)]$";
        private static readonly Regex _CompiledFunctionRegex = new Regex(FUNCTION_AND_ARGUMENTS_REGEX, RegexOptions.Compiled);

        private const string ARGUMENTS_REGEX = "\\(([^\\)]*?)\\)";
        private static readonly Regex _CompileArgumentsRegex = new Regex(ARGUMENTS_REGEX, RegexOptions.Compiled);

        internal static bool TryParseFunctionNameAndArguments(string input, out string functionName, out string arguments)
        {
            var match = _CompiledFunctionRegex.Match(input);
            functionName = match.Success ? match.Groups[1].Value : input;
            arguments = match.Success ? match.Groups[2].Value : null;
            return match.Success;
        }

        internal static string[] GetArguments(string functionString)
        {
            var arguments = new List<string>();
            var index = -1;

            var openParenCount = 0;
            var closeParenCount = 0;

            // Original
            var functionStringLength = functionString.Length;
            for (var i = 0; i < functionStringLength; i++)
            {
                if (functionString[i] == '(')
                    openParenCount++;
                else if (functionString[i] == ')')
                    closeParenCount++;

                var parensOpen = openParenCount != closeParenCount;
                if (functionString[i] != ',' || parensOpen)
                    continue;

                arguments.Add(functionString.Substring(index + 1, i - index - 1));
                index = i;
            }

            // IndexOf
            arguments = new List<string>();
            var nextOpenIndex = functionString.IndexOf('(');
            while (nextOpenIndex != -1)
            {
                var nextCloseIndex = functionString.IndexOf(')', nextOpenIndex + 1);
                if (nextCloseIndex != -1 && nextCloseIndex != nextOpenIndex + 1)
                {
                    var argumentsString = functionString.Substring(nextOpenIndex + 1, nextCloseIndex - nextOpenIndex - 1);
                    var argumentList = argumentsString.Split(',');
                    arguments.AddRange(argumentList.Select(argument => argument.TrimStart()));
                }

                if (nextCloseIndex + 1 == functionStringLength)
                    break;

                nextOpenIndex = functionString.IndexOf('(', nextCloseIndex + 1);
            }

            // Regex
            var match = _CompileArgumentsRegex.Match(functionString);
            if (match.Success)
            {
                var argumentsString = match.Groups[1].Value;
                var argumentList = argumentsString.Split(',');
                arguments.AddRange(argumentList.Select(argument => argument.TrimStart()));
            }

            arguments.Add(functionString.Substring(index + 1, functionStringLength - index - 1));

            return arguments.ToArray();
        }
    }
}