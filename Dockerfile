FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Memory.Domain/Memory.Domain.csproj src/Memory.Domain/
COPY src/Memory.Application/Memory.Application.csproj src/Memory.Application/
COPY src/Memory.Infrastructure/Memory.Infrastructure.csproj src/Memory.Infrastructure/
COPY src/Memory.Api/Memory.Api.csproj src/Memory.Api/
RUN dotnet restore src/Memory.Api/Memory.Api.csproj
COPY src/ src/
RUN dotnet publish src/Memory.Api/Memory.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /app/data && chown -R $APP_UID:$APP_UID /app/data
ENV ASPNETCORE_URLS=http://+:5022
ENV NEMORYN_DATA_DIR=/app/data
EXPOSE 5022
USER $APP_UID
ENTRYPOINT ["dotnet", "Memory.Api.dll"]
