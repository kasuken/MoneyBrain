# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY MoneyBrain.Web/MoneyBrain.Web.sln MoneyBrain.Web/
COPY MoneyBrain.Web/MoneyBrain.Web/MoneyBrain.Web.csproj MoneyBrain.Web/MoneyBrain.Web/

# Restore dependencies
WORKDIR /src/MoneyBrain.Web
RUN dotnet restore

# Copy the rest of the source code
WORKDIR /src
COPY MoneyBrain.Web/ MoneyBrain.Web/

# Build and publish
WORKDIR /src/MoneyBrain.Web/MoneyBrain.Web
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create a non-root user for security
RUN useradd -m -s /bin/bash appuser

# Copy published app
COPY --from=build /app/publish .

# Create data directory for SQLite fallback (if needed)
RUN mkdir -p /app/Data && chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose ports
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check against /health/live. The aspnet image has no curl or wget, so this speaks
# HTTP over bash's /dev/tcp. The start period covers the migrations run at startup.
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /health/live HTTP/1.1\\r\\nHost: localhost\\r\\nConnection: close\\r\\n\\r\\n' >&3 && head -n 1 <&3 | grep -q ' 200 '"]

ENTRYPOINT ["dotnet", "MoneyBrain.Web.dll"]
