# MerchantAdmin 商户后台管理系统

一个基于 **.NET 8 微服务架构** 的电商商品/订单后台管理系统。项目以微软官方 **eShopOnContainers** 的架构思想为蓝本，实践 **DDD（领域驱动设计）**、**CQRS**、**事件驱动**、**Outbox 事务发件箱**、**支付网关**、**容器化与 Kubernetes 部署** 等企业级后端技术，是我用来展示 .NET 后端工程能力的个人项目。

> **默认账号**：`admin / 123456`（SuperAdmin 超管）。内置角色：SuperAdmin、Admin、Operator（三者不可删除）。

## 技术栈

| 分类 | 技术 |
|---|---|
| 运行时 | .NET 8 |
| 应用框架 | ASP.NET Core Web API |
| 对象映射/CQRS | MediatR（Command/Query/Notification + Behavior 管道） |
| ORM | Entity Framework Core（SQL Server） |
| 消息队列 | RabbitMQ（集成事件 + 事务性发件箱 Outbox） |
| 缓存/延迟任务 | Redis（缓存 + keyspace notification 延迟取消订单） |
| 认证授权 | ASP.NET Core Identity + JWT Bearer |
| 日志 | Serilog + Seq |
| 前端 | Vue 3 + Vite（后台管理页） |
| 容器/编排 | Docker Compose、Kubernetes（k8s 清单） |
| CI | GitHub Actions |

## 架构

采用经典的 DDD 分层 + 微服务拆分：

```
Identity.API                 # 身份认证服务（注册/登录/签发 JWT）
MerchantAdmin.API            # 订单服务（商品/订单）
├── MerchantAdmin.Domain           # 领域层：实体、聚合根、值对象、领域事件
├── MerchantAdmin.Application      # 应用层：CQRS 命令/查询、领域事件处理、集成事件
└── MerchantAdmin.Infrastructure   # 基础设施层：EF Core、Redis、仓储实现
Payment.API                  # 独立支付网关（模拟第三方支付渠道 + 异步回调）
EventBus / EventBusRabbitMQ  # 事件总线抽象 + RabbitMQ 实现
IntegrationEventLogEF        # 事务性集成事件日志（发件箱模式）
MerchantAdmin.Frontend       # Vue 3 前端
MerchantAdmin.UnitTests      # 单元测试项目（xUnit + Moq + FluentAssertions）
```

### 核心分层职责

- **Domain（领域层）**：无外部依赖，只包含业务规则。`Order` 聚合根内实现下单、支付、取消等状态流转；`Product.ReduceStock` 保证库存不超卖。
- **Application（应用层）**：通过 MediatR 组织用例。`TransactionBehavior`、`LoggingBehavior`、`ValidatorBehavior` 三个交叉关注点用行为管道横切，避免在 Handler 里重复写事务/日志代码。
- **Infrastructure（基础设施层）**：`AppDbContext`、仓储、Redis 缓存、RabbitMQ 事件总线等具体实现，通过接口注入，领域层不依赖基础设施。

## 核心业务功能

- **商品管理**：创建 / 删除 / 列表查询（列表走 Redis 缓存，写操作主动失效缓存）。
- **订单流程**：
  - 下单 → 扣减库存 → 写入 Redis 延迟键（15 分钟未支付自动关闭）；
  - 支付 → 订单进入「支付处理中」→ 发布「发起支付」事件（发件箱）→ **Payment.API 订阅 → 调用支付渠道 → 发布「支付成功」事件** → 订单服务订阅并回写「已支付」（幂等）；
  - 取消 → 状态校验 → 触发领域事件 → 通过领域事件处理器回补库存；
  - 退款 → 已支付订单退款为「已退款」并回补库存。
- **订单状态机**：`Created → PaymentProcessing → Paid/Refunded`，另有终态 `Cancelled`（用户取消）、`TimedOut`（超时关闭），全部迁移由 `DomainException` 状态机 + `RowVersion` 乐观并发锁双重保护。
- **库存语义**：库存只由交易状态变更驱动——下单扣减，取消失败/超时/退款回补；**删除订单是纯归档、不改变库存**。
- **超时关闭（双保险）**：① Redis `keyspace notification` 实时监听过期键自动关闭；② 每 5 分钟定时兜底扫描 `Created 且超时` 的订单补关，弥补事件丢失窗口期。
- **独立支付网关**：`Payment.API` 通过 `IPaymentProvider` 抽象支付渠道（当前用 `MockPaymentProvider` 模拟，未来接支付宝/微信只需新增实现）；`PaymentCallbackController` 演示第三方回调验签。
- **事件驱动解耦**：跨服务通过「发件箱 → RabbitMQ → 消费者回写」形成完整闭环，配合 `IntegrationEventLogEF` 实现消息的最终一致性。
- **统一响应与异常处理**：所有接口返回统一的 `ApiResponse<T>` 结构；全局异常中间件将领域异常、校验异常、未预期异常分别映射为规范化的 HTTP 状态码与业务错误码。
- **参数校验**：FluentValidation 校验器 + MediatR 校验管道，在进入 Handler 前完成参数校验。

## 测试

测试项目 `MerchantAdmin.UnitTests`，包含单元测试与集成测试：

```bash
dotnet test MerchantAdmin.UnitTests
```

- **单元/集成测试**（70+ 个）：`Order` / `Product` 领域状态机与库存规则、FluentValidation 校验器、命令处理（下单/取消/删除/超时）、领域事件回补库存、订单超时处理器、支付回调幂等、身份种子数据、JWT 认证（401）、商品 CRUD、参数校验错误码规范化。

## 环境与启动

### Docker Compose 一键部署（推荐）

基础设施：SQL Server、Redis、RabbitMQ、Seq 日志。

```bash
# 1. 首次部署：数据库迁移 + 初始化种子数据（角色 + admin 超管）
docker compose -f docker-compose.yml --profile migrate up -d

# 2. 启动全部服务
docker compose -f docker-compose.yml up -d
```

**对外入口（唯一端口）：`http://localhost:8080`**——nginx 统一反向代理，前端静态资源 + 三个后端服务的 API 都在此入口，后端容器不暴露到宿主。

| 路由 | 转发到 |
|---|---|
| `/` | 前端静态资源 |
| `/api/identity/*` | Identity.API |
| `/api/merchant/*` | MerchantAdmin.API |
| `/api/payment/*` | Payment.API |

### 本地开发

```bash
# 1. 基础容器
docker compose -f docker-compose.yml up -d sqlserver redis rabbitmq seq

# 2. 数据迁移（首次）
dotnet ef database update --project MerchantAdmin.Infrastructure --startup-project MerchantAdmin.API

# 3. 后端（各自终端，端口见 launchSettings.json）
dotnet run --project Identity.API        # http://localhost:5001/swagger
dotnet run --project MerchantAdmin.API   # http://localhost:5002/swagger
dotnet run --project Payment.API         # http://localhost:5003

# 4. 前端
cd MerchantAdmin.Frontend
npm install
npm run dev                               # http://localhost:5173
```

## 认证与授权

基于 **ASP.NET Core Identity + JWT** 的 RBAC 权限体系（角色：SuperAdmin / Admin / Operator + 自定义角色），JWT 携带角色与 SecurityStamp（改密码后旧 token 立即失效）。接口通过 `[Authorize(Roles="...")]` 声明式鉴权。

1. 调用 `POST /api/Auth/login`（或 `register`）获取 `token`；
2. 请求业务接口时携带 `Authorization: Bearer {token}` 头。

默认超管账号 `admin / 123456`（不可修改角色、不可删除、不可重置密码，仅一台）。

## 敏感配置说明

数据库 / Seq 密码等敏感信息通过环境变量注入（见 `docker-compose.yml` 中的 `${...}` 占位符），不硬编码在源码中。本地开发默认值仅用于演示，生产环境请务必通过 Secret 管理（Kubernetes 中见 `k8s/05-secrets.yaml`）。

## 待完善

- [x] ~~接入真实支付渠道~~（当前用 `MockPaymentProvider` 模拟，未来可替换为支付宝/微信实现）

- [x] 支付超时自动关闭（Redis 过期事件 + 定时兜底扫描双保险）与退款流程

- [ ] 集成测试覆盖订单/支付完整流程（Testcontainers 真实 Redis/RabbitMQ 依赖）

  
