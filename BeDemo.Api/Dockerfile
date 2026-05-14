FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src/BeDemo.Api
COPY ["BeDemo.Api/BeDemo.Api.csproj", "./"]
RUN dotnet restore "BeDemo.Api.csproj"
COPY ["BeDemo.Api/", "./"]
COPY ["many_faces_proto/", "../many_faces_proto/"]
RUN dotnet build "BeDemo.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BeDemo.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BeDemo.Api.dll"]
