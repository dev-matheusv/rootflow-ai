FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /src

COPY RootFlow.sln ./
COPY global.json ./
COPY src/RootFlow.Api/RootFlow.Api.csproj src/RootFlow.Api/
COPY src/RootFlow.Application/RootFlow.Application.csproj src/RootFlow.Application/
COPY src/RootFlow.Domain/RootFlow.Domain.csproj src/RootFlow.Domain/
COPY src/RootFlow.Infrastructure/RootFlow.Infrastructure.csproj src/RootFlow.Infrastructure/
COPY tests/RootFlow.UnitTests/RootFlow.UnitTests.csproj tests/RootFlow.UnitTests/
COPY tests/RootFlow.Api.IntegrationTests/RootFlow.Api.IntegrationTests.csproj tests/RootFlow.Api.IntegrationTests/

RUN dotnet restore RootFlow.sln

FROM restore AS publish
COPY src ./src
RUN dotnet publish src/RootFlow.Api/RootFlow.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Storage__RootPath=/app/storage

EXPOSE 8080

COPY --from=publish /app/publish ./

RUN mkdir -p /app/storage \
    && chown -R $APP_UID:$APP_UID /app

USER $APP_UID

ENTRYPOINT ["dotnet", "RootFlow.Api.dll"]
