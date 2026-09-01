// 与后端 DTO 对齐的类型定义

/** 分页结果 */
export interface PagedResult<T> {
  total: number
  page: number
  pageSize: number
  items: T[]
}

/** 商品 */
export interface ProductDto {
  productId: number
  name: string
  price: number
  stock: number
  isActive: boolean
}

/** 订单明细 */
export interface OrderItemDto {
  productId: number
  productName: string
  price: number
  quantity: number
}

/** 订单 */
export interface OrderDto {
  orderId: number
  createdAt: string
  orderStatus: string
  orderItems: OrderItemDto[]
}

/** 用户 */
export interface UserDto {
  id: number
  userName: string
  email: string
  roles: string[]
}

/** 角色 */
export interface RoleDto {
  name: string
  userCount: number
  isActive: boolean
}

/** 操作日志 */
export interface LogDto {
  id: number
  userName: string
  action: string
  detail: string
  createdAt: string
}