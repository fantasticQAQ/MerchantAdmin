# ========== 第一阶段：编译 ==========
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 复制所有项目的 .csproj 文件（注意点号）
COPY MerchantAdmin.API/MerchantAdmin.API.csproj ./MerchantAdmin.API/
COPY MerchantAdmin.Application/MerchantAdmin.Application.csproj ./MerchantAdmin.Application/
COPY MerchantAdmin.Domain/MerchantAdmin.Domain.csproj ./MerchantAdmin.Domain/
COPY MerchantAdmin.Infrastructure/MerchantAdmin.Infrastructure.csproj ./MerchantAdmin.Infrastructure/
COPY EventBus/EventBus.csproj ./EventBus/
COPY EventBusRabbitMQ/EventBusRabbitMQ.csproj ./EventBusRabbitMQ/
COPY IntegrationEventLogEF/IntegrationEventLogEF.csproj ./IntegrationEventLogEF/
# 恢复依赖
RUN dotnet restore ./MerchantAdmin.API/MerchantAdmin.API.csproj

# 复制所有源代码
COPY MerchantAdmin.API/ ./MerchantAdmin.API/
COPY MerchantAdmin.Application/ ./MerchantAdmin.Application/
COPY MerchantAdmin.Domain/ ./MerchantAdmin.Domain/
COPY MerchantAdmin.Infrastructure/ ./MerchantAdmin.Infrastructure/
COPY EventBus/ ./EventBus/
COPY EventBusRabbitMQ/ ./EventBusRabbitMQ/
COPY IntegrationEventLogEF/ ./IntegrationEventLogEF/
# 发布主项目
WORKDIR /src/MerchantAdmin.API
RUN dotnet publish -c Release -o /app/publish

# ========== 第二阶段：运行 ==========
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MerchantAdmin.API.dll"]