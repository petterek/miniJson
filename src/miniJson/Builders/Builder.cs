using miniJson.Stream;
using System;

namespace miniJson.Builders
{
	abstract class Builder
	{
		public abstract object Parse(IReader nextChar, Type t);
	}

}