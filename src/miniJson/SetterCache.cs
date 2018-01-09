using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace miniJson
{
    static class SetterCreator
    {
        internal static Func<object, object, object> CreateValueTypeSetter(MemberInfo mInfo)
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
            
            var exValue = Expression.Parameter(typeof(Object));
            var wrapperTarget = Expression.Parameter(typeof(Object));
            
            var exTarget = Expression.Parameter(declaringType);
            var exBody = Expression.Lambda(
                Expression.Block(
                Expression.Assign(
                       Expression.PropertyOrField(exTarget, mInfo.Name),
                       Expression.Convert(exValue, valueType)), exTarget),
            exTarget,
            exValue);

            //'Wrapper expression

            var wrapperValue = Expression.Parameter(typeof(Object));

            var exBlock = Expression.Convert(Expression.Invoke(exBody, Expression.Convert(wrapperTarget, declaringType), wrapperValue), typeof(Object));

            Expression<Func<object, object, object>> expression = Expression.Lambda<Func<Object, Object, Object>>(exBlock, wrapperTarget, wrapperValue);

            return expression.Compile();

        }
        internal static Action<object, object> CreateRefSetter(MemberInfo mInfo)
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

            var exValue = Expression.Parameter(typeof(Object));
            var wrapperTarget = Expression.Parameter(typeof(Object));

            return Expression.Lambda<Action<Object, Object>>(
                            Expression.Assign(Expression.PropertyOrField(Expression.Convert(wrapperTarget, declaringType), mInfo.Name),
                            Expression.Convert(exValue, valueType)
                        ), wrapperTarget, exValue).Compile();
        }




    }

}
