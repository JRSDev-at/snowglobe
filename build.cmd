@echo off
rem Baut Snowglobe.exe mit dem in Windows enthaltenen C#-Compiler (.NET Framework 4.x).
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
cd /d "%~dp0"
if not exist bin mkdir bin
"%CSC%" /nologo /target:winexe /unsafe /optimize+ /platform:anycpu ^
  /win32manifest:src\app.manifest ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  /out:bin\Snowglobe.exe src\Snowglobe.cs
if errorlevel 1 exit /b 1
echo Fertig: bin\Snowglobe.exe
