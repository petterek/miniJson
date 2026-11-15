using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace miniJson
{
    internal static class Utf8Text
    {
        public static string Normalize(string input)
        {
            // StreamWriter.Write(null) historically produced an empty string input.
            if (input == null)
            {
                return string.Empty;
            }

            var characters = input.AsSpan();
            while (true)
            {
                var index = IndexOfSurrogate(characters);
                if (index < 0)
                {
                    return input;
                }
                if (char.IsHighSurrogate(characters[index]) &&
                    index + 1 < characters.Length &&
                    char.IsLowSurrogate(characters[index + 1]))
                {
                    characters = characters.Slice(index + 2);
                }
                else
                {
                    // Preserve the former UTF-8 round trip's replacement of unpaired surrogates.
                    return Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(input));
                }
            }

        }

        private static int IndexOfSurrogate(ReadOnlySpan<char> characters)
        {
            var index = 0;
            if (Vector.IsHardwareAccelerated)
            {
                var values = MemoryMarshal.Cast<char, ushort>(characters);
                var minimum = new Vector<ushort>(0xD800);
                var range = new Vector<ushort>(0x7FF);
                while (index <= values.Length - Vector<ushort>.Count)
                {
                    var block = MemoryMarshal.Read<Vector<ushort>>(MemoryMarshal.AsBytes(values.Slice(index)));
                    var matches = Vector.LessThanOrEqual(Vector.Subtract(block, minimum), range);
                    if (!Vector.EqualsAll(matches, Vector<ushort>.Zero))
                    {
                        break;
                    }
                    index += Vector<ushort>.Count;
                }
            }
            for (; index < characters.Length; index++)
            {
                if (char.IsSurrogate(characters[index]))
                {
                    return index;
                }
            }
            return -1;
        }
    }
}
