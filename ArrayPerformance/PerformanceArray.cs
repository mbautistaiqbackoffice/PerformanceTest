namespace ArrayPerformance
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using BenchmarkDotNet.Attributes;

    using JUST;

    using Newtonsoft.Json.Linq;

    [DryJob]
    [IterationCount(10), MinIterationCount(9), MaxIterationCount(10)]
    [RPlotExporter, RankColumn]
    public class PerformanceArray
    {
        [Params(10)]
        public int N;

        [Params("acctgroup", "invoice", "vendor")]
        public string Type;

        public bool ParseArray = true;
        public List<string> OutputList = new List<string>();
        public static string InputJson;
        public static string TransformerJson;
        public static string CurrentDirectory;
#if DEBUG
        public static string RunMode = "Debug";
 #else
        public static string RunMode = "Release";
 #endif

        public void Test()
        {
            N = 100;
            Type = "vendor";
            Initialize();
            for (var i = 1; i <= 100; ++i)
            {
                TransformTypeArray();
            }
        }

        [GlobalSetup]
        public void Initialize()
        {
            var s = Path.DirectorySeparatorChar;
            CurrentDirectory = Environment.GetEnvironmentVariable("SLS_DEV_ROOT");
            CurrentDirectory = CurrentDirectory == null ? Directory.GetCurrentDirectory() 
                                                        : $"{CurrentDirectory}{s}Tests{s}PerformanceTest{s}ArrayPerformance{s}bin{s}{RunMode}{s}netcoreapp3.0";
            (InputJson, TransformerJson) = GetInputAndTransformFor(Type);
            GenerateArrays(ref InputJson);
        }

        [Benchmark]
        public void TransformTypeArray()
        {
            OutputList = new List<string>();
            if (ParseArray)
            {
                var items = JArray.Parse(InputJson).Select(j => j.ToString()).ToArray();
                var outputs = new ConcurrentBag<string>();

                foreach (var item in items)
                { 
                    outputs.Add(JsonTransformer.Transform(TransformerJson, item));
                }

                OutputList.Add($"{{ \"{Type}s\": [ " + string.Join(",", outputs) + " ] }");
            }
            else
            {
                var modifiedInput = $"{{ \"{Type}s\":" + InputJson + " }";
                OutputList.Add(JsonTransformer.Transform(TransformerJson, modifiedInput));
            }

            File.WriteAllText(Path.Combine(CurrentDirectory,
                                           "Outputs", $"{Type}_array.json"),
                                           string.Join(",", OutputList));
        }

        private void GenerateArrays(ref string input)
        {
            var random = new Random();
            var seq = JArray.Parse(input).Select(j => j.ToString()).ToArray();

            var items = new List<string>();

            for (var i = 0; i < N; i++)
                items.Add(seq[random.Next(0, seq.Length - 1)]);

            input = "[ " + string.Join(",", items) + " ]";
        }
        
        private (string input, string transformer) GetInputAndTransformFor(string fileType)
        {
            var input = File.ReadAllText(Path.Combine(CurrentDirectory, "Inputs", fileType + "_array.json"));
            var transformerFile = fileType + (ParseArray ? "_transformer.json" : "_array_transformer.json");
            var transformer = File.ReadAllText(Path.Combine(CurrentDirectory, "Transformers", transformerFile));
            return (input, transformer);
        }
    }
}
