using miniJson.Builders;
using miniJson.Stream;
using System;

namespace miniJson.Parsers
{
    internal class NullableParser : Builder
    {
        public override object Parse(IReader nextChar, Type t)
        {
            TokenAcceptors.WhiteSpace(nextChar);

            string v = nextChar.BufferPeek;
            //Guess this is a NULL
            if (v == "N" | v == "n")
            {
                TokenAcceptors.BufferLegalCharacters(nextChar, "nulNUL");
                nextChar.ClearBuffer();
                return null;
            }

            var tParser = t.GetGenericArguments()[0];
            if (TokenAcceptors.TypeParserMapper.ContainsKey(tParser))
            {
                return TokenAcceptors.TypeParserMapper[tParser].Parse(nextChar, tParser);
            }
            else
            {
                return Parser.StringToObject(nextChar, tParser);
            }
        }
    }
}