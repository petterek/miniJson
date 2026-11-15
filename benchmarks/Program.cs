using System;
using System.Globalization;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using JsonConvert = Newtonsoft.Json.JsonConvert;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace miniJson.Benchmarks
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }

    [MemoryDiagnoser]
    [ShortRunJob]
    [CategoriesColumn]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class JsonBenchmarks
    {
        private Payload[] items = Array.Empty<Payload>();
        private string json = "";

        [Params(1, 100)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            items = Enumerable.Range(1, Count).Select(CreatePayload).ToArray();
            json = JsonSerializer.Serialize(items);

            Validate(MiniJsonDeserialize());
            Validate(MiniJsonGeneratedDeserialize());
            Validate(SystemTextJsonDeserialize());
            Validate(NewtonsoftDeserialize());
            Validate(JsonSerializer.Deserialize<Payload[]>(MiniJsonSerialize()));
            Validate(JsonSerializer.Deserialize<Payload[]>(SystemTextJsonSerialize()));
            Validate(JsonSerializer.Deserialize<Payload[]>(NewtonsoftSerialize()));
        }

        protected virtual Payload CreatePayload(int i)
        {
            return new Payload
            {
                Id = i,
                Name = $"Item {i}",
                Active = i % 2 == 0,
                Scores = new[] { i, i + 1, i + 2 }
            };
        }

        private void Validate(Payload[]? actual)
        {
            if (JsonSerializer.Serialize(actual) != json)
            {
                throw new InvalidOperationException("Benchmark serializer changed the payload.");
            }
        }

        [Benchmark(Baseline = true)]
        [BenchmarkCategory("Serialize")]
        public string MiniJsonSerialize() => Writer.ObjectToString(items);

        [Benchmark]
        [BenchmarkCategory("Serialize")]
        public string SystemTextJsonSerialize() => JsonSerializer.Serialize(items);

        [Benchmark]
        [BenchmarkCategory("Serialize")]
        public string NewtonsoftSerialize() => JsonConvert.SerializeObject(items);

        [Benchmark(Baseline = true)]
        [BenchmarkCategory("Deserialize")]
        public Payload[] MiniJsonDeserialize() =>
            (Payload[])Parser.StringToObject(json, typeof(Payload[]));

        [Benchmark]
        [BenchmarkCategory("Deserialize")]
        public Payload[] MiniJsonGeneratedDeserialize() =>
            Parser.StringToObject(json, BenchmarkJsonContext.PayloadArray);

        [Benchmark]
        [BenchmarkCategory("Deserialize")]
        public Payload[]? SystemTextJsonDeserialize() =>
            JsonSerializer.Deserialize<Payload[]>(json);

        [Benchmark]
        [BenchmarkCategory("Deserialize")]
        public Payload[]? NewtonsoftDeserialize() =>
            JsonConvert.DeserializeObject<Payload[]>(json);
    }

    public class UnicodeJsonBenchmarks : JsonBenchmarks
    {
        protected override Payload CreatePayload(int i)
        {
            var item = base.CreatePayload(i);
            item.Name += ": \u03c0 \u20ac \U0001D11E \"quoted\" \\slash";
            return item;
        }
    }

    [MemoryDiagnoser]
    [ShortRunJob]
    [CategoriesColumn]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class ExponentJsonBenchmarks
    {
        private double[] values = Array.Empty<double>();
        private string json = "";

        [Params(1, 100)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            values = Enumerable.Range(1, Count)
                .Select(i => (i % 2 == 0 ? 1.25e-100 : -2.5e100) * i)
                .ToArray();
            json = "[" + string.Join(",", values.Select(value => value.ToString("E17", CultureInfo.InvariantCulture))) + "]";

            Validate(MiniJsonDeserialize());
            Validate(MiniJsonGeneratedDeserialize());
            Validate(SystemTextJsonDeserialize());
            Validate(NewtonsoftDeserialize());
            Validate(JsonSerializer.Deserialize<double[]>(MiniJsonSerialize()));
            Validate(JsonSerializer.Deserialize<double[]>(SystemTextJsonSerialize()));
            Validate(JsonSerializer.Deserialize<double[]>(NewtonsoftSerialize()));
        }

        private void Validate(double[]? actual)
        {
            if (actual == null || !values.SequenceEqual(actual))
            {
                throw new InvalidOperationException("Benchmark serializer changed the exponent payload.");
            }
        }

        [Benchmark(Baseline = true)]
        [BenchmarkCategory("Serialize")]
        public string MiniJsonSerialize() => Writer.ObjectToString(values);

        [Benchmark]
        [BenchmarkCategory("Serialize")]
        public string SystemTextJsonSerialize() => JsonSerializer.Serialize(values);

        [Benchmark]
        [BenchmarkCategory("Serialize")]
        public string NewtonsoftSerialize() => JsonConvert.SerializeObject(values);

        [Benchmark(Baseline = true)]
        [BenchmarkCategory("Deserialize")]
        public double[] MiniJsonDeserialize() =>
            (double[])Parser.StringToObject(json, typeof(double[]));

        [Benchmark]
        [BenchmarkCategory("Deserialize")]
        public double[] MiniJsonGeneratedDeserialize() =>
            Parser.StringToObject(json, BenchmarkJsonContext.DoubleArray);

        [Benchmark]
        [BenchmarkCategory("Deserialize")]
        public double[]? SystemTextJsonDeserialize() =>
            JsonSerializer.Deserialize<double[]>(json);

        [Benchmark]
        [BenchmarkCategory("Deserialize")]
        public double[]? NewtonsoftDeserialize() =>
            JsonConvert.DeserializeObject<double[]>(json);
    }

    public class Payload
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool Active { get; set; }
        public int[] Scores { get; set; } = Array.Empty<int>();
    }
}
