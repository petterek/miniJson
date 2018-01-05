using System;

namespace miniJson.Exceptions
{
    [Serializable()]
    internal class UnknownAttributeException : Exception
    {
        public UnknownAttributeException(string name) : base(name)
        {
        }
    }
}