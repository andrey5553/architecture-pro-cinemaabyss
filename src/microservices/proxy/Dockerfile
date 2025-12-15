FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем весь проект
COPY . .

# Восстанавливаем и собираем
RUN dotnet restore "src/proxy-service/YarpProxyService.csproj"
RUN dotnet publish "src/proxy-service/YarpProxyService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Устанавливаем переменные окружения для порта
ENV ASPNETCORE_URLS=http://+:8000
ENV PORT=8000

EXPOSE 8000

ENTRYPOINT ["dotnet", "YarpProxyService.dll"]
