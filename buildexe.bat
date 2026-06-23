@echo off
setlocal enabledelayedexpansion

set "FRAMEWORK_BASE=C:\Windows\Microsoft.NET\Framework64"
set "VBC="

rem Duyet tu 4.8 xuong 4.0, lay phien ban cao nhat duoc cai
for %%V in (4.8 4.7.2 4.7.1 4.7 4.6.2 4.6.1 4.6 4.5.2 4.5.1 4.5 4.0) do (
    if "!VBC!"=="" (
        for /d %%D in ("%FRAMEWORK_BASE%\v%%V*") do (
            if exist "%%D\vbc.exe" (
                set "VBC=%%D\vbc.exe"
            )
        )
    )
)

rem Neu khong tim duoc thi thu Framework 32-bit
if "!VBC!"=="" (
    set "FRAMEWORK_BASE=C:\Windows\Microsoft.NET\Framework"
    for %%V in (4.8 4.7.2 4.7.1 4.7 4.6.2 4.6.1 4.6 4.5.2 4.5.1 4.5 4.0) do (
        if "!VBC!"=="" (
            for /d %%D in ("%FRAMEWORK_BASE%\v%%V*") do (
                if exist "%%D\vbc.exe" (
                    set "VBC=%%D\vbc.exe"
                )
            )
        )
    )
)

if "!VBC!"=="" (
    echo [ERROR] Khong tim thay vbc.exe cua .NET Framework 4.x
    pause
    exit /b 1
)

echo [INFO] Dung compiler: !VBC!

rem ============================================================
rem Kiem tra cac DLL can thiet co trong thu muc libs\ chua.
rem Neu chua co, chay setup_libs_pdf.bat truoc.
rem ============================================================
set "LIBDIR=%cd%\libs"

if not exist "%LIBDIR%\PdfiumViewer.dll" (
    echo [ERROR] Khong tim thay %LIBDIR%\PdfiumViewer.dll
    echo         Hay chay setup_libs_pdf.bat truoc khi build.
    pause
    exit /b 1
)
if not exist "%LIBDIR%\PdfSharp.dll" (
    echo [ERROR] Khong tim thay %LIBDIR%\PdfSharp.dll
    echo         Hay chay setup_libs_pdf.bat truoc khi build.
    pause
    exit /b 1
)

rem ============================================================
rem Build - PdfSharp.Pdf / PdfSharp.Pdf.IO la NAMESPACE, khong
rem phai assembly rieng, nen KHONG duoc dung /r voi 2 ten do.
rem Chi can reference dung 1 file PdfSharp.dll.
rem ============================================================
"!VBC!" ^
/target:winexe ^
/optionstrict+ ^
/utf8output ^
/r:System.dll ^
/r:System.Windows.Forms.dll ^
/r:System.Drawing.dll ^
/r:"%LIBDIR%\PdfiumViewer.dll" ^
/r:"%LIBDIR%\PdfSharp.dll" ^
/optimize+ ^
/platform:x86 ^
/out:"%cd%\RemoveBlankPdfPages.exe" ^
"%cd%\RemoveBlankPdfPages_v4.vb"

if errorlevel 1 (
    echo.
    echo [ERROR] Build that bai. Xem loi phia tren.
    pause
    exit /b 1
)

rem ============================================================
rem Copy cac DLL can thiet ra cung thu muc voi .exe.
rem pdfium.dll la native DLL - .NET se tim no CUNG thu muc voi
rem .exe luc runtime, KHONG phai trong libs\, nen phai copy ra.
rem ============================================================
echo.
echo [INFO] Dang copy cac DLL can thiet ra cung thu muc voi .exe...

copy "%LIBDIR%\PdfiumViewer.dll" "%cd%\" /y >nul
copy "%LIBDIR%\PdfSharp.dll"     "%cd%\" /y >nul

if exist "%LIBDIR%\pdfium.dll" (
    copy "%LIBDIR%\pdfium.dll" "%cd%\" /y >nul
    echo [OK]  Da copy pdfium.dll
) else (
    echo [!!] KHONG TIM THAY %LIBDIR%\pdfium.dll
    echo      Chuong trinh se loi DllNotFoundException khi chay.
    echo      Hay chay lai setup_libs_pdf.bat.
)

echo.
echo ============================================
echo  Build thanh cong: %cd%\RemoveBlankPdfPages.exe
echo ============================================
pause
endlocal
