msbuild /p:configuration=Release /t:clean
msbuild /p:configuration=Release
msbuild /p:configuration=Release /t:BuildAKVNetFx
msbuild /p:configuration=Release /t:BuildAKVNetCore
msbuild /p:configuration=Release /t:GenerateAKVProviderNugetPackage
