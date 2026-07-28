$ErrorActionPreference = "Stop"

$SHA = git rev-parse --short HEAD
Write-Host "Commit: $SHA"

# ---------- 构建 ----------
Write-Host "Building Docker images..."
docker build -t merchant-api:$SHA -f MerchantAdmin.API/Dockerfile .
docker build -t identity-api:$SHA -f IdentityApi/Dockerfile .
docker build -t merchant-admin-frontend:$SHA -f k8s-merchant/Dockerfile.frontend

# ---------- 迁移 ----------
Write-Host "Running EF Core migrations..."

dotnet tool install --global dotnet-ef --ignore-failed-sources

dotnet ef database update `
  --project src/MerchantAdmin.Infrastructure `
  --startup-project src/MerchantAdmin.API

dotnet ef database update `
  --project src/IdentityApi/Infrastructure `
  --startup-project src/IdentityApi

# ---------- 部署 ----------
Write-Host "Deploying to local K8s..."

kubectl set image deployment/merchant-api merchant-api=merchant-api:$SHA -n merchant
kubectl set image deployment/identity-api identity-api=identity-api:$SHA -n merchant
kubectl set image deployment/merchantadmin merchantadmin=merchant-admin-frontend:$SHA -n merchant

kubectl rollout status deployment/merchant-api -n merchant
kubectl rollout status deployment/identity-api -n merchant
kubectl rollout status deployment/merchantadmin -n merchant

Write-Host "✅ 本地 CI/CD 完成"