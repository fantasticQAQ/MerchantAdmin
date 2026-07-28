# ========== 第一阶段：编译 ==========
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 复制项目文件（注意文件名带点号）
COPY Identity.API/Identity.API.csproj ./Identity.API/
# 恢复依赖
RUN dotnet restore ./Identity.API/Identity.API.csproj

# 复制所有源代码
COPY Identity.API/ ./Identity.API/
# 发布主项目
WORKDIR /src/Identity.API
RUN dotnet publish -c Release -o /app/publish

# ========== 第二阶段：运行 ==========
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Identity.API.dll"]