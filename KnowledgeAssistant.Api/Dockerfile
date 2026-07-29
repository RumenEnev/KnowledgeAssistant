# syntax=docker/dockerfile:1
# Build context MUST be the solution root (src/KnowledgeAssistant) because the
# Api project references sibling projects (Application, Contracts, Infrastructure, Domain).
#   docker build -f KnowledgeAssistant.Api/Dockerfile -t knowledgeassistant-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY KnowledgeAssistant.Api/KnowledgeAssistant.Api.csproj KnowledgeAssistant.Api/
COPY KnowledgeAssistant.Application/KnowledgeAssistant.Application.csproj KnowledgeAssistant.Application/
COPY KnowledgeAssistant.Contracts/KnowledgeAssistant.Contracts.csproj KnowledgeAssistant.Contracts/
COPY KnowledgeAssistant.Infrastructure/KnowledgeAssistant.Infrastructure.csproj KnowledgeAssistant.Infrastructure/
COPY KnowledgeAssistant.Domain/KnowledgeAssistant.Domain.csproj KnowledgeAssistant.Domain/

RUN dotnet restore KnowledgeAssistant.Api/KnowledgeAssistant.Api.csproj

COPY KnowledgeAssistant.Api/ KnowledgeAssistant.Api/
COPY KnowledgeAssistant.Application/ KnowledgeAssistant.Application/
COPY KnowledgeAssistant.Contracts/ KnowledgeAssistant.Contracts/
COPY KnowledgeAssistant.Infrastructure/ KnowledgeAssistant.Infrastructure/
COPY KnowledgeAssistant.Domain/ KnowledgeAssistant.Domain/

WORKDIR /src/KnowledgeAssistant.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Bind to all interfaces so the API is reachable from other machines on the network.
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "KnowledgeAssistant.Api.dll"]
