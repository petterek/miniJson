using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using miniJson.Exceptions;
using miniJson.Stream;

namespace miniJson.Builders
{
    internal class ObjectArrayBuilder : ArrayBuilder
    {
        private readonly Type[] types;

        public ObjectArrayBuilder(Type[] types)
        {
            this.types = types ?? throw new ArgumentNullException(nameof(types));
        }

        public override object Parse(IReader nextChar, Type t)
        {
            if(!typeof(IReadOnlyCollection<object>).IsAssignableFrom(t))
            {
                throw new InvalidConversionException();
            }
            TokenAcceptors.WhiteSpace(nextChar);
            TokenAcceptors.BufferLegalCharacters(nextChar, "nul");
            var buffer = nextChar.Buffer;
            if (buffer == "null")
                return null;

            TokenAcceptors.EatUntil(TokenAcceptors.ListStart, nextChar);

            IParseStrategy objectStrategy;
            IParseStrategy[] strategies;
            if (t.IsArray)
            {
                
                strategies = types.Select(x => new ArrayParserStrategy(x.MakeArrayType())).ToArray();
                objectStrategy = new ArrayParserStrategy(typeof(object[]));
            }
            else
            {
                strategies = types.Select(x => new ListParseStrategy(typeof(List<>).MakeGenericType(x))).ToArray();
                objectStrategy = new ListParseStrategy(typeof(List<object>));
            }

            TokenAcceptors.WhiteSpace(nextChar);

            int i = 0;
            var list = new List<object>();


            if (nextChar.Current() != TokenAcceptors.ListEnd)
            {
                do
                {
                    var strategy = i >= types.Length
                        ? objectStrategy
                        : strategies[i];
                    list.Add(TokenAcceptors.ParseValue(strategy.InnerType, nextChar));
                    i++;
                } while (TokenAcceptors.CanFindValueSeparator(nextChar));
            }

            TokenAcceptors.EatUntil(TokenAcceptors.ListEnd, nextChar);

            if (t.IsArray)
                return list.ToArray();
            return list;
        }
    }
}
