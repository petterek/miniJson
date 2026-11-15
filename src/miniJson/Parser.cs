using miniJson.Builders;
using miniJson.Stream;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace miniJson
{
    public static class Parser
    {
        public static T StringToObject<T>(string input) where T : new()
        {
            return (T)StringToObject(input, typeof(T));
        }

        public static T StringToObject<T>(string input, MiniJsonTypeInfo<T> typeInfo)
        {
            if (typeInfo == null)
            {
                throw new ArgumentNullException(nameof(typeInfo));
            }
            return typeInfo.Deserialize(new ReadString(Utf8Text.Normalize(input)));
        }

        public static T StringToObject<T>(System.IO.Stream input, MiniJsonTypeInfo<T> typeInfo)
        {
            if (typeInfo == null)
            {
                throw new ArgumentNullException(nameof(typeInfo));
            }
            using (var reader = new StreamReader(input, System.Text.Encoding.UTF8, true, 1024, true))
            {
                return typeInfo.Deserialize(new ReadStream(reader));
            }
        }

        public static object[] StringToObjects(string input, Type[] types)
        {
            return StringToObjects(new ReadString(Utf8Text.Normalize(input)), types);
        }

        public static object[] StringToObjects(System.IO.Stream input, Type[] types)
        {
            return StringToObjects(new ReadStream(new StreamReader(input, System.Text.Encoding.UTF8)), types);
        }

        public static object StringToObject(string input, System.Type type)
        {
            return StringToObject(new ReadString(Utf8Text.Normalize(input)), type);
        }

        //Here we can add another function that accepts stream as parameter
        public static T StringToObject<T>(StreamReader input) where T : new()
        {
            return (T)StringToObject(new ReadStream(input), typeof(T));
        }

        public static T StringToObject<T>(System.IO.Stream input) where T : new()
        {
            return (T)StringToObject(new ReadStream(new StreamReader(input, System.Text.Encoding.UTF8)), typeof(T));
        }

        public static object StringToObject(System.IO.Stream input, Type type)
        {
            return StringToObject(new ReadStream(new StreamReader(input)), type);
        }

        static internal object[] StringToObjects(IReader input, Type[] types)
        {
            Builder builder = new ObjectArrayBuilder(types);
            return (object[])builder.Parse(input, typeof(object[]));
        }

        /// <summary>
        /// This is the main function to parse the incomming stream of bytes.
        /// It is important that the types is checked in the correct order.
        ///
        /// </summary>
        /// <param name="input"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        static internal object StringToObject(IReader input, Type type)
        {
            Builder builder;

            //we are trying to create something that is an interface. The we must guess what to create :)
            //This should boil down to IEnumerable of something.. :)

            if (TokenAcceptors.TypeParserMapper.TryGetValue(type, out var parser))
            {
                return parser.Parse(input, type);
            }
            else if (type.IsGenericType && object.ReferenceEquals(type.GetGenericTypeDefinition(), typeof(IEnumerable<>)))
            {
                builder = new ArrayBuilder();
            }
            else if (type.IsGenericType && object.ReferenceEquals(type.GetGenericTypeDefinition(), typeof(IList<>)))
            {
                builder = new ArrayBuilder();
            }
            else if (typeof(IList).IsAssignableFrom(type))
            {
                builder = new ArrayBuilder();
            }
            else if (typeof(IDictionary).IsAssignableFrom(type))
            {
                builder = new DictionaryBuilder();
            }
            else if (typeof(ICollection).IsAssignableFrom(type))
            {
                builder = new ArrayBuilder();
            }
            else
            {
                builder = new ObjectBuilder();
            }

            return builder.Parse(input, type);
        }
    }
}