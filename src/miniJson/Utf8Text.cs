using System;
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
                var index = characters.IndexOfAnyInRange('\uD800', '\uDFFF');
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
    }
}
