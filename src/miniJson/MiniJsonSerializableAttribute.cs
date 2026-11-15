using System;

namespace miniJson
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class MiniJsonSerializableAttribute : Attribute
    {
        public MiniJsonSerializableAttribute(Type type, string propertyName)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }
            if (propertyName == null)
            {
                throw new ArgumentNullException(nameof(propertyName));
            }
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException("The property name cannot be empty or whitespace.", nameof(propertyName));
            }
            Type = type;
            PropertyName = propertyName;
        }

        public Type Type { get; }
        public string PropertyName { get; }
    }
}
