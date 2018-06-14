Imports NUnit.Framework

<TestFixture>
Public Class TestCornerCase

    <Test>
    Public Sub FromPawel()
        Dim input = "[{""GroupId"":""ba809198-b3af-4bf5-bbad-24e71c216865"",""Translations"":{""en-US"":""Extended properties"",""sv-SE"" : ""Extended properties"",""nb-NO"":""Utvidede egenskaper""}},
                    {""GroupId"":""a0c7270e-7363-4bab-8352-3c0042585f3b"",""Translations"":{""en-US"":""Person notes"",""sv-SE"":""Person notes"",""nb-NO"":""Personnotater""}}]"

        Dim ret = miniJson.Parser.StringToObject(Of List(Of DeletableDataGroupTranslations))(input)

    End Sub

    Public Class DeletableDataGroupTranslations

        Public GroupId As Guid
        Public Translations As Dictionary(Of String, String)

    End Class

End Class