@echo off
title Setup Libraries - Xoa Trang Trang PDF
setlocal enabledelayedexpansion

if not exist "libs"          mkdir libs
if not exist "libs\x64"      mkdir libs\x64
if not exist "libs\tmp"      mkdir libs\tmp
if not exist "libs\tmp\pkg"  mkdir libs\tmp\pkg

call :TaiNuget
if errorlevel 1 goto :end

call :TaiGoiQuanLy "PdfiumViewer" "2.13.0" "PdfiumViewer.dll" "pdfiumviewer"
call :TaiGoiQuanLy "PdfSharp" "1.50.5147" "PdfSharp.dll" "pdfsharp"
call :TaiGoiNative

echo [INFO] Don dep package tam...
for /d %%D in ("libs\tmp\pkg\*") do rmdir /s /q "%%D" >nul 2>&1

call :BaoCaoKetQua
goto :end

:: ============================================================
:: SUBROUTINE: Tai nuget.exe (ep TLS 1.2)
:: ============================================================
:TaiNuget
if exist "libs\tmp\nuget.exe" (
    echo [OK]  nuget.exe da co san.
    exit /b 0
)

echo [INFO] Dang tai nuget.exe...
powershell -NoProfile -Command ^
    "[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12; ^
    try { Invoke-WebRequest -Uri 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile 'libs\tmp\nuget.exe' -UseBasicParsing } ^
    catch { Write-Host '[POWERSHELL ERROR]' $_.Exception.Message; exit 1 }"

if not exist "libs\tmp\nuget.exe" (
    echo [!!] Khong tai duoc nuget.exe. Xem loi [POWERSHELL ERROR] o tren.
    echo      Nguyen nhan thuong gap: khong co Internet, firewall chan
    echo      dist.nuget.org, hoac proxy cong ty.
    pause
    exit /b 1
)
echo [OK]  nuget.exe san sang.
exit /b 0

:: ============================================================
:: SUBROUTINE: Tai 1 goi NuGet quan ly (.dll thuan, khong native)
:: Tham so: %1 = TenGoi   %2 = Version   %3 = TenFileDLL can tim
:: Dò tìm DLL bằng "for /r" thay vì đoán cứng đường dẫn lib\netXX,
:: vì cấu trúc thư mục lib có thể khác nhau giữa các version.
:: ============================================================
:TaiGoiQuanLy
set "TENGOI=%~1"
set "PHIENBAN=%~2"
set "TENDLL=%~3"

if exist "libs\%TENDLL%" (
    echo [OK]  %TENDLL% da co trong libs\, bo qua %TENGOI%.
    copy "libs\%TENDLL%" "." /y >nul 2>&1
    exit /b 0
)

set "THUMUCGOI=libs\tmp\pkg\%TENGOI%.%PHIENBAN%"

echo [INFO] Dang tai %TENGOI% %PHIENBAN%...
libs\tmp\nuget.exe install "%TENGOI%" -Version "%PHIENBAN%" -OutputDirectory "libs\tmp\pkg" -NonInteractive
if errorlevel 1 (
    echo [!!] nuget install %TENGOI% THAT BAI ^(xem loi nuget phia tren^).
)

:: LUU Y QUAN TRONG: nuget.exe co the bao "already installed" va
:: KHONG tao thu muc OutputDirectory neu goi da co trong cache toan
:: cuc cua user (~\.nuget\packages). Vi vay phai kiem tra CA HAI noi.
:: Dung "for /r" de khong phu thuoc ten thu muc con (net20/net40/net45
:: khac nhau tuy goi).
set "DUONGDANTIMTHAY="
if exist "%THUMUCGOI%" (
    for /r "%THUMUCGOI%" %%F in (%TENDLL%) do (
        if exist "%%F" set "DUONGDANTIMTHAY=%%F"
    )
)

if not defined DUONGDANTIMTHAY (
    set "CACHEGOI=%USERPROFILE%\.nuget\packages\%TENGOI%\%PHIENBAN%"
    if exist "!CACHEGOI!" (
        for /r "!CACHEGOI!" %%F in (%TENDLL%) do (
            if exist "%%F" set "DUONGDANTIMTHAY=%%F"
        )
    )
)

:: Fallback cuoi: ten thu muc trong cache toan cuc cua NuGet luon
:: duoc luu VIET THUONG (vd "pdfiumviewer"), du ten goi goc viet hoa.
:: %4 (TENGOI_THUONG) duoc truyen san tu noi goi subroutine nay.
if not defined DUONGDANTIMTHAY (
    if not "%~4"=="" (
        set "CACHEGOI2=%USERPROFILE%\.nuget\packages\%~4\%PHIENBAN%"
        if exist "!CACHEGOI2!" (
            for /r "!CACHEGOI2!" %%F in (%TENDLL%) do (
                if exist "%%F" set "DUONGDANTIMTHAY=%%F"
            )
        )
    )
)

if not defined DUONGDANTIMTHAY (
    echo [!!] KHONG TIM THAY %TENDLL% sau khi tai %TENGOI%.
    echo      Co the goi nay khong ton tai dung version, hoac tai loi.
    exit /b 1
)

echo [OK]  Tim thay %TENDLL% tai: !DUONGDANTIMTHAY!
copy "!DUONGDANTIMTHAY!" "libs\" /y >nul 2>&1
copy "!DUONGDANTIMTHAY!" "."     /y >nul 2>&1
exit /b 0

:: ============================================================
:: SUBROUTINE: Tai pdfium.dll (native x64) cho PdfiumViewer
:: ============================================================
:TaiGoiNative
if exist "libs\pdfium.dll" (
    echo [OK]  pdfium.dll da co trong libs\.
    copy "libs\pdfium.dll" "." /y >nul 2>&1
    exit /b 0
)

set "TENGOI=PdfiumViewer.Native.x86_64.v8-xfa"
set "PHIENBAN=2018.4.8.256"
set "THUMUCGOI=libs\tmp\pkg\%TENGOI%.%PHIENBAN%"

if not exist "%THUMUCGOI%" (
    echo [INFO] Dang tai %TENGOI% %PHIENBAN%...
    libs\tmp\nuget.exe install "%TENGOI%" -Version "%PHIENBAN%" -OutputDirectory "libs\tmp\pkg" -NonInteractive
    if errorlevel 1 (
        echo [!!] nuget install %TENGOI% THAT BAI ^(xem loi nuget phia tren^).
    )
)

set "DUONGDANTIMTHAY="
if exist "%THUMUCGOI%" (
    for /r "%THUMUCGOI%" %%F in (pdfium.dll) do (
        if exist "%%F" set "DUONGDANTIMTHAY=%%F"
    )
)
if not defined DUONGDANTIMTHAY (
    set "CACHEGOI=%USERPROFILE%\.nuget\packages\pdfiumviewer.native.x86_64.v8-xfa\%PHIENBAN%"
    if exist "!CACHEGOI!" (
        for /r "!CACHEGOI!" %%F in (pdfium.dll) do (
            if exist "%%F" set "DUONGDANTIMTHAY=%%F"
        )
    )
)

if not defined DUONGDANTIMTHAY (
    echo [!!] KHONG TIM THAY pdfium.dll. Goi native co the doi version tren NuGet.
    exit /b 1
)

echo [OK]  Tim thay pdfium.dll tai: !DUONGDANTIMTHAY!
copy "!DUONGDANTIMTHAY!" "libs\"     /y >nul 2>&1
copy "!DUONGDANTIMTHAY!" "libs\x64\" /y >nul 2>&1
copy "!DUONGDANTIMTHAY!" "."         /y >nul 2>&1
exit /b 0

:: ============================================================
:: SUBROUTINE: Bao cao ket qua cuoi cung
:: ============================================================
:BaoCaoKetQua
echo.
echo ============================================
echo  Kiem tra libs\
echo ============================================
if exist "libs\PdfiumViewer.dll" (echo  [OK] libs\PdfiumViewer.dll) else (echo  [!!] libs\PdfiumViewer.dll - THIEU)
if exist "libs\pdfium.dll"       (echo  [OK] libs\pdfium.dll      ) else (echo  [!!] libs\pdfium.dll       - THIEU)
if exist "libs\PdfSharp.dll"     (echo  [OK] libs\PdfSharp.dll    ) else (echo  [!!] libs\PdfSharp.dll     - THIEU)
echo.
echo ============================================
echo  Kiem tra thu muc hien tai (cung voi .exe)
echo ============================================
if exist "pdfium.dll"       (echo  [OK] pdfium.dll      ) else (echo  [!!] pdfium.dll       - THIEU)
if exist "PdfiumViewer.dll" (echo  [OK] PdfiumViewer.dll) else (echo  [!!] PdfiumViewer.dll - THIEU)
if exist "PdfSharp.dll"     (echo  [OK] PdfSharp.dll    ) else (echo  [!!] PdfSharp.dll     - THIEU)
echo ============================================
echo.
echo [INFO] Setup hoan tat. Nho add reference toi:
echo        libs\PdfiumViewer.dll
echo        libs\PdfSharp.dll
exit /b 0

:end
echo.
pause
endlocal
