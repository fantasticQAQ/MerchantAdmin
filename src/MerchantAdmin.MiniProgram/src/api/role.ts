import { http } from '@/utils/request'
import type { RoleDto } from '@/types'

/** 角色列表（含已停用，前端显示状态） */
export function getRoles() {
  return http.get<RoleDto[]>('/identity/roles')
}

/** 新建角色（同名已停用则重新启用） */
export function createRole(name: string) {
  return http.post<string>('/identity/roles', { name })
}

/** 停用角色（软删除） */
export function deactivateRole(name: string) {
  return http.delete<string>(`/identity/roles/${encodeURIComponent(name)}`)
}

/** 启用角色 */
export function activateRole(name: string) {
  return http.post<string>(`/identity/roles/${encodeURIComponent(name)}/activate`)
}

/** 硬删除角色（有用户时后端会拒绝） */
export function hardDeleteRole(name: string) {
  return http.delete<string>(`/identity/roles/${encodeURIComponent(name)}/hard`)
}