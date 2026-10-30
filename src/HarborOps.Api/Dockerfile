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
LABEL org.opencontainers.image.revision=$SOURCE_REVISION
WORKDIR /app
COPY --from=build /app/publish ./
RUN mkdir -p /keys && chown "$APP_UID:$APP_UID" /keys
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "HarborOps.Api.dll"]
