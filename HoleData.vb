'Imports Autodesk.AutoCAD.Runtime
'Imports Autodesk.AutoCAD.DatabaseServices
'Imports Autodesk.AutoCAD.Geometry

Imports Carefine

Public Class HoleData
    Public handle As String
    Public r As Double
    Public x As Double
    Public y As Double

    Public Sub New(handle As String, r As Double, x As Double, y As Double)
        Me.handle = handle
        Me.r = r
        Me.x = x
        Me.y = y
    End Sub

    Friend Function isEqual(hd As HoleData) As Boolean

        If r = hd.r And x = hd.x And y = hd.y Then
            Return True
        End If

        Return False

    End Function
End Class
