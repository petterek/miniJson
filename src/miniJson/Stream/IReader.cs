using System;

namespace miniJson.Stream
{
    internal interface IReader
    {
        char Read();

        string Read(int count);

        char PeekToBuffer();

        bool TryPeekToBuffer(out char value);

        // Leaves the next quote or backslash buffered as the final lookahead character.
        void BufferString();

        char Current();

        void ClearBuffer();

        string Buffer { get; }
        ReadOnlySpan<char> BufferSpan { get; }
        string BufferPeek { get; }
        string BufferPreLastPeek { get; }

        char Peek();

        long Position { get; }
    }
}