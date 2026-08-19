import { http } from '@/utils/request'

/** 登录结果（账号密码 / 微信登录通用） */
export interface LoginResult {
  token: string
  userName: string
  roles: string[]
}

/** 账号密码登录 */
export function login(userName: string, password: string) {
  return http.post<LoginResult>('/identity/auth/login', { userName, password })
}

/** 注册账号 */
export function register(userName: string, password: string, email: string) {
  return http.post<string>('/identity/auth/register', { userName, password, email })
}

/** 微信小程序登录：用 wx.login() 返回的 code 换取 token */
export function wxLogin(code: string) {
  return http.post<LoginResult>('/identity/wxauth/login', { code })
}