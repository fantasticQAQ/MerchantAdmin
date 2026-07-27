# ========================================
# deploy.ps1 — 一键部署到 K8s
# 用法：在 PowerShell 里 cd 到 k8s-merchant 目录后执行
#   .\deploy.ps1
# ========================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  MerchantAdmin K8s 一键部署脚本" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: 构建前端镜像
Write-Host "[1/5] 构建前端 Docker 镜像..." -ForegroundColor Yellow
docker build -f Dockerfile.frontend -t merchant-admin-frontend:latest ..
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ 前端镜像构建失败！" -ForegroundColor Red
    exit 1
}
Write-Host "✅ 前端镜像构建成功" -ForegroundColor Green

# Step 2: 构建后端镜像
Write-Host ""
Write-Host "[2/5] 构建后端 Docker 镜像..." -ForegroundColor Yellow

# Merchant API
docker build -f ../MerchantAdmin.API/Dockerfile -t merchant-api:latest ..
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ merchant-api 镜像构建失败！" -ForegroundColor Red
    exit 1
}

# Identity API
docker build -f ../Identity.API/Dockerfile -t identity-api:latest ..
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ identity-api 镜像构建失败！" -ForegroundColor Red
    exit 1
}

Write-Host "✅ 后端镜像构建成功" -ForegroundColor Green

# Step 3: 应用 K8s YAML（按顺序）
Write-Host ""
Write-Host "[3/5] 创建 Namespace 和基础设施..." -ForegroundColor Yellow
kubectl apply -f 00-namespace.yaml
kubectl apply -f 01-sqlserver.yaml
kubectl apply -f 02-redis.yaml
kubectl apply -f 03-rabbitmq.yaml
kubectl apply -f 04-seq.yaml

Write-Host ""
Write-Host "[4/5] 创建 Secrets、ConfigMap 和后端服务..." -ForegroundColor Yellow
kubectl apply -f 05-secrets.yaml
kubectl apply -f 06-identity-api.yaml
kubectl apply -f 07-merchant-api.yaml

Write-Host ""
Write-Host "[5/5] 创建前端和 Ingress..." -ForegroundColor Yellow
kubectl apply -f 08-merchantadmin.yaml
kubectl apply -f 09-nginx-ingress.yaml
kubectl apply -f 10-deploy-all.yaml

# Step 4: 等待所有 Pod 就绪
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  等待所有 Pod 就绪..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$timeout = 300  # 5分钟超时
$elapsed = 0
while ($elapsed -lt $timeout) {
    $pods = kubectl get pods -n merchant -o json | ConvertFrom-Json
    $notReady = $pods.items | Where-Object {
        $_.status.phase -ne "Running" -or
        ($_.status.containerStatuses | Where-Object { $_.ready -eq $false })
    }

    if ($notReady.Count -eq 0) {
        Write-Host "✅ 所有 Pod 已就绪！" -ForegroundColor Green
        break
    }

    $notReadyNames = ($notReady | ForEach-Object { $_.metadata.name }) -join ", "
    Write-Host "⏳ 等待中... 未就绪: $notReadyNames" -ForegroundColor Yellow

    Start-Sleep -Seconds 10
    $elapsed += 10
}

if ($elapsed -ge $timeout) {
    Write-Host "⚠️ 超时！部分 Pod 可能未就绪，请检查：" -ForegroundColor Red
    kubectl get pods -n merchant
}

# Step 5: 显示状态
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  部署完成！当前状态：" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
kubectl get all -n merchant
Write-Host ""
kubectl get ingress -n merchant
Write-Host ""

# Step 6: 提示访问方式
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  访问方式：" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "方式一：NodePort（推荐本地测试）" -ForegroundColor White
Write-Host "  前端：http://localhost:30080" -ForegroundColor Green
Write-Host ""
Write-Host "方式二：Ingress（需配置 hosts）" -ForegroundColor White
Write-Host "  在 C:\Windows\System32\drivers\etc\hosts 添加：" -ForegroundColor Gray
Write-Host "    127.0.0.1  merchant.local" -ForegroundColor Gray
Write-Host "    127.0.0.1  seq.local" -ForegroundColor Gray
Write-Host "    127.0.0.1  rabbitmq.local" -ForegroundColor Gray
Write-Host "  然后访问：http://merchant.local" -ForegroundColor Green
Write-Host ""
Write-Host "方式三：kubectl port-forward" -ForegroundColor White
Write-Host "  kubectl port-forward svc/merchantadmin 8080:80 -n merchant" -ForegroundColor Gray
Write-Host "  然后访问：http://localhost:8080" -ForegroundColor Green
Write-Host ""
Write-Host "排查命令：" -ForegroundColor Yellow
Write-Host "  kubectl get pods -n merchant" -ForegroundColor Gray
Write-Host "  kubectl logs -n merchant <pod-name>" -ForegroundColor Gray
Write-Host "  kubectl get events -n merchant --sort-by='.lastTimestamp'" -ForegroundColor Gray
Write-Host ""
Write-Host "✅ 部署脚本执行完毕！" -ForegroundColor Green