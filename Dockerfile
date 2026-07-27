FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY BusinessApplicationIntegration.sln ./
COPY src/Integration.Api/Integration.Api.csproj src/Integration.Api/
COPY tests/Integration.Api.Tests/Integration.Api.Tests.csproj tests/Integration.Api.Tests/
RUN dotnet restore BusinessApplicationIntegration.sln

COPY . .
RUN dotnet publish src/Integration.Api/Integration.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && addgroup --system integration \
    && adduser --system --ingroup integration integration \
    && mkdir /data \
    && chown integration:integration /data
COPY --from=build --chown=integration:integration /app/publish .
USER integration
ENV ASPNETCORE_URLS=http://+:8080
ENV ConnectionStrings__IntegrationDatabase="Data Source=/data/integration.db"
EXPOSE 8080
ENTRYPOINT ["dotnet", "Integration.Api.dll"]
