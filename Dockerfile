FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=true

COPY global.json dotnet-tools.json TaskApi.slnx ./
COPY TaskApi/TaskApi.csproj TaskApi/
COPY TaskApi.Tests/TaskApi.Tests.csproj TaskApi.Tests/
RUN dotnet restore && dotnet tool restore

COPY .editorconfig ./
COPY TaskApi/ TaskApi/
COPY TaskApi.Tests/ TaskApi.Tests/
RUN dotnet build --configuration Release --no-restore
ENTRYPOINT ["dotnet"]

FROM build AS test
ENTRYPOINT ["dotnet", "test", "--configuration", "Release", "--no-build"]

FROM build AS publish
RUN dotnet publish TaskApi/TaskApi.csproj --configuration Release --no-build --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14 AS runtime
WORKDIR /app
COPY --from=publish /app/publish ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "TaskApi.dll"]
