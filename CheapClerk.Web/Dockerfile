FROM mcr.microsoft.com/dotnet/aspnet:11.0-preview AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:11.0-preview AS build
WORKDIR /src
COPY . .
RUN dotnet publish CheapClerk.Web/CheapClerk.Web.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CheapClerk.Web.dll"]
