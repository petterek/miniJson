using miniJson.Builders;
using miniJson.Stream;
using System;

namespace miniJson.Parsers
{
    internal class IntegerParser : Builder
    {
        public override object Parse(IReader nextChar, Type t)
        {
            TokenAcceptors.WhiteSpace(nextChar);
            TokenAcceptors.BufferLegalCharacters(nextChar, "0123456789-");
            var toParse = nextChar.Buffer;

            return int.Parse(toParse);
        }
    }
}