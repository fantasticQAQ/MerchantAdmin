# ========== 第一阶段：编译 ==========
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 复制所有项目的 .csproj 文件（注意点号）
COPY MerchantAdmin.API/MerchantAdmin.API.csproj ./MerchantAdmin.API/
COPY MerchantAdmin.Application/MerchantAdmin.Application.csproj ./MerchantAdmin.Application/
COPY MerchantAdmin.Domain/MerchantAdmin.Domain.csproj ./MerchantAdmin.Domain/
COPY MerchantAdmin.Infrastructure/MerchantAdmin.Infrastructure.csproj ./MerchantAdmin.Infrastructure/
COPY MerchantAdmin.Shared.Authentication/MerchantAdmin.Shared.Authentication.csproj ./MerchantAdmin.Shared.Authentication/
COPY MerchantAdmin.Shared.EventBus/MerchantAdmin.Shared.EventBus.csproj ./MerchantAdmin.Shared.EventBus/
COPY MerchantAdmin.Shared.EventBus.RabbitMQ/MerchantAdmin.Shared.EventBus.RabbitMQ.csproj ./MerchantAdmin.Shared.EventBus.RabbitMQ/
COPY MerchantAdmin.Shared.IntegrationEventLog/MerchantAdmin.Shared.IntegrationEventLog.csproj ./MerchantAdmin.Shared.IntegrationEventLog/
# 恢复依赖
RUN dotnet restore ./MerchantAdmin.API/MerchantAdmin.API.csproj

# 复制所有源代码
COPY MerchantAdmin.API/ ./MerchantAdmin.API/
COPY MerchantAdmin.Application/ ./MerchantAdmin.Application/
COPY MerchantAdmin.Domain/ ./MerchantAdmin.Domain/
COPY MerchantAdmin.Infrastructure/ ./MerchantAdmin.Infrastructure/
COPY MerchantAdmin.Shared.Authentication/ ./MerchantAdmin.Shared.Authentication/
COPY MerchantAdmin.Shared.EventBus/ ./MerchantAdmin.Shared.EventBus/
COPY MerchantAdmin.Shared.EventBus.RabbitMQ/ ./MerchantAdmin.Shared.EventBus.RabbitMQ/
COPY MerchantAdmin.Shared.IntegrationEventLog/ ./MerchantAdmin.Shared.IntegrationEventLog/
# 发布主项目
WORKDIR /src/MerchantAdmin.API
RUN dotnet publish -c Release -o /app/publish

# ========== 第二阶段：运行 ==========
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MerchantAdmin.API.dll"]