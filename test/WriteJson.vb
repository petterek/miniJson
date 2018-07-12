﻿Imports System.Runtime.CompilerServices
Imports System.Runtime.Serialization
Imports NUnit.Framework

Module StringExtension

    <Extension()> Public Sub Deserialize(obj As String)
        Dim i = 0
    End Sub

End Module

<TestFixture> Public Class WriteJson

    <SetUp> Public Sub Setup()

    End Sub

    <Test> Public Sub IntegerIsWritten()

        Assert.AreEqual("{""ToTest"":1}", Writer.ObjectToString(New With {.ToTest = 1}))

    End Sub

    <Test> Public Sub ParseStringWithObjectToString()
        Dim testString = <![CDATA[<h3>{0}</h3>
                <p>Man skal ha en samtale med den syke allerede f&oslash;rste frav&aelig;rsdag. Dette gjelder b&aring;de n&aring;r den syke leverer egenmelding og ved sykmelding. Avklar forventet lengde p&aring; frav&aelig;ret og hvilke arbeidsoppgaver er den ansatte i stand til &aring; utf&oslash;re/ikke utf&oslash;re:</p>
                <ul>
                    <li>Om du som leder kan bidra med tiltak/tilpasning av arbeidssituasjonen, slik at den ansatte kan komme raskere tilbake p&aring; arbeidet.</li>
                    <li>Om sykmelder er kontaktet og evt. om sykmelderen hadde innspill til tilpasning av arbeidet. Dette skal sykmelder gj&oslash;re umiddelbart.</li>
                    <li>Om frav&aelig;ret har sammenheng med forholdene p&aring; arbeidsplassen.</li>
                </ul>
                <p>Noen ganger vil den ansatte f&oslash;le seg i stand til &aring; utf&oslash;re arbeid p&aring; tross&nbsp;av at det er utstedt en&nbsp;100% sykmelding. Vedkommende kan faktisk v&aelig;re i arbeid uten &aring; avklare dette med lege, men dere m&aring; sammen lage en oppf&oslash;lgingsplan som beskriver at arbeidet n&aring; er tilpasset. I tillegg m&aring; arbeidet som utf&oslash;res angis p&aring; D-blanketten av sykmeldingen&nbsp;punkt 13.5.</p>
                <p>Gj&oslash;r notater som inneholder ovennevnte elementer n&aring;r du registrerer f&oslash;rste samtale.</p>
                <p><strong>Ikke still sp&oslash;rsm&aring;l om diagnose, det har du ikke krav p&aring; &aring; f&aring; vite.</strong></p>]]>.Value

        Dim result = Writer.ObjectToString(testString)
        Dim expectedResult = """" & testString.Replace(vbLf, "\n") & """"
        Assert.AreEqual(expectedResult, result)

    End Sub

    <Test> Public Sub ParseStringWithObjectToStringStream()
        Dim testString = <![CDATA[<h3>{0}</h3>
                <p>Man skal ha en samtale med den syke allerede f&oslash;rste frav&aelig;rsdag. Dette gjelder b&aring;de n&aring;r den syke leverer egenmelding og ved sykmelding. Avklar forventet lengde p&aring; frav&aelig;ret og hvilke arbeidsoppgaver er den ansatte i stand til &aring; utf&oslash;re/ikke utf&oslash;re:</p>
                <ul>
                    <li>Om du som leder kan bidra med tiltak/tilpasning av arbeidssituasjonen, slik at den ansatte kan komme raskere tilbake p&aring; arbeidet.</li>
                    <li>Om sykmelder er kontaktet og evt. om sykmelderen hadde innspill til tilpasning av arbeidet. Dette skal sykmelder gj&oslash;re umiddelbart.</li>
                    <li>Om frav&aelig;ret har sammenheng med forholdene p&aring; arbeidsplassen.</li>
                </ul>
                <p>Noen ganger vil den ansatte f&oslash;le seg i stand til &aring; utf&oslash;re arbeid p&aring; tross&nbsp;av at det er utstedt en&nbsp;100% sykmelding. Vedkommende kan faktisk v&aelig;re i arbeid uten &aring; avklare dette med lege, men dere m&aring; sammen lage en oppf&oslash;lgingsplan som beskriver at arbeidet n&aring; er tilpasset. I tillegg m&aring; arbeidet som utf&oslash;res angis p&aring; D-blanketten av sykmeldingen&nbsp;punkt 13.5.</p>
                <p>Gj&oslash;r notater som inneholder ovennevnte elementer n&aring;r du registrerer f&oslash;rste samtale.</p>
                <p><strong>Ikke still sp&oslash;rsm&aring;l om diagnose, det har du ikke krav p&aring; &aring; f&aring; vite.</strong></p>]]>.Value
        Dim ms = New System.IO.MemoryStream()
        Writer.ObjectToString(ms, testString)
        Dim result = System.Text.Encoding.UTF8.GetString(ms.ToArray())
        Dim expectedResult = """" & testString.Replace(vbLf, "\n") & """"
        Assert.AreEqual(expectedResult, result)
    End Sub

    <Test(Description:="Testing encoding of strings"),
        TestCase("StandardText"),
        TestCase("ÆØÅ{"),
        TestCase("Some€"),
        TestCase("with " & ChrW(&H22) & " "),
        TestCase("\\\///" & ChrW(9)),
        TestCase("--" & ChrW(&HC), Description:="Formfeed"),
        TestCase("--" & vbCrLf),
        TestCase(Nothing),
        TestCase("WithChr""34"" ")
        > Public Sub TextIsWritten(input As String)

        Dim value = New EncodingTest With {.ToTest = input, .test = input}
        Dim res = Writer.ObjectToString(value)

        Dim toTest As EncodingTest = Nothing

        Assert.DoesNotThrow(Sub() toTest = Newtonsoft.Json.JsonConvert.DeserializeObject(Of EncodingTest)(res))

        Assert.AreEqual(value.ToTest, toTest.ToTest)
        Assert.AreEqual(value.test, toTest.test)

    End Sub

    Public Class EncodingTest
        Public ToTest As String
        Public test As String
    End Class

    Public Class BoolTest
        Public ThisIsTrue As Boolean
        Public ThisIsFalse As Boolean
    End Class

    <Test> Public Sub BooleanIsWritten()
        Dim o = New BoolTest With {.ThisIsTrue = True, .ThisIsFalse = False}
        Dim o2 As BoolTest = Nothing

        Assert.AreEqual("{""ThisIsTrue"":true,""ThisIsFalse"":false}", Writer.ObjectToString(o))
        Assert.DoesNotThrow(Sub() o2 = Newtonsoft.Json.JsonConvert.DeserializeObject(Of BoolTest)(Writer.ObjectToString(o)))

        Assert.AreEqual(o.ThisIsFalse, o2.ThisIsFalse)
        Assert.AreEqual(o.ThisIsTrue, o2.ThisIsTrue)
    End Sub

    <Test,
        TestCase(1),
        TestCase(123467854684),
        TestCase(-1)> Public Sub IntegersIsWritten(input As Long)
        Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(New With {.ToTest = input}), Writer.ObjectToString(New With {.ToTest = input}))
    End Sub

    <Test,
        TestCase(1),
        TestCase(1234),
        TestCase(Nothing)> Public Sub NullableIntegers(input As Integer?)

        Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(New With {.ToTest = input}), Writer.ObjectToString(New With {.ToTest = input}))

    End Sub

    <Test,
        TestCase(1.123),
        TestCase(684.7853),
        TestCase(-1.45644)> Public Sub DoubleIsWritten(input As Double)
        Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(New With {.ToTest = input}), Writer.ObjectToString(New With {.ToTest = input}))
    End Sub

    <Test> Public Sub MultilevelObjects()
        Dim o = New With {.ToTest = "", .Child = New With {.Name = "Test", .Year = 12}}
        'Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(o), Writer.ObjectToString(o))
        Assert.IsTrue(Writer.ObjectToString(o).Contains("""Name"":""Test"""))
        Assert.IsTrue(Writer.ObjectToString(o).Contains("""Year"":12"))
    End Sub

    <Test> Public Sub MultilevelObjectsWithStructure()
        Dim o = New With {.ToTest = "", .Child = New Test With {.Name = "jklj", .Year = 12}}
        'Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(o), Writer.ObjectToString(o))
        Assert.IsTrue(Writer.ObjectToString(o).Contains("""Name"":""jklj"""))
        Assert.IsTrue(Writer.ObjectToString(o).Contains("""Year"":12"))
    End Sub

    <Test> Public Sub IntegerArray()
        Dim toWrite As Integer() = {1, 2, 3}
        Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(toWrite), Writer.ObjectToString(toWrite))

    End Sub

    <Test> Public Sub IgnoredAttributesIsNotSerialized()

        Dim res = Writer.ObjectToString(New ClassWithIgnoredField())

        Assert.AreEqual("{""IsHere"":""HERE""}", res)

    End Sub

    Public Class ClassWithIgnoredField
        Public IsHere As String = "HERE"
        <IgnoreDataMember> Public NotSerialized As String = "NOT HERE"
    End Class

    <Test> Public Sub InheritedAttributesIsWrittenToText()
        Dim o As New Person2

        o.Addresse = "blbla"
        o.Name = "Mikael"

        Dim v As String = Writer.ObjectToString(o)
        Dim o2 As Person2 = Nothing

        Assert.DoesNotThrow(Sub() o2 = Newtonsoft.Json.JsonConvert.DeserializeObject(Of Person2)(v))

        Assert.AreEqual(o.Addresse, o2.Addresse)

    End Sub

    Public Class PetterJson

        Public Shared Function Format(val As String) As String
            Return "TEST CONFIG"
        End Function

    End Class

    <Test> Public Sub DateAttributesIsWrittenToText()
        Dim o As New ExcavationTripDate

        o.StartDate = New Date(1999, 6, 1, 22, 5, 12, 25)
        o.EndDate = New Date(2000, 6, 1, 0, 0, 0)

        Dim timeZone = TimeZoneInfo.Local.GetUtcOffset(o.StartDate)

        StringAssert.Contains("""EndDate"":""2000-06-01T00:00:00", Writer.ObjectToString(o))
        StringAssert.Contains("""StartDate"":""1999-06-01T22:05:12.025", Writer.ObjectToString(o))

        Dim des = Writer.ObjectToString(o)
        Dim o2 = Parser.StringToObject(Of ExcavationTripDate)(des)

        Assert.AreEqual(o.StartDate, o2.StartDate)

    End Sub

    <Test> Public Sub StringArray()
        Dim toWrite As String() = {"abc", "æøå", ""}
        Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(toWrite), Writer.ObjectToString(toWrite))

    End Sub

    <Test> Public Sub ObjectArray()
        Dim toWrite As Object() = {New With {.Name = "sd"}, New With {.Age = 42}}
        Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(toWrite), Writer.ObjectToString(toWrite))

    End Sub

    <Test> Public Sub WriteValueTypeArray()

        Dim toWrite = New With {.Values = {1, 2, 3, 4}}
        Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(toWrite), Writer.ObjectToString(toWrite))
    End Sub

    <Test> Public Sub Serializeguid()

        Dim toWrite = New With {.g = New Guid("FE41254C-FFFC-4121-8345-7353C5D128DC")}
        Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(toWrite), Writer.ObjectToString(toWrite))
    End Sub

    <Test> Public Sub TypeinfoIsIncludede()

        Dim toWrite = New ClassWithLong
        toWrite.Value = 12
        Writer.AddTypeInfoForObjects = True

        StringAssert.Contains("$type$"":""test.ClassWithLong""", Writer.ObjectToString(toWrite))

        Writer.AddTypeInfoForObjects = False
    End Sub

    <Test> Public Sub WriteIEumerableAsArray()
        Dim list As New Stack(Of String)
        list.Push("A")
        list.Push("B")
        list.Push("C")

        Assert.AreEqual("[""C"",""B"",""A""]", Writer.ObjectToString(list))

    End Sub

    <Test> Public Sub DictionaryIsWrittenAsObjectHash()
        Dim dic As New Dictionary(Of String, String)
        dic.Add("Test", "Value")
        dic.Add("Test2", "Value2")

        Assert.AreEqual("{""Test"":""Value"",""Test2"":""Value2""}", Writer.ObjectToString(dic))

    End Sub

    <Test> Public Sub WriteEnumValues()
        Dim v As New TestParser.ClassWithEnum

        v.Value = TestParser.MyEnum.Value1

        Assert.AreEqual("{""Value"":1}", Writer.ObjectToString(v))

    End Sub

    <Test> Public Sub WriteNonObjects()

        Assert.AreEqual("""MyString""", Writer.ObjectToString("MyString"))
        Assert.AreEqual("1", Writer.ObjectToString(1))
        Assert.AreEqual("1.23", Writer.ObjectToString(1.23))
    End Sub

    <Test> Public Sub WriteDictionaryOfValues()
        Dim dic As New Dictionary(Of String, Object)
        dic.Add("Int", 1)
        dic.Add("Float", 1.23)
        dic.Add("String", "str")

        Assert.AreEqual("{""Int"":1,""Float"":1.23,""String"":""str""}", Writer.ObjectToString(dic))
    End Sub

    <Test> Public Sub NullableValuesIsParsed()

        Dim res = Parser.StringToObject(Of ClassWithNullable)("{""Intvalue"":1}")
        Assert.AreEqual(1, res.Intvalue)

    End Sub

    <Test> Public Sub NullableValuesIsParsedWithNullValue()

        Dim res = Parser.StringToObject(Of ClassWithNullable)("{""Intvalue"":NULL}")
        Assert.AreEqual(False, res.Intvalue.HasValue)

    End Sub

    <Test> Public Sub StrangeBehaviorOnDate()

        Dim test = New MyObj
        test.TransactionDate = DateTime.Now()

        Dim res = Writer.ObjectToString(test)

        'res = Now().ToString("yyyy-MM-ddTHH\:mm\:ss.FFFK")

        'StringAssert.EndsWith("+01:00""}", res)

        Dim resultString = Parser.StringToObject(Of MyObj)(res)

        Newtonsoft.Json.JsonConvert.DeserializeObject(Of MyObj)("{""SourceTicketId"":""d877c090-1fad-4f95-b2a9-22648787f2b4"",""AccountId"":""c983068f-04e9-402a-acee-0ec9c6995178"",""Owner"":""80a62ae1-ee06-45b7-a8e4-69780db59113"",""Approver"":""9366378f-a200-4cc7-ad1c-340f8a3217a1"",""MinutesAffected"":0,""NewBalance"":360,""TransactionDate"":""2017-03-21T17:46:34.444+01:00""}")
        Parser.StringToObject(Of MyObj)("{""SourceTicketId"":""d877c090-1fad-4f95-b2a9-22648787f2b4"",""AccountId"":""c983068f-04e9-402a-acee-0ec9c6995178"",""Owner"":""80a62ae1-ee06-45b7-a8e4-69780db59113"",""Approver"":""9366378f-a200-4cc7-ad1c-340f8a3217a1"",""MinutesAffected"":0,""NewBalance"":360,""TransactionDate"":""2017-03-21T17:46:34.444+01:00""}")

    End Sub

    <Test> Public Sub GenericOverrideThrowsAmbiguousMatchException()
        Dim obj = New NotificationObject(Of String) With {.ViewData = "Test"}
        Assert.DoesNotThrow(Sub() Writer.ObjectToString(obj))
    End Sub

    Public Class MyObj
        Public Property SourceTicketId As Guid = New Guid("d877c090-1fad-4f95-b2a9-22648787f2b4")
        Public Property AccountId As Guid = New Guid("c983068f-04e9-402a-acee-0ec9c6995178")
        Public Property Owner As Guid = New Guid("80a62ae1-ee06-45b7-a8e4-69780db59113")
        Public Property Approver As Guid = New Guid("9366378f-a200-4cc7-ad1c-340f8a3217a1")
        Public Property MinutesAffected As Integer = 10
        Public Property NewBalance As Integer = 100
        Public Property TransactionDate As Date
    End Class

    <Test> Public Sub NullableByteWithNull()
        Dim test As New ClassWithNullableByte With {.Gender = Nothing}
        Dim result = miniJson.Writer.ObjectToString(test)
        Assert.AreEqual("{""Gender"":null}", Writer.ObjectToString(test))
    End Sub

    <Test> Public Sub NullableByteWithValue()
        Dim test As New ClassWithNullableByte With {.Gender = 2}
        Dim result = miniJson.Writer.ObjectToString(test)
        Assert.AreEqual("{""Gender"":2}", Writer.ObjectToString(test))
    End Sub

End Class

Public Class NotificationObject(Of TViewData)
    Inherits NotificationObject

    Public Shadows Property ViewData As TViewData
End Class

Public Class NotificationObject
    Public Property ViewData As Object
End Class

Public Class ClassWithNullable
    Public Intvalue As Integer?
End Class

Public Class ClassWithLong
    Public Value As Long
End Class

Public Class Holder(Of T)
    Public Value As T
End Class

Public Class TestWithDArray
    Public Name As String
    Public Year As Integer
    Public Scores As Double()
End Class

Public Class TestWithIntArray
    Public Name As String
    Public Year As Integer
    Public Scores As Integer()
End Class

Public Class Test
    Public Name As String
    Public Year As Integer
    Public Scores As List(Of String)
End Class

Public Class Person
    Public Navn As String
    Public Alder As Integer
    Public Speed As Double
    Public SpeedyGonzales As Single
    Public Barn As List(Of Person)
    Public TestInfo As Test
    Public ChildCount As Integer?
End Class

Public Class Person2
    Inherits Test

    Public Addresse As String

End Class

Public Class ExcavationTripDateTime
    Public StartDate As DateTime
    Public EndDate As DateTime
End Class

Public Class ExcavationTripDate
    Public StartDate As Date
    Public EndDate As Date
End Class