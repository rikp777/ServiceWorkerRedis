FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["MockAppRedis/MockAppRedis.csproj", "MockAppRedis/"]
RUN dotnet restore "MockAppRedis/MockAppRedis.csproj"
COPY . .
WORKDIR "/src/MockAppRedis"
RUN dotnet build "MockAppRedis.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MockAppRedis.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MockAppRedis.dll"]
