#!/bin/bash
# ========================================
# 一键部署所有服务到 K8s
# ========================================
set -e

echo "🚀 开始部署 Merchant 平台到 Kubernetes..."

# 1. 创建命名空间
echo "📦 创建命名空间..."
kubectl apply -f 00-namespace.yaml

# 1.1 配置
kubectl apply -f 05-secrets.yaml

# 2. 部署基础服务（有状态服务优先）
echo "🗄️ 部署 SQL Server..."
kubectl apply -f 01-sqlserver.yaml

echo "📦 部署 Redis..."
kubectl apply -f 02-redis.yaml

echo "🐰 部署 RabbitMQ..."
kubectl apply -f 03-rabbitmq.yaml

echo "📊 部署 Seq 日志系统..."
kubectl apply -f 04-seq.yaml

# 等待有状态服务就绪
echo "⏳ 等待基础服务就绪..."
kubectl wait --for=condition=ready pod -l app=sqlserver -n merchant --timeout=300s
kubectl wait --for=condition=ready pod -l app=redis -n merchant --timeout=120s
kubectl wait --for=condition=ready pod -l app=rabbitmq -n merchant --timeout=120s
kubectl wait --for=condition=ready pod -l app=seq -n merchant --timeout=120s

echo "迁移"
kubectl apply -f identity-db-migrate-job.yaml
kubectl apply -f merchant-db-migrate-job.yaml
kubectl wait --for=condition=complete job/merchant-db-migrate -n merchant --timeout=60s
kubectl wait --for=condition=complete job/identity-db-migrate -n merchant --timeout=60s

# 3. 部署应用服务
echo "🔐 部署 Identity API..."
kubectl apply -f 06-identity-api.yaml
echo "🏪 部署 Merchant API..."
kubectl apply -f 07-merchant-api.yaml
echo "🖥️ 部署 MerchantAdmin 前端..."
kubectl apply -f 08-merchantadmin.yaml

# 等待应用服务就绪
echo "⏳ 等待应用服务就绪..."
kubectl wait --for=condition=ready pod -l app=identity-api -n merchant --timeout=120s
kubectl wait --for=condition=ready pod -l app=merchant-api -n merchant --timeout=120s
kubectl wait --for=condition=ready pod -l app=merchantadmin -n merchant --timeout=120s

# 4. 部署 Ingress
echo "🌐 部署 Ingress 路由..."
kubectl apply -f 09-nginx-ingress.yaml

echo ""
echo "✅ 部署完成！"
echo ""
echo "📋 当前 Pod 状态："
kubectl get pods -n merchant
echo ""
echo "📋 当前 Service 列表："
kubectl get svc -n merchant
echo ""
echo "📋 当前 Ingress 状态："
kubectl get ingress -n merchant
echo ""
echo "💡 提示："
echo "   - 请在 /etc/hosts 中添加以下映射："
echo "     127.0.0.1 merchant.local"
echo "     127.0.0.1 identity-api.local"
echo "     127.0.0.1 merchant-api.local"
echo "     127.0.0.1 seq.local"
echo "     127.0.0.1 rabbitmq.local"
echo ""
echo "   - 或使用 port-forward 临时访问："
echo "     kubectl port-forward svc/merchantadmin 8080:80 -n merchant"