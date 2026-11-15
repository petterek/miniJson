using System;

namespace miniJson
{
    internal static class JsonNumber
    {
        internal static bool IsCharacter(char value) =>
            IsDigit(value) || value is '.' or '+' or '-' or 'e' or 'E';

        private static bool IsDigit(char value) => (uint)(value - '0') <= 9;

        internal static int Scan(ReadOnlySpan<char> input, out bool valid)
        {
            var index = 0;
            if (index < input.Length && input[index] == '-')
            {
                index++;
            }
            if (index == input.Length)
            {
                goto Invalid;
            }
            if (input[index] == '0')
            {
                index++;
            }
            else if (input[index] is >= '1' and <= '9')
            {
                do
                {
                    index++;
                } while (index < input.Length && IsDigit(input[index]));
            }
            else
            {
                goto Invalid;
            }
            if (index < input.Length && input[index] == '.')
            {
                index++;
                var firstDigit = index;
                while (index < input.Length && IsDigit(input[index]))
                {
                    index++;
                }
                if (index == firstDigit)
                {
                    goto Invalid;
                }
            }
            if (index < input.Length && input[index] is 'e' or 'E')
            {
                index++;
                if (index < input.Length && input[index] is '+' or '-')
                {
                    index++;
                }
                var firstDigit = index;
                while (index < input.Length && IsDigit(input[index]))
                {
                    index++;
                }
                if (index == firstDigit)
                {
                    goto Invalid;
                }
            }
            if (index < input.Length && IsCharacter(input[index]))
            {
                goto Invalid;
            }
            valid = true;
            return index;

        Invalid:
            valid = false;
            while (index < input.Length && IsCharacter(input[index]))
            {
                index++;
            }
            return index;
        }
    }
}
