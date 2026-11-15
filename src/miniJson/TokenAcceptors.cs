using miniJson.Builders;
using miniJson.Parsers;
using miniJson.Stream;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace miniJson
{
    internal static class TokenAcceptors
    {
        public const char ListStart = '[';
        public const char ListEnd = ']';
        public const char ObjectStart = '{';
        public const char ObjectEnd = '}';
        public const char Qualifier = ':';
        public const char Separator = ',';

        public const char Hyphen = '"';

        public static Type BuilderFactory = typeof(ObjectBuilder);
        private static readonly object ValuePresent = new object();

        public static void EatUntil(char c, IReader nextChar)
        {
            WhiteSpace(nextChar);
            if (nextChar.Current() != c)
            {
                throw new Exceptions.MissingTokenException(c.ToString(), nextChar);
            }
            nextChar.Read();
        }

        public static void EatUntil(string c, IReader nextChar)
        {
            while (nextChar.BufferPeek.Length < c.Length)
            {
                nextChar.PeekToBuffer();
            }
            while (nextChar.BufferPeek != c)
            {
                nextChar.Read();
                nextChar.PeekToBuffer();
            }
            nextChar.ClearBuffer();
            nextChar.Read();
            //Dump end of buffer
        }

        public static void WhiteSpace(IReader nextchar)
        {
            if (nextchar.Current() > 32)
            {
                return;
            }
            while (nextchar.Peek() <= 32)
            {
                nextchar.Read();
            }
            ConsumeComment(nextchar);
        }

        private static void ConsumeComment(IReader nextchar)
        {
            nextchar.PeekToBuffer();
            //Start of single or multiline comment
            if (nextchar.Current() == '/')
            {
                nextchar.PeekToBuffer();
                if (nextchar.BufferPeek == "//")
                {
                    nextchar.ClearBuffer();
                    EatUntil(System.Environment.NewLine, nextchar);
                }
                if (nextchar.BufferPeek == "/*")
                {
                    nextchar.ClearBuffer();
                    EatUntil("*/", nextchar);
                }
                WhiteSpace(nextchar);
            }
        }

        public static object QuoteOrNull(IReader nextChar)
        {
            if (Quote(nextChar))
            {
                return ValuePresent;
            }

            TokenAcceptors.BufferLegalCharacters(nextChar, "NULnul");
            if (nextChar.Buffer.ToLower() == "null")
            {
                nextChar.ClearBuffer();
                return null;
            }

            return null;
        }

        public static object StartObjectOrNull(IReader nextChar)
        {
            if (nextChar.Current() == ObjectStart)
            {
                nextChar.Read();
                return ValuePresent;
            }
            else
            {
                TokenAcceptors.BufferLegalCharacters(nextChar, "NULnul");
                if (nextChar.Buffer.ToLower() == "null")
                {
                    nextChar.ClearBuffer();
                    return null;
                }
            }
            return null;
        }

        public static bool Quote(IReader nextChar)
        {
            if (nextChar.Current() == Hyphen)
            {
                nextChar.Read();
                //Dump quote
                return true;
            }
            return false;
        }

        public static string Attribute(IReader nextChar)
        {
            return (string)TypeParserMapper[typeof(string)].Parse(nextChar, null);
        }

        public static void Attributes(ref object result, IReader nextChar)
        {
            WhiteSpace(nextChar);
            //Cleaning out whitespace, check for " to ensure not empty object
            if (nextChar.Current() == Hyphen)
            {
                var resultType = result.GetType();
                var members = GetMembers(resultType);
                do
                {
                    WhiteSpace(nextChar);
                    ReadOnlySpan<char> name;
                    if (nextChar is ReadString text && text.TryReadUnescapedString(out var rawName))
                    {
                        name = rawName;
                    }
                    else
                    {
                        var decodedName = Attribute(nextChar);
                        if (decodedName == null)
                        {
                            throw new FormatException("Expected a JSON member name.");
                        }
                        name = decodedName.AsSpan();
                    }
                    EatUntil(Qualifier, nextChar);
                    CreateAttributeValue(nextChar, ref result, resultType, members, name);
                } while (CanFindValueSeparator(nextChar));
            }
        }

        private sealed class MemberParser
        {
            public string Name;
            public Type ParserType;
            public Type FieldType;
            public Func<object, object, object> Setter;
        }

        private static readonly ConcurrentDictionary<Type, MemberCache> parserCache =
            new ConcurrentDictionary<Type, MemberCache>();

        private sealed class MemberCache
        {
            private MemberParser[] entries = Array.Empty<MemberParser>();

            public MemberParser Get(Type type, ReadOnlySpan<char> name)
            {
                var snapshot = Volatile.Read(ref entries);
                var index = Find(snapshot, name);
                if (index >= 0)
                {
                    return snapshot[index];
                }
                lock (this)
                {
                    snapshot = entries;
                    index = Find(snapshot, name);
                    if (index >= 0)
                    {
                        return snapshot[index];
                    }
                    var info = CreateMemberParser(name.ToString(), type);
                    var insertion = ~index;
                    var updated = new MemberParser[snapshot.Length + 1];
                    Array.Copy(snapshot, 0, updated, 0, insertion);
                    updated[insertion] = info;
                    Array.Copy(snapshot, insertion, updated, insertion + 1, snapshot.Length - insertion);
                    Volatile.Write(ref entries, updated);
                    return info;
                }
            }

            private static int Find(MemberParser[] snapshot, ReadOnlySpan<char> name)
            {
                var low = 0;
                var high = snapshot.Length - 1;
                while (low <= high)
                {
                    var middle = low + (high - low) / 2;
                    var comparison = name.CompareTo(snapshot[middle].Name.AsSpan(), StringComparison.OrdinalIgnoreCase);
                    if (comparison == 0)
                    {
                        return middle;
                    }
                    if (comparison < 0)
                    {
                        high = middle - 1;
                    }
                    else
                    {
                        low = middle + 1;
                    }
                }
                return ~low;
            }
        }

        private static MemberCache GetMembers(Type type) =>
            parserCache.GetOrAdd(type, static _ => new MemberCache());

        internal static void ReadUnknownMember(IReader input, object target, ReadOnlySpan<char> name)
        {
            var type = target.GetType();
            CreateAttributeValue(input, ref target, type, GetMembers(type), name);
        }

        internal static void ReadUnknownMember(IReader input, Type type, ReadOnlySpan<char> name)
        {
            var info = GetMemberParser(type, GetMembers(type), name);
            ParseValue(info.FieldType, input, info.ParserType);
        }

        private static MemberParser CreateMemberParser(string name, Type resultType)
        {
            Type fieldType = typeof(UnknownFieldParser);
            Type parserType = null;
            MemberInfo field = null;
            MemberInfo otherMember = null;
            var memberInfo = resultType.GetMember(name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase);
            foreach (var member in memberInfo)
            {
                if (!string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (member is PropertyInfo || member is FieldInfo)
                {
                    if (field != null)
                    {
                        throw new AmbiguousMatchException("Ambiguous JSON member '" + name + "' on '" + resultType.FullName + "'.");
                    }
                    field = member;
                }
                else
                {
                    otherMember ??= member;
                }
            }
            field ??= otherMember;
            if (field != null)
            {
                fieldType = field is PropertyInfo property ? property.PropertyType :
                    field is FieldInfo publicField ? publicField.FieldType : resultType.GetField(field.Name).FieldType;
            }
            if (fieldType.IsEnum)
            {
                parserType = typeof(EnumParser);
            }
            if (Nullable.GetUnderlyingType(fieldType) != null)
            {
                parserType = typeof(NullableParser);
            }
            return new MemberParser
            {
                Name = name,
                ParserType = parserType,
                FieldType = fieldType,
                Setter = SetterCreator.CreateValueTypeSetter(field)
            };
        }

        private static MemberParser GetMemberParser(Type resultType,
            MemberCache members, ReadOnlySpan<char> name) => members.Get(resultType, name);

        private static void CreateAttributeValue(IReader nextChar, ref object result, Type resultType,
            MemberCache members, ReadOnlySpan<char> name)
        {
            var info = GetMemberParser(resultType, members, name);
            object value = ParseValue(info.FieldType, nextChar, info.ParserType);

            if (info.Setter != null)
            {
                if (resultType.IsValueType)
                {
                    result = info.Setter(result, value);
                }
                else
                {
                    info.Setter(result, value);
                }
            }
        }

        public static object ParseValue(Type t, IReader nextChar, Type parserType = null)
        {
            object value;
            Type useParser = t;

            if (parserType != null)
            {
                useParser = parserType;
            }
            else if (Nullable.GetUnderlyingType(t) != null)
            {
                useParser = typeof(NullableParser);
            }

            if (TypeParserMapper.TryGetValue(useParser, out var parser))
            {
                value = parser.Parse(nextChar, t);
            }
            else
            {
                value = Parser.StringToObject(nextChar, t);
            }
            return value;
        }

        public static ReadOnlySpan<char> Number(IReader nextChar)
        {
            WhiteSpace(nextChar);
            ReadOnlySpan<char> number;
            bool valid;
            if (nextChar is ReadString text && text.TryReadNumber(out number, out valid))
            {
                if (!valid)
                {
                    throw new FormatException("Invalid JSON number: " + number.ToString());
                }
                return number;
            }
            while (JsonNumber.IsCharacter(nextChar.Peek()))
            {
                nextChar.PeekToBuffer();
            }
            number = nextChar.BufferSpan;
            var length = JsonNumber.Scan(number, out valid);
            if (!valid || length != number.Length)
            {
                throw new FormatException("Invalid JSON number: " + number.ToString());
            }
            return number;
        }

        public static void BufferLegalCharacters(IReader nextChar, string leagal)
        {
            if (nextChar is ReadString text)
            {
                text.BufferLegalCharacters(leagal);
                return;
            }
            while (leagal.IndexOf(nextChar.Peek()) >= 0)
            {
                nextChar.PeekToBuffer();
            }
        }
        public static void BufferUntil(IReader nextChar, string stop)
        {
            var toArray = stop.ToArray();
            while (toArray.Contains(nextChar.Peek()))
            {
                nextChar.PeekToBuffer();
            }
        }

        public static Dictionary<Type, Builder> TypeParserMapper = new Dictionary<Type, Builder> {
            {
                typeof(string),
                new StringParser()
            },
            {
                typeof(int),
                new IntegerParser()
            },
            {
                typeof(Int64),
                new LongParser()
            },
            {
                typeof(Int16),
                new IntegerParser()
            },
            {
                typeof(System.DateTime),
                new DateParser()
            },
            {
                typeof(double),
                new DoubleParser()
            },
            {
                typeof(decimal),
                new DecimalParser()
            },
            {
                typeof(Guid),
                new GuidParser()
            },
            {
                typeof(bool),
                new BoolanParser()
            },
            {
                typeof(UnknownFieldParser),
                new UnknownFieldParser()
            },
            {
                typeof(EnumParser),
                new EnumParser()
            },
            {
                typeof(NullableParser),
                new NullableParser()
            },
            {
                typeof(float),
                new SingleParser()
            },
            {
                typeof(byte),
                new ByteParser()
            },
            {   typeof(byte[]),
                new ByteArrayParser()
            }

        };

        static internal bool CanFindValueSeparator(IReader nextChar)
        {
            WhiteSpace(nextChar);
            if (nextChar.Current() == TokenAcceptors.Separator)
            {
                nextChar.Read();
                return true;
            }
            return false;
        }
    }
}