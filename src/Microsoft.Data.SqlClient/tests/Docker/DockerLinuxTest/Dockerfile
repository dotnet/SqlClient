#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:8.0@sha256:a61e3913a7ca320d134d89062375940470a930b567530ce6f510e0ea2360e594 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0.404@sha256:f91f9181d68b5ed2c8dea199dbbd2f6d172e60bdc43f8a67878b1ad140cf8d6b AS build
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
