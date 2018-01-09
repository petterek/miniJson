using miniJson.Builders;
using miniJson.Parsers;
using miniJson.Stream;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace miniJson
{
    static class TokenAcceptors
    {
        public const char ListStart = '[';
        public const char ListEnd = ']';
        public const char ObjectStart = '{';
        public const char ObjectEnd = '}';
        public const char Qualifier = ':';
        public const char Separator = ',';

        public const char Hyphen = '"';

        public static Type BuilderFactory = typeof(ObjectBuilder);
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
            if (Convert.ToInt32(nextchar.Current()) > 32)
            {
                return;
            }
            while (Convert.ToInt32(nextchar.Peek()) <= 32)
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
                return new object();
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
                return new object();
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
            var stringP = new StringParser();
            return (string)stringP.Parse(nextChar, null);
        }

        public static void Attributes(ref object result, IReader nextChar)
        {
            WhiteSpace(nextChar);
            //Cleaning out whitespace, check for " to ensure not empty object
            if (nextChar.Current() == Hyphen)
            {
                do
                {
                    var name = Attribute(nextChar);
                    EatUntil(Qualifier, nextChar);
                    CreateAttributeValue(nextChar, ref result, name);
                } while (CanFindValueSeparator(nextChar));
            }

        }

        private static Dictionary<string, (Type parser, Type fieldType, MemberInfo field, Func<object, object, object> setter)> parserCache = new Dictionary<string, (Type, Type, MemberInfo, Func<object, object, object>)>();

        private static void CreateAttributeValue(IReader nextChar, ref object result, string name)
        {
            Type resultType = result.GetType();
            var key = $"{resultType.FullName}-{name}";

            if (!parserCache.ContainsKey(key))
            {
                lock (parserCache)
                {
                    if (!parserCache.ContainsKey(key))
                    {
                        //maybe generate all setters for the given type the first time it si encountered
                        Type fType = null;
                        Type parserType = null;
                        MemberInfo fInfo = null;

                        MemberInfo[] memberInfo = resultType.GetMember(name);
                        if (memberInfo.Length != 0)
                        {
                            fInfo = memberInfo[0];
                            if (fInfo.MemberType == MemberTypes.Property)
                            {
                                fType = resultType.GetProperty(name).PropertyType;
                            }
                            else
                            {
                                fType = resultType.GetField(name).FieldType;
                            }
                        }

                        if (fInfo == null) { fType = typeof(UnknownFieldParser); }
                        if (fType.IsEnum) { parserType = typeof(EnumParser); }
                        if (fType.IsGenericType && object.ReferenceEquals(fType.GetGenericTypeDefinition(), typeof(Nullable<>))) { parserType = typeof(NullableParser); }

                        parserCache.Add(key, (parserType, fType, fInfo, SetterCreator.CreateValueTypeSetter(fInfo)));
                    }
                }
            }

            var info = parserCache[key];

            object value = ParseValue(info.fieldType, nextChar, info.parser);

            if (info.field != null)
            {
                if (resultType.IsValueType)
                {

                    result = info.setter(result, value);
                }else
                {
                    info.setter(result, value);
                }
                
            }


            //fInfo.SetValue(result, value)
        }

        public static object ParseValue(Type t, IReader nextChar, Type parserType = null)
        {
            object value;
            Type useParser = t;

            if (parserType != null)
            {
                useParser = parserType;
            }

            if (TypeParserMapper.ContainsKey(useParser))
            {
                value = TypeParserMapper[useParser].Parse(nextChar, t);
            }
            else
            {
                value = Parser.StringToObject(nextChar, t);
            }
            return value;
        }

        public static void BufferLegalCharacters(IReader nextChar, string leagal)
        {
            var toArray = leagal.ToArray();
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