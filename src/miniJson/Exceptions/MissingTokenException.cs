using miniJson.Stream;
using System;

namespace miniJson.Exceptions
{
    [Serializable()]
    internal class MissingTokenException : Exception
    {
        private readonly string _S;

        public MissingTokenException(string s, IReader nextChar) : base("Missing: " + s + System.Environment.NewLine + "@" + nextChar.Position + nextChar.Read(25))
        {
            _S = s;
        }

        public string Token
        {
            get { return _S; }
        }
    }
}