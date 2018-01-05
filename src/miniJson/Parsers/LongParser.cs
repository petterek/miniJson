using miniJson.Builders;
using miniJson.Stream;
using System;

namespace miniJson.Parsers
{
	internal class LongParser : Builder
	{


		public override object Parse(IReader nextChar, Type t)
		{
			TokenAcceptors.WhiteSpace(nextChar);
			TokenAcceptors.BufferLegalCharacters(nextChar, "0123456789-");
			return long.Parse(nextChar.Buffer);

		}

	}
}