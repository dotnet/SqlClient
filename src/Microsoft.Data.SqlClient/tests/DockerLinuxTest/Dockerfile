#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /sqlclient
COPY . .

ARG PROJNAME="Microsoft.Data.SqlClient.DockerLinuxTest"
ARG PROJFILE=$PROJNAME".csproj"
ARG DLLFILE=$PROJNAME".dll"

WORKDIR /sqlclient/src/Microsoft.Data.SqlClient/tests/DockerLinuxTest
RUN dotnet build $PROJFILE -c Release -o /app/build 

FROM build AS publish
RUN dotnet publish $PROJFILE -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", $DLLFILE]
