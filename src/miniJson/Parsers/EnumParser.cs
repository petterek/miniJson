using miniJson.Builders;
using miniJson.Stream;
using System;

namespace miniJson.Parsers
{
    internal class EnumParser : Builder
    {
        public override object Parse(IReader nextChar, Type t)
        {
            TokenAcceptors.WhiteSpace(nextChar);
            object value;

            if (nextChar.Current() == TokenAcceptors.Hyphen)
            {
                value = TokenAcceptors.TypeParserMapper[typeof(string)].Parse(nextChar, typeof(string));
            }
            else
            {
                value = TokenAcceptors.TypeParserMapper[t.GetEnumUnderlyingType()].Parse(nextChar, t.GetEnumUnderlyingType());
            }

            return Enum.Parse(t, value.ToString());
        }
    }
}