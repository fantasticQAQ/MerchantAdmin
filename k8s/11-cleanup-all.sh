#!/bin/bash
# ========================================
# 一键清理所有 Merchant 平台资源
# ========================================
set -e

echo "🧹 开始清理 Merchant 平台..."

echo "🗑️ 删除 Ingress..."
kubectl delete -f 09-nginx-ingress.yaml --ignore-not-found=true

echo "🗑️ 删除 MerchantAdmin..."
kubectl delete -f 08-merchantadmin.yaml --ignore-not-found=true

echo "🗑️ 删除 Merchant API..."
kubectl delete -f 07-merchant-api.yaml --ignore-not-found=true

echo "🗑️ 删除 Identity API..."
kubectl delete -f 06-identity-api.yaml --ignore-not-found=true
kubectl delete -f 05-secrets.yaml --ignore-not-found=true

echo "🗑️ 删除 Seq..."
kubectl delete -f 04-seq.yaml --ignore-not-found=true

echo "🗑️ 删除 RabbitMQ..."
kubectl delete -f 03-rabbitmq.yaml --ignore-not-found=true

echo "🗑️ 删除 Redis..."
kubectl delete -f 02-redis.yaml --ignore-not-found=true

echo "🗑️ 删除 SQL Server..."
kubectl delete -f 01-sqlserver.yaml --ignore-not-found=true

echo "🗑️ 删除 迁移..."
kubectl delete job merchant-db-migrate -n merchant
kubectl delete job identity-db-migrate -n merchant

echo "🗑️ 删除命名空间（会清理剩余资源）..."
kubectl delete -f 00-namespace.yaml --ignore-not-found=true

echo ""
echo "✅ 清理完成！"