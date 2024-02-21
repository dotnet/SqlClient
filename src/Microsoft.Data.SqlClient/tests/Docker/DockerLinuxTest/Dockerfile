#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:8.0@sha256:bb9e61f07f93945ab97391b1dcbcc41136b03310583f36e52b3ec2815111e58a AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:8e77ad6fb7c33c17f026424d3bef05ea2ee15d1621e28f312adeab4dc1005866 AS build
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
