namespace miniJson.Benchmarks
{
    [MiniJsonSerializable(typeof(Payload[]), "PayloadArray")]
    [MiniJsonSerializable(typeof(double[]), "DoubleArray")]
    internal static partial class BenchmarkJsonContext
    {
    }
}
