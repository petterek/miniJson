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
            MemoryStream mem = new MemoryStream();
            StreamWriter write = new StreamWriter(mem, System.Text.Encoding.UTF8);
            write.Write(input);
            write.Flush();
            write.BaseStream.Position = 0;
            return StringToObject<T>(new StreamReader(mem, System.Text.Encoding.UTF8));
        }
        public static object[] StringToObjects(string input, Type[] types)
        {
            MemoryStream mem = new MemoryStream();
            StreamWriter write = new StreamWriter(mem, System.Text.Encoding.UTF8);
            write.Write(input);
            write.Flush();
            write.BaseStream.Position = 0;
            return StringToObjects(new ReadStream(new StreamReader(mem, System.Text.Encoding.UTF8)), types);
        }

        public static object StringToObject(string input, System.Type type)
        {
            MemoryStream mem = new MemoryStream();
            StreamWriter write = new StreamWriter(mem, System.Text.Encoding.UTF8);
            write.Write(input);
            write.Flush();
            write.BaseStream.Position = 0;
            return StringToObject(new ReadStream(new StreamReader(mem, System.Text.Encoding.UTF8)), type);
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

            if (TokenAcceptors.TypeParserMapper.ContainsKey(type))
            {
                return TokenAcceptors.TypeParserMapper[type].Parse(input, type);
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