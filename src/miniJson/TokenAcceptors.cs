using miniJson.Builders;
using miniJson.Parsers;
using miniJson.Stream;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
                var members = parserCache.GetOrAdd(resultType,
                    static _ => new ConcurrentDictionary<string, MemberParser>(StringComparer.Ordinal));
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
            public Type ParserType;
            public Type FieldType;
            public Func<object, object, object> Setter;
        }

        private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, MemberParser>> parserCache =
            new ConcurrentDictionary<Type, ConcurrentDictionary<string, MemberParser>>();

        private static MemberParser CreateMemberParser(string name, Type resultType)
        {
            Type fieldType = typeof(UnknownFieldParser);
            Type parserType = null;
            MemberInfo field = null;
            var memberInfo = resultType.GetMember(name);
            if (memberInfo.Length != 0)
            {
                field = memberInfo[0];
                fieldType = field.MemberType == MemberTypes.Property
                    ? resultType.GetProperty(name).PropertyType
                    : resultType.GetField(name).FieldType;
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
                ParserType = parserType,
                FieldType = fieldType,
                Setter = SetterCreator.CreateValueTypeSetter(field)
            };
        }

        private static void CreateAttributeValue(IReader nextChar, ref object result, Type resultType,
            ConcurrentDictionary<string, MemberParser> members, ReadOnlySpan<char> name)
        {
            if (!members.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name, out var info))
            {
                info = members.GetOrAdd(name.ToString(), static (key, type) => CreateMemberParser(key, type), resultType);
            }

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
            BufferLegalCharacters(nextChar, "0123456789.+-eE");
            var number = nextChar.BufferSpan;
            if (!IsJsonNumber(number))
            {
                throw new FormatException("Invalid JSON number: " + number.ToString());
            }
            return number;
        }

        private static bool IsJsonNumber(ReadOnlySpan<char> number)
        {
            var index = 0;
            if (index < number.Length && number[index] == '-')
            {
                index++;
            }
            if (index == number.Length)
            {
                return false;
            }
            if (number[index] == '0')
            {
                index++;
            }
            else
            {
                if (number[index] < '1' || number[index] > '9')
                {
                    return false;
                }
                while (index < number.Length && char.IsAsciiDigit(number[index]))
                {
                    index++;
                }
            }
            if (index < number.Length && number[index] == '.')
            {
                var fractionStart = ++index;
                while (index < number.Length && char.IsAsciiDigit(number[index]))
                {
                    index++;
                }
                if (index == fractionStart)
                {
                    return false;
                }
            }
            if (index < number.Length && (number[index] == 'e' || number[index] == 'E'))
            {
                index++;
                if (index < number.Length && (number[index] == '+' || number[index] == '-'))
                {
                    index++;
                }
                var exponentStart = index;
                while (index < number.Length && char.IsAsciiDigit(number[index]))
                {
                    index++;
                }
                if (index == exponentStart)
                {
                    return false;
                }
            }
            return index == number.Length;
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