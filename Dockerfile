# Etapa 1: Compilación (.NET 8 SDK)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar archivo de proyecto y restaurar dependencias
COPY ["RemesaSmartSV.csproj", "./"]
RUN dotnet restore "RemesaSmartSV.csproj"

# Copiar el resto del código y compilar en modo Release
COPY . .
RUN dotnet publish "RemesaSmartSV.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa 2: Imagen final para ejecutar (.NET 8 ASP.NET Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "RemesaSmartSV.dll"]