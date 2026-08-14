@echo off
rem DeepSeek Harness Launcher 一键编译
rem 自动检测图标资源: 缺失也能构建(使用无图标模式)
cd /d "%~dp0"
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

set RES=
if exist deepseek_logo.png set RES=%RES% /resource:deepseek_logo.png,DeepSeekHarness.logo.png
if exist deepseek_whale_white.png set RES=%RES% /resource:deepseek_whale_white.png,DeepSeekHarness.whale-white.png
if exist deepseek_whale_blue.png set RES=%RES% /resource:deepseek_whale_blue.png,DeepSeekHarness.whale-blue.png
set ICO=
if exist deepseek.ico set ICO=/win32icon:deepseek.ico
set MANIFEST=
if exist app.manifest set MANIFEST=/win32manifest:app.manifest

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ %RES% %ICO% %MANIFEST% /out:DeepSeekHarness.exe Launcher.cs
if %errorlevel%==0 (
    echo [OK] DeepSeekHarness.exe built
) else (
    echo [FAIL] compile error
)
