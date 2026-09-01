$ErrorActionPreference = "Stop"

# 仓库根目录（本脚本位于 build/ 下）
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot

$SHA = git rev-parse --short HEAD
Write-Host "Commit: $SHA"

# ============ 1. 生成迁移 SQL 脚本（幂等，可重复执行）============
Write-Host "`n[1/3] 生成迁移 SQL 脚本..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path "build\sql" | Out-Null

dotnet ef migrations script `
  --project src/MerchantAdmin.Infrastructure `
  --startup-project src/MerchantAdmin.API `
  --idempotent `
  --output build/sql/MerchantAdmin.sql

dotnet ef migrations script `
  --project src/Identity.API `
  --startup-project src/Identity.API `
  --idempotent `
  --output build/sql/Identity.sql

# ============ 2. 迁移数据库（docker-compose 的 migrate profile 用 sqlcmd 执行 SQL）============
Write-Host "`n[2/3] 迁移数据库（sqlcmd 执行 SQL）..." -ForegroundColor Cyan
# --project-directory . 让 compose 从仓库根目录读取 .env（否则默认从 build/ 读不到环境变量）
docker compose --project-directory . -f build/docker-compose.yml --profile migrate up --abort-on-container-exit merchant-migrator identity-migrator

# ============ 3. 构建生产镜像 ============
Write-Host "`n[3/3] 构建生产镜像..." -ForegroundColor Cyan
docker build -t merchant-api:latest -f src/MerchantAdmin.API/Dockerfile .
docker build -t identity-api:latest -f src/Identity.API/Dockerfile .
docker build -t payment-api:latest -f src/Payment.API/Dockerfile .
docker build -t merchant-admin-frontend:latest -f src/MerchantAdmin.Frontend/Dockerfile .

Write-Host "`n✅ 本地 CI/CD 完成：迁移 SQL 已生成、数据库已迁移、生产镜像已构建（tag = latest）" -ForegroundColor Green