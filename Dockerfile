# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["Ask2Ask.csproj", "./"]
RUN dotnet restore "Ask2Ask.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src"
RUN dotnet build "Ask2Ask.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Ask2Ask.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install curl for healthchecks
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copy published app
COPY --from=publish /app/publish .

# Create TrackingData directory with proper permissions
RUN mkdir -p /app/TrackingData && \
    chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose port 8080 (internal, behind Caddy)
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Ask2Ask.dll"]

