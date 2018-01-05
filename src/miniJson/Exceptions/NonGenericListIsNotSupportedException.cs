using System;
using System.Runtime.Serialization;

namespace miniJson.Exceptions
{
	[Serializable()]
	internal class NonGenericListIsNotSupportedException : Exception
	{

		public NonGenericListIsNotSupportedException()
		{
		}

		public NonGenericListIsNotSupportedException(string message) : base(message)
		{
		}

		public NonGenericListIsNotSupportedException(string message, Exception innerException) : base(message, innerException)
		{
		}

		protected NonGenericListIsNotSupportedException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
}