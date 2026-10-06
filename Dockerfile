FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet restore src/Ecommerce.Web/Ecommerce.Web.csproj
RUN dotnet publish src/Ecommerce.Web/Ecommerce.Web.csproj -m:1 -p:BuildInParallel=false -c Release -o /app/publish --no-restore
RUN rm -f /app/publish/appsettings.Local.json
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN .playwright/node/linux-x64/node .playwright/package/cli.js install --with-deps chromium && chmod -R a+rX /ms-playwright
RUN mkdir -p /app/wwwroot/uploads/products /app/keys /app/App_Data/documents App_Data/receipts && chown -R 1654:1654 /app/wwwroot/uploads /app/keys /app/App_Data
USER 1654
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Ecommerce.Web.dll"]
