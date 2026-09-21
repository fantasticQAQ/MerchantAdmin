import request, { newRequestId } from '@/utils/request'

export interface ProductDto {
  productId: number
  name: string
  price: number
  stock: number
  isActive: boolean
}

export interface PagedResult<T> {
  total: number
  page: number
  pageSize: number
  items: T[]
}

export interface CreateProductCommand {
  productDto: {
    productId: number
    name: string
    price: number
    stock: number
  }
}

export interface UpdateProductParams {
  name?: string
  price?: number
  stockDelta?: number
  isActive?: boolean
}

export interface ProductQueryParams {
  name?: string
  page?: number
  pageSize?: number
}

/** 获取商品列表（分页 + 名称搜索） */
export function getProducts(params?: ProductQueryParams) {
  return request.get<PagedResult<ProductDto>>('/merchant/products', { params })
}

/** 创建商品（返回新商品 Id） */
export function createProduct(data: CreateProductCommand) {
  return request.post<number>('/merchant/products', data)
}

/**
 * 更新商品（编辑名称/价格、调整库存、上下架）。
 * 带 x-requestid 幂等键：同一 ID 的请求服务端只执行一次，避免超时重发导致重复扣减。
 * 调用方若自己做重试，把第一次的 requestId 传进来即可复用。
 */
export function updateProduct(id: number, data: UpdateProductParams, requestId = newRequestId()) {
  return request.put<boolean>(`/merchant/products/${id}`, data, {
    headers: { 'x-requestid': requestId }
  })
}

/** 删除商品 */
export function deleteProduct(id: number) {
  return request.delete<boolean>(`/merchant/products/${id}`)
}
