import request from '@/utils/request'


export interface ProductDto {
    productId: number
    name: string
    price: number
    stock: number
}

export interface GetProductsResult {
    products: ProductDto[]
}
export interface CreateProductCmommand {
    productDto: ProductDto
}

/** 获取商品列表 */
export function getProducts() {
    return request.get<GetProductsResult>('/merchant/products')
}

/** 创建商品 */
export function createProduct(data: CreateProductCmommand) {
    return request.post('/merchant/products', data)
}

/** 删除商品 */
export function deleteProduct(id: number) {
    return request.delete(`/merchant/products/${id}`)
}