Imports Autodesk.AutoCAD.Runtime
Imports Autodesk.AutoCAD.DatabaseServices
Imports Autodesk.AutoCAD.Geometry
Imports System.Windows.Forms

Imports Autodesk.AutoCAD.Windows

Public Class CareFine_commands

    Public myPaletteSet As PaletteSet

    Public ui_HoleExtractor As UI_HoleExtractor

    <CommandMethod("HE")>
    Public Sub HoleExtractor()
        LoadUI()
    End Sub

    Private Sub LoadUI()
        If (myPaletteSet = Nothing) Then

            myPaletteSet = New PaletteSet("CareFine", New Guid("FB23C350-88C7-4585-8080-148DFA32AE7A"))

            ui_HoleExtractor = New UI_HoleExtractor

            myPaletteSet.Add("CareFine", ui_HoleExtractor)

        End If

        myPaletteSet.Visible = True
    End Sub

End Class
