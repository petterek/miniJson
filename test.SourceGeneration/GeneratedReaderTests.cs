using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;

namespace miniJson.SourceGeneration.Tests
{
    [MiniJsonSerializable(typeof(Node), "Node")]
    [MiniJsonSerializable(typeof(Node[]), "Nodes")]
    [MiniJsonSerializable(typeof(double[]), "Doubles")]
    [MiniJsonSerializable(typeof(bool[]), "Flags")]
    [MiniJsonSerializable(typeof(int[][]), "NestedArrays")]
    [MiniJsonSerializable(typeof(byte[]), "Bytes")]
    [MiniJsonSerializable(typeof(ReadOnlyModel), "ReadOnly")]
    [MiniJsonSerializable(typeof(MutableRecord), "Record")]
    [MiniJsonSerializable(typeof(Numbers), "Numbers")]
    [MiniJsonSerializable(typeof(ImmutablePerson), "Person")]
    [MiniJsonSerializable(typeof(ImmutablePerson[]), "People")]
    [MiniJsonSerializable(typeof(RecordEnvelope), "Envelope")]
    [MiniJsonSerializable(typeof(OptionalRecord), "Optional")]
    [MiniJsonSerializable(typeof(ValidatedRecord), "Validated")]
    [MiniJsonSerializable(typeof(RecursiveRecord), "Recursive")]
    [MiniJsonSerializable(typeof(DerivedRecord), "Derived")]
    internal static partial class TestContext
    {
    }

    public class NodeBase
    {
        public int Id;
    }

    public class Node : NodeBase
    {
        public string? Name { get; set; }
        public bool Active { get; set; }
        public int[]? Scores { get; set; }
        public Node? Child { get; set; }
        public int? Maybe { get; set; }
        public TestKind Kind { get; set; }
        public Dictionary<string, int>? Map { get; set; }
        public byte[]? Bytes { get; set; }
    }

    public enum TestKind
    {
        First,
        Second
    }

    public class ReadOnlyModel
    {
        public string? Name { get; set; }
        public string Unused => "unused";
    }

    public record MutableRecord
    {
        public string? Name { get; set; }
        public int Age { get; set; }
    }

    public class Numbers
    {
        public long Long { get; set; }
        public byte Byte { get; set; }
        public float Single { get; set; }
        public decimal Decimal { get; set; }
    }

    public class AmbiguousMembers
    {
        public int Value;
        public int value;
    }

    public class ConcurrentMembers
    {
        public int Alpha;
        public int Beta;
        public int Gamma;
        public int Delta;
    }

    public record ImmutablePerson(string? Name, int Age);
    public record RecordEnvelope(ImmutablePerson? Person, ImmutablePerson?[]? People, byte[] Bytes, TestKind Kind);
    public record OptionalRecord(string? Name = "guest", int Age = 18, TestKind Kind = TestKind.Second,
        decimal Amount = 1.25m, float Single = float.NaN, double Double = double.PositiveInfinity,
        float NegativeSingle = -0.0f, double NegativeDouble = -0.0d, decimal NegativeDecimal = -0.0m);
    public record RecursiveRecord(int Value, RecursiveRecord? Next);
    public record BaseRecord(string Name);
    public record DerivedRecord(string Name, int Age) : BaseRecord(Name);

    public record ValidatedRecord
    {
        public string? Name { get; }
        public int Age { get; }
        public string? Note { get; set; } = "preset";

        public ValidatedRecord(int age, string? name)
        {
            RecordConstructorState.Calls++;
            if (age < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(age));
            }
            Name = name?.Trim();
            Age = age;
        }
    }

    internal static class RecordConstructorState
    {
        public static int Calls;
    }

    [TestFixture]
    public class GeneratedReaderTests
    {
        private static readonly JsonSerializerOptions ComparisonOptions = new JsonSerializerOptions { IncludeFields = true };

        [TestCase(false)]
        [TestCase(true)]
        public void GeneratedObjectsMatchReflection(bool useStream)
        {
            const string json = """
                {"\u0049d":7,"Name":"prefix\uD834\uDD1E\\\"suffix","Active":true,
                 "Scores":[1,-2,3],"Child":{"Id":8,"Name":"child"},"Maybe":42,
                 "Kind":"Second","Map":{"x":4},"Bytes":"AQID","Ignored":[true,false]}
                """;
            var actual = Read(json, TestContext.Node, useStream);
            var expected = ReadReflection<Node>(json, useStream);
            Assert.AreEqual(JsonSerializer.Serialize(expected, ComparisonOptions), JsonSerializer.Serialize(actual, ComparisonOptions));
            Assert.AreEqual(7, actual.Id);
            Assert.AreEqual("prefix" + char.ConvertFromUtf32(0x1D11E) + "\\\"suffix", actual.Name);
            Assert.AreEqual(8, actual.Child!.Id);
            Assert.AreEqual(42, actual.Maybe);
            Assert.AreEqual(TestKind.Second, actual.Kind);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, actual.Bytes);
        }

        [TestCase("[]")]
        [TestCase("null")]
        [TestCase("[null,{\"Id\":1,\"Name\":null,\"Scores\":[],\"Maybe\":null},null]")]
        public void GeneratedObjectArraysPreserveNullsAndEmptyValues(string json)
        {
            foreach (var useStream in new[] { false, true })
            {
                var actual = Read(json, TestContext.Nodes, useStream);
                var expected = ReadReflection<Node[]>(json, useStream);
                Assert.AreEqual(JsonSerializer.Serialize(expected, ComparisonOptions), JsonSerializer.Serialize(actual, ComparisonOptions));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GeneratedPrimitiveArraysMatchReflection(bool useStream)
        {
            const string numbers = "[1e2,-1.25E-2,2.5e100,1.25e-100]";
            CollectionAssert.AreEqual(ReadReflection<double[]>(numbers, useStream), Read(numbers, TestContext.Doubles, useStream));
            const string flags = "[true,False,false]";
            CollectionAssert.AreEqual(ReadReflection<bool[]>(flags, useStream), Read(flags, TestContext.Flags, useStream));
            const string nested = "[[],[1,2],null]";
            Assert.AreEqual(JsonSerializer.Serialize(ReadReflection<int[][]>(nested, useStream)),
                JsonSerializer.Serialize(Read(nested, TestContext.NestedArrays, useStream)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GeneratedFallbackPreservesBase64AndUnusedReadOnlyMembers(bool useStream)
        {
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, Read("\"AQID\"", TestContext.Bytes, useStream));
            Assert.AreEqual("fallback", Read("{\"Name\":\"fallback\"}", TestContext.ReadOnly, useStream).Name);
            Assert.Throws<ArgumentException>(() => Read("{\"Unused\":\"bad\"}", TestContext.ReadOnly, useStream));
        }

        [TestCase("{\"Name\":\"\\u12xz\"}")]
        [TestCase("{\"Name\":\"unterminated")]
        [TestCase("{\"Id\":1 \"Name\":\"missing comma\"}")]
        public void GeneratedObjectErrorsMatchReflection(string json)
        {
            foreach (var useStream in new[] { false, true })
            {
                var expected = Assert.Catch(() => ReadReflection<Node>(json, useStream));
                var actual = Assert.Catch(() => Read(json, TestContext.Node, useStream));
                Assert.AreEqual(expected!.GetType(), actual!.GetType());
            }
        }

        [TestCase("[1e+]")]
        [TestCase("[01]")]
        [TestCase("[1.e2]")]
        [TestCase("[-01]")]
        [TestCase("[-]")]
        [TestCase("[.2e-3]")]
        [TestCase("[1ee2]")]
        [TestCase("[1e2.3]")]
        [TestCase("[1e-]")]
        public void GeneratedNumericArraysRetainNumberGrammar(string json)
        {
            foreach (var useStream in new[] { false, true })
            {
                Assert.Throws<FormatException>(() => Read(json, TestContext.Doubles, useStream));
            }
        }

        [TestCase("-0")]
        [TestCase("-0.0e+0")]
        [TestCase("4.9406564584124654e-324")]
        [TestCase("2.2250738585072014e-308")]
        [TestCase("1.7976931348623157e308")]
        [TestCase("1e309")]
        [TestCase("-1e309")]
        [TestCase("1e-9999")]
        [TestCase("9007199254740993e0")]
        [TestCase("1.00000000000000011102230246251565404236316680908203125e0")]
        public void FastNumberScanningPreservesBitsAndFollowingArrayValues(string token)
        {
            var expected = BitConverter.DoubleToInt64Bits(double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture));
            foreach (var useStream in new[] { false, true })
            {
                Assert.AreEqual(expected, BitConverter.DoubleToInt64Bits(ReadReflection<double>(token, useStream)));
                var actual = Read("[" + token + ",2e0]", TestContext.Doubles, useStream);
                Assert.AreEqual(expected, BitConverter.DoubleToInt64Bits(actual[0]));
                Assert.AreEqual(2d, actual[1]);
            }
        }

        [Test]
        public void FastNumberScanningPreservesFollowingObjectMembersAndErrorTokens()
        {
            foreach (var useStream in new[] { false, true })
            {
                var actual = Read("""{"Double":-1.25E2,"Amount":1e-2,"Age":42,"Name":"after"}""",
                    TestContext.Optional, useStream);
                Assert.AreEqual(-125d, actual.Double);
                Assert.AreEqual(0.01m, actual.Amount);
                Assert.AreEqual(42, actual.Age);
                Assert.AreEqual("after", actual.Name);
                const string malformed = "1e++2";
                var error = Assert.Throws<FormatException>(() => ReadReflection<double>(malformed, useStream));
                Assert.AreEqual("Invalid JSON number: " + malformed, error!.Message);
            }
        }

        [Test]
        public void DoubleConversionPreservesRoundTripBitsAcrossReaders()
        {
            var random = new Random(12345);
            var bytes = new byte[8];
            var values = new List<double>();
            while (values.Count < 1000)
            {
                random.NextBytes(bytes);
                var value = BitConverter.ToDouble(bytes, 0);
                if (!double.IsNaN(value) && !double.IsInfinity(value))
                {
                    values.Add(value);
                }
            }
            var tokens = new string[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                tokens[index] = values[index].ToString(index % 2 == 0 ? "R" : "E17", CultureInfo.InvariantCulture);
            }
            var json = "[" + string.Join(",", tokens) + "]";
            foreach (var useStream in new[] { false, true })
            {
                var reflected = ReadReflection<double[]>(json, useStream);
                var generated = Read(json, TestContext.Doubles, useStream);
                for (var index = 0; index < values.Count; index++)
                {
                    var expected = BitConverter.DoubleToInt64Bits(values[index]);
                    Assert.AreEqual(expected, BitConverter.DoubleToInt64Bits(reflected[index]), tokens[index]);
                    Assert.AreEqual(expected, BitConverter.DoubleToInt64Bits(generated[index]), tokens[index]);
                }
            }
        }

        [Test]
        public void GeneratedInputPreservesUtf8ReplacementAndCallerOwnedStreams()
        {
            var json = "{\"Name\":\"" + '\uD800' + "\"}";
            Assert.AreEqual("\uFFFD", Parser.StringToObject(json, TestContext.Node).Name);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"Name\":\"unterminated"));
            Assert.Throws<EndOfStreamException>(() => Parser.StringToObject(stream, TestContext.Node));
            Assert.IsTrue(stream.CanRead);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MutableRecordsUseTypedDeserialization(bool useStream)
        {
            Assert.AreEqual(new MutableRecord { Name = "record", Age = 42 },
                Read("{\"Name\":\"record\",\"Age\":42}", TestContext.Record, useStream));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ImmutableRecordsUseConstructorsAndPreserveReaderSemantics(bool useStream)
        {
            const string json = """
                {"Age":42,"Unknown":[null,true,{"x":"ignored"}],
                 "\u004eame":"record\uD834\uDD1E","aGE":43}
                """;
            Assert.AreEqual(new ImmutablePerson("record" + char.ConvertFromUtf32(0x1D11E), 43),
                Read(json, TestContext.Person, useStream));
            Assert.IsNull(Read("null", TestContext.Person, useStream));
            Assert.AreEqual(new ImmutablePerson(null, 0), Read("{}", TestContext.Person, useStream));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ImmutableRecordsComposeWithArraysAndFallbackMembers(bool useStream)
        {
            const string json = """
                {"Kind":"Second","People":[null,{"Age":7,"Name":"child"},null],
                 "Person":{"Name":"parent","Age":42},"Bytes":"AQID"}
                """;
            var expected = new RecordEnvelope(new ImmutablePerson("parent", 42),
                new ImmutablePerson?[] { null, new ImmutablePerson("child", 7), null },
                new byte[] { 1, 2, 3 }, TestKind.Second);
            var actual = Read(json, TestContext.Envelope, useStream);
            Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
            CollectionAssert.AreEqual(expected.People, Read("[null,{\"Name\":\"child\",\"Age\":7},null]", TestContext.People, useStream));
            Assert.IsNull(Read("null", TestContext.People, useStream));
            Assert.IsEmpty(Read("[]", TestContext.People, useStream));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ImmutableRecordsUseOptionalDefaultsOnlyForMissingValues(bool useStream)
        {
            var defaults = Read("{}", TestContext.Optional, useStream);
            var expectedDefaults = new OptionalRecord();
            Assert.AreEqual(expectedDefaults, defaults);
            Assert.AreEqual(BitConverter.SingleToInt32Bits(expectedDefaults.NegativeSingle),
                BitConverter.SingleToInt32Bits(defaults.NegativeSingle));
            Assert.AreEqual(BitConverter.DoubleToInt64Bits(expectedDefaults.NegativeDouble),
                BitConverter.DoubleToInt64Bits(defaults.NegativeDouble));
            CollectionAssert.AreEqual(decimal.GetBits(expectedDefaults.NegativeDecimal), decimal.GetBits(defaults.NegativeDecimal));
            var actual = Read("{\"Name\":null,\"Age\":0,\"Kind\":\"First\",\"Amount\":2.5,\"Single\":1e2,\"Double\":2e2}",
                TestContext.Optional, useStream);
            Assert.AreEqual(new OptionalRecord(null, 0, TestKind.First, 2.5m, 100f, 200d), actual);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConstructorsRunOnceAfterParsingAndKeepUnboundMemberInitializers(bool useStream)
        {
            RecordConstructorState.Calls = 0;
            var actual = Read("{\"Name\":\" trimmed \",\"Age\":42}", TestContext.Validated, useStream);
            Assert.AreEqual("trimmed", actual.Name);
            Assert.AreEqual(42, actual.Age);
            Assert.AreEqual("preset", actual.Note);
            Assert.AreEqual(1, RecordConstructorState.Calls);
            Assert.AreEqual("assigned", Read("{\"Note\":\"assigned\"}", TestContext.Validated, useStream).Note);
            Assert.IsNull(Read("{\"Note\":null}", TestContext.Validated, useStream).Note);
            Assert.Throws<ArgumentOutOfRangeException>(() => Read("{\"Age\":-1}", TestContext.Validated, useStream));

            RecordConstructorState.Calls = 0;
            Assert.Catch(() => Read("{\"Name\":\"parsed\",\"Age\":42", TestContext.Validated, useStream));
            Assert.AreEqual(0, RecordConstructorState.Calls);
            Assert.Throws<EndOfStreamException>(() => Read("{\"Name\":\"unterminated", TestContext.Validated, useStream));
            Assert.AreEqual(0, RecordConstructorState.Calls);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RecursiveAndInheritedRecordsBindConstructorMembers(bool useStream)
        {
            Assert.AreEqual(new RecursiveRecord(1, new RecursiveRecord(2, null)),
                Read("{\"Next\":{\"Value\":2,\"Next\":null},\"Value\":1}", TestContext.Recursive, useStream));
            Assert.AreEqual(new DerivedRecord("inherited", 42),
                Read("{\"Age\":42,\"Name\":\"inherited\"}", TestContext.Derived, useStream));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnknownRecordMembersRetainLegacyErrors(bool useStream)
        {
            foreach (var json in new[] { "{\"ToString\":0}", "{\"Unknown\":{\"items\":[1]}}" })
            {
                var expected = Assert.Catch(() => ReadReflection<Node>(json, useStream));
                var actual = Assert.Catch(() => Read(json, TestContext.Person, useStream));
                Assert.AreEqual(expected!.GetType(), actual!.GetType());
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OtherTypedNumericReadersPreserveValues(bool useStream)
        {
            const string json = """
                {"Long":9223372036854775806,"Byte":255,"Single":1.25e2,"Decimal":123456789.0123456789}
                """;
            var actual = Read(json, TestContext.Numbers, useStream);
            Assert.AreEqual(JsonSerializer.Serialize(ReadReflection<Numbers>(json, useStream)), JsonSerializer.Serialize(actual));
            Assert.AreEqual(9223372036854775806L, actual.Long);
            Assert.AreEqual(255, actual.Byte);
            Assert.AreEqual(125f, actual.Single);
            Assert.AreEqual(123456789.0123456789m, actual.Decimal);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BothReadersMatchMemberNamesIgnoringCaseWithoutChangingDictionaryKeys(bool useStream)
        {
            foreach (var json in new[]
            {
                """{"id":7,"name":"first","NAME":"MiXeD","Name*":"not assigned","map":{"Key":1,"key":2}}""",
                """{"ID":7,"\u006eAmE":"MiXeD","MAP":{"Key":1,"key":2}}"""
            })
            {
                var expected = ReadReflection<Node>(json, useStream);
                var actual = Read(json, TestContext.Node, useStream);
                Assert.AreEqual("MiXeD", expected.Name);
                Assert.AreEqual(7, expected.Id);
                Assert.AreEqual(JsonSerializer.Serialize(expected, ComparisonOptions), JsonSerializer.Serialize(actual, ComparisonOptions));
                Assert.AreEqual(1, actual.Map!["Key"]);
                Assert.AreEqual(2, actual.Map["key"]);
                StringAssert.Contains("\"Name\":", Writer.ObjectToString(actual));
                StringAssert.Contains("\"Id\":", Writer.ObjectToString(actual));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CaseCollidingReflectionMembersAreRejected(bool useStream)
        {
            Assert.Throws<AmbiguousMatchException>(() => ReadReflection<AmbiguousMembers>("{\"VALUE\":1}", useStream));
        }

        [TestCase(0)]
        [TestCase(7)]
        [TestCase(15)]
        [TestCase(16)]
        [TestCase(31)]
        [TestCase(32)]
        [TestCase(63)]
        [TestCase(64)]
        public void PortableSurrogateScanningPreservesPairsAndReplacesUnpairedCharacters(int prefixLength)
        {
            var prefix = new string('x', prefixLength);
            foreach (var text in new[]
            {
                prefix + char.ConvertFromUtf32(0x1D11E) + "tail",
                prefix + '\uD800' + "tail",
                prefix + '\uDC00' + "tail",
                prefix + char.ConvertFromUtf32(0x1D11E) + '\uD800' + "tail",
                prefix + '\uFFFF' + '\uFFFE' + "tail"
            })
            {
                var expected = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(text));
                var json = "{\"Name\":\"" + text + "\"}";
                Assert.AreEqual(expected, ReadReflection<Node>(json, false).Name);
                Assert.AreEqual(expected, Read(json, TestContext.Node, false).Name);
            }
        }

        [Test]
        public void PortableMemberCachePublishesCompleteCaseInsensitiveEntries()
        {
            Parallel.For(0, 128, index =>
            {
                var json = index % 2 == 0
                    ? $"{{\"delta\":{index},\"ALPHA\":{index + 1},\"Beta\":{index + 2},\"gamma\":{index + 3}}}"
                    : $"{{\"GAMMA\":{index + 3},\"beta\":{index + 2},\"alpha\":{index + 1},\"DELTA\":{index}}}";
                var actual = ReadReflection<ConcurrentMembers>(json, false);
                Assert.AreEqual(index, actual.Delta);
                Assert.AreEqual(index + 1, actual.Alpha);
                Assert.AreEqual(index + 2, actual.Beta);
                Assert.AreEqual(index + 3, actual.Gamma);
            });
        }

        private static T Read<T>(string json, MiniJsonTypeInfo<T> info, bool useStream)
        {
            if (!useStream)
            {
                return Parser.StringToObject(json, info);
            }
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var result = Parser.StringToObject(stream, info);
            Assert.IsTrue(stream.CanRead);
            return result;
        }

        private static T ReadReflection<T>(string json, bool useStream)
        {
            if (!useStream)
            {
                return (T)Parser.StringToObject(json, typeof(T));
            }
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            return (T)Parser.StringToObject(stream, typeof(T));
        }
    }
}
