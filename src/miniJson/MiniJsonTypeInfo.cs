using miniJson.Stream;
using System;

namespace miniJson
{
    public sealed class MiniJsonTypeInfo<T>
    {
        private readonly Func<MiniJsonReader, T> read;

        public MiniJsonTypeInfo(Func<MiniJsonReader, T> read)
        {
            if (read == null)
            {
                throw new ArgumentNullException(nameof(read));
            }
            this.read = read;
        }

        internal T Deserialize(IReader input) => read(new MiniJsonReader(input));
    }
}
