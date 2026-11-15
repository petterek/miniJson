using System;
using System.Collections.Generic;
using System.IO;

namespace miniJson.Stream
{
    internal class ReadStream : IReader
    {
        private readonly TextReader _Reader;
        private readonly Queue<char> internaleBuffer = new Queue<char>();
        private char lastBuffered;

        private long _position;

        public ReadStream(TextReader s)
        {
            _Reader = s;
        }

        public char Peek()
        {
            if (internaleBuffer.Count > 0)
            {
                return lastBuffered;
            }
            else
            {
                return (char)(_Reader.Peek());
            }
        }

        public char PeekToBuffer()
        {
            if (TryPeekToBuffer(out var value))
            {
                return value;
            }
            internaleBuffer.Enqueue(char.MaxValue);
            lastBuffered = char.MaxValue;
            return char.MaxValue;
        }

        public bool TryPeekToBuffer(out char value)
        {
            var next = _Reader.Read();
            if (next < 0)
            {
                value = default;
                return false;
            }
            value = (char)next;
            internaleBuffer.Enqueue(value);
            lastBuffered = value;
            return true;
        }

        public void BufferString()
        {
            while (TryPeekToBuffer(out var value))
            {
                if (value == '"' || value == '\\')
                {
                    return;
                }
            }
            throw new EndOfStreamException("Unexpected end of JSON string.");
        }

        public char Read()
        {
            _position += 1;
            if (internaleBuffer.Count == 0)
            {
                return (char)_Reader.Read();
            }
            else
            {
                return internaleBuffer.Dequeue();
            }
        }

        public string Read(int count)
        {
            string retVal = "";
            for (var x = 0; x <= count - 1; x++)
            {
                retVal += Read();
            }

            return retVal;
        }

        public string Buffer
        {
            get
            {
                return string.Create(Math.Max(0, internaleBuffer.Count - 1), internaleBuffer, static (characters, queue) =>
                {
                    for (var index = 0; index < characters.Length; index++)
                    {
                        characters[index] = queue.Dequeue();
                    }
                });
            }
        }

        public string BufferPeek
        {
            get { return PeekBuffer(internaleBuffer.Count); }
        }

        public ReadOnlySpan<char> BufferSpan => Buffer.AsSpan();

        public char Current()
        {
            if (internaleBuffer.Count == 0)
            {
                return (char)(0);
            }
            return lastBuffered;
        }

        public void ClearBuffer()
        {
            while (internaleBuffer.Count > 1)
            {
                internaleBuffer.Dequeue();
            }
        }

        public string BufferPreLastPeek
        {
            get
            {
                return PeekBuffer(Math.Max(0, internaleBuffer.Count - 1));
            }
        }

        private string PeekBuffer(int count)
        {
            return string.Create(count, internaleBuffer, static (characters, queue) =>
            {
                var index = 0;
                foreach (var value in queue)
                {
                    if (index == characters.Length)
                    {
                        break;
                    }
                    characters[index++] = value;
                }
            });
        }

        public long Position
        {
            get { return _position; }
        }
    }
}