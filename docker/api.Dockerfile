# Build context: repository root (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/ backend/
RUN dotnet publish backend/src/KukaFm.Middleware.Api/KukaFm.Middleware.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# Trust the self-signed KUKA FM certificate (system definition, section 4 TLS).
COPY docker/certs/kukafm.crt /usr/local/share/ca-certificates/kukafm.crt
RUN update-ca-certificates
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=5000
EXPOSE 5000
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "KukaFm.Middleware.Api.dll"]
