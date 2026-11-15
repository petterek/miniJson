Imports System.Globalization
Imports NUnit.Framework

<TestFixture> Public Class TestParser

    <Test> Public Sub ParseObjectArray()
        Dim types() As Type = {GetType(Person), GetType(ClassWithLongBooleanStringProperty)}
        Dim p = Parser.StringToObjects("[{""Navn"":""Petter""}, {""Value"": 1446212820320,""ValueTrue"":True,""ValueFalse"":False}]", types)
        Assert.AreEqual(2, p.Length)
        Assert.True(GetType(Person).IsInstanceOfType(p(0)))
        Assert.True(GetType(ClassWithLongBooleanStringProperty).IsInstanceOfType(p(1)))
    End Sub

    <Test> Public Sub ParseSimpleObject()
        Dim p = Parser.StringToObject(Of Person)("{""Navn"":""Petter""}  ")
        Assert.AreEqual("Petter", p.Navn)
        'Assert.AreEqual(43, p.Alder

    End Sub

    <TestCase("\u0060\u012a\u12AB", "`" & ChrW(&H12A) & ChrW(&H12AB)),
        TestCase("\uD834\uDd1e", ChrW(&HD834) & ChrW(&HDD1E)),
        TestCase("\u0000", vbNullChar),
        TestCase("\u0022\u005C", """\"),
        TestCase("\u000A", vbLf),
        TestCase("\\u0061", "\u0061")>
    Public Sub ParseUnicodeEscapes(input As String, expected As String)
        Assert.AreEqual(expected, Parser.StringToObject("""" & input & """", GetType(String)))
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub ParseAdjacentEscapesAndFollowingArrayValues(useStream As Boolean)
        Dim escaped = "\t\r\n\b\f\/\\" & "\""" & "\u0041\u0062\uD834\uDD1E!"
        Dim input = "[""" & escaped & """,""next"",""""]"
        Dim expected = vbTab & vbCr & Environment.NewLine & vbBack & vbFormFeed & "/\""" &
            "Ab" & Char.ConvertFromUtf32(&H1D11E) & "!"
        Dim actual As List(Of String)
        If useStream Then
            Using stream = New System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(input))
                actual = Parser.StringToObject(Of List(Of String))(stream)
                Assert.IsTrue(stream.CanRead)
            End Using
        Else
            actual = Parser.StringToObject(Of List(Of String))(input)
        End If
        CollectionAssert.AreEqual({expected, "next", ""}, actual)
    End Sub

    <TestCase(0), TestCase(15), TestCase(16), TestCase(31),
        TestCase(32), TestCase(63), TestCase(64), TestCase(65)>
    Public Sub BulkStringScanningPreservesEscapesAndDelimiters(prefixLength As Integer)
        Dim prefix = New String("x"c, prefixLength)
        Dim suffix = New String("y"c, prefixLength + 3)
        Dim encoded = prefix & "\u0041\" & ChrW(34) & "\\\u0000" & suffix
        Dim input = "[""" & encoded & """,""next"",""""]"
        Dim expected = {prefix & "A" & ChrW(34) & ChrW(92) & vbNullChar & suffix, "next", ""}
        CollectionAssert.AreEqual(expected, Parser.StringToObject(Of List(Of String))(input))
        Using stream = New System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(input))
            CollectionAssert.AreEqual(expected, Parser.StringToObject(Of List(Of String))(stream))
        End Using
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub MemberLookupPreservesDecodedNamesCaseAndType(useStream As Boolean)
        Const input = "{""Va\u006cue"":1,""Ignored"":[true,false],""Value"":2,""value"":99}"
        For pass = 1 To 2
            Dim actual As MemberLookupTarget
            If useStream Then
                Using stream = New System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(input))
                    actual = Parser.StringToObject(Of MemberLookupTarget)(stream)
                End Using
            Else
                actual = Parser.StringToObject(Of MemberLookupTarget)(input)
            End If
            Assert.AreEqual(2, actual.Value)
            Assert.AreEqual("text", Parser.StringToObject(Of Holder(Of String))("{""Value"":""text""}").Value)
        Next
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub ParseLiteralNoncharactersAndEscapedNullBeforeDelimiters(useStream As Boolean)
        Dim text = "before" & ChrW(&HFFFF) & ChrW(&HFFFE) & "after"
        Dim input = "{""" & text & """:[""" & text & """,""\u0000"",""""]}"
        Dim actual As Dictionary(Of String, List(Of String))
        If useStream Then
            Using stream = New System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(input))
                actual = Parser.StringToObject(Of Dictionary(Of String, List(Of String)))(stream)
            End Using
        Else
            actual = Parser.StringToObject(Of Dictionary(Of String, List(Of String)))(input)
        End If
        CollectionAssert.AreEqual({text, vbNullChar, ""}, actual(text))
    End Sub

    <TestCase("0"), TestCase("-1.25e+2"), TestCase("1e-100")>
    Public Sub ParseNumberAtEndOfInputMatchesStream(input As String)
        Dim expected = Double.Parse(input, CultureInfo.InvariantCulture)
        Assert.AreEqual(expected, Parser.StringToObject(Of Double)(input))
        Using stream = New System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(input))
            Assert.AreEqual(expected, Parser.StringToObject(Of Double)(stream))
        End Using
    End Sub

    <TestCase("\x"), TestCase("\U0041"), TestCase("\u00/g")>
    Public Sub RejectInvalidStringEscapes(input As String)
        Assert.Throws(Of FormatException)(Sub() Parser.StringToObject("""" & input & """", GetType(String)))
    End Sub

    <TestCase("D800", "FFFD"),
        TestCase("DC00", "FFFD"),
        TestCase("D800 0078 DC00", "FFFD 0078 FFFD"),
        TestCase("D834 DD1E", "D834 DD1E")>
    Public Sub StringInputPreservesUtf8SurrogateReplacement(inputUnits As String, expectedUnits As String)
        Dim input = New String(inputUnits.Split(" "c).Select(Function(unit) ChrW(Convert.ToInt32(unit, 16))).ToArray())
        Dim expected = New String(expectedUnits.Split(" "c).Select(Function(unit) ChrW(Convert.ToInt32(unit, 16))).ToArray())
        Assert.AreEqual(expected, Parser.StringToObject("""" & input & """", GetType(String)))
    End Sub

    <Test> Public Sub StringInputPreservesNullInputBehavior()
        Dim input As String = Nothing
        Assert.IsNull(Parser.StringToObject(Of Person)(input))
        Assert.IsNull(Parser.StringToObject(input, GetType(Person)))
        Assert.IsNull(Parser.StringToObject(input, GetType(String)))
        Assert.Throws(Of FormatException)(Sub() Parser.StringToObject(Of Integer)(input))
    End Sub

    <Test> Public Sub StringInputPreservesLeadingBomBehavior()
        Dim input = ChrW(&HFEFF) & "{""Navn"":""Test""}"
        Assert.IsNull(Parser.StringToObject(Of Person)(input))
        Assert.IsNull(Parser.StringToObject(input, GetType(Person)))
    End Sub

    <Test> Public Sub ParseUnicodeEscapesInDictionaryKeysAndArrayValues()
        Dim dictionary = Parser.StringToObject(Of Dictionary(Of String, Integer))("{""\u0061\u0000"":42}")
        Assert.AreEqual(42, dictionary("a" & vbNullChar))

        Dim values = Parser.StringToObject(Of List(Of String))("[""\u0061"",""\u0062""]")
        CollectionAssert.AreEqual({"a", "b"}, values)
    End Sub

    <TestCase("\u"), TestCase("\u12"), TestCase("\u12xz")>
    Public Sub RejectMalformedUnicodeEscapes(input As String)
        Assert.Throws(Of FormatException)(Sub() Parser.StringToObject("""" & input & """", GetType(String)))
    End Sub

    <TestCase(""), TestCase("abc"), TestCase("\uD800\""")>
    Public Sub RejectUnterminatedStrings(input As String)
        Assert.Throws(Of System.IO.EndOfStreamException)(Sub() Parser.StringToObject("""" & input, GetType(String)))
    End Sub

    <TestCase("1e2", "100"),
        TestCase("1E+2", "100"),
        TestCase("1.25e-2", "0.0125"),
        TestCase("-2.5e+3", "-2500"),
        TestCase("0e1", "0")>
    Public Sub ParseExponentNumbers(input As String, expected As String)
        Dim expectedDouble = Double.Parse(expected, CultureInfo.InvariantCulture)
        Assert.AreEqual(expectedDouble, Parser.StringToObject(Of Double)(input))
        Assert.AreEqual(Single.Parse(expected, CultureInfo.InvariantCulture), Parser.StringToObject(Of Single)(input))
        Assert.AreEqual(Decimal.Parse(expected, CultureInfo.InvariantCulture), Parser.StringToObject(Of Decimal)(input))

        Dim values = Parser.StringToObject(Of List(Of Double))("[" & input & ",2]")
        CollectionAssert.AreEqual({expectedDouble, 2.0}, values)
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub ParseUnicodeAndExponentsAcrossReaderBuffers(useStream As Boolean)
        Dim prefix = New String("x"c, 4096)
        Dim input = "{""Navn"":""" & prefix & "\uD834\uDD1E\u0000"",""Speed"":-1.25E+2}"
        Dim result As Person

        If useStream Then
            Using stream = New System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(input))
                result = Parser.StringToObject(Of Person)(stream)
                Assert.IsTrue(stream.CanRead)
            End Using
        Else
            result = Parser.StringToObject(Of Person)(input)
        End If

        Assert.AreEqual(prefix & Char.ConvertFromUtf32(&H1D11E) & vbNullChar, result.Navn)
        Assert.AreEqual(-125.0, result.Speed)
    End Sub

    <TestCase("+1"), TestCase("1e+"), TestCase("1e-"), TestCase("1e++2"),
        TestCase(".2e-3"), TestCase("0.e1"), TestCase("2.e+3")>
    Public Sub RejectMalformedExponentNumbers(input As String)
        Assert.Throws(Of FormatException)(Sub() Parser.StringToObject(Of Double)(input))
        Assert.Throws(Of FormatException)(Sub() Parser.StringToObject(Of Single)(input))
        Assert.Throws(Of FormatException)(Sub() Parser.StringToObject(Of Decimal)(input))
    End Sub

    <TestCase("00"), TestCase("01"), TestCase("-01"), TestCase("1."),
        TestCase("1e"), TestCase("--1"), TestCase("1e2.3"), TestCase("1ee2")>
    Public Sub RejectMalformedFloatingNumberTokens(input As String)
        For Each targetType In {GetType(Double), GetType(Single), GetType(Decimal)}
            Assert.Throws(Of FormatException)(Sub() Parser.StringToObject(input, targetType))
            Using stream = New System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(input))
                Assert.Throws(Of FormatException)(Sub() Parser.StringToObject(stream, targetType))
            End Using
        Next
    End Sub

    <Test> Public Sub ParseNullableNumbersInArraysAndLists()
        Const input = "[1,null,null,null,2]"
        Dim expected As Double?() = {1, Nothing, Nothing, Nothing, 2}

        Dim array = DirectCast(Parser.StringToObject(input, GetType(Double?())), Double?())
        CollectionAssert.AreEqual(expected, array)
        CollectionAssert.AreEqual(expected, Parser.StringToObject(Of List(Of Double?))(input))
        CollectionAssert.IsEmpty(Parser.StringToObject("[]", GetType(Double?())))
    End Sub

    <Test> Public Sub ParseTypedObjectArrayPreservesNullsAndPositions()
        Dim types = {GetType(Nullable(Of Double)), GetType(Nullable(Of Double)), GetType(String)}
        Dim values = Parser.StringToObjects("[null,1e2,""end""]", types)

        CollectionAssert.AreEqual(New Object() {Nothing, 100.0, "end"}, values)
        CollectionAssert.IsEmpty(Parser.StringToObjects("[]", types))
    End Sub

    <Test> Public Sub ParseObjectArrayPreservesNullElements()
        Dim values = Parser.StringToObject(Of List(Of Person))("[null,{""Navn"":""Test""},null]")
        Assert.AreEqual(3, values.Count)
        Assert.IsNull(values(0))
        Assert.AreEqual("Test", values(1).Navn)
        Assert.IsNull(values(2))
    End Sub

    <Test> Public Sub ParseObjectWithPropertyThatsNotInClass()
        Assert.DoesNotThrow(Sub() Parser.StringToObject(Of Person)("{""Comment"":null}"))
    End Sub

    <Test> Public Sub ParseObjectWithNullablePropertySetToNull()
        Assert.DoesNotThrow(Sub() Parser.StringToObject(Of Person)("{""ChildCount"":null}  "))
    End Sub

    <Test> Public Sub ParseObjectWithNullableStructFilled()
        Dim toParse = "{""SomeThing"":1,""ADate"":{""Year"":2016,""Month"":10,""Day"":9}}"
        Assert.DoesNotThrow(Sub() Parser.StringToObject(Of ClassWithNullableStruct)(toParse))
        Assert.AreEqual(9, Parser.StringToObject(Of ClassWithNullableStruct)(toParse).ADate.Value.Day)
    End Sub

    <Test> Public Sub ParseObjectWithNullableStructNulled()
        Dim toParse = "{""SomeThing"":1,""ADate"":null}"
        Assert.DoesNotThrow(Sub() Parser.StringToObject(Of ClassWithNullableStruct)(toParse))
        Assert.AreEqual(Parser.StringToObject(Of ClassWithNullableStruct)(toParse).ADate, Nothing)
    End Sub

    <Test> Public Sub ParseObjectWithSingleAsNonDecimal()
        Assert.DoesNotThrow(Sub() Parser.StringToObject(Of Person)("{""SpeedyGonzales"":0}"))
    End Sub

    <Test> Public Sub ParseTextWithEscapeObject()
        Dim p = Parser.StringToObject(Of Person)("{""Navn"":""Petter\nGjermund\\ \""Han er skikkelig tøff\"" ""}")
        Assert.AreEqual("Petter" & Environment.NewLine & "Gjermund\ ""Han er skikkelig tï¿½ff"" ", p.Navn)
        'Assert.AreEqual(43, p.Alder)
    End Sub

    <Test> Public Sub ParseComplexObject()
        Dim p = Parser.StringToObject(Of Person)("{""Navn"":""Petter"",""TestInfo"": {""Name"":""Nils""  }   }")
        Assert.AreEqual("Petter", p.Navn)
        Assert.AreEqual("Nils", p.TestInfo.Name)
        'Assert.AreEqual(43, p.Alder)
    End Sub

    <Test> Public Sub ParseSimpleObjectWithInteger()
        Dim p = Parser.StringToObject(Of Person)("{""Navn"":""Petter"",""Alder"":  42   }")
        Assert.AreEqual("Petter", p.Navn)
        Assert.AreEqual(42, p.Alder)
    End Sub

    <Test> Public Sub ParseSimpleObjectWithDouble()
        Dim p = Parser.StringToObject(Of Person)("{""Navn"":""Petter"",""Speed"":  4.2,""Alder"":42  }")
        Assert.AreEqual("Petter", p.Navn)
        Assert.AreEqual(4.2, p.Speed)
    End Sub

    <Test> Public Sub ParseEmptyList()
        Dim p = Parser.StringToObject(Of List(Of Person))("[]")
        Assert.IsInstanceOf(Of List(Of Person))(p)
        Assert.AreEqual(0, p.Count)
    End Sub

    <Test> Public Sub ParseListOfOneSimpleObject()
        Dim p = Parser.StringToObject(Of List(Of Person))("[{""Navn"":""Petter"",""Speed"":  4.2,""Alder"":42  }]")
        Assert.AreEqual("Petter", p(0).Navn)
        Assert.AreEqual(4.2, p(0).Speed)
    End Sub

    <Test> Public Sub ParseListOfManySimpleObject()
        Dim p = Parser.StringToObject(Of List(Of Person))("[{""Navn"":""Petter"",""Speed"":  4.2,""Alder"":42  },{""Navn"":""Gjermund"",""Speed"":  4.0,""Alder"":40  }]")
        Assert.AreEqual("Gjermund", p(1).Navn)
        Assert.AreEqual(4.0, p(1).Speed)
    End Sub

    <Test> Public Sub ParseListOfManyValueTypes()
        Dim p = Parser.StringToObject(Of List(Of Integer))("[1,2,3]")
        Assert.AreEqual(1, p(0))
        Assert.AreEqual(2, p(1))
    End Sub

    <Test> Public Sub ParseListOfStrings()
        Dim p = Parser.StringToObject(Of List(Of String))("[""1"",""2"",""3""]")
        Assert.AreEqual("1", p(0))
        Assert.AreEqual("2", p(1))
    End Sub

    <Test> Public Sub ParseListAsPartOfObject()
        Dim p = Parser.StringToObject(Of Test)("{""Name"":""Petter"",""Scores"" : [""1"",""2"",""3""]}")
        Assert.AreEqual("Petter", p.Name)
        Assert.AreEqual("1", p.Scores(0))
    End Sub

    <Test> Public Sub ParseValueTypesIntoArray()
        Dim p = Parser.StringToObject(Of TestWithIntArray)("{""Name"":""Petter"",""Scores"" : [1,2,3]}")
        Assert.AreEqual(1, p.Scores(0))
    End Sub

    <Test> Public Sub ParseValueTypesDoubleIntoArray()
        Dim p = Parser.StringToObject(Of TestWithDArray)("{""Name"":""Petter"",""Scores"" : [1.2,2.34,3.12]}")
        Assert.AreEqual(1.2, p.Scores(0))
    End Sub

    <Test> Public Sub ParseWithBoolValueNotInObject()
        Assert.DoesNotThrow(Sub() Parser.StringToObject(Of TestWithDArray)("{""Name"":""Petter"",""NotInObject"":false}"))
    End Sub

    <Test> Public Sub Readguid()
        Dim p = Parser.StringToObject(Of Holder(Of Guid))("{""Value"":""FE41254C-FFFC-4121-8345-7353C5D128DC""}")
        Assert.AreEqual(New Guid("FE41254C-FFFC-4121-8345-7353C5D128DC"), p.Value)

    End Sub

    <Test> Public Sub EmptyObjectIsRead()

        Assert.DoesNotThrow(Sub() Newtonsoft.Json.JsonConvert.DeserializeObject(Of Test)("{}"))
        Assert.DoesNotThrow(Sub() Parser.StringToObject(Of Test)("{}"))

    End Sub

    <Test> Public Sub LongValueIsParsed()

        Dim v As ClassWithLong = Nothing
        Assert.DoesNotThrow(Sub() v = Parser.StringToObject(Of ClassWithLong)("{""Value"":1446212820320}"))
        Assert.AreEqual(1446212820320, v.Value)

    End Sub

    <Test> Public Sub LongValueIsParsedToProperty()

        Dim v As ClassWithLongBooleanStringProperty = Nothing
        Assert.DoesNotThrow(Sub() v = Parser.StringToObject(Of ClassWithLongBooleanStringProperty)("{""Value"":1446212820320}"))
        Assert.AreEqual(1446212820320, v.Value)

    End Sub

    <Test> Public Sub ValueInStrinIsIgnoredWhenFieldDoesNotExist()
        Dim v As ClassWithLongBooleanStringProperty = Nothing
        Assert.DoesNotThrow(Sub() v = Parser.StringToObject(Of ClassWithLongBooleanStringProperty)("{""Value"":1446212820320,""ValueTwo"":124}"))
        Assert.AreEqual(1446212820320, v.Value)

    End Sub

    <Test> Public Sub BooleanValueIsParsed()
        Dim v As ClassWithLongBooleanStringProperty = Nothing
        Assert.DoesNotThrow(Sub() v = Parser.StringToObject(Of ClassWithLongBooleanStringProperty)("{""Value"":1446212820320,""ValueTrue"":True,""ValueFalse"":False}"))
        Assert.True(v.ValueTrue)
        Assert.False(v.ValueFalse)

    End Sub

    <Test> Public Sub NegativIntIsParsed()

        Dim toParse = "{""SomeThing"":-1,""ADate"":{""Year"":2016,""Month"":10,""Day"":9}}"

        Dim retValue = Parser.StringToObject(Of ClassWithStruct)(toParse)

        Assert.AreEqual(-1, retValue.SomeThing)

    End Sub

    <Test> Public Sub ParseStruct()

        'Dim toParse = "{""SomeThing"":1,""ADate"":{""Year"":2016,""Month"":10,""Day"":9}}"
        Dim toParse = "{""Year"":2016,""Month"":10,""Day"":9}"

        Dim retValue = Parser.StringToObject(Of DateHolder)(toParse)

        'Dim retValue = New ClassWithStruct

        'SetTheValue(retValue.ADate)

        Assert.AreEqual(2016, retValue.Year)

    End Sub

    Sub SetTheValue(ByRef s As DateHolder)
        s.Year = 2016
    End Sub

    <Test> Public Sub ClassWithArrayDoesNotThrow()

        'empty array
        Dim v = Parser.StringToObject(Of ClassWithArray)("{""Data"":[]}")
        'empty array
        v = Parser.StringToObject(Of ClassWithArray)("{""Data"":[""Value2""]}")

        v = Parser.StringToObject(Of ClassWithArray)("{""Data"":null}")

        v = Parser.StringToObject(Of ClassWithArray)("{""DataInt"":null}")

        v = Parser.StringToObject(Of ClassWithArray)("{""DataInt"":[1,2,3]}")

        v = Parser.StringToObject(Of ClassWithArray)("{""StringValue"": null }")

        v = Parser.StringToObject(Of ClassWithArray)("{""Attributes"": null }")

        v = Parser.StringToObject(Of ClassWithArray)("{""Attributes"": {""Test"":""Value"",""Test2"":""Value2""} }")

    End Sub

    <Test> Public Sub ParseObjectWithIEnumerableOfStringDoesNotThrow()
        Dim v = Parser.StringToObject(Of ClassWithArray)("{""AList"": [""String1"",""String2""] }")
        Assert.AreEqual(2, v.AList.Count)

    End Sub

    <Test> Public Sub ParseObjectWithIEnumerableOfSomeObjectTypeDoesNotThrow()
        Dim v = Parser.StringToObject(Of ClassWithArray)("{""AnotherList"": [{""A"":""String1"",""B"":15},{""A"":""String1"",""B"":15}] }")
        Assert.AreEqual(2, v.AnotherList.Count)

    End Sub

    <Test> Public Sub DeSerializingValueTypesIsOk()

        Assert.AreEqual(1, miniJson.Parser.StringToObject(Of Integer)("1"))
        Assert.AreEqual("En to tre", miniJson.Parser.StringToObject("""En to tre""", GetType(String)))

    End Sub

    <Test> Public Sub DeserializeEnums()

        Dim toTest As ClassWithEnum

        toTest = Parser.StringToObject(Of ClassWithEnum)("{""Value"": 1}")
        toTest = Parser.StringToObject(Of ClassWithEnum)("{""Value"": ""Value1""}")

    End Sub

    <Test> Public Sub SomethingStrangeFromGoogle()
        Dim toTest = "{""azp"": ""529794868022-pholasipv70npdhlaj41lrft2en7m8cg.apps.googleusercontent.com"",
                         ""aud"": ""529794868022-nuif63n2dit7510tvn1h1t5mrsl6k0dn.apps.googleusercontent.com"",
                         ""Sub"": ""106692936747611074481"",
                         ""email"": ""fredrikaxk@gmail.com"",
                         ""email_verified"": ""True"",
                         ""iss"": ""https//accounts.google.com"",
                         ""iat"": ""1501151900"",
                         ""exp"": ""1501155500"",
                         ""name"": ""Fredrik Aleksander Kristiansen"",
                         ""picture"": ""https://lh4.googleusercontent.com/-1vRNQCdRoZw/AAAAAAAAAAI/AAAAAAAAAc8/SVeiP0j7rsg/s96-c/photo.jpg"",
                         ""given_name"": ""Fredrik Aleksander"",
                         ""family_name"": ""Kristiansen"",
                         ""locale"": ""no"",
                         ""alg"": ""RS256"",
                         ""kid"": ""6cd8597a1f685ce06cca42de0c25f6f4b048d689""
                        }"

        Dim res = Parser.StringToObject(Of GoogleUserInfo)(toTest)

        Assert.AreEqual("fredrikaxk@gmail.com", res.email)

    End Sub

    <Test> Public Sub StringWithEQSignDoesNotBreakTheParser()

        Const Input1 As String = "{""Name"":""Petter = the bug killer""}"

        Dim toTest = Parser.StringToObject(Of DataObject)(Input1)

        Assert.AreEqual("Petter = the bug killer", toTest.Name)

    End Sub

    <Test> Public Sub EatListOfUnknownProperty()
        Const inp = "{""UnknownField"":[""String1"",{""Strange"":1}],""KnownField"":1}"

        Dim toTest As MissingFieldClass = Nothing
        Assert.DoesNotThrow(Sub() toTest = Parser.StringToObject(Of MissingFieldClass)(inp))

        Assert.AreEqual(1, toTest.KnownField)

    End Sub

    <Test> Public Sub NULLasContentReturnsNothing()
        Const inp = "null"

        Dim toTest As Object = Nothing
        Assert.DoesNotThrow(Sub() toTest = Parser.StringToObject(Of MissingFieldClass)(inp))

        Assert.IsNull(toTest)

    End Sub



    <Test> Public Sub DeserialilzingEmptyIDictionary()
        Const inp = "{""Data"":{}}"
        Dim d As ClassWithIDictionary

        Assert.DoesNotThrow(Sub() d = Parser.StringToObject(Of ClassWithIDictionary)(inp))


    End Sub

    <Test> Public Sub DeserialilzingIDictionary()
        Const inp = "{""Data"":{""Value1"":""Value1""}}"
        Dim d As ClassWithIDictionary = Nothing

        Assert.DoesNotThrow(Sub() d = Parser.StringToObject(Of ClassWithIDictionary)(inp))
        Assert.AreEqual("Value1", d.Data("Value1"))

    End Sub

    Public Class ClassWithIDictionary
        Public Data As IDictionary
    End Class


    Public Class MemberLookupBase
        Public Property Value As Integer
    End Class

    Public Class MemberLookupTarget
        Inherits MemberLookupBase

        Public ReadOnly Property Unused As String
            Get
                Return "unused"
            End Get
        End Property
    End Class

    Public Class MissingFieldClass
        Public KnownField As Integer
    End Class

    Public Class GoogleUserInfo
        Public email As String
        Public email_verified As String
    End Class

    Public Class ClassWithEnum
        Public Value As MyEnum
    End Class

    Public Enum MyEnum
        Value1 = 1
        Value2 = 2
    End Enum

    Public Class ClassWithLongBooleanStringProperty
        Public Property Value As Long
        Public Property ValueTrue As Boolean
        Public Property ValueFalse As Boolean

        Public Property StringValue As String

    End Class

    <Test> Public Sub GenericObjectAsDictionary()

        Dim toTest = "{""Integer"" : 1,""Double"":2.1,""String"":""A string""}"
        Dim res = Newtonsoft.Json.JsonConvert.DeserializeObject(Of Dictionary(Of String, Object))(toTest)

        Assert.AreEqual(1, res("Integer"))
        Assert.AreEqual(2.1, res("Double"))
        Assert.AreEqual("A string", res("String"))
        Assert.IsInstanceOf(Of Int64)(res("Integer"))
        Assert.IsInstanceOf(Of Double)(res("Double"))
    End Sub

    Public Class ClassWithArray
        Public Data As String()
        Public DataInt As Integer()
        Public StringValue As String
        Public Attributes As Dictionary(Of String, String)
        Public AList As IEnumerable(Of String)
        Public AnotherList As IEnumerable(Of SmallType)
    End Class

    Public Class ClassWithStruct
        Public SomeThing As Integer
        Public ADate As DateHolder
    End Class

    Public Class ClassWithNullableStruct
        Public SomeThing As Integer
        Public ADate As DateHolder?
    End Class

    Public Structure DateHolder
        Public Year As Integer
        Public Month As Integer
        Public Day As Integer
    End Structure

    Public Class SmallType
        Public A As String
        Public B As Integer
    End Class

    <Test> Public Sub EdgeCaseFromPawel()

        Dim inp = "{""Actions"":[],""SettingId"":""93Dea120-e62f-48ee-8837-d10a9aee6a8c"",""NodeId"":""c65904cf-c85a-49cc-b8f2-4445Dee941be"",""NodeName"":""Sjokoladefabrikken As"",""Value"":""1""}"
        miniJson.Parser.StringToObject(Of GetByIdAndNodeIdResponse)(inp)

    End Sub

    <Test> Public Sub ObjectIlistPropertyDoesNotFail()

        Dim inp = "[{""Id"":""00cc7820-b6e4-4473-93e6-c3645b92bdd2"",""Mails"" :[{""PersonId"":1}]}]"

        Dim res = miniJson.Parser.StringToObject(Of List(Of ClassWithEnumerable))(inp)

        Assert.AreEqual(New Guid("00cc7820-b6e4-4473-93e6-c3645b92bdd2"), res(0).Id)
        Assert.AreEqual(1, res(0).Mails.Count)
        Assert.AreEqual(1, res(0).Mails(0).PersonId)
    End Sub

    <Test> Public Sub NullableByteWithNull()
        Dim inp = "{""Gender"":null}"
        Dim res = miniJson.Parser.StringToObject(Of ClassWithNullableByte)(inp)
        Assert.AreEqual(Nothing, res.Gender)
    End Sub

    <Test> Public Sub NullableByteWithValue()
        Dim inp = "{""Gender"":2}"
        Dim res = miniJson.Parser.StringToObject(Of ClassWithNullableByte)(inp)
        Assert.AreEqual(2, res.Gender)
    End Sub

End Class

Public Class ClassWithNullableByte
    Public Gender As Nullable(Of Byte)
End Class


Public Class ClassWithEnumerable
    Public Id As Guid
    Public Mails As IList(Of DataObject.Mail)
End Class

Public Class GetByIdAndNodeIdResponse
    Public Actions As List(Of String)
    Public SettingId As Guid
    Public NodeId As Guid
    Public NodeName As String
    Public Value As String
End Class

Public Class DataObject

    Public Guid As Guid
    Private _id As Integer

    Property Id As Integer
        Get
            Return _id
        End Get
        Set(value As Integer)
            _id = value
        End Set
    End Property

    Property Name As String
    Property Age As Integer
    Property BirthDay As DateTime

    Property MailList As List(Of Mail)
    Property IsSet As Boolean?

    Public Class Mail
        Property PersonId As Integer
        Property Address As String
    End Class

End Class