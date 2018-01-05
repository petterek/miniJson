using miniJson.Stream;
using System;

namespace miniJson.Builders
{
	 class ObjectBuilder : Builder
	{


		public override object Parse(IReader nextChar, Type t)
		{
			TokenAcceptors.WhiteSpace(nextChar);

			if (TokenAcceptors.StartObjectOrNull(nextChar) == null) {
				return null;
			}

			var Result = Activator.CreateInstance(t);

			TokenAcceptors.Attributes(ref Result, nextChar);

			TokenAcceptors.EatUntil(TokenAcceptors.ObjectEnd, nextChar);

			return Result;
		}
	}
}