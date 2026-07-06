# Build context must be the repository root:
#   docker build -f src/AreWeDoomd.AgentService/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so 'dotnet restore' is layer-cached
COPY src/AreWeDoomd.AgentService/AreWeDoomd.AgentService.csproj src/AreWeDoomd.AgentService/
COPY src/AreWeDoomd.ActivityNotifications.Contracts/AreWeDoomd.ActivityNotifications.Contracts.csproj src/AreWeDoomd.ActivityNotifications.Contracts/
COPY src/AreWeDoomd.ChatProviders/AreWeDoomd.ChatProviders.csproj src/AreWeDoomd.ChatProviders/
RUN dotnet restore src/AreWeDoomd.AgentService/AreWeDoomd.AgentService.csproj

COPY src/ src/
# Prompts/**/*.md and appsettings.json reach the publish output via the
# csproj's CopyToOutputDirectory items — keep them out of .dockerignore.
RUN dotnet publish src/AreWeDoomd.AgentService/AreWeDoomd.AgentService.csproj -c Release -o /app/publish --no-restore

# Plain worker host, not a web app -> runtime image, no EXPOSE
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AreWeDoomd.AgentService.dll"]
