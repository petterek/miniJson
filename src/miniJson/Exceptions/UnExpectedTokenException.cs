using System;

namespace miniJson.Exceptions
{
	public class UnExpectedTokenException : Exception
	{

		public UnExpectedTokenException(char current) : base(current.ToString())
		{
		}
	}
}