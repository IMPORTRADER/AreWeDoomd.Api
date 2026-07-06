# Build context must be the repository root:
#   docker build -f src/AreWeDoomd.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so 'dotnet restore' is layer-cached
COPY src/AreWeDoomd.Api/AreWeDoomd.Api.csproj src/AreWeDoomd.Api/
COPY src/AreWeDoomd.Application/AreWeDoomd.Application.csproj src/AreWeDoomd.Application/
COPY src/AreWeDoomd.Domain/AreWeDoomd.Domain.csproj src/AreWeDoomd.Domain/
COPY src/AreWeDoomd.Infrastructure/AreWeDoomd.Infrastructure.csproj src/AreWeDoomd.Infrastructure/
COPY src/AreWeDoomd.ActivityNotifications.Contracts/AreWeDoomd.ActivityNotifications.Contracts.csproj src/AreWeDoomd.ActivityNotifications.Contracts/
COPY src/AreWeDoomd.ChatProviders/AreWeDoomd.ChatProviders.csproj src/AreWeDoomd.ChatProviders/
RUN dotnet restore src/AreWeDoomd.Api/AreWeDoomd.Api.csproj

COPY src/ src/
RUN dotnet publish src/AreWeDoomd.Api/AreWeDoomd.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "AreWeDoomd.Api.dll"]
