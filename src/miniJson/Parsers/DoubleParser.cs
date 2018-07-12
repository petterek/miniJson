using miniJson.Builders;
using miniJson.Stream;
using System;
using System.Globalization;

namespace miniJson.Parsers
{
    internal class DoubleParser : Builder
    {
        public override object Parse(IReader nextChar, Type t)
        {
            TokenAcceptors.WhiteSpace(nextChar);
            TokenAcceptors.BufferLegalCharacters(nextChar, "0123456789.-");
            return double.Parse(nextChar.Buffer, CultureInfo.InvariantCulture.NumberFormat);
        }
    }

    internal class SingleParser : Builder
    {
        public override object Parse(IReader nextChar, Type t)
        {
            TokenAcceptors.WhiteSpace(nextChar);
            TokenAcceptors.BufferLegalCharacters(nextChar, "0123456789.-");
            return float.Parse(nextChar.Buffer, CultureInfo.InvariantCulture.NumberFormat);
        }
    }
    
    internal class DecimalParser : Builder
    {
        public override object Parse(IReader nextChar, Type t)
        {
            TokenAcceptors.WhiteSpace(nextChar);
            TokenAcceptors.BufferLegalCharacters(nextChar, "0123456789.-");
            return decimal.Parse(nextChar.Buffer, CultureInfo.InvariantCulture.NumberFormat);
        }
    }
}