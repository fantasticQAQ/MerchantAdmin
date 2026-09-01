# Merchant 平台 — Kubernetes 部署配置

将原有的 `docker-compose.yml` 完整转换为 Kubernetes 资源配置文件。

## 📁 文件结构

```
k8s-merchant/
├── 00-namespace.yaml      # 命名空间 + 网络策略
├── 01-sqlserver.yaml      # SQL Server (StatefulSet + PVC + Service)
├── 02-redis.yaml          # Redis (StatefulSet + PVC + Service)
├── 03-rabbitmq.yaml       # RabbitMQ (StatefulSet + PVC + Service)
├── 04-seq.yaml            # Seq 日志系统 (StatefulSet + PVC + Service)
├── 05-secrets.yaml        # 敏感信息 (密码等)
├── 06-identity-api.yaml   # Identity API (Deployment + Service)
├── 07-merchant-api.yaml   # Merchant API (Deployment + Service)
├── 08-merchantadmin.yaml  # MerchantAdmin 前端 (Deployment + Service)
├── 09-nginx-ingress.yaml  # Ingress 路由配置
├── 10-deploy-all.sh       # 一键部署脚本
├── 11-cleanup-all.sh      # 一键清理脚本
└── README.md              # 本文件
```

## 🚀 快速开始

### 前置条件
- Docker Desktop 已启用 Kubernetes (kind)
- 已构建好以下镜像：
  - `identity-api:latest`
  - `merchant-api:latest`
  - `merchantadmin:latest`

### 一键部署
```bash
cd k8s-merchant
chmod +x *.sh
./10-deploy-all.sh
```

### 访问服务
在 `/etc/hosts` 中添加：
```
127.0.0.1 merchant.local
127.0.0.1 identity-api.local
127.0.0.1 merchant-api.local
127.0.0.1 seq.local
127.0.0.1 rabbitmq.local
```

然后浏览器访问：
- 前端：http://merchant.local
- Identity API：http://identity-api.local
- Merchant API：http://merchant-api.local
- Seq 日志：http://seq.local
- RabbitMQ 管理：http://rabbitmq.local:15672

### 或使用 port-forward（不需要 Ingress）
```bash
kubectl port-forward svc/merchantadmin 8080:80 -n merchant
# 然后访问 http://localhost:8080
```

## 🧹 清理环境
```bash
./11-cleanup-all.sh
```

## 📊 常用调试命令

```bash
# 查看所有 Pod 状态
kubectl get pods -n merchant

# 查看 Pod 详情（排错必用）
kubectl describe pod <pod-name> -n merchant

# 查看容器日志
kubectl logs <pod-name> -n merchant -f

# 进入容器调试
kubectl exec -it <pod-name> -n merchant -- /bin/sh

# 查看所有 Service
kubectl get svc -n merchant

# 查看 Ingress
kubectl get ingress -n merchant
```

## 🔄 Docker Compose → K8s 映射关系

| Docker Compose                            | Kubernetes                     |
| ----------------------------------------- | ------------------------------ |
| service (sqlserver/redis/rabbitmq/seq)    | StatefulSet + PVC + Service    |
| service (identity-api/merchant-api/nginx) | Deployment + Service           |
| container_name                            | ❌ 不需要（由 K8s 自动管理）    |
| depends_on                                | InitContainer（等待端口就绪）  |
| healthcheck                               | livenessProbe + readinessProbe |
| networks                                  | 同一 Namespace 天然互通        |
| volumes                                   | PersistentVolumeClaim          |
| environment                               | env + Secret + ConfigMap       |
| ports                                     | Service (ClusterIP / NodePort) |

## ⚠️ 注意事项

1. **镜像构建**：需要先在你的项目目录执行 `docker build` 构建好三个 API 镜像
2. **密码管理**：生产环境建议使用 K8s 外部密钥管理（如 Sealed Secrets / Vault）
3. **存储类**：本地 kind 集群默认使用 `standard` 存储类，生产环境需根据实际云厂商调整
4. **Ingress Controller**：需要确保集群已安装 Nginx Ingress Controller
5. **资源限制**：生产环境建议为每个容器添加 `resources.requests` 和 `resources.limits`