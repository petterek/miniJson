# Repository guidance

miniJson is a small C# JSON parser/writer with string and stream APIs, intentionally simple
and not intended to cover every JSON use case (see `readme.md`). The library
and its VB.NET test project both target `net11.0`.
The library has no external package references. Newtonsoft.Json is used in
tests and benchmarks as a comparison serializer/deserializer.

## Build and test

Run from the repository root with the .NET 11 SDK:

```sh
dotnet restore miniJson.sln
dotnet build miniJson.sln
dotnet test test/test.vbproj

# Coverage in Cobertura format using the test SDK's collector
dotnet test test/test.vbproj -c Release --collect:"Code Coverage;Format=Cobertura"

# Single test
dotnet test test/test.vbproj --filter "FullyQualifiedName=test.TestParser.ParseSimpleObject"

# One fixture
dotnet test test/test.vbproj --filter "FullyQualifiedName~test.WriteJson."

# Library-only build
dotnet build src/miniJson/miniJson.csproj

# Short-run serialization/deserialization benchmarks with allocation tracking
dotnet run -c Release --project benchmarks/miniJson.Benchmarks.csproj -- --filter '*'
```

Building the library also generates a NuGet package because
`GeneratePackageOnBuild` is enabled. Tests use NUnit 3 and NUnit3TestAdapter
through the VSTest runner.

## Architecture

- `Parser` is the public deserialization entry point. String and stream
  overloads converge on `IReader`. String input uses indexed `ReadString`
  slices without a UTF-8 round trip or character queue; stream input uses
  `ReadStream`, backed by `TextReader`. Dispatch first checks
  `TokenAcceptors.TypeParserMapper`, then chooses a collection, dictionary,
  or object builder. Dispatch order matters, particularly for strings and
  byte arrays versus general collections.
- All scalar parsers and composite builders implement
  `Builder.Parse(IReader, Type)`. `TokenAcceptors` owns shared token handling,
  member lookup, and recursive value dispatch. `ObjectBuilder` constructs
  objects via `Activator.CreateInstance`; `ArrayBuilder` uses strategies for
  arrays versus generic lists; `ObjectArrayBuilder` supports positional types
  supplied to `Parser.StringToObjects`.
- Member parsing caches member types, parser choices, and compiled setters.
  Per-type concurrent member dictionaries use `StringComparer.Ordinal` and
  alternate span lookup so warmed, unescaped string-input names need no string
  allocation. Discover and compile members lazily: unused read-only members
  must not cause deserialization to fail.
  `SetterCreator` lives in `SetterCache.cs` and builds expression-tree
  delegates. Struct assignment must return and replace the boxed value;
  reference-type assignment mutates the existing instance.
- `Writer.ObjectToString` is the public serialization entry point, with
  string-returning and stream-writing overloads. It dispatches built-in
  formatters before dictionaries, enumerables, and ordinary objects.
  Object serializers are compiled expression trees cached per runtime type;
  member metadata is cached separately. Internal writers and compiled delegates
  use `TextWriter`; string output uses `StringWriter`, stream output uses
  `StreamWriter` and flushes once at the public boundary.

## Key conventions

- Preserve the reader's lookahead contract: `PeekToBuffer` advances the
  underlying input into a logical buffer; `Current` returns the last buffered
  character, not the next unread character. Reading `Buffer` or `BufferSpan` consumes all
  buffered characters except the last, and `ClearBuffer` also preserves that
  last character. Parsers rely on this to leave delimiters for their callers.
  `PeekToBuffer` buffers `char.MaxValue` at EOF for numeric/token parsing;
  `TryPeekToBuffer` instead reports EOF without buffering it. `ReadString`
  tracks EOF markers separately from real U+FFFF characters.
  `BufferString` scans through the next quote or backslash and leaves that
  boundary buffered; indexed input uses bulk span scanning.
- JSON member names map directly and case-sensitively to public .NET fields
  and properties. Unknown members are routed through `UnknownFieldParser`
  rather than assigned. Serialization includes public instance fields and
  properties, including inherited members, and honors `IgnoreDataMember`.
- Collection interfaces `IEnumerable<T>` and `IList<T>` are materialized as
  `List<T>`. Non-generic lists are rejected. Dictionary parsing supports
  concrete generic dictionaries with string keys and has a separate
  non-generic `IDictionary` path.
- Adding a scalar type involves the parser registry in `TokenAcceptors` and
  the formatter registry in `Writer`; also consider member, collection, and
  top-level dispatch, which take different paths. Enum and nullable member
  parsing use explicit parser overrides.
- Preserve existing wire representations: byte arrays are Base64 strings,
  enum members serialize numerically, floating-point parsing uses invariant
  culture, and dates are written with `yyyy-MM-ddTHH\:mm\:ss.FFFK`.
  String parsing maps `\n` to `Environment.NewLine`.
  Floating-point/decimal tokens are validated against JSON number grammar
  before span parsing; do not broaden acceptance to .NET numeric syntax.
- String APIs use `Utf8Text.Normalize` to preserve null-input and unpaired
  surrogate replacement behavior without encoding valid UTF-16. Stream output
  retains its strict encoder (unpaired surrogates throw), and caller-owned
  streams remain open. Preserve both paths when changing text handling.
- `Writer.AddTypeInfoForObjects` is static, and its value is read when a
  serializer is compiled, not on every write. Changes involving this flag
  must account for the per-type serializer cache and test state.
- Tests are VB.NET NUnit fixtures in `test/TestParser.vb`,
  `test/WriteJson.vb`, and `test/TestCornerCase.vb`, with test model classes
  alongside them. Existing tests mix exact JSON assertions, round trips,
  and Newtonsoft.Json comparisons; retain stream-overload coverage when
  changing serialization or encoding.
