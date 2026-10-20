FROM node:22-alpine AS node-build
WORKDIR /src/client
COPY AgentStudio.Client/package*.json ./
RUN npm ci || npm install
COPY AgentStudio.Client/ ./
COPY Protos/ ../Protos/
RUN npm run build

FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AgentStudio.Client/AgentStudio.Client.csproj", "AgentStudio.Client/"]
RUN dotnet restore "AgentStudio.Client/AgentStudio.Client.csproj"
COPY . .
COPY --from=node-build /src/client/dist ./AgentStudio.Client/dist
WORKDIR "/src/AgentStudio.Client"
RUN dotnet build "./AgentStudio.Client.csproj" -c $BUILD_CONFIGURATION -o /app/build /p:NpmBuild=false

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AgentStudio.Client.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:NpmBuild=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=node-build /src/client/dist ./dist
ENTRYPOINT ["dotnet", "AgentStudio.Client.dll"]
