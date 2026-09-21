# ========== 第一阶段：编译 ==========
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 复制所有项目的 .csproj 文件（注意点号）
COPY src/MerchantAdmin.API/MerchantAdmin.API.csproj ./MerchantAdmin.API/
COPY src/MerchantAdmin.Domain/MerchantAdmin.Domain.csproj ./MerchantAdmin.Domain/
COPY src/MerchantAdmin.Infrastructure/MerchantAdmin.Infrastructure.csproj ./MerchantAdmin.Infrastructure/
COPY src/MerchantAdmin.Shared.Authentication/MerchantAdmin.Shared.Authentication.csproj ./MerchantAdmin.Shared.Authentication/
COPY src/MerchantAdmin.Shared.EventBus/MerchantAdmin.Shared.EventBus.csproj ./MerchantAdmin.Shared.EventBus/
COPY src/MerchantAdmin.Shared.EventBus.RabbitMQ/MerchantAdmin.Shared.EventBus.RabbitMQ.csproj ./MerchantAdmin.Shared.EventBus.RabbitMQ/
COPY src/MerchantAdmin.Shared.IntegrationEventLog/MerchantAdmin.Shared.IntegrationEventLog.csproj ./MerchantAdmin.Shared.IntegrationEventLog/
# 恢复依赖
RUN dotnet restore ./MerchantAdmin.API/MerchantAdmin.API.csproj

# 复制所有源代码
COPY src/MerchantAdmin.API/ ./MerchantAdmin.API/
COPY src/MerchantAdmin.Domain/ ./MerchantAdmin.Domain/
COPY src/MerchantAdmin.Infrastructure/ ./MerchantAdmin.Infrastructure/
COPY src/MerchantAdmin.Shared.Authentication/ ./MerchantAdmin.Shared.Authentication/
COPY src/MerchantAdmin.Shared.EventBus/ ./MerchantAdmin.Shared.EventBus/
COPY src/MerchantAdmin.Shared.EventBus.RabbitMQ/ ./MerchantAdmin.Shared.EventBus.RabbitMQ/
COPY src/MerchantAdmin.Shared.IntegrationEventLog/ ./MerchantAdmin.Shared.IntegrationEventLog/
# 发布主项目
WORKDIR /src/MerchantAdmin.API
RUN dotnet publish -c Release -o /app/publish

# ========== 第二阶段：运行 ==========
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# aspnet 运行镜像里没有 curl，而 docker-compose 的 healthcheck 要用它探活，
# 缺了会让容器永远停在 unhealthy（进而让依赖它的服务起不来）。
# 放在 COPY 之前：业务代码改动不会使这一层缓存失效。
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MerchantAdmin.API.dll"]