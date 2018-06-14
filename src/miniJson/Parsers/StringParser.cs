using miniJson.Builders;
using miniJson.Stream;
using System;
using System.Collections.Generic;

namespace miniJson.Parsers
{
    internal class StringParser : Builder

    {
        public delegate string TransformText(IReader reader);

        public delegate bool Match(string input);

        private static readonly MatchList ML = new MatchList {
            {
                inp => inp == "\\\\",
                (IReader r) =>
                {
                    r.ClearBuffer();
                    return "\\";
                }
            },
            {
                inp => inp == "\\\"",
                (IReader r) =>
                {
                    r.ClearBuffer();
                    return '"'.ToString();
                }
            },
            {
                inp => inp == "\\n",
                (IReader r) =>
                {
                    r.ClearBuffer();
                    return System.Environment.NewLine;
                }
            },
            {
                inp => inp == "\\t",
                (IReader r) =>
                {
                    r.ClearBuffer();
                    return ((char)9).ToString();
                }
            },
            {
                inp => inp == "\\/",
                (IReader r) =>
                {
                    r.ClearBuffer();
                    return "/";
                }
            },
            {
                inp => inp == "\\b",
                (IReader r) =>
                {
                    r.ClearBuffer();
                    return ((char)8).ToString();
                }
            },
            {
                inp => inp == "\\f",
                (IReader r) =>
                {
                    r.ClearBuffer();
                    return ((char)12).ToString();
                }
            },
            {
                inp => inp == "\\r",
                (IReader r) =>
                {
                    r.ClearBuffer();
                    return ((char)13).ToString();
                }
            },
            {
                (string inp) =>
                {
                    if (inp.StartsWith("\\u")) {
                        for (var x = 1; x <= inp.Length; x++) {
                            if ((int)inp[x] < 48 || (int)inp[x] > 57) {
                                return false;
                            }
                        }
                    }
                    return false;
                },
                (IReader r) =>
                {
                    object toTrans = r.Buffer;
                    return "";
					//ChrW()
				}
            }
        };

        public override object Parse(IReader nextChar, Type t)
        {
            System.Text.StringBuilder buffer = new System.Text.StringBuilder();
            TokenAcceptors.WhiteSpace(nextChar);

            if (TokenAcceptors.QuoteOrNull(nextChar) == null)
            {
                return null;
            }

            var cur = nextChar.PeekToBuffer().ToString();
            while (cur != TokenAcceptors.Hyphen.ToString())
            {
                //Not allowed, is the start of an escape
                if (cur == "\\")
                {
                    buffer.Append(nextChar.Buffer);

                    cur = nextChar.PeekToBuffer().ToString();

                    if (cur == "\\65000") throw new System.IO.EndOfStreamException();

                    while (ML.ContainsKey(nextChar.BufferPeek))
                    {
                        cur = nextChar.PeekToBuffer().ToString();
                    }

                    buffer.Append(ML[nextChar.BufferPreLastPeek](nextChar));
                }
                else
                {
                    cur = nextChar.PeekToBuffer().ToString();
                }
            }
            buffer.Append(nextChar.Buffer);
            TokenAcceptors.Quote(nextChar);

            return buffer.ToString();
        }

        private class MatchList : Dictionary<Match, TransformText>
        {
            public bool ContainsKey(string s)
            {
                foreach (var key in this.Keys)
                {
                    if (key(s))
                    {
                        return true;
                    }
                }
                return false;
            }

            public TransformText this[string s]
            {
                get
                {
                    foreach (var i in this)
                    {
                        if (i.Key(s))
                        {
                            return i.Value;
                        }
                    }
                    throw new KeyNotFoundException();
                }
                set
                {
                    foreach (var i in this)
                    {
                        if (i.Key(s))
                        {
                            //i.Value = value
                        }
                    }
                    throw new KeyNotFoundException();
                }
            }
        }
    }
}