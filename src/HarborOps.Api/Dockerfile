FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY Directory.Build.props ./
COPY src/HarborOps.Domain/*.csproj src/HarborOps.Domain/
COPY src/HarborOps.Application/*.csproj src/HarborOps.Application/
COPY src/HarborOps.Infrastructure/*.csproj src/HarborOps.Infrastructure/
COPY src/HarborOps.Api/*.csproj src/HarborOps.Api/
RUN dotnet restore src/HarborOps.Api/HarborOps.Api.csproj
COPY src/ src/
RUN dotnet publish src/HarborOps.Api/HarborOps.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "HarborOps.Api.dll"]
