FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

# Install chromium, fonts, udev, and tzdata for PuppeteerSharp
RUN apk add --no-cache \
    chromium \
    font-freefont \
    udev \
    tzdata

# Set environment variables for PuppeteerSharp on Alpine
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium-browser
# Make sure we don't try to download chromium again
ENV PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY ["FlyNotify.Core/FlyNotify.Core.csproj", "FlyNotify.Core/"]
COPY ["FlyNotify.Web/FlyNotify.Web.csproj", "FlyNotify.Web/"]
RUN dotnet restore "FlyNotify.Web/FlyNotify.Web.csproj"
COPY . .
WORKDIR "/src/FlyNotify.Web"
RUN dotnet build "FlyNotify.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FlyNotify.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FlyNotify.Web.dll"]
