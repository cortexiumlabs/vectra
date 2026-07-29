ARG APP_VERSION="1.0.0"

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG APP_VERSION="1.0.0"
WORKDIR /src

COPY Synentra.slnx .

COPY src/Synentra/Synentra.csproj src/Synentra/
COPY src/Synentra.Application/Synentra.Application.csproj src/Synentra.Application/
COPY src/Synentra.Domain/Synentra.Domain.csproj src/Synentra.Domain/
COPY src/Synentra.BuildingBlocks/Synentra.BuildingBlocks.csproj src/Synentra.BuildingBlocks/
COPY src/Synentra.Infrastructure/Synentra.Infrastructure.csproj src/Synentra.Infrastructure/
COPY src/Synentra.Infrastructure.Persistence.Common/Synentra.Infrastructure.Persistence.Common.csproj src/Synentra.Infrastructure.Persistence.Common/
COPY src/Synentra.Infrastructure.Persistence.Sqlite/Synentra.Infrastructure.Persistence.Sqlite.csproj src/Synentra.Infrastructure.Persistence.Sqlite/

RUN dotnet restore src/Synentra/Synentra.csproj --runtime linux-x64

COPY src/ src/

RUN dotnet publish "./src/Synentra/Synentra.csproj" -c Release -o /app/publish \
    /p:UseAppHost=false \
    /p:Version=${APP_VERSION} \
    --runtime "linux-x64"

# Runtime stage (IMPORTANT: match glibc runtime)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Static OCI image metadata
LABEL org.opencontainers.image.title="Synentra"
LABEL org.opencontainers.image.description="Intent-aware governance gateway for autonomous AI agents"
LABEL org.opencontainers.image.source="https://github.com/synentra/synentra"
LABEL org.opencontainers.image.licenses="Apache-2.0"

ENV ASPNETCORE_URLS=http://+:7080
ENV DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app/publish .

EXPOSE 7080

ARG APP_VERSION
ARG REVISION
LABEL org.opencontainers.image.version="${APP_VERSION}"
LABEL org.opencontainers.image.revision="${REVISION}"

ENTRYPOINT ["dotnet", "synentra.dll"]