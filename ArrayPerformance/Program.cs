namespace ArrayPerformance
{
    using System;
    using System.IO;
    using BenchmarkDotNet.Running;
    using JUST;

    class Program
    {
        static void Main(string[] args)
        {
            //var summary = BenchmarkRunner.Run<PerformanceArray>();

            var performanceArray = new PerformanceArray();
            performanceArray.Test();

            //TransformNonExistingFields();

            Console.WriteLine("COMPLETED");
            Console.ReadLine();
        }

        static void TransformNonExistingFields()
        {
            string input = File.ReadAllText($@"{Directory.GetCurrentDirectory()}\Inputs\test_non_existing.json");
            string transformer = File.ReadAllText($@"{Directory.GetCurrentDirectory()}\Transformers\test_non_existing_transformer.json");

            string output = JsonTransformer.Transform(transformer, input);
        }
    }
}
