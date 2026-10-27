FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src ./src
RUN dotnet publish src/Cineclay.Api/Cineclay.Api.csproj -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 5081
ENTRYPOINT ["dotnet", "Cineclay.Api.dll"]
