import request from '@/utils/request'

export interface OrderDto {
    orderId: number
    createdAt: Date
    orderStatus: string
    orderItems: OrderItemDto[]
}
export enum OrderStatus {
    Created = 1,
    Paid = 2,
    Cancelled = 3,
}

export interface OrderItemDto {
    productId: number
    productName: string
    price: number
    quantity: number
}

export interface GetOrdersResult {
    orders: OrderDto[]
}
export interface CreateOrderParams {
    orderItems: OrderItemDto[]
}


/** 获取订单列表 */
export function getOrders() {
    return request.get<GetOrdersResult>('/merchant/orders')
}

/** 创建订单 */
export function createOrder(data: CreateOrderParams) {
    return request.post('/merchant/orders/create', data)
}

/** 取消订单 */
export function cancelOrder(id: number) {
    return request.post(`/merchant/orders/${id}/cancel`)
}

/** 支付订单 */
export function payOrder(id: number) {
    return request.post(`/merchant/orders/${id}/pay`)
}