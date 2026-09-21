# ========== 第一阶段：编译 ==========
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 复制项目文件（Identity.Infrastructure 已合并到 Identity.API，无需单独复制）
COPY src/Identity.API/Identity.API.csproj ./Identity.API/
COPY src/MerchantAdmin.Shared.Authentication/MerchantAdmin.Shared.Authentication.csproj ./MerchantAdmin.Shared.Authentication/

# 恢复依赖
RUN dotnet restore ./Identity.API/Identity.API.csproj

# 复制所有源代码
COPY src/Identity.API/ ./Identity.API/
COPY src/MerchantAdmin.Shared.Authentication/ ./MerchantAdmin.Shared.Authentication/

# 发布主项目
WORKDIR /src/Identity.API
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
ENTRYPOINT ["dotnet", "Identity.API.dll"]