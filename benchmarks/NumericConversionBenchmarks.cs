using System;
using System.Buffers.Text;
using System.Globalization;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace miniJson.Benchmarks
{
    [MemoryDiagnoser]
    [ShortRunJob]
    public class NumericConversionBenchmarks
    {
        private string[] tokens = Array.Empty<string>();
        private byte[][] utf8Tokens = Array.Empty<byte[]>();

        [Params(1, 100)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            tokens = Enumerable.Range(1, Count)
                .Select(index => ((index % 2 == 0 ? 1.25e-100 : -2.5e100) * index)
                    .ToString("E17", CultureInfo.InvariantCulture)).ToArray();
            utf8Tokens = tokens.Select(Encoding.UTF8.GetBytes).ToArray();
            for (var index = 0; index < tokens.Length; index++)
            {
                var expected = BitConverter.DoubleToInt64Bits(double.Parse(tokens[index], CultureInfo.InvariantCulture));
                if (expected != BitConverter.DoubleToInt64Bits(ParseCopied(tokens[index])) ||
                    expected != BitConverter.DoubleToInt64Bits(ParseUtf8(utf8Tokens[index])))
                {
                    throw new InvalidOperationException("Numeric conversion changed a floating-point value.");
                }
            }
        }

        [Benchmark(Baseline = true)]
        public double CharSpanParse()
        {
            double sum = 0;
            foreach (var token in tokens)
            {
                sum += double.Parse(token.AsSpan(), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            return sum;
        }

        [Benchmark]
        public double Utf8StackCopyParse()
        {
            double sum = 0;
            foreach (var token in tokens)
            {
                sum += ParseCopied(token);
            }
            return sum;
        }

        [Benchmark]
        public double Utf8PreencodedParse()
        {
            double sum = 0;
            foreach (var token in utf8Tokens)
            {
                sum += ParseUtf8(token);
            }
            return sum;
        }

        private static double ParseCopied(ReadOnlySpan<char> token)
        {
            Span<byte> bytes = stackalloc byte[64];
            if (token.Length > bytes.Length)
            {
                return double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            for (var index = 0; index < token.Length; index++)
            {
                bytes[index] = (byte)token[index];
            }
            return ParseUtf8(bytes.Slice(0, token.Length));
        }

        private static double ParseUtf8(ReadOnlySpan<byte> bytes)
        {
            if (!Utf8Parser.TryParse(bytes, out double value, out var consumed) || consumed != bytes.Length)
            {
                throw new FormatException("Invalid numeric benchmark token.");
            }
            return value;
        }
    }
}
