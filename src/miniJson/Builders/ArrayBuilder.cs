using miniJson.Exceptions;
using miniJson.Stream;
using System;
using System.Collections;
using System.Collections.Generic;

namespace miniJson.Builders
{
    internal class ArrayBuilder : Builder
    {
        public override object Parse(IReader nextChar, Type t)
        {
            TokenAcceptors.WhiteSpace(nextChar);
            TokenAcceptors.BufferLegalCharacters(nextChar, "nul");
            var buffer = nextChar.Buffer;
            if (buffer == "null")
                return null;

            TokenAcceptors.EatUntil(TokenAcceptors.ListStart, nextChar);

            IParseStrategy strategy;
            if (t.IsArray)
            {
                strategy = new ArrayParserStrategy(t);
            }
            else
            {
                strategy = new ListParseStrategy(t);
            }

            TokenAcceptors.WhiteSpace(nextChar);

            do
            {
                if (strategy.InnerType.IsValueType | strategy.InnerType == typeof(string))
                {
                    TokenAcceptors.WhiteSpace(nextChar);
                    if (nextChar.Peek() != TokenAcceptors.ListEnd)
                    {
                        strategy.ItemList.Add(TokenAcceptors.TypeParserMapper[strategy.InnerType].Parse(nextChar, t));
                    }
                }
                else
                {
                    object v = Parser.StringToObject(nextChar, strategy.InnerType);
                    if (v != null)
                    {
                        strategy.ItemList.Add(v);
                    }
                }
            } while (TokenAcceptors.CanFindValueSeparator(nextChar));

            TokenAcceptors.EatUntil(TokenAcceptors.ListEnd, nextChar);
            
            return strategy.Result;
        }

        protected interface IParseStrategy
        {
            IList ItemList { get; }
            Type InnerType { get; }
            object Result { get; }
        }

        protected class ArrayParserStrategy : IParseStrategy
        {
            private Type _innerType;
            private Type _t;

            public ArrayParserStrategy(Type t)
            {
                _t = t;
                _innerType = t.GetElementType();
                var tempType = typeof(List<>);
                _itemList = (IList)Activator.CreateInstance(tempType.MakeGenericType(InnerType));
            }

            private IList _itemList;

            public IList ItemList
            {
                get { return _itemList; }
            }

            public object Result
            {
                get
                {
                    object newA = Activator.CreateInstance(_t, _itemList.Count);
                    _itemList.CopyTo((Array)newA, 0);
                    return newA;
                }
            }

            public Type InnerType
            {
                get { return _innerType; }
            }
        }

        protected class ListParseStrategy : IParseStrategy
        {
            private IList _ItemList;

            private Type _innertype;

            public ListParseStrategy(Type t)
            {
                if (t.IsGenericType)
                {
                    _innertype = t.GetGenericArguments()[0];
                }
                else
                {
                    throw new NonGenericListIsNotSupportedException();
                }

                if (t.IsInterface)
                {
                    var tempType = typeof(List<>);
                    _ItemList = (IList)Activator.CreateInstance(tempType.MakeGenericType(InnerType));
                }
                else
                {
                    _ItemList = (IList)Activator.CreateInstance(t);
                }
            }

            public Type InnerType
            {
                get { return _innertype; }
            }

            public IList ItemList
            {
                get { return _ItemList; }
            }

            public object Result
            {
                get { return _ItemList; }
            }
        }
    }
}