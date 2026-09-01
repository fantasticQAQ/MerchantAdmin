# ========== 第一阶段：构建 ==========
FROM node:24.16.0-alpine AS builder
WORKDIR /app/MerchantAdmin.Frontend

# 复制前端目录的 package 文件
COPY src/MerchantAdmin.Frontend/package.json src/MerchantAdmin.Frontend/package-lock.json ./

# 安装依赖
RUN npm ci --registry=https://registry.npmmirror.com

# 复制前端目录的所有源码
COPY src/MerchantAdmin.Frontend/ .

# 构建（只跑 vite build，跳过 type-check）
RUN npm run build-only

# ========== 第二阶段：运行 ==========
FROM nginx:alpine

# 删掉默认的 conf.d 配置（避免和主配置冲突）
RUN rm -f /etc/nginx/conf.d/default.conf

# 复制主配置文件（纯静态，只服务前端产物；API 转发由统一入口 nginx 负责）
COPY src/MerchantAdmin.Frontend/nginx.conf /etc/nginx/nginx.conf

# 复制构建产物
COPY --from=builder /app/MerchantAdmin.Frontend/dist /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]