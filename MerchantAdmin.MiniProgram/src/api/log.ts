import { http } from '@/utils/request'
import type { PagedResult, LogDto } from '@/types'

/** 操作日志列表（分页） */
export function getLogs(page = 1, pageSize = 20) {
  return http.get<PagedResult<LogDto>>('/merchant/logs', { page, pageSize })
}