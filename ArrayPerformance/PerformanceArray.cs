// ReSharper disable StringLiteralTypo

namespace ArrayPerformance
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using BenchmarkDotNet.Attributes;

    using JUST;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [DryJob]
    [IterationCount(100), MinIterationCount(99), MaxIterationCount(100)]
    [RPlotExporter, RankColumn]
    public class PerformanceArray
    {
        [Params(100)]
        public int N;

        [Params("acctgroup", "invoice", "vendor")]
        public string Type;
        private readonly string[] _types = { "acctgroup", "invoice", "vendor" };

        public bool ParseArray = true;
        public ConcurrentBag<string> OutputList = new ConcurrentBag<string>();
        public ConcurrentDictionary<string, JToken> TransformerTokens = new ConcurrentDictionary<string, JToken>();
        public static string InputJson;
        public static JToken TransformerToken;
        public static string CurrentDirectory;
#if DEBUG
        public static string RunMode = "Debug";
 #else
        public static string RunMode = "Release";
 #endif

        private readonly Stopwatch _stopwatch = new Stopwatch();

        public void Test()
        {
            N = 100;
            foreach (var type in _types)
            {
                Type = type;
                Initialize();
                _stopwatch.Restart();
                for (var i = 1; i <= 100; ++i)
                {
                    TransformTypeArray();
                }

                _stopwatch.Stop();
                Console.WriteLine($"Average execution time for {Type} = {_stopwatch.ElapsedMilliseconds / 100.0}");
            }
        }

        [GlobalSetup]
        public void Initialize()
        {
            var s = Path.DirectorySeparatorChar;
            CurrentDirectory = Environment.GetEnvironmentVariable("SLS_DEV_ROOT");
            CurrentDirectory = CurrentDirectory == null ? Directory.GetCurrentDirectory() 
                                                        : $"{CurrentDirectory}{s}Tests{s}PerformanceTest{s}ArrayPerformance{s}bin{s}{RunMode}{s}netcoreapp3.0";
            (InputJson, TransformerToken) = GetInputAndTransformFor(Type);
            GenerateArrays(ref InputJson);
        }

        [Benchmark]
        public void TransformTypeArray()
        {
            OutputList = new ConcurrentBag<string>();
            if (ParseArray)
            {
                var outputs = new ConcurrentBag<string>();
                var itemArray = JArray.Parse(InputJson);
                //Parallel.ForEach(itemArray, item => { outputs.Add(JsonTransformer.Transform(TransformerToken, item.ToString(), new JUSTContext())); });
                foreach (var item in itemArray)
                {
                    outputs.Add(JsonTransformer.Transform(TransformerToken, item.ToString()));
                }

                OutputList.Add($"{{ \"{Type}s\": [ " + string.Join(",", outputs) + " ] }");
            }
            else
            {
                var modifiedInput = $"{{ \"{Type}s\":" + InputJson + " }";
                OutputList.Add(JsonTransformer.Transform(TransformerToken, modifiedInput));
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
        
        private (string input, JToken transformerToken) GetInputAndTransformFor(string fileType)
        {
            lock (TransformerTokens)
            {
                var input = File.ReadAllText(Path.Combine(CurrentDirectory, "Inputs", fileType + "_array.json"));
                if (TransformerTokens.ContainsKey(fileType))
                    return (input, TransformerTokens[fileType]);

                var transformerFile = fileType + (ParseArray ? "_transformer.json" : "_array_transformer.json");
                var transformerJson = File.ReadAllText(Path.Combine(CurrentDirectory, "Transformers", transformerFile));
                var transformerToken = JsonConvert.DeserializeObject<JToken>(transformerJson);
                TransformerTokens.TryAdd(fileType, transformerToken);
                return (input, transformerToken);
            }
        }
    }
}
