using miniJson.Builders;
using miniJson.Stream;
using System;

namespace miniJson.Parsers
{
    internal class ByteArrayParser : Builder
    {
        public override object Parse(IReader nextChar, Type t)
        {
            
            var data =  TokenAcceptors.Attribute(nextChar);

            if (data==null) return null;
            
            return System.Convert.FromBase64String(data);

        }
    }
}
