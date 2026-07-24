import request from '@/utils/request'

/** 登录参数 */
export interface LoginParams {
  username: string
  password: string
}
export interface LoginResult {
  accessToken: string
  refreshToken: string
}
export interface LoginResult2 {
  token: string
}


export interface RegisterParams {
  userName: string
  password: string
  email: string
}
export interface RegisterResult {
  value: string
}


export interface RefreshTokenResult {
  accessToken: string
  refreshToken: string
}



export function refreshTokenApi(refreshToken: string) {
  return request.post<RefreshTokenResult>(
    '/api/auth/refresh',
    { refreshToken }
  )
}


export function login(data: LoginParams) {
  return request.post<LoginResult>('/identity/auth/login', data)
}

export function login2(data: LoginParams) {
  return request.post<LoginResult2>('/identity/auth/login', data)
}

export function register(data: RegisterParams) {
  return request.post<RegisterResult>('/identity/auth/register', data)
}