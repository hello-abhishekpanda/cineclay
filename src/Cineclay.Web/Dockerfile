FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src ./src
RUN dotnet publish src/Cineclay.Web/Cineclay.Web.csproj -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# The volume holds cookie-protection keys across restarts.
RUN mkdir /keys && chown $APP_UID:$APP_UID /keys
USER $APP_UID
EXPOSE 5080
ENTRYPOINT ["dotnet", "Cineclay.Web.dll"]
