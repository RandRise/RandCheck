FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY apps/backend/Directory.Build.props apps/backend/
COPY apps/backend/src/UaeDocumentIntelligence.Domain/UaeDocumentIntelligence.Domain.csproj apps/backend/src/UaeDocumentIntelligence.Domain/
COPY apps/backend/src/UaeDocumentIntelligence.Application/UaeDocumentIntelligence.Application.csproj apps/backend/src/UaeDocumentIntelligence.Application/
COPY apps/backend/src/UaeDocumentIntelligence.Infrastructure/UaeDocumentIntelligence.Infrastructure.csproj apps/backend/src/UaeDocumentIntelligence.Infrastructure/
COPY apps/backend/src/UaeDocumentIntelligence.Api/UaeDocumentIntelligence.Api.csproj apps/backend/src/UaeDocumentIntelligence.Api/
RUN dotnet restore apps/backend/src/UaeDocumentIntelligence.Api/UaeDocumentIntelligence.Api.csproj
COPY apps/backend apps/backend
RUN dotnet publish apps/backend/src/UaeDocumentIntelligence.Api/UaeDocumentIntelligence.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
RUN mkdir -p /data/uploads
ENTRYPOINT ["dotnet", "UaeDocumentIntelligence.Api.dll"]
