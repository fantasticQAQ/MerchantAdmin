import { http } from '@/utils/request'
import type { PagedResult, ProductDto } from '@/types'

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
  return http.get<PagedResult<ProductDto>>('/merchant/products', params)
}

/** 创建商品 */
export function createProduct(data: CreateProductCommand) {
  return http.post<number>('/merchant/products', data)
}

/** 更新商品（编辑名称/价格、调整库存、上下架） */
export function updateProduct(id: number, data: UpdateProductParams) {
  return http.put<boolean>(`/merchant/products/${id}`, data)
}

/** 删除商品 */
export function deleteProduct(id: number) {
  return http.delete<boolean>(`/merchant/products/${id}`)
}