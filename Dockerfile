# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/Yallarhorn/Yallarhorn.csproj", "Yallarhorn/"]

# Restore dependencies
RUN dotnet restore "Yallarhorn/Yallarhorn.csproj"

# Copy source code
COPY src/Yallarhorn/. "Yallarhorn/"

# Build and publish
WORKDIR "/src/Yallarhorn"
RUN dotnet publish "Yallarhorn.csproj" -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install yt-dlp, ffmpeg, and curl for media processing and health checks
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    yt-dlp \
    ffmpeg \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=build /app/publish .

# Copy sample config as fallback
COPY yallarhorn.yaml.example /app/yallarhorn.yaml.example

# Create directories for persistence
RUN mkdir -p /app/data /app/downloads /app/temp

# Expose port 5001
EXPOSE 5001

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5001
ENV ASPNETCORE_ENVIRONMENT=Production

# Entry point - no --config needed, app auto-detects config files
ENTRYPOINT ["dotnet", "Yallarhorn.dll"]