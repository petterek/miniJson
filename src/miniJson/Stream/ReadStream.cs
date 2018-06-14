using System.Collections.Generic;
using System.IO;
using System.Text;

namespace miniJson.Stream
{
    internal class ReadStream : IReader
    {
        private readonly StreamReader _StreamReader;
        private readonly Queue<char> internaleBuffer = new Queue<char>();

        private long _position;

        public ReadStream(StreamReader s)
        {
            _StreamReader = s;
        }

        public char Peek()
        {
            if (internaleBuffer.Count > 0)
            {
                return internaleBuffer.ToArray()[internaleBuffer.Count - 1];
            }
            else
            {
                return (char)(_StreamReader.Peek());
            }
        }

        public char PeekToBuffer()
        {
            var ret = (char)_StreamReader.Read();
            internaleBuffer.Enqueue(ret);
            return ret;
        }

        public char Read()
        {
            _position += 1;
            if (internaleBuffer.Count == 0)
            {
                return (char)_StreamReader.Read();
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
                System.Text.StringBuilder ret = new StringBuilder();
                while (internaleBuffer.Count > 1)
                {
                    ret.Append(internaleBuffer.Dequeue());
                }
                return ret.ToString();
            }
        }

        public string BufferPeek
        {
            get { return new string(internaleBuffer.ToArray()); }
        }

        public char Current()
        {
            if (internaleBuffer.Count == 0)
            {
                return (char)(0);
            }
            return internaleBuffer.ToArray()[internaleBuffer.Count - 1];
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
                if (internaleBuffer.Count < 2)
                {
                    return "";
                }
                StringBuilder ret = new StringBuilder();
                var buffA = internaleBuffer.ToArray();
                for (var x = 0; x <= internaleBuffer.Count - 2; x++)
                {
                    ret.Append(buffA[x]);
                }

                return ret.ToString();
            }
        }

        public long Position
        {
            get { return _position; }
        }
    }
}