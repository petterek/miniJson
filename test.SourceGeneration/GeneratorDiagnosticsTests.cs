using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace miniJson.SourceGeneration.Tests
{
    [TestFixture]
    public class GeneratorDiagnosticsTests
    {
        private static (GeneratorDriverRunResult Generator, Compilation Compilation) Generate(string source)
        {
            var options = new CSharpParseOptions(LanguageVersion.CSharp11);
            var frameworkAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
            var references = frameworkAssemblies.Split(Path.PathSeparator)
                .Append(typeof(Parser).Assembly.Location)
                .Distinct()
                .Select(path => MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create("GeneratorFixture",
                new[] { CSharpSyntaxTree.ParseText(source, options) }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                new[] { new MiniJsonGenerator().AsSourceGenerator() }, parseOptions: options);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
            return (driver.GetRunResult(), output);
        }

        [TestCase("class")]
        [TestCase("record")]
        public void EmitsTypedReadersWithoutRuntimeConstructionOrSetterReflection(string kind)
        {
            var result = Generate("""
                using miniJson;
                [MiniJsonSerializable(typeof(Model[]), "Models")]
                public static partial class Context {}
                public class Model { public int Value; public string Name { get; set; } public int[] Scores { get; set; } }
                """.Replace("public class Model", "public " + kind + " Model"));
            Assert.IsEmpty(result.Generator.Diagnostics);
            Assert.IsEmpty(result.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            var code = result.Generator.GeneratedTrees.Single().GetText().ToString();
            StringAssert.Contains("reader.ReadInt32()", code);
            StringAssert.Contains("result.@Value =", code);
            StringAssert.Contains("global::System.Collections.Generic.List<", code);
            StringAssert.DoesNotContain("Activator", code);
            StringAssert.DoesNotContain("ReadMemberFallback", code);
        }

        [Test]
        public void CollectsAttributesAcrossPartialDeclarationsOnce()
        {
            var result = Generate("""
                using miniJson;
                [MiniJsonSerializable(typeof(int[]), "Ints")]
                public static partial class Context {}
                [MiniJsonSerializable(typeof(bool[]), "Flags")]
                public static partial class Context {}
                """);
            Assert.IsEmpty(result.Generator.Diagnostics);
            Assert.AreEqual(1, result.Generator.GeneratedTrees.Length);
            Assert.IsEmpty(result.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            var code = result.Generator.GeneratedTrees.Single().GetText().ToString();
            StringAssert.Contains("@Ints", code);
            StringAssert.Contains("@Flags", code);
        }

        [Test]
        public void ReportsFallbackAndEmitsUsableDescriptorForUnsupportedModels()
        {
            var result = Generate("""
                using miniJson;
                [MiniJsonSerializable(typeof(Model), "Model")]
                public static partial class Context {}
                public class Model { public string Value { get; set; } public string Unused => "unused"; }
                """);
            Assert.AreEqual("MJG002", result.Generator.Diagnostics.Single().Id);
            Assert.IsEmpty(result.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            StringAssert.Contains("reader.ReadFallback<", result.Generator.GeneratedTrees.Single().GetText().ToString());
        }

        [Test]
        public void CaseCollidingMembersProduceFallbackDiagnostic()
        {
            var result = Generate("""
                using miniJson;
                [MiniJsonSerializable(typeof(Model), "Model")]
                public static partial class Context {}
                public class Model { public int Value; public int value; }
                """);
            Assert.AreEqual("MJG002", result.Generator.Diagnostics.Single().Id);
            Assert.IsEmpty(result.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            StringAssert.Contains("reader.ReadFallback<", result.Generator.GeneratedTrees.Single().GetText().ToString());
        }

        [TestCase("public class Context {}", "MJG001")]
        [TestCase("public partial class Context<T> {}", "MJG001")]
        [TestCase("file partial class Context {}", "MJG001")]
        public void RejectsInvalidContexts(string declaration, string diagnosticId)
        {
            var result = Generate("using miniJson; [MiniJsonSerializable(typeof(int[]), \"Ints\")] " + declaration);
            Assert.IsTrue(result.Generator.Diagnostics.Any(diagnostic => diagnostic.Id == diagnosticId));
        }

        [TestCase("not a name")]
        [TestCase("__MiniJsonRead_0")]
        [TestCase("Context")]
        public void RejectsInvalidDescriptorNames(string name)
        {
            var result = Generate("using miniJson; [MiniJsonSerializable(typeof(int[]), \"" + name +
                "\")] public static partial class Context {}");
            Assert.IsTrue(result.Generator.Diagnostics.Any(diagnostic => diagnostic.Id == "MJG003"));
        }

        [Test]
        public void RejectsDuplicateDescriptorNames()
        {
            var result = Generate("""
                using miniJson;
                [MiniJsonSerializable(typeof(int[]), "Values")]
                [MiniJsonSerializable(typeof(bool[]), "Values")]
                public static partial class Context {}
                """);
            Assert.IsTrue(result.Generator.Diagnostics.Any(diagnostic => diagnostic.Id == "MJG003"));
        }

        [Test]
        public void RejectsOpenRootTypes()
        {
            var result = Generate("""
                using miniJson;
                [MiniJsonSerializable(typeof(System.Collections.Generic.List<>), "Values")]
                public static partial class Context {}
                """);
            Assert.IsTrue(result.Generator.Diagnostics.Any(diagnostic => diagnostic.Id == "MJG003"));
        }

        [Test]
        public void PositionalRecordsGenerateDirectConstructorCalls()
        {
            var result = Generate("""
                using miniJson;
                [MiniJsonSerializable(typeof(Person), "Person")]
                public static partial class Context {}
                public record Person(string Name, int Age);
                """);
            Assert.IsEmpty(result.Generator.Diagnostics);
            Assert.IsEmpty(result.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            var code = result.Generator.GeneratedTrees.Single().GetText().ToString();
            StringAssert.Contains("new global::Person(value_1, value_0)", code);
            StringAssert.DoesNotContain("ReadFallback", code);
            StringAssert.DoesNotContain("Activator", code);
        }

        [Test]
        public void ConstructorSelectionExcludesCopyAndUnmatchedOverloads()
        {
            var result = Generate("""
                using miniJson;
                [MiniJsonSerializable(typeof(Person), "Person")]
                public static partial class Context {}
                public record Person(string Name, int Age)
                {
                    public Person(string name) : this(name, 0) {}
                    public Person(Person original) { Name = original.Name; Age = original.Age; }
                }
                """);
            Assert.IsEmpty(result.Generator.Diagnostics);
            Assert.IsEmpty(result.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            StringAssert.Contains("new global::Person(value_1, value_0)", result.Generator.GeneratedTrees.Single().GetText().ToString());
        }

        [TestCase("""
            public record Person(string Name)
            {
                public int Age { get; set; }
                public Person(string name, int age) : this(name) { Age = age; }
            }
            """)]
        [TestCase("""
            public record Person
            {
                public string Name { get; }
                public Person(int name) { Name = name.ToString(); }
            }
            """)]
        [TestCase("""
            public record Person(string Name) { public string Extra { get; init; } }
            """)]
        public void UnsupportedConstructorBindingsProduceFallbackDiagnostic(string model)
        {
            var result = Generate("""
                using miniJson;
                [MiniJsonSerializable(typeof(Person), "Person")]
                public static partial class Context {}
                """ + model);
            Assert.AreEqual("MJG002", result.Generator.Diagnostics.Single().Id);
            Assert.IsEmpty(result.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            StringAssert.Contains("reader.ReadFallback<", result.Generator.GeneratedTrees.Single().GetText().ToString());
        }

        [Test]
        public void RejectsFileLocalRootTypes()
        {
            var result = Generate("""
                using miniJson;
                [MiniJsonSerializable(typeof(Model), "Model")]
                public static partial class Context {}
                file class Model { public int Value; }
                """);
            Assert.IsTrue(result.Generator.Diagnostics.Any(diagnostic => diagnostic.Id == "MJG003"));
        }

        [Test]
        public void RejectsPointerArrayRootTypes()
        {
            var result = Generate("""
                using miniJson;
                [MiniJsonSerializable(typeof(int*[]), "Values")]
                public unsafe static partial class Context {}
                """);
            Assert.IsTrue(result.Generator.Diagnostics.Any(diagnostic => diagnostic.Id == "MJG003"));
        }

        [Test]
        public void RejectsCollisionsWithGeneratedReaderNames()
        {
            var result = Generate("""
                using miniJson;
                [MiniJsonSerializable(typeof(int[]), "Values")]
                public static partial class Context { private static int __MiniJsonRead_0(MiniJsonReader reader) => 0; }
                """);
            Assert.IsTrue(result.Generator.Diagnostics.Any(diagnostic => diagnostic.Id == "MJG003"));
        }
    }
}
