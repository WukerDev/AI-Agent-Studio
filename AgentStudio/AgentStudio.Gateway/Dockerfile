FROM mcr.microsoft.com/dotnet/runtime:11.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AgentStudio.Gateway/AgentStudio.Gateway.csproj", "AgentStudio.Gateway/"]
RUN dotnet restore "AgentStudio.Gateway/AgentStudio.Gateway.csproj"
COPY . .
WORKDIR "/src/AgentStudio.Gateway"
RUN dotnet build "./AgentStudio.Gateway.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AgentStudio.Gateway.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AgentStudio.Gateway.dll"]
