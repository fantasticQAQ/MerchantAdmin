import request from '@/utils/request'

export interface UserDto {
  id: number
  userName: string
  email: string
  roles: string[]
}

export interface RoleDto {
  name: string
  userCount: number
  isActive: boolean
}

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

/** 获取用户列表 */
export function getUsers() {
  return request.get<UserDto[]>('/identity/users')
}

/** 获取角色列表（含用户数） */
export function getRoles() {
  return request.get<RoleDto[]>('/identity/roles')
}

/** 新建角色 */
export function createRole(name: string) {
  return request.post<string>('/identity/roles', { name })
}

/** 删除角色（软删除停用） */
export function deleteRole(name: string) {
  return request.delete<string>(`/identity/roles/${name}`)
}

/** 启用角色（重新激活停用的角色） */
export function activateRole(name: string) {
  return request.post<string>(`/identity/roles/${name}/activate`)
}

/** 删除角色（硬删，仅无用户的角色） */
export function deleteRoleHard(name: string) {
  return request.delete<string>(`/identity/roles/${name}/hard`)
}

/** 新增用户 */
export function createUser(data: CreateUserParams) {
  return request.post<string>('/identity/users', data)
}

/** 编辑用户（邮箱/角色） */
export function updateUser(id: number, data: UpdateUserParams) {
  return request.put<string>(`/identity/users/${id}`, data)
}

/** 删除用户 */
export function deleteUser(id: number) {
  return request.delete<string>(`/identity/users/${id}`)
}

/** 管理员重置用户密码 */
export function resetPassword(id: number, newPassword: string) {
  return request.post<string>(`/identity/users/${id}/reset-password`, { newPassword })
}
