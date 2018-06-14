using System;

namespace miniJson.Exceptions
{
    [Serializable()]
    internal class PropertyNotFoundException : Exception
    {
        public PropertyNotFoundException(string result) : base(result)
        {
        }
    }
}