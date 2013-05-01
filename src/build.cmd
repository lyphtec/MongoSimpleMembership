@echo Off

%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild build.proj /p:Configuration=Release /m /v:M /fl /flp:LogFile=msbuild.log;Verbosity=Normal /nr:false

rem del _artifacts\"%config%"\*.dll

if "%1" == "-PushLocal" (
    xcopy _artifacts\*.nupkg D:\Development\LocalNuGet\LocalNuGet\Packages\ /F /Y
)