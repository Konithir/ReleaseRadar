FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ReleaseRadar.sln ./
COPY src ./src
RUN dotnet restore src/ReleaseRadar.Api/ReleaseRadar.Api.csproj
RUN dotnet publish src/ReleaseRadar.Api/ReleaseRadar.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "ReleaseRadar.Api.dll"]
