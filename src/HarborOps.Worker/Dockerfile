FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY Directory.Build.props global.json ./
COPY src/HarborOps.Domain/*.csproj src/HarborOps.Domain/
COPY src/HarborOps.Application/*.csproj src/HarborOps.Application/
COPY src/HarborOps.Infrastructure/*.csproj src/HarborOps.Infrastructure/
COPY src/HarborOps.Worker/*.csproj src/HarborOps.Worker/
RUN dotnet restore src/HarborOps.Worker/HarborOps.Worker.csproj
COPY src/ src/
RUN dotnet publish src/HarborOps.Worker/HarborOps.Worker.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0
ARG SOURCE_REVISION=unverified
LABEL org.opencontainers.image.revision=$SOURCE_REVISION
WORKDIR /app
COPY --from=build /app/publish ./
USER $APP_UID
ENTRYPOINT ["dotnet", "HarborOps.Worker.dll"]
