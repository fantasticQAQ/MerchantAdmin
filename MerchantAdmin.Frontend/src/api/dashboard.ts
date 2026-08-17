import request from '@/utils/request'

export interface DashboardDto {
  productCount: number
  orderCount: number
  paidOrderCount: number
  pendingOrderCount: number
  totalSales: number
}

/** 获取仪表盘统计数据 */
export function getDashboard() {
  return request.get<DashboardDto>('/merchant/dashboard')
}
