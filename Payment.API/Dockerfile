# ========== 第一阶段：编译 ==========
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 复制依赖项目文件
COPY Payment.API/Payment.API.csproj ./Payment.API/
COPY MerchantAdmin.Shared.EventBus/MerchantAdmin.Shared.EventBus.csproj ./MerchantAdmin.Shared.EventBus/
COPY MerchantAdmin.Shared.EventBus.RabbitMQ/MerchantAdmin.Shared.EventBus.RabbitMQ.csproj ./MerchantAdmin.Shared.EventBus.RabbitMQ/
# 恢复依赖
RUN dotnet restore ./Payment.API/Payment.API.csproj

# 复制所有源代码
COPY Payment.API/ ./Payment.API/
COPY MerchantAdmin.Shared.EventBus/ ./MerchantAdmin.Shared.EventBus/
COPY MerchantAdmin.Shared.EventBus.RabbitMQ/ ./MerchantAdmin.Shared.EventBus.RabbitMQ/
# 发布主项目
WORKDIR /src/Payment.API
RUN dotnet publish -c Release -o /app/publish

# ========== 第二阶段：运行 ==========
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Payment.API.dll"]
