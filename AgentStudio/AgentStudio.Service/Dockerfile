FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS base
WORKDIR /app
RUN mkdir -p /app/skills && chown -R $APP_UID:$APP_UID /app
USER $APP_UID

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AgentStudio.Service/AgentStudio.Service.csproj", "AgentStudio.Service/"]
COPY ["Protos/", "Protos/"]
RUN dotnet restore "AgentStudio.Service/AgentStudio.Service.csproj"
COPY . .
WORKDIR "/src/AgentStudio.Service"
RUN dotnet build "./AgentStudio.Service.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AgentStudio.Service.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish --chown=$APP_UID:$APP_UID /app/publish .
ENTRYPOINT ["dotnet", "AgentStudio.Service.dll"]
