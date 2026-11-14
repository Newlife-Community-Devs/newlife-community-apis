FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Newlife.Domain/Newlife.Domain.csproj src/Newlife.Domain/
COPY src/Newlife.Application/Newlife.Application.csproj src/Newlife.Application/
COPY src/Newlife.Infrastructure/Newlife.Infrastructure.csproj src/Newlife.Infrastructure/
COPY src/Newlife.Api/Newlife.Api.csproj src/Newlife.Api/
RUN dotnet restore src/Newlife.Api/Newlife.Api.csproj

COPY . .
RUN dotnet publish src/Newlife.Api/Newlife.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
USER $APP_UID
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet Newlife.Api.dll --urls http://+:${PORT:-8080}"]
