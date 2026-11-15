using System;
using System.Text;

namespace miniJson.Stream
{
    internal sealed class ReadString : IReader
    {
        private readonly string input;
        private int start;
        private int end;
        private int endMarkers;
        private long position;

        public ReadString(string input)
        {
            this.input = input;
        }

        private int BufferedCount => end - start + endMarkers;

        public char Peek()
        {
            if (BufferedCount > 0)
            {
                return Current();
            }
            return end < input.Length ? input[end] : char.MaxValue;
        }

        public char PeekToBuffer()
        {
            if (TryPeekToBuffer(out var value))
            {
                return value;
            }
            // Numeric parsers retain a buffered EOF sentinel as their last lookahead.
            endMarkers++;
            return char.MaxValue;
        }

        public bool TryPeekToBuffer(out char value)
        {
            if (end == input.Length)
            {
                value = default;
                return false;
            }
            value = input[end++];
            return true;
        }

        public void BufferString()
        {
            var offset = input.AsSpan(end).IndexOfAny('"', '\\');
            if (offset < 0)
            {
                end = input.Length;
                throw new System.IO.EndOfStreamException("Unexpected end of JSON string.");
            }
            end += offset + 1;
        }

        public bool TryReadUnescapedString(out ReadOnlySpan<char> value)
        {
            value = default;
            if (BufferedCount != 1 || Current() != '"')
            {
                return false;
            }
            var offset = input.AsSpan(end).IndexOfAny('"', '\\');
            if (offset < 0 || input[end + offset] != '"')
            {
                return false;
            }
            Read();
            value = input.AsSpan(end, offset);
            end += offset + 1;
            start = end - 1;
            Read();
            return true;
        }

        public void BufferLegalCharacters(string legal)
        {
            if (legal.IndexOf(Peek()) < 0)
            {
                return;
            }
            var remaining = input.AsSpan(end);
            var offset = 0;
            while (offset < remaining.Length && legal.IndexOf(remaining[offset]) >= 0)
            {
                offset++;
            }
            if (offset == remaining.Length)
            {
                end = input.Length;
                endMarkers++;
            }
            else
            {
                end += offset + 1;
            }
        }

        public bool TryReadNumber(out ReadOnlySpan<char> number, out bool valid)
        {
            number = default;
            valid = false;
            if (endMarkers != 0 || end - start != 1)
            {
                return false;
            }
            var remaining = input.AsSpan(start);
            var length = JsonNumber.Scan(remaining, out valid);
            number = remaining.Slice(0, length);
            start += length;
            end = start;
            if (end < input.Length)
            {
                end++;
            }
            else
            {
                endMarkers++;
            }
            return true;
        }

        public char Read()
        {
            position++;
            if (start < end)
            {
                return input[start++];
            }
            if (endMarkers > 0)
            {
                endMarkers--;
                return char.MaxValue;
            }
            if (end < input.Length)
            {
                var value = input[end++];
                start = end;
                return value;
            }
            return char.MaxValue;
        }

        public string Read(int count)
        {
            var result = new StringBuilder();
            for (var index = 0; index < count; index++)
            {
                result.Append(Read());
            }
            return result.ToString();
        }

        public char Current()
        {
            if (endMarkers > 0)
            {
                return char.MaxValue;
            }
            return start < end ? input[end - 1] : '\0';
        }

        public string Buffer => BufferSpan.ToString();

        public ReadOnlySpan<char> BufferSpan
        {
            get
            {
                var count = Math.Max(0, BufferedCount - 1);
                var result = count <= end - start
                    ? input.AsSpan(start, count)
                    : GetBuffer(count).AsSpan();
                ConsumeBuffer(count);
                return result;
            }
        }

        public string BufferPeek => GetBuffer(BufferedCount);

        public string BufferPreLastPeek => GetBuffer(Math.Max(0, BufferedCount - 1));

        public void ClearBuffer()
        {
            ConsumeBuffer(Math.Max(0, BufferedCount - 1));
        }

        private string GetBuffer(int count)
        {
            var realCount = Math.Min(count, end - start);
            if (realCount == count)
            {
                return input.Substring(start, count);
            }
            return string.Create(count, (input, start, realCount), static (characters, state) =>
            {
                state.input.AsSpan(state.start, state.realCount).CopyTo(characters);
                characters.Slice(state.realCount).Fill(char.MaxValue);
            });
        }

        private void ConsumeBuffer(int count)
        {
            var realCount = Math.Min(count, end - start);
            start += realCount;
            endMarkers -= count - realCount;
        }

        public long Position => position;
    }
}
