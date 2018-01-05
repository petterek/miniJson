using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace miniJson
{
	internal static class SetterCreator
	{
        internal static Func<object, object,object> CreateSetter(MemberInfo mInfo)
        {

            if (mInfo == null) return null;

            System.Type valueType;
            var declaringType = mInfo.DeclaringType;

            if (mInfo.MemberType == MemberTypes.Field)
            {
                valueType = declaringType.GetField(mInfo.Name).FieldType;
            }
            else
            {
                valueType = declaringType.GetProperty(mInfo.Name).PropertyType;
            }

            var exTarget = Expression.Parameter(declaringType);
            var exValue = Expression.Parameter(typeof(Object));

            var exBody = Expression.Lambda(
                            Expression.Block(
                            Expression.Assign(
                                   Expression.PropertyOrField(exTarget, mInfo.Name), 
                                   Expression.Convert(exValue, valueType)),exTarget), 
                        exTarget, 
                        exValue);

            //'Wrapper expression
            var wrapperTarget = Expression.Parameter(typeof(Object));
            var wrapperValue = Expression.Parameter(typeof(Object));

            var exBlock = Expression.Convert(Expression.Invoke(exBody, Expression.Convert(wrapperTarget, declaringType), wrapperValue), typeof(Object));

            return  Expression.Lambda<Func<Object, Object, Object>>(exBlock, wrapperTarget, wrapperValue).Compile();
            
        }
	
	}



}