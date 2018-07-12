using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace miniJson
{
    public class Writer
    {
        public static void ObjectToString(System.IO.Stream result, object o)
        {
            ObjectToString(new System.IO.StreamWriter(result), o);
        }

        public static string ObjectToString(object o)
        {
            var result = new StreamWriter(new MemoryStream(), System.Text.Encoding.UTF8);
            ObjectToString(result, o);
            result.Flush();
            result.BaseStream.Position = 0;
            return new StreamReader(result.BaseStream).ReadToEnd();
        }

        private static MethodInfo writeToStreamMethodInfo = typeof(StreamWriter).GetMethod("Write", new Type[] { typeof(string) });
        private static MethodInfo writeValueMethodInfo = typeof(Writer).GetMethod("WriteValue", BindingFlags.NonPublic | BindingFlags.Static);

        private static Action<StreamWriter, object> Serializer(Type objType)
        {
            List<Expression> exprns = new List<Expression>();
            var writer = Expression.Parameter(typeof(StreamWriter));
            var valueObject = Expression.Parameter(typeof(object));

            exprns.Add(Expression.Call(writer, writeToStreamMethodInfo, Expression.Constant("{")));
            bool first = true;

            if (AddTypeInfoForObjects)
            {
                exprns.Add(Expression.Call(writer, writeToStreamMethodInfo, Expression.Constant("\"$type$\":")));
                var typeString = Expression.Call(null, TypeInfoWriter.Method, valueObject);
                exprns.Add(Expression.Call(null, writeValueMethodInfo, writer, typeString));
                exprns.Add(Expression.Call(writer, writeToStreamMethodInfo, Expression.Constant('"'.ToString())));
                first = false;
            }

            foreach (var m in GetMembers(objType))
            {
                if (m.GetCustomAttributes(typeof(System.Runtime.Serialization.IgnoreDataMemberAttribute), false).Count() != 0)
                {
                    continue;
                }

                if (!first)
                {
                    exprns.Add(Expression.Call(writer, writeToStreamMethodInfo, Expression.Constant(",")));
                }
                first = false;

                Expression memberValue = null;
                if (m.MemberType == MemberTypes.Property)
                {
                    PropertyInfo propInfo = null;
                    foreach (var p in m.DeclaringType.GetProperties())
                    {
                        if (p.Name == m.Name)
                        {
                            propInfo = p;
                            break;
                        }
                    }
                    memberValue = Expression.Property(Expression.Convert(valueObject, objType), propInfo);
                }
                else
                {
                    memberValue = Expression.Field(Expression.Convert(valueObject, objType), m.Name);
                }

                exprns.Add(Expression.Call(writer, writeToStreamMethodInfo, Expression.Constant((char)0x22 + m.Name + (char)0x22 + ":")));
                exprns.Add(Expression.Call(null, writeValueMethodInfo, writer, Expression.Convert(memberValue, typeof(object))));

                //      var expr2 = Expression.Lambda(Expression.Call(
                //      Expression.Convert(Expression.PropertyOrField(Expression.Constant(parent), "Data"), typeof(ICollection<>).MakeGenericType(parent.Data.GetType().GetGenericArguments()))
                //, "Clear", null, null), null);
            }

            exprns.Add(Expression.Call(writer, writeToStreamMethodInfo, Expression.Constant("}")));

            var block = Expression.Block(exprns.ToArray());

            Expression<Action<StreamWriter, object>> expression1 = Expression.Lambda<Action<StreamWriter, object>>(block, writer, valueObject);
            expression1.Reduce();
            return expression1.Compile();
        }

        private delegate string GetTypeInfo(object t);

        private static Dictionary<Type, MyWriter> Formatters = new Dictionary<Type, MyWriter> {
            {
                typeof(int),
                (w, val) => w.Write(val.ToString())
            },
            {
                typeof(long),
                (w, val) => w.Write(val.ToString())
            },
            {
                typeof(uint),
                (w, val) => w.Write(val.ToString())
            },
            {
                typeof(ulong),
                (w, val) => w.Write(val.ToString())
            },
            {
                typeof(double),
                WriteNumber
            },
            {
                typeof(float),
                WriteNumber
            },
            {
                typeof(string),
                Writetext
            },
            {
                typeof(decimal),
                WriteNumber
            },
            {
                typeof(System.DateTime),
                WriteDate
            },
            {
                typeof(Guid),
                Writetext
            },
            {
                typeof(bool),
                (w, val) =>
                {
                    if ((bool)val) {
                        w.Write("true");
                    } else {
                        w.Write("false");
                    }
                }
            },
            {
                typeof(byte),
                (w, val) => w.Write(val.ToString())
            },

        };

        public static bool AddTypeInfoForObjects;

        private static GetTypeInfo TypeInfoWriter = DefaultTypeInfoWriter;

        private static string DefaultTypeInfoWriter(object t)
        {
            return t.GetType().FullName;
        }

        private delegate void MyWriter(StreamWriter writer, object value);

        private static void ObjectToString(StreamWriter result, object o)
        {
            if (o == null)
            {
                result.Write("null");
                return;
            }

            if (Formatters.ContainsKey(o.GetType()))
            {
                Formatters[o.GetType()](result, o);
            }
            else
            {
                if ((o) is IDictionary)
                {
                    WriteDictionary(result, o);
                }
                else if ((o) is IEnumerable)
                {
                    WriteList(result, o);
                }
                else
                {
                    WriteObject(result, o);
                }
            }

            result.Flush();
        }

        private static void WriteDictionary(StreamWriter result, object o)
        {
            result.Write("{");
            bool first = true;
            foreach (DictionaryEntry value in (IDictionary)o)
            {
                if (!first)
                {
                    result.Write(",");
                }
                result.Write((char)(0x22) + value.Key.ToString() + (char)(0x22));
                result.Write(":");
                ObjectToString(result, value.Value);
                first = false;
            }

            result.Write("}");
        }

        private static object padLock = new object();
        private static Dictionary<Type, List<System.Reflection.MemberInfo>> typeinfoCache = new Dictionary<Type, List<System.Reflection.MemberInfo>>();

        private static IEnumerable<System.Reflection.MemberInfo> GetMembers(Type t)
        {
            if (!typeinfoCache.ContainsKey(t))
            {
                lock (padLock)
                {
                    if (!typeinfoCache.ContainsKey(t))
                    {
                        typeinfoCache.Add(t, t.GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Where(v => v.MemberType == System.Reflection.MemberTypes.Field | v.MemberType == System.Reflection.MemberTypes.Property).ToList());
                    }
                }
            }
            return typeinfoCache[t];
        }

        private static Dictionary<Type, Action<StreamWriter, object>> funcCache = new Dictionary<Type, Action<StreamWriter, object>>();

        private static object funcCacheLock = new object();

        private static void WriteObject(StreamWriter result, object o)
        {
            if (!funcCache.ContainsKey(o.GetType()))
            {
                lock (funcCacheLock)
                {
                    if (!funcCache.ContainsKey(o.GetType()))
                    {
                        funcCache[o.GetType()] = Serializer(o.GetType());
                    }
                }
            }

            funcCache[o.GetType()](result, o);
            return;
        }

        private static void WriteList(StreamWriter result, object o)
        {
            bool first = true;
            result.Write("[");
            foreach (var element in (IEnumerable)o)
            {
                if (!first)
                {
                    result.Write(',');
                }
                ObjectToString(result, element);
                first = false;
            }
            result.Write("]");
        }

        private static void WriteValue(StreamWriter writer, object value)
        {
            if (value == null)
            {
                writer.Write("null");
                return;
            }

            System.Type t = value.GetType();

            if (t.IsEnum)
            {
                t = value.GetType().GetEnumUnderlyingType();
                value = Convert.ChangeType(value, t);
            }

            if (Formatters.ContainsKey(t))
            {
                Formatters[t](writer, value);
            }
            else
            {
                if (value.GetType().IsValueType)
                {
                    if (value.GetType().GetMembers(System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Count() > 0)
                    {
                        ObjectToString(writer, value);
                    }
                    else
                    {
                        writer.Write(value.ToString());
                    }
                }
                else
                {
                    ObjectToString(writer, value);
                }
            }
        }

        private static void WriteDictionary(StreamWriter writer, Type t, IDictionary value)
        {
        }

        private static readonly int[] ToEscape = {
            0x22,
            0x2,
            0x5c
        };

        private static Dictionary<int, string> Translate = new Dictionary<int, string> {
            {
                0x9,
                "\\t"
            },
            {
                0xa,
                "\\n"
            },
            {
                0xc,
                "\\f"
            },
            {
                0xd,
                "\\r"
            }
        };

        private static void Writetext(StreamWriter writer, object value)
        {
            writer.Write((char)0x22);
            foreach (var c in value.ToString())
            {
                if (ToEscape.Contains((int)c))
                {
                    writer.Write("\\");
                }
                if (Translate.ContainsKey((int)c))
                {
                    writer.Write(Translate[(int)c]);
                }
                else
                {
                    writer.Write(c);
                }
            }
            writer.Write((char)0x22);
        }

        private static void WriteNumber(StreamWriter w, object val)
        {
            w.Write(val.ToString().Replace(',', '.'));
        }

        private static void WriteDate(StreamWriter w, object value)
        {
            System.DateTime d = (System.DateTime)value;
            w.Write((char)34);
            w.Write(d.ToString("yyyy-MM-ddTHH\\:mm\\:ss.FFFK"));
            w.Write((char)34);
        }
    }
}