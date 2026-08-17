# MerchantAdmin 商户后台管理系统

一个基于 **.NET 8 微服务架构** 的电商商品/订单后台管理系统。项目以微软官方 **eShopOnContainers** 的架构思想为蓝本，实践 **DDD（领域驱动设计）**、**CQRS**、**事件驱动**、**容器化与 Kubernetes 部署** 等企业级后端技术，是我用来展示 .NET 后端工程能力的个人项目。

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
  - 下单 → 扣减库存 → 写入 Redis 延迟键（15 分钟未支付自动取消）；
  - 支付 → 订单进入「支付处理中」→ 发布「发起支付」事件（发件箱）→ **Payment.API 订阅 → 调用支付渠道 → 发布「支付成功」事件** → 订单服务订阅并回写「已支付」（幂等）；
  - 取消 → 状态校验 → 触发领域事件 → 通过领域事件处理器回补库存。
- **超时自动取消**：Redis `keyspace notification` 监听过期键 + `Channel` 异步消费，实现下单后超时未支付自动取消并回补库存。
- **独立支付网关**：`Payment.API` 通过 `IPaymentProvider` 抽象支付渠道（当前用 `MockPaymentProvider` 模拟，未来接支付宝/微信只需新增实现）；`PaymentCallbackController` 演示第三方回调验签。
- **事件驱动解耦**：跨服务通过「发件箱 → RabbitMQ → 消费者回写」形成完整闭环，配合 `IntegrationEventLogEF` 实现消息的最终一致性。
- **统一响应与异常处理**：所有接口返回统一的 `ApiResponse<T>` 结构；全局异常中间件将领域异常、校验异常、未预期异常分别映射为规范化的 HTTP 状态码与业务错误码。
- **参数校验**：FluentValidation 校验器 + MediatR 校验管道，在进入 Handler 前完成参数校验。

## 测试

测试项目 `MerchantAdmin.UnitTests`，包含单元测试与集成测试：

```bash
dotnet test MerchantAdmin.UnitTests
```

- **单元测试**（36 个）：`Order` 聚合根状态流转、`Product` 库存规则、值对象相等性、FluentValidation 校验器正反用例、`CreateOrderCommandHandler` 下单与库存扣减、支付事件消费者的幂等回写。
- **集成测试**（5 个）：基于 `WebApplicationFactory` + SQLite 内存库（mock 掉 Redis/RabbitMQ），验证 JWT 认证（未携带 token 返回 401）、商品 CRUD 的完整 HTTP 链路、以及参数校验错误码的规范化返回。

## 环境与启动

### 依赖（Docker Compose 一键启动）

基础设施：SQL Server、Redis、RabbitMQ、Seq 日志。

```bash
docker-compose up -d sqlserver redis rabbitmq seq
```

数据库迁移（首次部署时，带 `migrate` profile）：

```bash
docker-compose --profile migrate up db-migrator
```

### 启动后端服务

- **Identity.API**（认证服务）：`http://localhost:5001/swagger`
- **MerchantAdmin.API**（业务服务）：`http://localhost:5002/swagger`
- **Seq 日志**：`http://localhost:5341`

### 前端

```bash
cd MerchantAdmin.Frontend
npm install
npm run dev
```

## 认证与授权

所有业务接口（商品/订单）标注了 `[Authorize]`，通过 JWT Bearer 认证：

1. 调用 `POST /api/Auth/register` 注册；
2. 调用 `POST /api/Auth/login` 获取 `token`；
3. 请求业务接口时携带 `Authorization: Bearer {token}` 头。

## 敏感配置说明

数据库 / Seq 密码等敏感信息通过环境变量注入（见 `docker-compose.yml` 中的 `${...}` 占位符），不硬编码在源码中。本地开发默认值仅用于演示，生产环境请务必通过 Secret 管理（Kubernetes 中见 `k8s/05-secrets.yaml`）。

## 待完善

- [ ] 接入真实支付渠道（替换 `MockPaymentProvider` 为支付宝/微信实现）
- [ ] 集成测试覆盖订单/支付流程（Testcontainers 真实依赖）
- [ ] 支付失败/超时的补偿与退款流程
