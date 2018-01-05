using miniJson.Builders;
using miniJson.Stream;
using System;

namespace miniJson.Parsers
{
	class DateParser : Builder
	{


		public override object Parse(IReader nextChar, Type t)
		{
			TokenAcceptors.WhiteSpace(nextChar);

			if (TokenAcceptors.QuoteOrNull(nextChar) == null) {
				return null;
			}
			TokenAcceptors.BufferLegalCharacters(nextChar, "0123456789.:T+Z- ");

			var bufferVal = nextChar.Buffer;

			TokenAcceptors.Quote(nextChar);

			return System.DateTime.Parse(bufferVal);

		}
	}
}