using JUST;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArrayPerformance
{
    public static class PeformanceArray
    {
        public static void Test()
        {
            bool parseArray = false;
            int[] arraySizes = { 100, 1000 };

            parseArray = false;
            NewMethod(parseArray, arraySizes);
        }

        private static void NewMethod(bool parseArray, int[] arraySizes)
        {
            AcctGroupArray(out string acctGroupInput, out string acctGroupTransformer, parseArray);
            InvoiceArray(out string invoiceInput, out string invoiceTransformer, parseArray);
            VendorArray(out string vendorInput, out string vendorTransformer, parseArray);

            foreach (int arraySize in arraySizes)
            {
                TransformArray("acctgroups", acctGroupInput, acctGroupTransformer, arraySize, parseArray);
                TransformArray("invoices", invoiceInput, invoiceTransformer, arraySize, parseArray);
                TransformArray("vendors", vendorInput, vendorTransformer, arraySize, parseArray);
            }
        }

        public static void TransformArray(string type, string input, string transformer, int arrayCount, bool parseArray = false)
        {
            int iteration = 100;
            double average = 0.0;

            List<double> duration = new List<double>();
            Stopwatch watch = new Stopwatch();

            GenerateArrays(ref input, arrayCount);

            for (int i = 0; i < iteration; i++)
            {
                watch.Reset();
                watch.Start();

                string output = String.Empty;

                if (parseArray)
                {
                    var items = JArray.Parse(input).Select(j => j.ToString()).ToArray();
                    ConcurrentBag<string> outputs = new ConcurrentBag<string>();

                    Parallel.ForEach(items, j =>
                    {
                        string tmp = JsonTransformer.Transform(transformer, j);
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
            Random random = new Random();
            var seq = JArray.Parse(input).Select(j => j.ToString()).ToArray();

            List<string> items = new List<string>();

            for (int i = 0; i < arraySize; i++)
                items.Add(seq[random.Next(0, seq.Length - 1)]);

            input = "[ " + String.Join(",", items) + " ]";
        }

        private static void AcctGroupArray(out string input, out string transformer, bool parseArray = false)
        {
            input = File.ReadAllText($@"{Directory.GetCurrentDirectory()}\Inputs\acctgroup_array.json");

            if (parseArray)
                transformer = File.ReadAllText($@"{Directory.GetCurrentDirectory()}\Transformers\acctgroup_transformer.json");
            else
                transformer = File.ReadAllText($@"{Directory.GetCurrentDirectory()}\Transformers\acctgroup_array_transformer.json");
        }

        private static void InvoiceArray(out string input, out string transformer, bool parseArray = false)
        {
            input = File.ReadAllText($@"{Directory.GetCurrentDirectory()}\Inputs\invoice_array.json");

            if (parseArray)
                transformer = File.ReadAllText($@"{Directory.GetCurrentDirectory()}\Transformers\invoice_transformer.json");
            else
                transformer = File.ReadAllText($@"{Directory.GetCurrentDirectory()}\Transformers\invoice_array_transformer.json");
        }

        private static void VendorArray(out string input, out string transformer, bool parseArray = false)
        {
            input = File.ReadAllText($@"{Directory.GetCurrentDirectory()}\Inputs\vendor_array.json");

            if (parseArray)
                transformer = File.ReadAllText($@"{Directory.GetCurrentDirectory()}\Transformers\vendor_transformer.json");
            else
                transformer = File.ReadAllText($@"{Directory.GetCurrentDirectory()}\Transformers\vendor_array_transformer.json");
        }
    }
}
