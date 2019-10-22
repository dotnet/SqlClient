call :pauseOnError msbuild /p:configuration=Release /t:clean
call :pauseOnError msbuild /p:configuration=Release /t:BuildAllConfigurations
call :pauseOnError msbuild /p:configuration=Release /t:BuildAKVNetFx
call :pauseOnError msbuild /p:configuration=Release /t:BuildAKVNetCoreAllOS
call :pauseOnError msbuild /p:configuration=Release /t:GenerateAKVProviderNugetPackage

goto :eof

:pauseOnError
%*
if ERRORLEVEL 1 pause
goto :eof
