FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY CampClotNot/CampClotNot.csproj CampClotNot/
RUN dotnet restore CampClotNot/CampClotNot.csproj
COPY CampClotNot/ CampClotNot/
RUN dotnet publish CampClotNot/CampClotNot.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CampClotNot.dll"]
