using miniJson.Parsers;
using miniJson.Stream;
using System;

namespace miniJson
{
    public sealed class MiniJsonReader
    {
        private readonly IReader input;

        internal MiniJsonReader(IReader input)
        {
            this.input = input;
        }

        public int ReadInt32() => IntegerParser.Read(input);
        public long ReadInt64() => LongParser.Read(input);
        public byte ReadByte() => ByteParser.Read(input);
        public double ReadDouble() => DoubleParser.Read(input);
        public float ReadSingle() => SingleParser.Read(input);
        public decimal ReadDecimal() => DecimalParser.Read(input);
        public bool ReadBoolean() => BoolanParser.Read(input);
        public string ReadString() => TokenAcceptors.Attribute(input);

        public bool StartObject()
        {
            TokenAcceptors.WhiteSpace(input);
            return TokenAcceptors.StartObjectOrNull(input) != null;
        }

        public bool HasObjectMember()
        {
            TokenAcceptors.WhiteSpace(input);
            return input.Current() == TokenAcceptors.Hyphen;
        }

        public ReadOnlySpan<char> ReadMemberName()
        {
            TokenAcceptors.WhiteSpace(input);
            ReadOnlySpan<char> name;
            if (input is ReadString text && text.TryReadUnescapedString(out var rawName))
            {
                name = rawName;
            }
            else
            {
                var decodedName = TokenAcceptors.Attribute(input);
                if (decodedName == null)
                {
                    throw new FormatException("Expected a JSON member name.");
                }
                name = decodedName.AsSpan();
            }
            TokenAcceptors.EatUntil(TokenAcceptors.Qualifier, input);
            return name;
        }

        public void ReadUnknownMember(object target, ReadOnlySpan<char> name)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            TokenAcceptors.ReadUnknownMember(input, target, name);
        }

        public void ReadUnknownMember<T>(ReadOnlySpan<char> name) =>
            TokenAcceptors.ReadUnknownMember(input, typeof(T), name);

        public void EndObject() => TokenAcceptors.EatUntil(TokenAcceptors.ObjectEnd, input);

        public bool StartArray()
        {
            TokenAcceptors.WhiteSpace(input);
            TokenAcceptors.BufferLegalCharacters(input, "nul");
            if (input.BufferSpan.SequenceEqual("null"))
            {
                return false;
            }
            TokenAcceptors.EatUntil(TokenAcceptors.ListStart, input);
            return true;
        }

        public bool HasArrayValue()
        {
            TokenAcceptors.WhiteSpace(input);
            return input.Current() != TokenAcceptors.ListEnd;
        }

        public bool MoveNext() => TokenAcceptors.CanFindValueSeparator(input);
        public void EndArray() => TokenAcceptors.EatUntil(TokenAcceptors.ListEnd, input);

        public T ReadFallback<T>() => (T)Parser.StringToObject(input, typeof(T));

        public T ReadMemberFallback<T>() => (T)TokenAcceptors.ParseValue(typeof(T), input,
            typeof(T).IsEnum ? typeof(EnumParser) : null);
    }
}
