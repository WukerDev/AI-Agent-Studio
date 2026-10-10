FROM mcr.microsoft.com/dotnet/runtime:11.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AgentStudio.Client/AgentStudio.Client.csproj", "AgentStudio.Client/"]
RUN dotnet restore "AgentStudio.Client/AgentStudio.Client.csproj"
COPY . .
WORKDIR "/src/AgentStudio.Client"
RUN dotnet build "./AgentStudio.Client.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AgentStudio.Client.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AgentStudio.Client.dll"]
