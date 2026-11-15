# miniJson
A C# implementation of json parser/writer. 

Written because I wanted to :) 

Intended to be simple and might not cover all use cases.

Small codebase, should be understandable. 

Supports string and stream input/output.

## Contents

- [Version 3.0.0](#version-300)
- [Released version 2.0.0](#released-version-200)
- [Install](#install)
- [Usage](#usage)
- [Experimental source-generated deserialization](#experimental-source-generated-deserialization)
- [Compatibility and limitations](#compatibility-and-limitations)
- [Build and test](#build-and-test)
- [Benchmarks](#benchmarks)

## Version 3.0.0

The runtime library targets **.NET Standard 2.1** and includes opt-in
source-generated deserialization for mutable classes, arrays, and immutable
record classes. JSON fields and properties match case-insensitively in both
generated and reflection readers. Double parsing uses a faster shared numeric
scanner and stack-buffer conversion; serialization is unchanged.

**Breaking behavior change:** member matching is now case-insensitive.
Case-colliding fields/properties are rejected as ambiguous, and the last
matching JSON value wins. Dictionary keys and serialized member names are
unchanged.

The experimental generator is distributed separately as
`miniJson.SourceGeneration`. Existing APIs remain reflection-based unless
you pass a generated descriptor.

## Released version 2.0.0

**Breaking compatibility change:** the released 2.0.0 library targets .NET 11 instead of
.NET Standard 2.0. Applications targeting older frameworks must stay on 1.4.0
or upgrade to .NET 11.

This release adds Unicode escape and exponent-number support, preserves null
array positions, and rejects unterminated strings without hanging. String APIs
avoid UTF-8 round trips and use indexed, bulk-scanned input; numeric tokens and
cached unescaped member names use spans to reduce allocations.

The original 100-object deserialization benchmark improved from 307.936 us /
795,425 allocated bytes to 89.319 us / 81,074 bytes: an observed 3.45x speedup
and 89.8% fewer allocated bytes. Shared-machine short runs are indicative,
not performance guarantees.

## Install

The 3.0.0 runtime package targets .NET Standard 2.1; .NET Framework is not
supported. Packages are distributed through
[GitHub Releases](https://github.com/petterek/miniJson/releases/tag/v3.0.0),
not NuGet.org.

Download `miniJson.3.0.0.nupkg` into a local `packages` directory, then run
from your application's project directory:

```sh
dotnet add package miniJson --version 3.0.0 --source ./packages
```

For source-generated deserialization, also download
`miniJson.SourceGeneration.3.0.0.nupkg` to the same directory and run:

```sh
dotnet add package miniJson.SourceGeneration --version 3.0.0 --source ./packages
```

Set `PrivateAssets="all"` on the generator's `PackageReference` so it remains
a build-time dependency of your project.

## Usage

Supply the concrete .NET type to deserialize:

```csharp
using miniJson;

var item = Parser.StringToObject<Item>("{\"Name\":\"example\"}");
string json = Writer.ObjectToString(item);

public class Item
{
    public string Name { get; set; } = "";
}
```

For arrays, use the type-based overload:

```csharp
var items = (Item[])Parser.StringToObject(
    "[{\"Name\":\"example\"}]", typeof(Item[]));
```

Stream overloads use the same known-type model and leave caller-owned streams
open. `Parser.StringToObjects` accepts explicitly supplied positional types
for heterogeneous arrays.

## Experimental source-generated deserialization

Available in the separate 3.0.0 analyzer package, **not in the
released 2.0.0 package**. This opt-in feature generates typed object construction,
member assignments, and array parsing; it reuses the existing tokenizer and
scalar readers. Serialization is unchanged.

The runtime library targets **.NET Standard 2.1**; .NET Framework
does not support this target. The analyzer targets .NET Standard 2.0. Building
this checkout still requires the .NET 11 SDK for its tests and benchmarks.
The generator requires a C# compiler host with Roslyn 4.14 or newer.
Projects declaring records on .NET Standard 2.1 also need a
`System.Runtime.CompilerServices.IsExternalInit` compatibility shim.

After installing both packages, your application should reference them as:

```xml
<ItemGroup>
  <PackageReference Include="miniJson" Version="3.0.0" />
  <PackageReference Include="miniJson.SourceGeneration" Version="3.0.0"
                    PrivateAssets="all" />
</ItemGroup>
```

When working from a source checkout, you can instead reference both projects,
adjusting the paths:

```xml
<ItemGroup>
  <ProjectReference Include="../src/miniJson/miniJson.csproj" />
  <ProjectReference Include="../src/miniJson.SourceGeneration/miniJson.SourceGeneration.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

Register known types on a top-level, non-generic partial class:

```csharp
using System.IO;
using System.Text;
using miniJson;

const string json = "[{\"Name\":\"example\",\"Age\":42,\"Scores\":[1,2]}]";
Person[] people = Parser.StringToObject(json, AppJsonContext.People);
using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
Person[] streamedPeople = Parser.StringToObject(stream, AppJsonContext.People);

[MiniJsonSerializable(typeof(Person[]), "People")]
internal static partial class AppJsonContext { }

public record Person(string Name, int Age, int[] Scores);
```

Passing a generated descriptor opts in; existing `typeof(Person[])` calls
still use reflection. The descriptor overloads do not require a parameterless
constructor, unlike `Parser.StringToObject<T>(json)` with no descriptor.
Generated readers support writable public fields and
properties on classes with a public parameterless constructor, supported
primitive types, and their one-dimensional arrays. Mutable records with
`get; set;` properties also work.

Immutable record classes are read into typed locals, then constructed once
after the object is parsed. A unique public non-copy constructor must have
parameters matching public fields/properties by name (ignoring case) and exact
type. Read-only/init-only members must be constructor-bound; additional
writable members are assigned only when present, preserving their initializers.
Missing arguments use optional parameter defaults or `default(T)` otherwise.
Explicit JSON null overrides an optional default where the existing type
reader accepts null. Nested records, record arrays, and recursive record
graphs use the same generated readers.

**3.0.0 behavior change:** JSON property and field names now match
case-insensitively in both generated and reflection readers. The last matching
value wins. Dictionary keys and serialized CLR member names are unchanged.
Case-colliding schemas produce a generator fallback warning and throw
`AmbiguousMatchException` when an ambiguous name is read.

Unsupported members use existing reflection parsing; unsupported roots emit
`MJG002` and a reflection-backed descriptor. Ambiguous/unmatched record
constructors, unbound read-only/init-only members, required members, and
record structs do not get generated readers. Reflection fallback still needs
its existing constructor/type support; immutable records generally require
a generated descriptor. Byte arrays retain their Base64 format.
The generator is experimental; serialization is not generated.

The implementation is in
[`src/miniJson.SourceGeneration/MiniJsonGenerator.cs`](src/miniJson.SourceGeneration/MiniJsonGenerator.cs).
It emits `MiniJsonTypeInfo<T>` descriptors that call the shared `MiniJsonReader`.
Generator diagnostics identify invalid contexts (`MJG001`), unsupported root
types using reflection fallback (`MJG002`), and invalid registrations (`MJG003`).

### Experiment results

Mean microseconds / allocated bytes per complete 100-element array operation,
measured on 2026-11-15 with the .NET Standard 2.1 library running on .NET 11.
All implementations use the same BenchmarkDotNet ShortRun:

| Workload | Reflection miniJson | Generated miniJson | System.Text.Json | Newtonsoft.Json |
| --- | ---: | ---: | ---: | ---: |
| ASCII objects | 84.098 / 77,874 | 24.271 / 22,288 | 27.469 / 22,688 | 51.613 / 33,856 |
| Unicode objects | 102.740 / 106,676 | 40.606 / 51,088 | 43.362 / 27,488 | 62.532 / 40,728 |
| Exponent doubles | 8.103 / 5,872 | 6.508 / 3,088 | 7.646 / 3,016 | 15.649 / 15,200 |

Generated object readers measured 3.46x / 2.53x faster than reflection and
roughly matched System.Text.Json throughput on these workloads. Unicode
allocations remain higher. The exponent row was refreshed after numeric
optimization; its generated reader now measures roughly System.Text.Json
throughput and about 2.40x faster than its previous 15.596 us result.
These are warmed, shared-machine short runs, not performance guarantees.
The object workloads use mutable classes, not immutable records.
The original 24-case run and updated 8-case exponent run completed with
setup-time value checks.

Floating-point tokens now use a dedicated ASCII grammar scanner, combining
token boundaries and validation in one pass for string input. Double conversion
uses `Utf8Parser` through a fixed 64-byte stack buffer, with `double.Parse` for
longer tokens or conversions the UTF-8 parser cannot handle. A separate
conversion benchmark measured 7.134 us for char-span parsing versus 4.399 us
including the ASCII stack-buffer copy, per 100 exponent tokens.

## Compatibility and limitations

miniJson is a known-type parser, not an arbitrary dynamic JSON parser or a
complete JSON syntax validator. The released-version check accepted all 95
valid JSONTestSuite fixtures with declared schemas, but the typed adapter also
accepted 31 invalid fixtures and excluded three unsupported deep-recursion
schemas. Acceptance
does not prove full syntax validation or value preservation.

Existing wire behavior is retained: `\n` becomes `Environment.NewLine`,
string APIs replace unpaired UTF-16 surrogates, and stream output rejects them.
Byte arrays use Base64 and unknown object members are not assigned.
Released 2.0.0 matches member names case-sensitively; 3.0.0
uses case-insensitive matching.

## Build and test

Requires the .NET 11 SDK.

```sh
dotnet restore miniJson.sln
dotnet build miniJson.sln
dotnet test test/test.vbproj
dotnet test test.SourceGeneration/miniJson.SourceGeneration.Tests.csproj
dotnet test test/test.vbproj --filter "FullyQualifiedName=test.TestParser.ParseSimpleObject"
dotnet test test/test.vbproj -c Release --collect:"Code Coverage;Format=Cobertura"
```

The runtime library targets .NET Standard 2.1; the analyzer targets .NET
Standard 2.0; tests and benchmarks target .NET 11. Building the runtime library
also creates its NuGet packages. To pack the analyzer:

```sh
dotnet pack src/miniJson.SourceGeneration/miniJson.SourceGeneration.csproj -c Release
```

Both suites pass: **247 tests** (163 VB.NET tests and 84 source-generation
integration/diagnostic tests). The Release build also succeeds, with existing
VB.NET warnings and expected generator fallback warnings in the tests.
Released-version coverage was 87.1% of library lines and 82.8% of library
branches; these figures
exclude test assemblies, the generator experiment, and external JSONTestSuite
runs.

## Benchmarks

```sh
dotnet run -c Release --project benchmarks/miniJson.Benchmarks.csproj -- --filter '*'
```

### Released 2.0.0 deserialization results

Mean microseconds / managed bytes allocated per complete 100-element array
operation, measured on 2026-11-14 with .NET 11:

| Workload | miniJson | System.Text.Json | Newtonsoft.Json |
| --- | ---: | ---: | ---: |
| ASCII objects | 89.319 / 81,074 | 28.864 / 22,688 | 53.677 / 33,856 |
| Unicode objects | 109.457 / 109,876 | 44.576 / 27,488 | 64.318 / 40,728 |
| Exponent doubles | 13.938 / 5,872 | 7.492 / 3,016 | 18.993 / 15,200 |

Released 2.0.0 measured approximately 3.09x / 2.46x / 1.86x slower than System.Text.Json
for these workloads. Full methodology and optimization history follow.

### Methodology

The BenchmarkDotNet short run compares miniJson, System.Text.Json, and
Newtonsoft.Json serialization and deserialization with three workloads:
arrays of ASCII objects (integer, string, boolean, and integer-array properties),
the same objects with Unicode and escaped strings, and arrays of doubles with
exponent-form input. Each workload uses 1 or 100 elements. Each
operation processes the whole array using string input/output. Setup verifies
equivalent payloads before measurement; data generation and verification are
not timed. Warmup excludes first-use serializer compilation from the results.
Reports, including timings and managed allocations, are written to
`BenchmarkDotNet.Artifacts/results/`. Short-run results on shared machines
are indicative, not a performance guarantee.
The benchmark project uses a BenchmarkDotNet preview for .NET 11 support.

### Measurement history

Baseline measured on 2026-11-14 with .NET 11.0.0, SDK 11.0.100, Ubuntu 24.04,
and an Intel Core Ultra 5 225U in a shared virtualized environment. Values are
mean microseconds / managed bytes allocated per complete array operation.

| Operation | Objects | miniJson | System.Text.Json | Newtonsoft.Json |
| --- | ---: | ---: | ---: | ---: |
| Serialize | 1 | 0.788 / 6,440 | 0.235 / 456 | 0.439 / 1,776 |
| Serialize | 100 | 36.251 / 66,064 | 13.052 / 12,568 | 27.194 / 45,168 |
| Deserialize | 1 | 3.863 / 14,360 | 0.427 / 784 | 0.838 / 3,048 |
| Deserialize | 100 | 307.936 / 795,425 | 29.588 / 22,688 | 51.449 / 33,856 |

Rerun after the valid-JSON fixes, using the same SDK, machine, payloads, and
short-run configuration:

| miniJson operation | Objects | Before (us) | After (us) | Time change | Allocated bytes, before / after |
| --- | ---: | ---: | ---: | ---: | ---: |
| Serialize | 1 | 0.788 | 0.917 | +16.3% | 6,440 / 6,440 |
| Serialize | 100 | 36.251 | 36.336 | +0.2% | 66,064 / 66,064 |
| Deserialize | 1 | 3.863 | 4.089 | +5.9% | 14,360 / 14,232 |
| Deserialize | 100 | 307.936 | 312.675 | +1.5% | 795,425 / 777,051 |

These are observed differences, not established regressions: the short-run
confidence intervals are wide, and unchanged System.Text.Json and
Newtonsoft.Json also measured 11-12% slower for single-object serialization
and roughly 7% slower for 100-object deserialization. miniJson deserialization
allocations decreased by 0.9% and 2.3%, respectively. This reuses the original
ASCII/integer payload and does not measure Unicode-escape or exponent-heavy
workloads.

After hot-path optimizations, compared with the post-valid-JSON-fix rerun:

| miniJson operation | Objects | Before (us) | Optimized (us) | Speedup | Allocated bytes, before / optimized |
| --- | ---: | ---: | ---: | ---: | ---: |
| Serialize | 1 | 0.917 | 0.647 | 1.42x | 6,440 / 6,440 |
| Serialize | 100 | 36.336 | 23.248 | 1.56x | 66,064 / 68,688 |
| Deserialize | 1 | 4.089 | 2.511 | 1.63x | 14,232 / 8,256 |
| Deserialize | 100 | 312.675 | 144.778 | 2.16x | 777,051 / 203,051 |

Reader lookahead now caches the last buffered character instead of copying
the queue, buffer strings are constructed directly, and string scanning no
longer creates per-character strings. Member-cache keys use type/name tuples
instead of concatenated strings. Token and formatter lookups avoid temporary
arrays and repeated searches. Writers flush once at the public boundary
rather than recursively, and string escaping uses character dispatch.

Deserialization allocations fell by 42.0% for one object and 73.9% for 100.
The buffered writer trades a 4.0% allocation increase for the faster
100-object serialization result. All 125 unit tests pass, and the typed
JSONTestSuite acceptance/rejection outcomes are unchanged, including all 95
valid cases accepted. Timings remain short-run measurements on a shared VM.

After removing UTF-8 round trips from string APIs, compared with the preceding
hot-path optimization:

| miniJson operation | Objects | Before (us) | Direct text (us) | Observed speedup | Allocated bytes, before / direct text |
| --- | ---: | ---: | ---: | ---: | ---: |
| Serialize | 1 | 0.647 | 0.283 | 2.28x | 6,440 / 736 |
| Serialize | 100 | 23.248 | 19.884 | 1.17x | 68,688 / 44,656 |
| Deserialize | 1 | 2.511 | 1.740 | 1.44x | 8,256 / 2,440 |
| Deserialize | 100 | 144.778 | 139.483 | 1.04x | 203,051 / 178,614 |

String input now uses `StringReader`, and string output uses `StringWriter`.
Internal parsing/writing shares `TextReader`/`TextWriter` with stream APIs.
Valid UTF-16 avoids encoding entirely; malformed surrogates retain the former
string API's UTF-8 replacement behavior. Stream output still rejects malformed
surrogates, and caller-owned streams remain open. Null string input and leading
BOM behavior are preserved.

Serialization allocations fell by 88.6% / 35.0%, and deserialization allocations
by 70.4% / 12.0% for 1 / 100 objects. The small 100-object deserialization timing
difference is within short-run uncertainty, not an established improvement.
All 135 unit tests pass, and typed JSONTestSuite outcomes remain unchanged.

Initial miniJson measurements for the new workloads (mean microseconds /
allocated bytes per complete array operation):

| Operation | Elements | Unicode objects | Exponent doubles |
| --- | ---: | ---: | ---: |
| Serialize | 1 | 0.338 / 984 | 0.128 / 312 |
| Serialize | 100 | 23.444 / 66,128 | 9.755 / 14,312 |
| Deserialize | 1 | 3.600 / 4,976 | 1.125 / 960 |
| Deserialize | 100 | 343.881 / 426,636 | 50.221 / 13,360 |

These workloads have no pre-change baseline. All 36 benchmark cases completed
with setup-time value checks. Unicode input includes escaped BMP characters,
a surrogate pair, quotes, and a backslash; exponent input alternates tiny and
large finite doubles. The original ASCII workload remains unchanged.

After replacing delegate-based string escape matching with direct character
dispatch and four-digit hexadecimal decoding, deserialization measured:

| Workload | Elements | Before (us) | Direct escapes (us) | Allocated bytes, before / direct escapes |
| --- | ---: | ---: | ---: | ---: |
| ASCII objects | 1 | 1.740 | 1.684 | 2,440 / 2,440 |
| ASCII objects | 100 | 139.483 | 129.874 | 178,614 / 178,614 |
| Unicode objects | 1 | 3.600 | 2.017 | 4,976 / 2,816 |
| Unicode objects | 100 | 343.881 | 166.289 | 426,636 / 210,626 |
| Exponent doubles | 1 | 1.125 | 1.027 | 960 / 960 |
| Exponent doubles | 100 | 50.221 | 43.621 | 13,360 / 13,360 |

Unicode deserialization is 1.78x / 2.07x faster with 43.4% / 50.6% fewer
allocated bytes for 1 / 100 objects. Escape decoding no longer materializes
temporary matcher strings, searches delegates, or parses hexadecimal substrings.
ASCII and exponent allocation counts are unchanged; their timing differences
are not established gains, since short-run uncertainty and unchanged competitor
timings also vary.

In this run, System.Text.Json deserialized 100 Unicode objects in 45.580 us
with 27,488 allocated bytes, versus miniJson's 166.289 us and 210,626 bytes:
miniJson remains about 3.65x slower for this workload.
All 18 deserialization benchmarks completed with value checks, all 140 unit
tests pass, and all 318 typed JSONTestSuite outcomes remain unchanged
(including three unsupported-schema exclusions).

The next optimization replaces string input's `StringReader`/character queue
with an indexed `ReadString` implementation of `IReader`. Buffer text is sliced
from the existing normalized string instead of enqueued and copied character
by character. Unescaped strings also bypass `StringBuilder`; escaped strings
still use the shared decoder. Stream input retains `ReadStream`.

| Deserialization workload | Elements | Before (us) | Indexed input (us) | Allocated bytes, before / indexed input |
| --- | ---: | ---: | ---: | ---: |
| ASCII objects | 1 | 1.684 | 1.389 | 2,440 / 1,592 |
| ASCII objects | 100 | 129.874 | 100.969 | 178,614 / 108,011 |
| Unicode objects | 1 | 2.017 | 1.618 | 2,816 / 2,056 |
| Unicode objects | 100 | 166.289 | 124.746 | 210,626 / 154,423 |
| Exponent doubles | 1 | 1.027 | 0.838 | 960 / 672 |
| Exponent doubles | 100 | 43.621 | 37.861 | 13,360 / 13,072 |

At 100 elements, observed speedups are 1.29x for ASCII objects and 1.33x for
Unicode objects, with 39.5% and 26.7% fewer allocated bytes. In the same run,
System.Text.Json measured 28.537 us for ASCII objects, 44.446 us for Unicode
objects, and 7.559 us for exponent doubles; miniJson is respectively 3.54x,
2.81x, and 5.01x slower. Timings remain indicative shared-machine short runs.

No pooled array is needed for string input: it uses the caller's existing
string, and returned string values have independent managed lifetimes.
All 145 unit tests and all 18 deserialization benchmarks pass. Each API's
318 typed JSONTestSuite outcomes were compared separately before and after;
both remain unchanged, including all 95 valid cases accepted per API.

The next combined optimization adds bulk string scanning, span numeric tokens,
and span-based member-name lookup. Indexed input finds quotes/backslashes with
`IndexOfAny` rather than per-character interface calls. Integer, byte, and
floating-point parsers consume `BufferSpan`; floating-point/decimal validation
uses an ASCII grammar scanner instead of regex. Existing integer acceptance
rules are unchanged.

Member metadata is cached lazily in per-type concurrent dictionaries with
ordinal alternate span lookup. Warm, unescaped object member names from string
input no longer require a string allocation. Escaped names still use the shared
decoder, and dictionary keys still become strings because they are result data.
Stream input retains queued token materialization.

| Deserialization workload | Elements | Before (us) | Combined spans (us) | Allocated bytes, before / combined spans |
| --- | ---: | ---: | ---: | ---: |
| ASCII objects | 1 | 1.389 | 1.272 | 1,592 / 1,352 |
| ASCII objects | 100 | 100.969 | 89.319 | 108,011 / 81,074 |
| Unicode objects | 1 | 1.618 | 1.424 | 2,056 / 1,640 |
| Unicode objects | 100 | 124.746 | 109.457 | 154,423 / 109,876 |
| Exponent doubles | 1 | 0.838 | 0.535 | 672 / 600 |
| Exponent doubles | 100 | 37.861 | 13.938 | 13,072 / 5,872 |

At 100 elements, observed speedups are 1.13x / 1.14x / 2.72x for ASCII objects,
Unicode objects, and exponent doubles, with 24.9% / 28.8% / 55.1% fewer allocated
bytes. The smaller object-workload timing differences remain indicative, not
established gains, given the wide short-run confidence intervals.

In the same run, System.Text.Json measured 28.864 / 44.576 / 7.492 us for the
three 100-element workloads, leaving miniJson approximately 3.09x / 2.46x /
1.86x slower. The one-element System.Text.Json exponent timing was particularly
noisy; these measurements are not performance guarantees.

All 163 unit tests pass, including bulk-scan boundary cases, number grammar
regressions, and escaped/case-sensitive/inherited member lookup with cold and
warm caches. Both APIs retain their previous typed JSONTestSuite outcomes,
and all 18 deserialization benchmarks completed with value checks.
