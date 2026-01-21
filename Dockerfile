# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy solution and project files first for better layer caching
COPY services/backend/Backend.sln ./services/backend/
COPY services/backend/src/Core/Domain/Domain.csproj ./services/backend/src/Core/Domain/
COPY services/backend/src/Core/Application/Application.csproj ./services/backend/src/Core/Application/
COPY services/backend/src/Adapters/Infrastructure/Infrastructure.csproj ./services/backend/src/Adapters/Infrastructure/
COPY services/backend/src/Adapters/WebApi/WebApi.csproj ./services/backend/src/Adapters/WebApi/

# Restore dependencies
WORKDIR /src/services/backend
RUN dotnet restore src/Adapters/WebApi/WebApi.csproj

# Copy everything else
WORKDIR /src
COPY services/backend/ ./services/backend/

# Build and publish
WORKDIR /src/services/backend
RUN dotnet publish src/Adapters/WebApi/WebApi.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Create non-root user for security
RUN addgroup -g 1000 appgroup && \
    adduser -u 1000 -G appgroup -s /bin/sh -D appuser

# Install required dependencies for some native libraries
RUN apk add --no-cache \
    icu-libs \
    tzdata \
    krb5-libs

# Copy published app
COPY --from=build /app/publish .

# Set environment variables
# Port 8080: Main API with authentication
# Port 9090: Management endpoints (metrics, health) without auth - for internal use
ENV ASPNETCORE_HTTP_PORTS=8080
ENV Ports__Main=8080
ENV Ports__Management=9090
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV TZ=UTC

# Expose ports
EXPOSE 8080 9090

# Switch to non-root user
USER appuser

# Health check (uses management port 9090 where /health is exposed)
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:9090/health || exit 1

# Start the application
ENTRYPOINT ["dotnet", "WebApi.dll"]
