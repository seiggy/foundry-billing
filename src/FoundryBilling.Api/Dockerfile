FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

COPY foundry-billing.slnx ./
COPY src/FoundryBilling.Api/FoundryBilling.Api.csproj src/FoundryBilling.Api/
COPY src/FoundryBilling.ServiceDefaults/FoundryBilling.ServiceDefaults.csproj src/FoundryBilling.ServiceDefaults/

RUN dotnet restore src/FoundryBilling.Api/FoundryBilling.Api.csproj

FROM restore AS publish
COPY src/FoundryBilling.Api/ src/FoundryBilling.Api/
COPY src/FoundryBilling.ServiceDefaults/ src/FoundryBilling.ServiceDefaults/

RUN dotnet publish src/FoundryBilling.Api/FoundryBilling.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=publish /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "FoundryBilling.Api.dll"]
