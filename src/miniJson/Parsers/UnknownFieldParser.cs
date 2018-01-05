using miniJson.Builders;
using miniJson.Parsers;
using miniJson.Stream;
using System;
using System.Collections.Generic;

namespace miniJson.Parsers
{
     class UnknownFieldParser : Builder
    {


        public override object Parse(IReader nextChar, Type t)
        {
            TokenAcceptors.WhiteSpace(nextChar);
            object value = null;

            var peek = nextChar.Peek();

            switch ((int)peek)
            {
                case 34:
                    //This is a " start of string pass on to stringparser... 
                    value = TokenAcceptors.TypeParserMapper[typeof(string)].Parse(nextChar, typeof(string));
                    break;
                case 45:
                case 46:
                case 48:
                case 49:
                case 50:
                case 51:
                case 52:
                case 53:
                case 54:
                case 55:
                case 56:
                case 57:
                    //This is a number of some kind..
                    value = TokenAcceptors.TypeParserMapper[typeof(long)].Parse(nextChar, typeof(double));
                    break;
                case 70:
                case 84:
                case 116:
                case 102:
                    // T, F, t, f -> boolean
                    value = TokenAcceptors.TypeParserMapper[typeof(bool)].Parse(nextChar, typeof(BoolanParser));
                    break;
                case 44:
                    value = null;
                    break;

                case 110:
                    //[N,n]ull or , means no value.
                    TokenAcceptors.TypeParserMapper[typeof(NullableParser)].Parse(nextChar, typeof(NullableParser));
                    value = null;
                    break;

                case 91:
                    // { [
                    //Must do generic push/pop on stack....
                    PushPopStack(nextChar, nextChar.Read(), new Stack<char>());
                    break;

                default:
                    value = null;
                    break;
            }

            return value;

        }



        private void PushPopStack(IReader nextChar, char start, Stack<char> stack)
        {
            var stackCount = stack.Count;
            stack.Push(start);

            char endToken = ']';
            char currToken;
            
            if ((start == '{'))
                endToken = '}';

            while (stack.Count != stackCount)
            {
                currToken = nextChar.Read();
                if (currToken == endToken)
                {
                    stack.Pop();
                }
                if (currToken == '[' | currToken == '{')
                {
                    PushPopStack(nextChar, currToken, stack);
                }
            }
        }
    }
}