using miniJson.Builders;
using miniJson.Stream;
using System;
using System.Text;

namespace miniJson.Parsers
{
    internal class StringParser : Builder
    {
        public override object Parse(IReader nextChar, Type t)
        {
            TokenAcceptors.WhiteSpace(nextChar);
            if (TokenAcceptors.QuoteOrNull(nextChar) == null)
            {
                return null;
            }

            StringBuilder buffer = null;
            nextChar.BufferString();
            while (nextChar.Current() != TokenAcceptors.Hyphen)
            {
                if (buffer == null)
                {
                    buffer = new StringBuilder();
                }
                buffer.Append(nextChar.BufferSpan);
                switch (ReadCharacter(nextChar))
                {
                    case '\\': buffer.Append('\\'); break;
                    case '"': buffer.Append('"'); break;
                    case '/': buffer.Append('/'); break;
                    case 'b': buffer.Append('\b'); break;
                    case 'f': buffer.Append('\f'); break;
                    case 'n': buffer.Append(Environment.NewLine); break;
                    case 'r': buffer.Append('\r'); break;
                    case 't': buffer.Append('\t'); break;
                    case 'u': buffer.Append(ReadUnicodeEscape(nextChar)); break;
                    default: throw new FormatException("Invalid JSON string escape.");
                }
                nextChar.ClearBuffer();
                nextChar.Read();
                nextChar.BufferString();
            }
            var tail = nextChar.Buffer;
            TokenAcceptors.Quote(nextChar);
            if (buffer == null)
            {
                return tail;
            }
            buffer.Append(tail);
            return buffer.ToString();
        }

        private static char ReadUnicodeEscape(IReader reader)
        {
            var value = 0;
            for (var index = 0; index < 4; index++)
            {
                var character = ReadCharacter(reader);
                int digit;
                if (character >= '0' && character <= '9')
                {
                    digit = character - '0';
                }
                else if (character >= 'a' && character <= 'f')
                {
                    digit = character - 'a' + 10;
                }
                else if (character >= 'A' && character <= 'F')
                {
                    digit = character - 'A' + 10;
                }
                else
                {
                    throw new FormatException("Unicode escapes must contain four hexadecimal digits.");
                }
                value = (value << 4) | digit;
            }
            return (char)value;
        }

        private static char ReadCharacter(IReader reader)
        {
            if (!reader.TryPeekToBuffer(out var value))
            {
                throw new System.IO.EndOfStreamException("Unexpected end of JSON string.");
            }
            return value;
        }
    }
}
