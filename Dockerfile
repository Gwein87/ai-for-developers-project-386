FROM node:22-alpine AS web-build
WORKDIR /source
COPY package.json package-lock.json ./
COPY src/HexletCalendar.Web/package.json src/HexletCalendar.Web/package.json
RUN npm ci
COPY src/HexletCalendar.Web src/HexletCalendar.Web
COPY src/HexletCalendar.Contracts/generated src/HexletCalendar.Contracts/generated
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /source
COPY src/HexletCalendar.Api/HexletCalendar.Api.csproj src/HexletCalendar.Api/
COPY src/HexletCalendar.Data/HexletCalendar.Data.csproj src/HexletCalendar.Data/
RUN dotnet restore src/HexletCalendar.Api/HexletCalendar.Api.csproj
COPY src/HexletCalendar.Api src/HexletCalendar.Api
COPY src/HexletCalendar.Data src/HexletCalendar.Data
COPY src/HexletCalendar.Contracts/generated src/HexletCalendar.Contracts/generated
RUN dotnet publish src/HexletCalendar.Api/HexletCalendar.Api.csproj -c Release --no-restore -o /app/publish
COPY .config/dotnet-tools.json .config/dotnet-tools.json
RUN dotnet tool restore \
    && mkdir -p /app/publish/data \
    && dotnet ef database update --project src/HexletCalendar.Data --startup-project src/HexletCalendar.Api --connection "Data Source=/app/publish/data/calendar.db"
COPY --from=web-build /source/src/HexletCalendar.Web/dist /app/publish/wwwroot

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api-build /app/publish ./
RUN chown -R $APP_UID:$APP_UID /app/data
ENV ConnectionStrings__Calendar="Data Source=/app/data/calendar.db;Mode=ReadWrite"
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["sh", "-c", "exec dotnet HexletCalendar.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
