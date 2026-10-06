# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-azurelinux3.0 AS build

# Arguments
ARG BUILD_CONFIGURATION=Release

# Workdir
WORKDIR /src

# Copy statements
COPY ["src/Mercurius.LAN.Web", "Mercurius.LAN.Web/"]

# Run statements
RUN dotnet restore \
    "./Mercurius.LAN.Web/Mercurius.LAN.Web.csproj"

RUN dotnet build \
    "./Mercurius.LAN.Web/Mercurius.LAN.Web.csproj" \
    --no-restore  \
    --configuration $BUILD_CONFIGURATION \
    --output /app/build

RUN dotnet publish \
    "./Mercurius.LAN.Web/Mercurius.LAN.Web.csproj" \
    --configuration $BUILD_CONFIGURATION \
    --output /app/publish

# Run Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-azurelinux3.0 AS run
RUN tdnf upgrade -y pcre2 \
    && tdnf clean all
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Mercurius.LAN.Web.dll"]
