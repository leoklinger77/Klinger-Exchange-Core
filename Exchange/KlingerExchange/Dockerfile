# Base stage for runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

# Expose FIX protocol ports
EXPOSE 9823
EXPOSE 9824

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files
COPY ["Exchange/KlingerExchange/KlingerExchange.csproj", "Exchange/KlingerExchange/"]
COPY ["Shared/KlingerShared/KlingerShared.csproj", "Shared/KlingerShared/"]

# Restore dependencies
RUN dotnet restore "Exchange/KlingerExchange/KlingerExchange.csproj"

# Copy source code
COPY . .

# Build
WORKDIR "/src/Exchange/KlingerExchange"
RUN dotnet build "KlingerExchange.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "KlingerExchange.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app

# Copy published files
COPY --from=publish /app/publish .

# Copy configuration files
RUN mkdir -p /app/Config /app/Matching/manifest /app/store /app/data

# Environment variables for .NET optimization
ENV DOTNET_GCServer=1
ENV DOTNET_GCConcurrent=0
ENV DOTNET_TieredCompilation=0
ENV DOTNET_ReadyToRun=1

# Run as non-root user (for Windows containers, comment this out)
# USER $APP_UID

ENTRYPOINT ["dotnet", "KlingerExchange.dll"]