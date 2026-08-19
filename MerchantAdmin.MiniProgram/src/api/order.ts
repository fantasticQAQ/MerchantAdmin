import { http } from '@/utils/request'
import type { PagedResult, OrderDto, OrderItemDto } from '@/types'

export interface CreateOrderParams {
  orderItems: OrderItemDto[]
}

export interface OrderQueryParams {
  orderId?: number
  status?: string
  page?: number
  pageSize?: number
}

/** 获取订单列表（分页 + 搜索） */
export function getOrders(params?: OrderQueryParams) {
  return http.get<PagedResult<OrderDto>>('/merchant/orders', params)
}

/** 创建订单（支持多商品） */
export function createOrder(data: CreateOrderParams) {
  return http.post<number>('/merchant/orders/create', data)
}

/** 取消订单 */
export function cancelOrder(id: number) {
  return http.post<boolean>(`/merchant/orders/${id}/cancel`)
}

/** 支付订单 */
export function payOrder(id: number) {
  return http.post<number>(`/merchant/orders/${id}/pay`)
}

/** 退款（仅已支付订单） */
export function refundOrder(id: number) {
  return http.post<boolean>(`/merchant/orders/${id}/refund`)
}

/** 删除订单（仅已取消订单） */
export function deleteOrder(id: number) {
  return http.delete<boolean>(`/merchant/orders/${id}`)
}