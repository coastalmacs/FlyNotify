FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

# Enable main and community repositories, then install Chromium and dependencies
RUN echo "https://dl-cdn.alpinelinux.org/alpine/v$(cat /etc/alpine-release | cut -d'.' -f1,2)/main" > /etc/apk/repositories && \
    echo "https://dl-cdn.alpinelinux.org/alpine/v$(cat /etc/alpine-release | cut -d'.' -f1,2)/community" >> /etc/apk/repositories && \
    apk update && \
    (apk add --no-cache \
        chromium \
        nss \
        freetype \
        harfbuzz \
        ca-certificates \
        ttf-freefont \
        udev \
        tzdata \
        icu-libs \
        icu-data-full || true) && \
    chmod +x /usr/bin/chromium-browser && \
    /usr/bin/chromium-browser --version

# Enable culture and timezone database support in Alpine .NET
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
# Set environment variables for PuppeteerSharp on Alpine
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium-browser
# Make sure we don't try to download chromium again
ENV PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true

COPY publish/ .
ENTRYPOINT ["dotnet", "FlyNotify.Web.dll"]
