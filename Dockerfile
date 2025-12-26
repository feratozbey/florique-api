# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY *.csproj .
RUN dotnet restore

# Copy everything else and build (just the project, not solution)
COPY . .
RUN dotnet publish Florique.Api.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copy published files
COPY --from=build /app/publish .

# Expose port (Railway/Render will set PORT dynamically)
EXPOSE 8080

# Start the application
# Railway sets PORT env variable, ASP.NET Core will automatically use it
ENTRYPOINT ["dotnet", "Florique.Api.dll"]
