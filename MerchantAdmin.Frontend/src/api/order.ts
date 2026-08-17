import request from '@/utils/request'
import type { PagedResult } from './product'

export interface OrderItemDto {
  productId: number
  productName: string
  price: number
  quantity: number
}

export interface OrderDto {
  orderId: number
  createdAt: string
  orderStatus: string
  orderItems: OrderItemDto[]
}

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
  return request.get<PagedResult<OrderDto>>('/merchant/orders', { params })
}

/** 创建订单（支持多商品） */
export function createOrder(data: CreateOrderParams) {
  return request.post<number>('/merchant/orders/create', data)
}

/** 取消订单 */
export function cancelOrder(id: number) {
  return request.post<boolean>(`/merchant/orders/${id}/cancel`)
}

/** 支付订单 */
export function payOrder(id: number) {
  return request.post<number>(`/merchant/orders/${id}/pay`)
}

/** 退款（仅已支付订单） */
export function refundOrder(id: number) {
  return request.post<boolean>(`/merchant/orders/${id}/refund`)
}

/** 删除订单（仅已取消订单） */
export function deleteOrder(id: number) {
  return request.delete<boolean>(`/merchant/orders/${id}`)
}

/** 导出订单 CSV（带 token，直接下载到浏览器下载目录） */
export async function exportOrders(status?: string): Promise<boolean> {
  const token = localStorage.getItem('token')
  const url = `/api/merchant/orders/export${status ? `?status=${status}` : ''}`
  const res = await fetch(url, {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined
  })
  if (!res.ok) throw new Error('导出失败')
  const blob = await res.blob()
  const fileName = `orders_${new Date().toISOString().slice(0, 14).replace(/\D/g, '')}.csv`

  // 直接下载到浏览器默认下载目录（快，无对话框延迟）
  const objectUrl = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = objectUrl
  a.download = fileName
  a.click()
  URL.revokeObjectURL(objectUrl)
  return true
}
