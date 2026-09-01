import request from '@/utils/request'

/** 登录参数 */
export interface LoginParams {
  username: string
  password: string
}

/** 登录结果（Identity.API 返回 { token, userName, roles }） */
export interface LoginResult {
  token: string
  userName: string
  roles: string[]
}

/** 注册参数 */
export interface RegisterParams {
  userName: string
  password: string
  email: string
}

/** 登录 */
export function login(data: LoginParams) {
  return request.post<LoginResult>('/identity/auth/login', data)
}

/** 注册 */
export function register(data: RegisterParams) {
  return request.post<string>('/identity/auth/register', data)
}

/** 修改自己的密码 */
export function changePassword(oldPassword: string, newPassword: string) {
  return request.post<string>('/identity/auth/change-password', { oldPassword, newPassword })
}
