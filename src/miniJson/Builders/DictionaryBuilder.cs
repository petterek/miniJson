using miniJson.Exceptions;
using miniJson.Parsers;
using miniJson.Stream;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace miniJson.Builders
{
    internal class DictionaryBuilder : Builder
    {
        public override object Parse(IReader nextChar, Type t)
        {
            IDictionary res = null;
            //this is a dictionary...

            TokenAcceptors.WhiteSpace(nextChar);

            if (TokenAcceptors.StartObjectOrNull(nextChar) != null)
            {
                string key;
                object value;
                var getGenericArguments = t.GetGenericArguments();
                Type typeOfValue;
                Type useParser =null;

                //Name value
                if (getGenericArguments.Count() == 2)
                {
                    if (!object.ReferenceEquals(getGenericArguments[0], typeof(string)))
                    {
                        throw new UnsupportedDictionaryException();
                    }
                    typeOfValue = getGenericArguments[1];
                    res = (IDictionary)Activator.CreateInstance(t);
                }
                else
                {
                    if(t == typeof(IDictionary))
                    {
                        typeOfValue = typeof(object);
                        res = new Dictionary<string, object>();
                        useParser = typeof(UnknownFieldParser);
                    }else
                    {
                        throw new UnsupportedDictionaryException();
                    }
                    
                }

                
                TokenAcceptors.WhiteSpace(nextChar);

                if (nextChar.Current() == (Char)34)
                {
                    do
                    {
                        key = TokenAcceptors.Attribute(nextChar);
                        TokenAcceptors.EatUntil(TokenAcceptors.Qualifier, nextChar);
                        value = TokenAcceptors.ParseValue(typeOfValue, nextChar, useParser);
                        res.Add(key, value);
                    } while (TokenAcceptors.CanFindValueSeparator(nextChar));
                }

                TokenAcceptors.WhiteSpace(nextChar);
                if (nextChar.Current() != TokenAcceptors.ObjectEnd) throw new MissingTokenException(TokenAcceptors.ObjectEnd.ToString(), nextChar);
                nextChar.Read(); //Swollow the last "}"
            }
            //Cleaning out whitespace, check for " to ensure not empty object

            return res;
        }
    }
}