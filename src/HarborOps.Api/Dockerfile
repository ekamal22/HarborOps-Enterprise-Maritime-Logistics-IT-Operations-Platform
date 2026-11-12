FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY Directory.Build.props global.json ./
COPY src/HarborOps.Domain/*.csproj src/HarborOps.Domain/
COPY src/HarborOps.Application/*.csproj src/HarborOps.Application/
COPY src/HarborOps.Infrastructure/*.csproj src/HarborOps.Infrastructure/
COPY src/HarborOps.Api/*.csproj src/HarborOps.Api/
RUN dotnet restore src/HarborOps.Api/HarborOps.Api.csproj
COPY src/ src/
RUN dotnet publish src/HarborOps.Api/HarborOps.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG SOURCE_REVISION=unverified
# The Ubuntu base layer ships apt 2.8.3 (CVE-2026-109518, HIGH, fixed in 2.8.4ubuntu0.1) and the upstream
# image has not been rebuilt. Upgrade only those two packages; the image still runs as the non-root app user.
RUN apt-get update \
    && apt-get install -y --no-install-recommends --only-upgrade apt libapt-pkg6.0t64 \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
LABEL org.opencontainers.image.revision=$SOURCE_REVISION
WORKDIR /app
COPY --from=build /app/publish ./
RUN mkdir -p /keys && chown "$APP_UID:$APP_UID" /keys
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "HarborOps.Api.dll"]
