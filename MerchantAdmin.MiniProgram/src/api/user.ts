import { http } from '@/utils/request'
import type { UserDto } from '@/types'

export interface CreateUserParams {
  userName: string
  email: string
  password: string
  roles?: string[]
}

export interface UpdateUserParams {
  email?: string
  roles?: string[]
}

/** 用户列表（含角色） */
export function getUsers() {
  return http.get<UserDto[]>('/identity/users')
}

/** 新增用户 */
export function createUser(data: CreateUserParams) {
  return http.post<string>('/identity/users', data)
}

/** 编辑用户（邮箱、角色） */
export function updateUser(id: number, data: UpdateUserParams) {
  return http.put<string>(`/identity/users/${id}`, data)
}

/** 删除用户 */
export function deleteUser(id: number) {
  return http.delete<string>(`/identity/users/${id}`)
}

/** 重置密码 */
export function resetPassword(id: number, newPassword: string) {
  return http.post<string>(`/identity/users/${id}/reset-password`, { newPassword })
}