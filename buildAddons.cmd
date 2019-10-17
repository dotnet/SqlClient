msbuild /p:configuration=Release /t:clean
msbuild /p:configuration=Release /t:BuildAll
msbuild /p:configuration=Release /t:BuildAKVNetFx
msbuild /p:configuration=Release /t:BuildAKVNetCoreAllOS
msbuild /p:configuration=Release /t:GenerateAKVProviderNugetPackage
