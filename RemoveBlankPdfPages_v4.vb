' ============================================================
' Xóa trang trắng trong PDF - v4
' .NET Framework 4.x, WinForms, compile standalone bằng vbc.exe
'
' YÊU CẦU NUGET (dùng setup_libs_pdf.bat):
'   PdfiumViewer + PdfiumViewer.Native.x86_64.v8-xfa
'   PdfSharp
'
' TÍNH NĂNG:
'   - Xử lý 1 file PDF hoặc cả thư mục
'   - Nút Bắt đầu / Dừng ở cả bước phân tích lẫn bước ghi file
'   - Gom tất cả trang nghi ngờ vào 1 preview chung, group theo file
'   - Tick/bỏ tick từng trang trước khi xóa
'   - Output thư mục: subfolder output\ bên trong thư mục gốc
'   - Output file đơn: _da_xoa_trang_trang.pdf cạnh file gốc
'   - Điều chỉnh 4 thông số phân tích qua UI
' ============================================================

Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Windows.Forms
Imports PdfSharp.Pdf
Imports PdfSharp.Pdf.IO

' ------------------------------------------------------------
' Thông tin 1 trang nghi ngờ
' ------------------------------------------------------------
Public Class ThongTinTrang
    Public Property ChiSo As Integer
    Public Property DuongDanFile As String
    Public Property TenFile As String
    Public Property Thumbnail As Image
    Public Property TyLeMuc As Double
    Public Property DeXuatXoa As Boolean
End Class

' ------------------------------------------------------------
' Dữ liệu truyền qua BackgroundWorker
' ------------------------------------------------------------
Public Class WorkerArgs
    Public Property DanhSachFile As String()
    Public Property ThuMucOutput As String   ' Nothing = file đơn
End Class

Public Class WorkerResult
    Public Property DanhSachNghiNgo As List(Of ThongTinTrang)
    Public Property BiHuy As Boolean
End Class

Public Class GhiArgs
    Public Property KetQuaChon As Dictionary(Of String, HashSet(Of Integer))
    Public Property ThuMucOutput As String
End Class

' ============================================================
' Form chính
' ============================================================
Public Class Form1
    Inherits Form

    ' Controls
    Private btnChonFile As Button
    Private btnChonThuMuc As Button
    Private btnDung As Button
    Private lblStatus As Label
    Private ProgressBar1 As ProgressBar
    Private nudNguong As NumericUpDown
    Private nudDoLech As NumericUpDown
    Private nudCropVien As NumericUpDown
    Private nudDpi As NumericUpDown

    ' BackgroundWorkers
    Private _workerPhanTich As BackgroundWorker
    Private _workerGhi As BackgroundWorker

    ' Lưu args cho bước ghi (cần sau khi preview xong)
    Private _pendingGhiArgs As GhiArgs
    Private _pendingThuMucOutput As String = ""

    ' Thông số đọc từ UI
    Private ReadOnly Property NGUONG_TY_LE_MUC As Double
        Get
            Return CDbl(nudNguong.Value)
        End Get
    End Property
    Private ReadOnly Property DO_LECH_MUC As Integer
        Get
            Return CInt(nudDoLech.Value)
        End Get
    End Property
    Private ReadOnly Property TY_LE_CROP_VIEN As Double
        Get
            Return CDbl(nudCropVien.Value)
        End Get
    End Property
    Private ReadOnly Property DPI_KIEM_TRA As Integer
        Get
            Return CInt(nudDpi.Value)
        End Get
    End Property

    Public Sub New()
        Me.Text = "Xóa trang trắng PDF v4"
        Me.Width = 520
        Me.Height = 380
        Me.FormBorderStyle = FormBorderStyle.FixedSingle
        Me.MaximizeBox = False

        ' --- GroupBox thông số ---
        Dim grp As New GroupBox() With {
            .Text = "Thông số phân tích",
            .Left = 8, .Top = 8, .Width = 490, .Height = 172
        }

        grp.Controls.Add(New Label() With {.Text = "Ngưỡng tỷ lệ mực (0.001–0.05):", .Left = 8, .Top = 22, .Width = 220, .Height = 20})
        nudNguong = New NumericUpDown() With {
            .Left = 230, .Top = 20, .Width = 100,
            .Minimum = 0.001D, .Maximum = 0.05D,
            .DecimalPlaces = 4, .Increment = 0.0005D, .Value = 0.0015D
        }
        grp.Controls.Add(nudNguong)
        grp.Controls.Add(New Label() With {.Text = "Thấp = ít xóa nhầm hơn", .Left = 336, .Top = 22, .Width = 148, .Height = 20, .Font = New Font("Segoe UI", 7)})

        grp.Controls.Add(New Label() With {.Text = "Độ lệch mực (1–100):", .Left = 8, .Top = 50, .Width = 220, .Height = 20})
        nudDoLech = New NumericUpDown() With {
            .Left = 230, .Top = 48, .Width = 100,
            .Minimum = 1, .Maximum = 100,
            .DecimalPlaces = 0, .Increment = 5, .Value = 40
        }
        grp.Controls.Add(nudDoLech)
        grp.Controls.Add(New Label() With {.Text = "Cao = nhạy hơn với chữ mờ", .Left = 336, .Top = 50, .Width = 148, .Height = 20, .Font = New Font("Segoe UI", 7)})

        grp.Controls.Add(New Label() With {.Text = "Crop viền mỗi bên (0–0.15):", .Left = 8, .Top = 78, .Width = 220, .Height = 20})
        nudCropVien = New NumericUpDown() With {
            .Left = 230, .Top = 76, .Width = 100,
            .Minimum = 0D, .Maximum = 0.15D,
            .DecimalPlaces = 3, .Increment = 0.005D, .Value = 0.04D
        }
        grp.Controls.Add(nudCropVien)
        grp.Controls.Add(New Label() With {.Text = "Bỏ viền đen / mép quăn", .Left = 336, .Top = 78, .Width = 148, .Height = 20, .Font = New Font("Segoe UI", 7)})

        grp.Controls.Add(New Label() With {.Text = "DPI render (72–300):", .Left = 8, .Top = 106, .Width = 220, .Height = 20})
        nudDpi = New NumericUpDown() With {
            .Left = 230, .Top = 104, .Width = 100,
            .Minimum = 72, .Maximum = 300,
            .DecimalPlaces = 0, .Increment = 25, .Value = 150
        }
        grp.Controls.Add(nudDpi)
        grp.Controls.Add(New Label() With {.Text = "Cao = chính xác hơn, chậm hơn", .Left = 336, .Top = 106, .Width = 148, .Height = 20, .Font = New Font("Segoe UI", 7)})

        Dim btnReset As New Button() With {.Text = "Mặc định", .Left = 8, .Top = 136, .Width = 90, .Height = 26}
        AddHandler btnReset.Click, Sub(s, ev)
                                       nudNguong.Value = 0.0015D
                                       nudDoLech.Value = 40
                                       nudCropVien.Value = 0.04D
                                       nudDpi.Value = 150
                                   End Sub
        grp.Controls.Add(btnReset)

        ' --- Nút chọn file / thư mục / dừng ---
        btnChonFile = New Button() With {
            .Text = "📄  Chọn 1 file PDF...",
            .Left = 8, .Top = 190, .Width = 190, .Height = 36
        }
        btnChonThuMuc = New Button() With {
            .Text = "📁  Chọn thư mục PDF...",
            .Left = 206, .Top = 190, .Width = 190, .Height = 36
        }
        btnDung = New Button() With {
            .Text = "⛔  Dừng",
            .Left = 404, .Top = 190, .Width = 94, .Height = 36,
            .Enabled = False,
            .BackColor = Color.FromArgb(220, 80, 80),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }

        ' --- Status + ProgressBar ---
        lblStatus = New Label() With {
            .Left = 8, .Top = 236, .Width = 490, .Height = 40,
            .Text = "Chưa chọn file."
        }
        ProgressBar1 = New ProgressBar() With {
            .Left = 8, .Top = 282, .Width = 490, .Height = 20
        }

        Me.Controls.AddRange(New Control() {grp, btnChonFile, btnChonThuMuc, btnDung, lblStatus, ProgressBar1})

        AddHandler btnChonFile.Click, AddressOf BtnChonFile_Click
        AddHandler btnChonThuMuc.Click, AddressOf BtnChonThuMuc_Click
        AddHandler btnDung.Click, AddressOf BtnDung_Click

        ' --- BackgroundWorker phân tích ---
        _workerPhanTich = New BackgroundWorker()
        _workerPhanTich.WorkerSupportsCancellation = True
        _workerPhanTich.WorkerReportsProgress = True
        AddHandler _workerPhanTich.DoWork, AddressOf WorkerPhanTich_DoWork
        AddHandler _workerPhanTich.ProgressChanged, AddressOf WorkerPhanTich_Progress
        AddHandler _workerPhanTich.RunWorkerCompleted, AddressOf WorkerPhanTich_Completed

        ' --- BackgroundWorker ghi file ---
        _workerGhi = New BackgroundWorker()
        _workerGhi.WorkerSupportsCancellation = True
        _workerGhi.WorkerReportsProgress = True
        AddHandler _workerGhi.DoWork, AddressOf WorkerGhi_DoWork
        AddHandler _workerGhi.ProgressChanged, AddressOf WorkerGhi_Progress
        AddHandler _workerGhi.RunWorkerCompleted, AddressOf WorkerGhi_Completed
    End Sub

    ' ============================================================
    ' Nút chọn file / thư mục
    ' ============================================================
    Private Sub BtnChonFile_Click(sender As Object, e As EventArgs)
        Using ofd As New OpenFileDialog()
            ofd.Filter = "PDF files (*.pdf)|*.pdf"
            If ofd.ShowDialog() <> DialogResult.OK Then Return
            BatDauPhanTich(New String() {ofd.FileName}, Nothing)
        End Using
    End Sub

    Private Sub BtnChonThuMuc_Click(sender As Object, e As EventArgs)
        Using fbd As New FolderBrowserDialog()
            fbd.Description = "Chọn thư mục chứa các file PDF cần xử lý"
            If fbd.ShowDialog() <> DialogResult.OK Then Return
            Dim thuMuc As String = fbd.SelectedPath
            Dim files() As String = Directory.GetFiles(thuMuc, "*.pdf", SearchOption.TopDirectoryOnly)
            If files.Length = 0 Then
                MessageBox.Show("Không tìm thấy file PDF nào trong thư mục này.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If
            BatDauPhanTich(files, Path.Combine(thuMuc, "output"))
        End Using
    End Sub

    Private Sub BtnDung_Click(sender As Object, e As EventArgs)
        If _workerPhanTich.IsBusy Then
            _workerPhanTich.CancelAsync()
            lblStatus.Text = "Đang dừng phân tích..."
        ElseIf _workerGhi.IsBusy Then
            _workerGhi.CancelAsync()
            lblStatus.Text = "Đang dừng ghi file..."
        End If
        btnDung.Enabled = False
    End Sub

    ' ============================================================
    ' BẮT ĐẦU PHÂN TÍCH (chạy trên BackgroundWorker)
    ' ============================================================
    Private Sub BatDauPhanTich(files() As String, thuMucOutput As String)
        DatTrangThaiDangChay(True)
        ProgressBar1.Value = 0
        lblStatus.Text = "Bắt đầu phân tích..."

        Dim args As New WorkerArgs() With {
            .DanhSachFile = files,
            .ThuMucOutput = thuMucOutput
        }
        _workerPhanTich.RunWorkerAsync(args)
    End Sub

    Private Sub WorkerPhanTich_DoWork(sender As Object, e As DoWorkEventArgs)
        Dim worker As BackgroundWorker = CType(sender, BackgroundWorker)
        Dim args As WorkerArgs = CType(e.Argument, WorkerArgs)
        Dim ketQua As New WorkerResult() With {.DanhSachNghiNgo = New List(Of ThongTinTrang)()}

        Dim tongFile As Integer = args.DanhSachFile.Length

        For fileIdx As Integer = 0 To tongFile - 1
            If worker.CancellationPending Then
                ketQua.BiHuy = True
                e.Result = ketQua
                Return
            End If

            Dim duongDan As String = args.DanhSachFile(fileIdx)
            worker.ReportProgress(0, String.Format("Phân tích file {0}/{1}: {2}", fileIdx + 1, tongFile, Path.GetFileName(duongDan)))

            Using doc As PdfiumViewer.PdfDocument = PdfiumViewer.PdfDocument.Load(duongDan)
                Dim tongTrang As Integer = doc.PageCount
                For chiSo As Integer = 0 To tongTrang - 1
                    If worker.CancellationPending Then
                        ketQua.BiHuy = True
                        e.Result = ketQua
                        Return
                    End If

                    ' Progress: (fileIdx * maxTrang + chiSo) / (tongFile * maxTrang) nhưng đơn giản hoá
                    Dim pct As Integer = CInt((fileIdx * 100.0 / tongFile) + (chiSo * 100.0 / tongTrang / tongFile))
                    worker.ReportProgress(Math.Min(99, pct),
                        String.Format("File {0}/{1} — Trang {2}/{3}: {4}",
                            fileIdx + 1, tongFile, chiSo + 1, tongTrang, Path.GetFileName(duongDan)))

                    Using bmpGoc As Bitmap = CType(doc.Render(chiSo, args_DPI(args), args_DPI(args), False), Bitmap)
                        Dim tyLe As Double = TinhTyLeMuc(bmpGoc, args)
                        If tyLe < args_Nguong(args) Then
                            Dim thong As New ThongTinTrang With {
                                .ChiSo = chiSo,
                                .DuongDanFile = duongDan,
                                .TenFile = Path.GetFileName(duongDan),
                                .Thumbnail = TaoThumbnail(bmpGoc, 160, 220),
                                .TyLeMuc = tyLe,
                                .DeXuatXoa = True
                            }
                            ketQua.DanhSachNghiNgo.Add(thong)
                        End If
                    End Using
                Next
            End Using
        Next

        e.Result = ketQua
    End Sub

    Private Sub WorkerPhanTich_Progress(sender As Object, e As ProgressChangedEventArgs)
        ProgressBar1.Value = e.ProgressPercentage
        lblStatus.Text = CStr(e.UserState)
    End Sub

    Private Sub WorkerPhanTich_Completed(sender As Object, e As RunWorkerCompletedEventArgs)
        DatTrangThaiDangChay(False)

        If e.Error IsNot Nothing Then
            lblStatus.Text = "Lỗi: " & e.Error.Message
            MessageBox.Show(e.Error.ToString(), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        Dim ketQua As WorkerResult = CType(e.Result, WorkerResult)

        If ketQua.BiHuy Then
            lblStatus.Text = "Đã dừng phân tích."
            ProgressBar1.Value = 0
            Return
        End If

        If ketQua.DanhSachNghiNgo.Count = 0 Then
            lblStatus.Text = "Không phát hiện trang trắng nào."
            MessageBox.Show(lblStatus.Text, "Kết quả", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        ' Lấy thuMucOutput từ args gốc - lưu tạm vào field _pendingThuMucOutput
        Dim thuMucOutput As String = _pendingThuMucOutput
        If thuMucOutput = "" Then thuMucOutput = Nothing

        ' Mở preview
        Dim ketQuaChon As Dictionary(Of String, HashSet(Of Integer))
        Using frm As New FormPreview(ketQua.DanhSachNghiNgo)
            lblStatus.Text = String.Format("Tìm thấy {0} trang nghi ngờ. Đang chờ xác nhận...", ketQua.DanhSachNghiNgo.Count)
            If frm.ShowDialog(CType(Me, IWin32Window)) <> DialogResult.OK Then
                lblStatus.Text = "Đã hủy — không có trang nào bị xóa."
                Return
            End If
            ketQuaChon = frm.LayKetQuaChon()
        End Using

        ' Đếm tổng trang được chọn
        Dim tongChon As Integer = 0
        For Each kv As KeyValuePair(Of String, HashSet(Of Integer)) In ketQuaChon
            tongChon += kv.Value.Count
        Next
        If tongChon = 0 Then
            lblStatus.Text = "Không có trang nào được chọn để xóa."
            Return
        End If

        ' Bắt đầu bước ghi
        _pendingGhiArgs = New GhiArgs() With {
            .KetQuaChon = ketQuaChon,
            .ThuMucOutput = thuMucOutput
        }
        BatDauGhi()
    End Sub

    ' ============================================================
    ' BẮT ĐẦU GHI FILE (chạy trên BackgroundWorker riêng)
    ' ============================================================
    Private Sub BatDauGhi()
        DatTrangThaiDangChay(True)
        ProgressBar1.Value = 0
        lblStatus.Text = "Bắt đầu ghi file output..."
        _workerGhi.RunWorkerAsync(_pendingGhiArgs)
    End Sub

    Private Sub WorkerGhi_DoWork(sender As Object, e As DoWorkEventArgs)
        Dim worker As BackgroundWorker = CType(sender, BackgroundWorker)
        Dim args As GhiArgs = CType(e.Argument, GhiArgs)

        If args.ThuMucOutput IsNot Nothing AndAlso Not Directory.Exists(args.ThuMucOutput) Then
            Directory.CreateDirectory(args.ThuMucOutput)
        End If

        Dim danhSachFile As New List(Of String)(args.KetQuaChon.Keys)
        Dim tongFile As Integer = danhSachFile.Count
        Dim soXong As Integer = 0

        For Each duongDanGoc As String In danhSachFile
            If worker.CancellationPending Then
                e.Cancel = True
                Return
            End If

            Dim nhomXoa As HashSet(Of Integer) = args.KetQuaChon(duongDanGoc)
            If nhomXoa.Count = 0 Then Continue For

            Dim duongDanMoi As String
            If args.ThuMucOutput IsNot Nothing Then
                duongDanMoi = Path.Combine(args.ThuMucOutput, Path.GetFileName(duongDanGoc))
            Else
                duongDanMoi = Path.Combine(
                    Path.GetDirectoryName(duongDanGoc),
                    Path.GetFileNameWithoutExtension(duongDanGoc) & "_da_xoa_trang_trang.pdf")
            End If

            worker.ReportProgress(CInt(soXong * 100.0 / tongFile),
                String.Format("Đang ghi file {0}/{1}: {2}", soXong + 1, tongFile, Path.GetFileName(duongDanGoc)))

            GhiPdfMoi(duongDanGoc, duongDanMoi, nhomXoa)
            soXong += 1
        Next

        e.Result = soXong
    End Sub

    Private Sub WorkerGhi_Progress(sender As Object, e As ProgressChangedEventArgs)
        ProgressBar1.Value = e.ProgressPercentage
        lblStatus.Text = CStr(e.UserState)
    End Sub

    Private Sub WorkerGhi_Completed(sender As Object, e As RunWorkerCompletedEventArgs)
        DatTrangThaiDangChay(False)
        ProgressBar1.Value = 100

        If e.Error IsNot Nothing Then
            lblStatus.Text = "Lỗi ghi file: " & e.Error.Message
            MessageBox.Show(e.Error.ToString(), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        If e.Cancelled Then
            lblStatus.Text = "Đã dừng ghi file. Một số file có thể chưa được xử lý."
            ProgressBar1.Value = 0
            Return
        End If

        Dim soFile As Integer = CInt(e.Result)
        Dim msg As String
        If _pendingGhiArgs.ThuMucOutput IsNot Nothing Then
            msg = String.Format("Hoàn tất! Đã xử lý {0} file. Kết quả trong: output\", soFile)
        Else
            msg = String.Format("Hoàn tất! Đã lưu file output.")
        End If
        lblStatus.Text = msg
        MessageBox.Show(msg, "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    ' ============================================================
    ' Helper: bật/tắt trạng thái đang chạy
    ' ============================================================
    Private Sub DatTrangThaiDangChay(dangChay As Boolean)
        btnChonFile.Enabled = Not dangChay
        btnChonThuMuc.Enabled = Not dangChay
        btnDung.Enabled = dangChay
        Application.UseWaitCursor = dangChay
    End Sub

    ' ============================================================
    ' Helper: đọc thông số từ UI (dùng trong DoWork thread-safe bằng
    ' cách đóng gói vào WorkerArgs trước khi chạy background)
    ' ============================================================
    ' Thay vì truyền thông số qua args riêng, ta lưu snapshot vào Tag
    ' của worker trước khi RunWorkerAsync
    Private _snapNguong As Double = 0.0015
    Private _snapDoLech As Integer = 40
    Private _snapCrop As Double = 0.04
    Private _snapDpi As Integer = 150

    Private Function args_DPI(args As WorkerArgs) As Integer
        Return _snapDpi
    End Function
    Private Function args_Nguong(args As WorkerArgs) As Double
        Return _snapNguong
    End Function

    ' Override để chụp snapshot thông số trước khi start worker
    Private Sub BatDauPhanTich_WithSnapshot(files() As String, thuMucOutput As String)
        _snapNguong = NGUONG_TY_LE_MUC
        _snapDoLech = DO_LECH_MUC
        _snapCrop = TY_LE_CROP_VIEN
        _snapDpi = DPI_KIEM_TRA
        _pendingThuMucOutput = If(thuMucOutput, "")   ' lưu để dùng lại ở Completed
        BatDauPhanTich(files, thuMucOutput)
    End Sub

    ' ============================================================
    ' Phân tích pixel
    ' ============================================================
    Private Function TinhTyLeMuc(bmp As Bitmap, args As WorkerArgs) As Double
        Dim cropX As Integer = CInt(bmp.Width * _snapCrop)
        Dim cropY As Integer = CInt(bmp.Height * _snapCrop)
        Dim vung As New Rectangle(cropX, cropY,
            Math.Max(1, bmp.Width - 2 * cropX),
            Math.Max(1, bmp.Height - 2 * cropY))

        Dim bmpData As BitmapData = bmp.LockBits(vung, ImageLockMode.ReadOnly, bmp.PixelFormat)
        Try
            Dim bpp As Integer = Bitmap.GetPixelFormatSize(bmp.PixelFormat) \ 8
            Dim soByte As Integer = bmpData.Stride * vung.Height
            Dim buf(soByte - 1) As Byte
            Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, buf, 0, soByte)

            Dim tongDoSang As Long = 0
            Dim tongPixel As Integer = vung.Width * vung.Height
            If tongPixel = 0 Then Return 0

            Dim doSang(tongPixel - 1) As Byte
            Dim viTri As Integer = 0
            For y As Integer = 0 To vung.Height - 1
                Dim rowOff As Integer = y * bmpData.Stride
                For x As Integer = 0 To vung.Width - 1
                    Dim off As Integer = rowOff + x * bpp
                    If off + 2 >= buf.Length Then Continue For
                    Dim sang As Byte = CByte((CInt(buf(off + 2)) + buf(off + 1) + buf(off)) \ 3)
                    doSang(viTri) = sang
                    tongDoSang += sang
                    viTri += 1
                Next
            Next

            Dim nenTB As Double = tongDoSang / tongPixel
            Dim soMuc As Integer = 0
            For i As Integer = 0 To tongPixel - 1
                If nenTB - doSang(i) >= _snapDoLech Then soMuc += 1
            Next
            Return soMuc / tongPixel
        Finally
            bmp.UnlockBits(bmpData)
        End Try
    End Function

    Private Function TaoThumbnail(bmpGoc As Bitmap, rongMax As Integer, caoMax As Integer) As Image
        Dim tySo As Double = Math.Min(rongMax / bmpGoc.Width, caoMax / bmpGoc.Height)
        Dim w As Integer = Math.Max(1, CInt(bmpGoc.Width * tySo))
        Dim h As Integer = Math.Max(1, CInt(bmpGoc.Height * tySo))
        Dim thumb As New Bitmap(w, h)
        Using g As Graphics = Graphics.FromImage(thumb)
            g.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
            g.DrawImage(bmpGoc, 0, 0, w, h)
        End Using
        Return thumb
    End Function

    Private Sub GhiPdfMoi(duongDanGoc As String, duongDanMoi As String, chiSoCanXoa As HashSet(Of Integer))
        Dim docGoc As PdfSharp.Pdf.PdfDocument = PdfReader.Open(duongDanGoc, PdfDocumentOpenMode.Import)
        Dim docMoi As New PdfSharp.Pdf.PdfDocument()
        For i As Integer = 0 To docGoc.PageCount - 1
            If Not chiSoCanXoa.Contains(i) Then
                docMoi.AddPage(docGoc.Pages(i))
            End If
        Next
        If docMoi.PageCount = 0 AndAlso docGoc.PageCount > 0 Then
            docMoi.AddPage(docGoc.Pages(0))
        End If
        docMoi.Save(duongDanMoi)
    End Sub

    ' Gọi đúng hàm có snapshot (override click handlers)
    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        RemoveHandler btnChonFile.Click, AddressOf BtnChonFile_Click
        RemoveHandler btnChonThuMuc.Click, AddressOf BtnChonThuMuc_Click
        AddHandler btnChonFile.Click, Sub(s, ev)
                                          Using ofd As New OpenFileDialog()
                                              ofd.Filter = "PDF files (*.pdf)|*.pdf"
                                              If ofd.ShowDialog() <> DialogResult.OK Then Return
                                              BatDauPhanTich_WithSnapshot(New String() {ofd.FileName}, Nothing)
                                          End Using
                                      End Sub
        AddHandler btnChonThuMuc.Click, Sub(s, ev)
                                            Using fbd As New FolderBrowserDialog()
                                                fbd.Description = "Chọn thư mục chứa các file PDF cần xử lý"
                                                If fbd.ShowDialog() <> DialogResult.OK Then Return
                                                Dim thuMuc As String = fbd.SelectedPath
                                                Dim files() As String = Directory.GetFiles(thuMuc, "*.pdf", SearchOption.TopDirectoryOnly)
                                                If files.Length = 0 Then
                                                    MessageBox.Show("Không tìm thấy file PDF nào.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information)
                                                    Return
                                                End If
                                                BatDauPhanTich_WithSnapshot(files, Path.Combine(thuMuc, "output"))
                                            End Using
                                        End Sub
    End Sub

End Class

' ============================================================
' Entry point
' ============================================================
Module MainEntry
    <STAThread>
    Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.Run(New Form1())
    End Sub
End Module

' ============================================================
' FormPreview
' ============================================================
Public Class FormPreview
    Inherits Form

    Private ReadOnly _danhSach As List(Of ThongTinTrang)
    Private _listView As ListView
    Private _imageList As ImageList
    Private _lblInfo As Label

    Public Sub New(danhSach As List(Of ThongTinTrang))
        _danhSach = danhSach
        KhoiTaoGiaoDien()
        NapDuLieu()
    End Sub

    Private Sub KhoiTaoGiaoDien()
        Me.Text = "Review & Xác nhận các trang sẽ bị xóa"
        Me.Width = 900
        Me.Height = 680
        Me.StartPosition = FormStartPosition.CenterParent

        _lblInfo = New Label() With {
            .Dock = DockStyle.Top,
            .Height = 36,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(8, 0, 0, 0),
            .Font = New Font("Segoe UI", 9)
        }

        _imageList = New ImageList() With {
            .ImageSize = New Size(160, 220),
            .ColorDepth = ColorDepth.Depth32Bit
        }
        _listView = New ListView() With {
            .Dock = DockStyle.Fill,
            .View = View.LargeIcon,
            .CheckBoxes = True,
            .LargeImageList = _imageList,
            .MultiSelect = True,
            .ShowGroups = True
        }

        Dim panelBot As New Panel() With {.Dock = DockStyle.Bottom, .Height = 52}

        Dim btnChonTat As New Button() With {.Text = "Chọn tất cả", .Left = 8, .Top = 10, .Width = 110, .Height = 32}
        Dim btnBoTat As New Button() With {.Text = "Bỏ chọn tất cả", .Left = 126, .Top = 10, .Width = 120, .Height = 32}
        Dim btnOk As New Button() With {.Text = "✔  Xóa các trang đã chọn", .Left = 620, .Top = 10, .Width = 240, .Height = 32,
            .BackColor = Color.FromArgb(60, 160, 80), .ForeColor = Color.White, .FlatStyle = FlatStyle.Flat}
        Dim btnCancel As New Button() With {.Text = "Hủy", .Left = 530, .Top = 10, .Width = 82, .Height = 32}

        AddHandler btnChonTat.Click, Sub(s, ev)
                                         For Each item As ListViewItem In _listView.Items
                                             item.Checked = True
                                         Next
                                     End Sub
        AddHandler btnBoTat.Click, Sub(s, ev)
                                       For Each item As ListViewItem In _listView.Items
                                           item.Checked = False
                                       Next
                                   End Sub
        AddHandler btnOk.Click, Sub(s, ev)
                                    Me.DialogResult = DialogResult.OK
                                    Me.Close()
                                End Sub
        AddHandler btnCancel.Click, Sub(s, ev)
                                        Me.DialogResult = DialogResult.Cancel
                                        Me.Close()
                                    End Sub

        panelBot.Controls.AddRange(New Control() {btnChonTat, btnBoTat, btnCancel, btnOk})
        Me.Controls.Add(_listView)
        Me.Controls.Add(panelBot)
        Me.Controls.Add(_lblInfo)
    End Sub

    Private Sub NapDuLieu()
        Dim danhSachFile As New List(Of String)
        For Each t As ThongTinTrang In _danhSach
            If Not danhSachFile.Contains(t.DuongDanFile) Then danhSachFile.Add(t.DuongDanFile)
        Next

        For Each dd As String In danhSachFile
            _listView.Groups.Add(New ListViewGroup(Path.GetFileName(dd), Path.GetFileName(dd)))
        Next

        For Each trang As ThongTinTrang In _danhSach
            _imageList.Images.Add(trang.Thumbnail)
            Dim imgIdx As Integer = _imageList.Images.Count - 1
            Dim nhan As String = String.Format("Trang {0}{1}{2:P2} mực", trang.ChiSo + 1, Environment.NewLine, trang.TyLeMuc)
            Dim nhomCuaTrang As ListViewGroup = Nothing
            For Each g As ListViewGroup In _listView.Groups
                If g.Header = trang.TenFile Then : nhomCuaTrang = g : Exit For : End If
            Next
            Dim item As New ListViewItem(nhan, imgIdx) With {.Checked = True, .Tag = trang, .Group = nhomCuaTrang}
            _listView.Items.Add(item)
        Next

        _lblInfo.Text = String.Format("Tìm thấy {0} trang nghi ngờ trong {1} file. ✔ Tick = xóa  |  Bỏ tick = giữ lại.",
            _danhSach.Count, _listView.Groups.Count)
    End Sub

    Public Function LayKetQuaChon() As Dictionary(Of String, HashSet(Of Integer))
        Dim ketQua As New Dictionary(Of String, HashSet(Of Integer))
        For Each item As ListViewItem In _listView.Items
            If Not item.Checked Then Continue For
            Dim trang As ThongTinTrang = CType(item.Tag, ThongTinTrang)
            If Not ketQua.ContainsKey(trang.DuongDanFile) Then
                ketQua(trang.DuongDanFile) = New HashSet(Of Integer)
            End If
            ketQua(trang.DuongDanFile).Add(trang.ChiSo)
        Next
        Return ketQua
    End Function

End Class
