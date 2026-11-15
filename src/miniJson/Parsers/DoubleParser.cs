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
            return double.Parse(TokenAcceptors.Number(nextChar), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }

    internal class SingleParser : Builder
    {
        public override object Parse(IReader nextChar, Type t)
        {
            return float.Parse(TokenAcceptors.Number(nextChar), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
    
    internal class DecimalParser : Builder
    {
        public override object Parse(IReader nextChar, Type t)
        {
            return decimal.Parse(TokenAcceptors.Number(nextChar), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}