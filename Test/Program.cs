namespace Test
{
    using System.Collections.Generic;
    using System.Linq;

    class Program
    {
        static void Main(string[] args)
        {
            var enumerables = new List<object>();
            for (var i = 0; i < 10000; ++i)
            {
                enumerables.Add("s");
                enumerables.Add(2);
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (var j = 0; j < 1000; ++j)
            {
                for (var k = 0; k < enumerables.Count; k++)
                {
                    if (enumerables[k] is string)
                    {
                        var test2 = (string)enumerables[k];
                    }
                }
            }

            watch.Stop();
            var elapsed2 = watch.ElapsedMilliseconds;

            watch.Restart();
            for (var j = 0; j < 1000; ++j)
            {
                for (var k = 0; k < enumerables.Count; k++)
                {
                    if (enumerables[k] is string test)
                    {
                        var test2 = test;
                    }
                }
            }

            watch.Stop();
            var elapsed3 = watch.ElapsedMilliseconds;

            watch.Restart();
            for (var j = 0; j < 1000; ++j)
            {
                for (var k = 0; k < enumerables.Count; k++)
                {
                    var test = enumerables[k] as string;
                    if (test != null)
                    {
                        var test2 = test;
                    }
                }
            }

            watch.Stop();
            var elapsed1 = watch.ElapsedMilliseconds;
        }
    }
}
