Imports Autodesk.AutoCAD.Runtime
Imports Autodesk.AutoCAD.DatabaseServices
Imports Autodesk.AutoCAD.Geometry
Imports System.Windows.Forms

Imports Autodesk.AutoCAD.Windows
Imports System.IO
Imports Carefine

Public Class UI_HoleExtractor

    Private filename As String

    Private minPt As Point3d

    Private maxPt As Point3d

    Dim rootNode As TreeNode

    Dim previousDia As String

    Dim thick As Double = 0

    Private Sub btnScan_Click(sender As Object, e As EventArgs) Handles btnScan.Click
        ScanHoles()

        btnExport.Enabled = True
    End Sub

    Private Sub ScanHoles()
        Dim selectedObjectIds As List(Of ObjectId) = DCS.RxServices.CADUtility.GetSelectedObjects(OpenMode.ForRead)

        Dim name As String = GetName(selectedObjectIds)

        minPt = GetMinPoint(selectedObjectIds)

        maxPt = GetMaxPoint(selectedObjectIds)

        Dim circleIds As List(Of ObjectId) = GetCircles(selectedObjectIds)

        ExtractCirclesData(name, minPt, maxPt, circleIds)
    End Sub

    Private Sub ExtractCirclesData(name As String, minPt As Point3d, maxPt As Point3d, circleIds As List(Of ObjectId))

        Dim database As Database = HostApplicationServices.WorkingDatabase

        Dim DRAWINGNAME As String = database.Filename

        Dim foldername As String = Path.GetDirectoryName(DRAWINGNAME)

        Dim strings As String() = DRAWINGNAME.Split("\"c)

        DRAWINGNAME = strings(strings.Length - 1)

        filename = foldername + "\" + DRAWINGNAME + "_" + name + ".mpr"

        btnExport.Text = "Export " + filename

        If File.Exists(filename) Then
            Dim fileinfo As FileInfo = New FileInfo(filename)

            If DCS.RxServices.FileHandelingUtility.IsFileOpen(fileinfo) Then

                MessageBox.Show(filename + " is open. Close it and try again")

                Exit Sub
            Else

            End If
        End If

        PopulateCirclesDataToTree(name, minPt, maxPt, circleIds)

    End Sub

    Private Function getHoleDataList(circleIds As List(Of ObjectId), maxPt As Point3d) As List(Of HoleData)

        Dim holeDataList As List(Of HoleData) = New List(Of HoleData)

        ProgressBar1.Maximum = circleIds.Count + 1
        ProgressBar1.Value = 0

        For Each id As ObjectId In circleIds
            If DCS.RxServices.CADUtility.IsCircle(id) Then

                Dim handle As String = DCS.RxServices.CADUtility.GetHandle(id)

                Dim cirCenter As Point3d = DCS.RxServices.CADUtility.GetCircleCenterPoint(id)

                Dim x As Double = maxPt.X - cirCenter.X

                Dim y As Double = maxPt.Y - cirCenter.Y

                Dim r As Double = DCS.RxServices.CADUtility.GetCircleRadius(id)

                Dim holeData As HoleData = New HoleData(handle, r, x, y)

                AddHoleDataToList(holeData, holeDataList, True)

            End If
            ProgressBar1.Value = ProgressBar1.Value + 1
        Next

        ProgressBar1.Value = 0

        Return holeDataList

    End Function

    Private Sub AddHoleDataToList(holeData As HoleData, holeDataList As List(Of HoleData), Optional bOverKill As Boolean = False)

        If isDuplicate(holeData, holeDataList) = False Then
            holeDataList.Add(holeData)
        Else
            If bOverKill = True Then
                DeleteHole(holeData)
            End If
        End If

    End Sub

    Public Sub deleteDBObject(ByRef dbObj As DBObject)
        'Dim ed As Editor = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor
        Dim db As Database = HostApplicationServices.WorkingDatabase
        Dim tm As Transaction = db.TransactionManager.StartTransaction()
        Try
            Dim ent As Entity = CType(tm.GetObject(dbObj.Id, OpenMode.ForWrite), Entity)
            ent.Erase()
            ent = Nothing
            dbObj = Nothing
            tm.Commit()
        Catch ex As Exception
        Finally
            tm.Dispose()
        End Try
        Autodesk.AutoCAD.ApplicationServices.Application.UpdateScreen()
    End Sub

    Private Function isDuplicate(holeData As HoleData, holeDataList As List(Of HoleData)) As Boolean

        For Each hd As HoleData In holeDataList

            If holeData.isEqual(hd) = True Then
                Return True
            End If

        Next

        Return False

    End Function

    Private Sub PopulateCirclesDataToTree(name As String, minPt As Point3d, maxPt As Point3d, circleIds As List(Of ObjectId))

        Dim holeDataList As List(Of HoleData) = Nothing

        holeDataList = getHoleDataList(circleIds, maxPt)

        holeDataList = holeDataList.OrderBy(Function(hd) hd.r).ToList()

        AutomaticHoleCorrection(holeDataList)

        Dim holeDictionary As Dictionary(Of Double, List(Of HoleData)) = New Dictionary(Of Double, List(Of HoleData))

        For Each hd As HoleData In holeDataList

            If holeDictionary.ContainsKey(hd.r) = True Then
                holeDictionary.Item(hd.r).Add(hd)
            Else
                Dim hdList As List(Of HoleData) = New List(Of HoleData)
                hdList.Add(hd)
                holeDictionary.Add(hd.r, hdList)
            End If

        Next

        holeTree.Nodes.Clear()

        rootNode = holeTree.Nodes.Add(name)

        For Each key As Double In holeDictionary.Keys

            Dim dia As Double = key * 2

            Dim diaNode As TreeNode = rootNode.Nodes.Add(dia.ToString())

            Dim hdlst As List(Of HoleData) = holeDictionary.Item(key)

            For Each hd As HoleData In hdlst
                Dim handleNode As TreeNode = diaNode.Nodes.Add(hd.handle)

                handleNode.Nodes.Add(" X =" + hd.x.ToString)

                handleNode.Nodes.Add(" Y =" + hd.y.ToString)

                'handleNode.Nodes.Add(" radius =" + hd.r.ToString)
            Next

        Next

    End Sub

    Private Sub LoadDIs(fileName As String)

        Try
            Using r As StreamReader = New StreamReader(fileName)

                Dim line As String = r.ReadLine

                ListBox1.Items.Add(line)

                If line <> Nothing Then

                    Do While (Not line Is Nothing)

                        line = r.ReadLine

                        If line <> Nothing Then

                            ListBox1.Items.Add(line)

                        End If

                    Loop

                End If

            End Using
        Catch ex As Exception

        End Try

    End Sub

    Private Function getAutoCorrectionMap(fileName As String) As Dictionary(Of Double, Double)

        Dim map As New Dictionary(Of Double, Double)

        Try
            Using r As StreamReader = New StreamReader(fileName)

                Dim line As String = r.ReadLine

                If line <> Nothing Then

                    Dim words As String() = line.Split(New Char() {","c})
                    map.Add(Convert.ToDouble(words(0)), Convert.ToDouble(words(1)))

                    Do While (Not line Is Nothing)

                        line = r.ReadLine

                        If line <> Nothing Then
                            words = line.Split(New Char() {","c})
                            map.Add(Convert.ToDouble(words(0)), Convert.ToDouble(words(1)))
                        End If

                    Loop

                End If

            End Using
        Catch ex As Exception
            Return map
        End Try

        Return map

    End Function

    Private Sub AutomaticHoleCorrection(holeDataList As List(Of HoleData))

        Try
            Dim fullAssemblyPath As String = System.Reflection.Assembly.GetExecutingAssembly().Location
            Dim foldername As String = Path.GetDirectoryName(fullAssemblyPath)
            'MessageBox.Show(foldername)
            Dim map As Dictionary(Of Double, Double) = getAutoCorrectionMap(foldername + "\" + "AutoCorrect.csv")

            For Each hd As HoleData In holeDataList

                If map.ContainsKey(hd.r * 2) Then '' looking for diameter
                    Dim newdia As Double = map.Item(hd.r * 2)
                    hd.r = newdia / 2.0

                    updateHoleRadius(hd)

                End If

            Next
        Catch ex As Exception

        End Try

    End Sub

    Private Sub updateHoleRadius(hd As HoleData)

        Dim handle As String = hd.handle
        Dim r As Double = hd.r

        DCS.RxServices.CADUtility.updateCircleRadius(hd.handle, r)

    End Sub

    Private Sub DeleteHole(hd As HoleData)
        DCS.RxServices.CADUtility.EraseObject(hd.handle)
    End Sub

    Private Function GetCircles(selectedObjectIds As List(Of ObjectId)) As List(Of ObjectId)

        ProgressBar1.Maximum = selectedObjectIds.Count + 1
        ProgressBar1.Value = 0

        Dim circleIds As List(Of ObjectId) = New List(Of ObjectId)
        For Each id As ObjectId In selectedObjectIds
            If DCS.RxServices.CADUtility.IsCircle(id) Then
                circleIds.Add(id)
            End If
            ProgressBar1.Value = ProgressBar1.Value + 1
        Next

        ProgressBar1.Value = 0

        Return circleIds

    End Function

    Private Function GetMinPoint(selectedObjectIds As List(Of ObjectId)) As Point3d
        For Each id As ObjectId In selectedObjectIds
            If DCS.RxServices.CADUtility.IsPolyLine(id) Then
                Dim ext As Autodesk.AutoCAD.DatabaseServices.Extents3d = DCS.RxServices.CADUtility.getExtent(id)
                Return ext.MinPoint
            End If
        Next
        Return New Point3d()
    End Function

    Private Function GetMaxPoint(selectedObjectIds As List(Of ObjectId)) As Point3d
        For Each id As ObjectId In selectedObjectIds
            If DCS.RxServices.CADUtility.IsPolyLine(id) Then
                Dim ext As Autodesk.AutoCAD.DatabaseServices.Extents3d = DCS.RxServices.CADUtility.getExtent(id)
                Return ext.MaxPoint
            End If
        Next
        Return New Point3d()
    End Function

    Private Function GetName(selectedObjectIds As List(Of ObjectId)) As String
        For Each id As ObjectId In selectedObjectIds
            If DCS.RxServices.CADUtility.IsText(id) Then
                Dim txtStr As String = DCS.RxServices.CADUtility.GetText(id)
                Return txtStr
            End If
        Next
        Return ""
    End Function

    Private Sub holeTree_AfterSelect(sender As Object, e As TreeViewEventArgs) Handles holeTree.AfterSelect

        Dim radius As Double
        If Double.TryParse(e.Node.Text, radius) = True Then

            If e.Node.Parent.Parent Is Nothing Then
                holeTree.LabelEdit = True
                'AddHandler holeTree.KeyUp, AddressOf holeTree_KeyUp
            End If

        Else
            holeTree.LabelEdit = False
            'RemoveHandler holeTree.KeyUp, AddressOf holeTree_KeyUp
        End If


        TextBox1.Text = ""

        Dim handle As String = e.Node.Text

        If DCS.RxServices.CADUtility.Zoom2Handle(handle) = True Then Return

        If handle.Length < 4 Then
            Return
        End If

    End Sub

    Private Sub btnExport_Click(sender As Object, e As EventArgs) Handles btnExport.Click
        Try

            If File.Exists(filename) Then
                File.Delete(filename)
            End If

            Using fs As New FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write)

                Using sw As New StreamWriter(fs)

                    WriteHeaderToFile(sw)

                    WriteBorderDataToFile(sw)

                    WriteHoleDataToFile(sw)

                    WriteFooterToFile(sw)

                End Using
            End Using

            ProgressBar1.Value = 0

            Using reader As StreamReader = New StreamReader(filename)
                TextBox1.Text = reader.ReadToEnd
            End Using

        Catch ex As Exception
            MessageBox.Show(ex.Message)
        End Try



    End Sub

    Public Sub WriteHeaderToFile(sw As StreamWriter)
        sw.WriteLine("[" + "H")
        sw.WriteLine("VERSION=" + Chr(34) + "4.0 Alpha" + Chr(34))
        sw.WriteLine("OP=" + Chr(34) + "1" + Chr(34))
        sw.WriteLine("FM=" + Chr(34) + "1" + Chr(34))
        sw.WriteLine("ML=" + Chr(34) + "2000" + Chr(34))
        sw.WriteLine("MAT=" + Chr(34) + "HOMAG" + Chr(34))
        sw.WriteLine("VIEW=" + Chr(34) + "NOMIRROR" + Chr(34))
        sw.WriteLine("ANZ=" + Chr(34) + "1" + Chr(34))
        sw.WriteLine("")
    End Sub

    Public Sub WriteBorderDataToFile(sw As StreamWriter)

        If ListBox1.SelectedItem Is Nothing Then
            thick = 9
        Else
            thick = Convert.ToDouble(ListBox1.SelectedItem.ToString)
        End If

        Dim deltaX As Double = maxPt.X - minPt.X
        Dim deltaY As Double = maxPt.Y - minPt.Y

        sw.WriteLine("<100 \WerkStck\")
        sw.WriteLine("LA=" + Chr(34) + deltaX.ToString("0.0#") + Chr(34))
        sw.WriteLine("BR=" + Chr(34) + deltaY.ToString("0.0#") + Chr(34))
        sw.WriteLine("DI=" + Chr(34) + (thick + 18).ToString("0.0#") + Chr(34)) 'thick is from the selection of the listview
        sw.WriteLine("FNX=" + Chr(34) + "0" + Chr(34))
        sw.WriteLine("FNY=" + Chr(34) + "0" + Chr(34))
        sw.WriteLine("AX=" + Chr(34) + "0" + Chr(34))
        sw.WriteLine("AY=" + Chr(34) + "0" + Chr(34))

        sw.WriteLine()

    End Sub

    Public Sub WriteHoleDataToFile(sw As StreamWriter)

        Try
            'Dim thick As Double = 0 ' from where we will get the thickness? Is it same what we select in the listview ?

            Dim n As Integer = rootNode.Nodes.Count

            Dim delta As Double = 100 / n

            For Each diaNode As TreeNode In rootNode.Nodes

                For Each circleNode As TreeNode In diaNode.Nodes

                    Try
                        Dim dia As Double = Convert.ToDouble(diaNode.Text)

                        Dim nodes As TreeNodeCollection = circleNode.Nodes

                        Dim xNode As TreeNode = nodes(0)
                        Dim yNode As TreeNode = nodes(1)

                        Dim x As Double = Convert.ToDouble(xNode.Text.Split(New Char() {"="c})(1).Trim)
                        Dim y As Double = Convert.ToDouble(yNode.Text.Split(New Char() {"="c})(1).Trim)

                        If dia < 10 And dia > 2 Then
                            sw.WriteLine("<102\BohrVert\")
                            sw.WriteLine("XA=" + Chr(34) + x.ToString("0.0#") + Chr(34))
                            sw.WriteLine("YA=" + Chr(34) + y.ToString("0.0#") + Chr(34))
                            sw.WriteLine("BM=" + Chr(34) + "LS" + Chr(34))
                            ''sw.WriteLine("TI=" + Chr(34) + "12" + Chr(34))
                            sw.WriteLine("TI=" + Chr(34) + (thick + 2).ToString("0.0#") + Chr(34))
                            sw.WriteLine("DU=" + Chr(34) + dia.ToString() + Chr(34))
                        Else
                            sw.WriteLine("<112\Tasche\")
                            sw.WriteLine("XA=" + Chr(34) + x.ToString("0.0#") + Chr(34))
                            sw.WriteLine("YA=" + Chr(34) + y.ToString("0.0#") + Chr(34))
                            sw.WriteLine("RD=" + Chr(34) + dia.ToString("0.0#") + "/2" + Chr(34))
                            ''sw.WriteLine("TI=" + Chr(34) + "14" + Chr(34))
                            sw.WriteLine("TI=" + Chr(34) + (thick + 2).ToString("0.0#") + Chr(34))
                            sw.WriteLine("XY=" + Chr(34) + "100" + Chr(34))
                            sw.WriteLine("T_=" + Chr(34) + "210" + Chr(34))
                            sw.WriteLine("F_=" + Chr(34) + "5" + Chr(34))
                        End If


                        sw.WriteLine()
                    Catch ex As Exception

                    End Try
                Next

                ProgressBar1.Value = ProgressBar1.Value + delta
                ProgressBar1.Update()

            Next
        Catch ex As Exception

        End Try
    End Sub

    Public Sub WriteFooterToFile(sw As StreamWriter)
        sw.WriteLine("!")
    End Sub

    Private Sub UI_HoleExtractor_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Dim fullAssemblyPath As String = System.Reflection.Assembly.GetExecutingAssembly().Location
        Dim foldername As String = Path.GetDirectoryName(fullAssemblyPath)
        LoadDIs(foldername + "\" + "DI.txt")
    End Sub

    Private Sub holeTree_AfterLabelEdit(sender As Object, e As NodeLabelEditEventArgs) Handles holeTree.AfterLabelEdit

        Dim dia As Double
        If Double.TryParse(e.Label, dia) = True Then

            If IsNumeric(e.Label) Then

                previousDia = ""

                UpdateAllHoles(e.Node, dia / 2.0)

            End If

        Else
            e.Node.Text = previousDia
            e.CancelEdit = True
        End If

    End Sub

    Private Sub UpdateAllHoles(node As TreeNode, r As Double)

        Dim circleHandles As TreeNodeCollection = node.Nodes

        For Each nd As TreeNode In circleHandles

            DCS.RxServices.CADUtility.updateCircleRadius(nd.Text, r)

        Next

        Autodesk.AutoCAD.ApplicationServices.Application.UpdateScreen()
        Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor.UpdateScreen()
        Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor.Regen()

    End Sub

    Private Sub holeTree_BeforeLabelEdit(sender As Object, e As NodeLabelEditEventArgs) Handles holeTree.BeforeLabelEdit
        previousDia = e.Node.Text
    End Sub

    Private Sub btnDeleteNode_Click(sender As Object, e As EventArgs) Handles btnDeleteNode.Click
        Try
            holeTree.SelectedNode.Remove()
        Catch ex As Exception

        End Try
    End Sub



    'Private Sub holeTree_KeyUp(sender As Object, e As KeyEventArgs) Handles holeTree.KeyUp
    '    If e.KeyCode = Keys.Delete Then

    '        If holeTree.SelectedNode Is Nothing Then

    '        Else
    '            RemoveHandler holeTree.KeyUp, AddressOf holeTree_KeyUp
    '            holeTree.SelectedNode.Remove()
    '        End If
    '    End If
    'End Sub


End Class
