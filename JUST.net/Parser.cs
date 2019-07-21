// ReSharper disable UnusedMember.Global

namespace JUST
{
    using System;
    using System.Text;

    public class LoopContents
    {
        public string Evaluated { get; set; }

        public int Start { get; set; }

        public int End { get; set; }
    }

    public class Parser
    {
        public static string Parse(string input, string loop)
        {
            int startIndex = 0, index;

            while ((index = input.IndexOf('#', startIndex)) != -1)
            {
                var endElementIndex = input.IndexOf('"', index);
                //var startingElementIndex = input.LastIndexOf('"', startIndex);

                if (endElementIndex > index)
                {
                    //startIndex = endElementIndex + 1;
                    var functionString = input.Substring(index, endElementIndex - index);

                    if (functionString.Trim().Contains("#loop"))
                    {
                        var content = FindLoopContents(input, endElementIndex, functionString);
                        Console.WriteLine(content.Evaluated);

                        var builder = new StringBuilder(input);
                        builder.Remove(content.Start, content.End - content.Start + 1);
                        builder.Insert(content.Start, content.Evaluated);
                        input = builder.ToString();

                        startIndex = content.Start + content.Evaluated.Length; // content.End;

                        //StringBuilder builder = new StringBuilder(input);
                        //builder.Remove(startingElementIndex, content.End - startingElementIndex);
                        //builder.Insert(startingElementIndex, "[" + content.Evaluated + "]");
                        //input = builder.ToString();

                        //startIndex = startingElementIndex + content.Evaluated.Length + 2;
                    }
                    else
                    {
                        var builder = new StringBuilder(input);
                        builder.Remove(index, endElementIndex - index);
                        var evaluatedFunction = EvaluateFunction(functionString, loop);
                        builder.Insert(index, evaluatedFunction);
                        input = builder.ToString();

                        startIndex = index + evaluatedFunction.Length;
                    }

                    //Console.WriteLine(functionString);
                }
                else
                {
                    break;
                }
            }

            return input;
        }

        public static string EvaluateFunction(string functionString, string loop) => loop == null ? "SAY_WHAT" : loop + "_YES";

        public static LoopContents FindLoopContents(string input, int startIndex, string loop)
        {
            var contents = new LoopContents();
            var result = string.Empty;

            var remainingString = input.Substring(startIndex);
            var indexOfColon = remainingString.IndexOf(':');
            if (indexOfColon != -1)
            {
                remainingString = remainingString.Substring(indexOfColon + 1);

                var startCharIndex = indexOfColon;
                var endCharIndex = indexOfColon;

                var opened = 0;
                var closed = 0;
                var searchCharacterInitialized = false;
                var searchCharacter = '{';

                var i = 0;
                foreach (var c in remainingString)
                {
                    switch (c)
                    {
                        case '"':
                            if (!searchCharacterInitialized)
                            {
                                searchCharacterInitialized = true;
                                searchCharacter = '"';
                                startCharIndex = i;
                                opened++;
                            }
                            else
                            {
                                if (searchCharacter == '"')
                                    closed++;
                                endCharIndex = i;
                            }
                            break;

                        case '[':
                            if (!searchCharacterInitialized)
                            {
                                searchCharacterInitialized = true;
                                searchCharacter = '[';
                                startCharIndex = i;
                            }

                            if (searchCharacter == '[')
                                opened++;

                            break;

                        case '{':
                            if (!searchCharacterInitialized)
                            {
                                searchCharacterInitialized = true;
                                searchCharacter = '{';
                                startCharIndex = i;
                            }

                            if (searchCharacter == '{')
                                opened++;
                            break;

                        case ']':
                            if (searchCharacterInitialized && searchCharacter == '[')
                            {
                                closed++;
                                endCharIndex = i;
                            }

                            break;

                        case '}':
                            if (searchCharacterInitialized && searchCharacter == '{')
                            {
                                closed++;
                                endCharIndex = i;
                            }

                            break;
                    }

                    if (closed != 0 && closed >= opened)
                        break;

                    i++;
                }

                result = remainingString.Substring(startCharIndex, endCharIndex - startCharIndex + 1);

                contents.Start = startIndex + startCharIndex + indexOfColon + 1;
                contents.End = startIndex + endCharIndex + indexOfColon + 1;
            }

            contents.Evaluated = result;

            if (contents.Start == 0)
                contents.Start = startIndex + 1;

            if (contents.End == 0)
                contents.End = startIndex + 1;

            contents.Evaluated = Parse(contents.Evaluated, loop);

            return contents;
        }
    }
}