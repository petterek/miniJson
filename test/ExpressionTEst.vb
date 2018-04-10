Imports System.Linq.Expressions
Imports NUnit.Framework

<TestFixture> Public Class ExpressionTEst

    <Test> Public Sub GetOvrriddenProperyInExpression()

        Dim ex = Expression.PropertyOrField(Expression.Constant(Nothing, GetType(Inherited(Of String))), "field1")
        Dim ex2 = Expression.PropertyOrField(Expression.Constant(Nothing, GetType(Inherited(Of String))), "t2")

        GetType(Inherited(Of String)).GetField("field1")
        GetType(Inherited(Of String)).GetProperty("Prop1")

        Dim ex3 = Expression.PropertyOrField(Expression.Constant(Nothing, GetType(Inherited(Of String))), "Prop1")

    End Sub

    Public Class Base
        Public field1 As Object
        Public Property Prop1 As Object

        Public Overridable Property t2 As String
    End Class

    Public Class Inherited(Of T)
        Inherits Base

        Public Overrides Property t2 As String
            Get
                Return MyBase.t2
            End Get
            Set(value As String)
                MyBase.t2 = value
            End Set
        End Property

        Public Shadows field1 As T
        Public Shadows Property Prop1 As T

    End Class

End Class