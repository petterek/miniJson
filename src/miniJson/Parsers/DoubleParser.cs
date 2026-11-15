using miniJson.Builders;
using miniJson.Stream;
using System;
using System.Buffers.Text;
using System.Globalization;

namespace miniJson.Parsers
{
    internal class DoubleParser : Builder
    {
        public override object Parse(IReader nextChar, Type t)
        {
            return Read(nextChar);
        }

        internal static double Read(IReader nextChar)
        {
            var number = TokenAcceptors.Number(nextChar);
            if (number.Length <= 64)
            {
                // Number has already validated that every token character is ASCII.
                Span<byte> bytes = stackalloc byte[64];
                for (var index = 0; index < number.Length; index++)
                {
                    bytes[index] = (byte)number[index];
                }
                if (Utf8Parser.TryParse(bytes.Slice(0, number.Length), out double value, out var consumed) &&
                    consumed == number.Length)
                {
                    return value;
                }
            }
            return double.Parse(number, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }

    internal class SingleParser : Builder
    {
        public override object Parse(IReader nextChar, Type t)
        {
            return Read(nextChar);
        }

        internal static float Read(IReader nextChar)
        {
            return float.Parse(TokenAcceptors.Number(nextChar), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
    
    internal class DecimalParser : Builder
    {
        public override object Parse(IReader nextChar, Type t)
        {
            return Read(nextChar);
        }

        internal static decimal Read(IReader nextChar)
        {
            return decimal.Parse(TokenAcceptors.Number(nextChar), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}