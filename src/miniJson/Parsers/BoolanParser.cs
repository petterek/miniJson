using miniJson.Builders;
using miniJson.Stream;
using System;

namespace miniJson.Parsers
{
    internal class BoolanParser : Builder
    {
        public override object Parse(IReader nextChar, Type t)
        {
            TokenAcceptors.WhiteSpace(nextChar);
            TokenAcceptors.BufferLegalCharacters(nextChar, "TtrueFfals");
            var bufferVal = nextChar.Buffer;

            bool res;
            if (!bool.TryParse(bufferVal, out res))
            {
                throw new InvalidCastException(bufferVal + " is not a boolean. True/False");
            }
            return res;
        }
    }
}