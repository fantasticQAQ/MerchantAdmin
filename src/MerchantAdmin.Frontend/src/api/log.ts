import request from '@/utils/request'
import type { PagedResult } from './product'

export interface LogDto {
  id: number
  userName: string
  action: string
  detail: string
  createdAt: string
}

/** 获取操作日志（分页） */
export function getLogs(params?: { page?: number; pageSize?: number }) {
  return request.get<PagedResult<LogDto>>('/merchant/logs', { params })
}
