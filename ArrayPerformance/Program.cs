namespace ArrayPerformance
{
    using System;

    using BenchmarkDotNet.Running;

    class Program
    {
        static void Main(string[] args)
        {
            //var summary = BenchmarkRunner.Run<PerformanceArray>();

            var performanceArray = new PerformanceArray(); 
            performanceArray.Test();

            //Console.WriteLine("COMPLETED");
            //Console.ReadLine();
        }
    }
}
