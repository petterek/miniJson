using miniJson.Builders;
using miniJson.Stream;
using System;

namespace miniJson.Parsers
{
	 class GuidParser : Builder
	{


		public override object Parse(IReader nextChar, Type t)
		{
			TokenAcceptors.WhiteSpace(nextChar);
			if (TokenAcceptors.QuoteOrNull(nextChar) == null) {
				return null;
			}
			TokenAcceptors.BufferLegalCharacters(nextChar, "0123456789ABCDEFabcdef-{}");
			object val = new Guid(nextChar.Buffer);
			TokenAcceptors.Quote(nextChar);
			return val;

		}
	}
}