FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY FlowDesk.csproj ./
RUN dotnet restore FlowDesk.csproj

COPY . ./
RUN dotnet publish FlowDesk.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

RUN mkdir -p /app/App_Data && chown app:app /app/App_Data
COPY --from=build --chown=app:app /app/publish ./

USER app
ENTRYPOINT ["dotnet", "FlowDesk.dll"]
