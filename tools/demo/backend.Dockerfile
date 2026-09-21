FROM mcr.microsoft.com/dotnet/sdk:8.0.425@sha256:5ef85cc12cb25be6ec319a7392d1e9efd53c3bc8abb971c53d8058a473f09053 AS build
WORKDIR /src
COPY global.json ./
COPY apps/backend/Directory.Build.props apps/backend/
COPY apps/backend/src apps/backend/src
COPY tools/demo/migrate tools/demo/migrate
RUN dotnet publish apps/backend/src/UaeDocumentIntelligence.Api -c Release -o /api
RUN dotnet publish tools/demo/migrate -c Release -o /migrate

FROM mcr.microsoft.com/dotnet/aspnet:8.0.31@sha256:9a464e9a7e8c6144631020975f703c89034fe386417cb740620df69c2c6cfe24 AS migrate
WORKDIR /app
COPY --from=build /migrate .
ENTRYPOINT ["dotnet", "DemoMigrate.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:8.0.31@sha256:9a464e9a7e8c6144631020975f703c89034fe386417cb740620df69c2c6cfe24 AS api
WORKDIR /app
COPY --from=build /api .
ENTRYPOINT ["dotnet", "UaeDocumentIntelligence.Api.dll"]
