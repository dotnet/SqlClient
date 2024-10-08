#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:8.0@sha256:113324d7d68c23885ee6ce61f192f5cd2bed7a42d80a2038f26538b5653a1250 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0.101@sha256:1b99a629b4f55a496db835d9c3347737aa4a1d995d43ffe5ab92d04bee69adeb AS build
WORKDIR /sqlclient
COPY . .

ARG PROJNAME="Microsoft.Data.SqlClient.DockerLinuxTest"
ARG PROJFILE=$PROJNAME".csproj"
ARG DLLFILE=$PROJNAME".dll"

WORKDIR /sqlclient/src/Microsoft.Data.SqlClient/tests/Docker/DockerLinuxTest
RUN dotnet build $PROJFILE -c Release -o /app/build 

FROM build AS publish
RUN dotnet publish $PROJFILE -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", $DLLFILE]
