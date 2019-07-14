using JUST;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ArrayPerformance
{
    public static class PerformanceArray
    {
        public static void Test()
        {
            int[] arraySizes = { 100 };

            var parseArray = false;
            NewMethod(parseArray, arraySizes);
        }

        private static void NewMethod(bool parseArray, int[] arraySizes)
        {
            var acctGroupJson = GetInputAndTransformFor("acctgroup", parseArray);
            var invoiceJson = GetInputAndTransformFor("invoice", parseArray);
            var (input, transformer) = GetInputAndTransformFor("vendor", parseArray);

            foreach (var arraySize in arraySizes)
            {
                TransformArray("acctgroups", acctGroupJson.input, acctGroupJson.transformer, arraySize, parseArray);
                TransformArray("invoices", invoiceJson.input, invoiceJson.transformer, arraySize, parseArray);
                TransformArray("vendors", input, transformer, arraySize, parseArray);
            }
        }

        private static void TransformArray(string type, string input, string transformer, int arrayCount, bool parseArray = false)
        {
            const int iteration = 100;
            var average = 0.0;

            var duration = new List<double>();
            var watch = new Stopwatch();

            GenerateArrays(ref input, arrayCount);

            for (var i = 0; i < iteration; i++)
            {
                watch.Reset();
                watch.Start();

                var output = String.Empty;

                if (parseArray)
                {
                    var items = JArray.Parse(input).Select(j => j.ToString()).ToArray();
                    var outputs = new ConcurrentBag<string>();

                    Parallel.ForEach(items, j =>
                    {
                        var tmp = JsonTransformer.Transform(transformer, j);
                        outputs.Add(tmp);
                    });

                    output = "{ \"" + type + "\": [ " + String.Join(",", outputs) + " ] }";
                }
                else
                {
                    input = "{ \"" + type + "\": " + input + " }";
                    output = JsonTransformer.Transform(transformer, input);
                }

                watch.Stop();

                double interval = watch.ElapsedMilliseconds;
                duration.Add(interval);
            }

            average = duration.Skip(1).Sum() / (double)iteration;

            Console.WriteLine($"{type.ToLower()}|1st item|{arrayCount}|{duration.First()}");
            Console.WriteLine($"{type.ToLower()}|AVE of items 2-{iteration:#,#}|{arrayCount}|{average}\n");
        }

        private static void GenerateArrays(ref string input, int arraySize)
        {
            var random = new Random();
            var seq = JArray.Parse(input).Select(j => j.ToString()).ToArray();

            var items = new List<string>();

            for (var i = 0; i < arraySize; i++)
                items.Add(seq[random.Next(0, seq.Length - 1)]);

            input = "[ " + String.Join(",", items) + " ]";
        }
        
        private static (string input, string transformer) GetInputAndTransformFor(string fileType, bool parseArray)
        {
            var input = File.ReadAllText(Path.Combine($@"{Directory.GetCurrentDirectory()}", "Inputs", fileType + "_array.json"));
            var transformerFile = fileType + (parseArray ? "_transformer.json" : "_array_transformer.json");
            var transformer = File.ReadAllText(Path.Combine($@"{Directory.GetCurrentDirectory()}", "Transformers", transformerFile));
            return (input, transformer);
        }
    }
}
