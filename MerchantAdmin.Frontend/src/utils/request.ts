import axios, { type AxiosInstance } from 'axios'
import type { RefreshTokenResult } from '@/api/auth'

const request: AxiosInstance = axios.create({
  baseURL: 'api',
  timeout: 10000
})

let isRefreshing = false
let queue: ((token: string) => void)[] = []

const onRefreshed = (token: string) => {
  queue.forEach(cb => cb(token))
  queue = []
}

// 请求拦截器
request.interceptors.request.use(config => {
  const token = localStorage.getItem('token')
  if (token && config.headers) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// 响应拦截器
request.interceptors.response.use(
  res => res.data,
  async error => {
    const original = error.config

    if (error.response?.status === 401 && !original._retry) {
      original._retry = true

      if (!isRefreshing) {
        isRefreshing = true
        try {
          const refreshToken = localStorage.getItem('refreshToken')
          const res = await request.post<RefreshTokenResult>(
            '/api/auth/refresh',
            { refreshToken }
          )

          localStorage.setItem('token', res.accessToken)
          localStorage.setItem('refreshToken', res.refreshToken)

          onRefreshed(res.accessToken)
        } catch {
          localStorage.clear()
          window.location.href = '/login'
        } finally {
          isRefreshing = false
        }
      }

      return new Promise(resolve => {
        queue.push(token => {
          original.headers!.Authorization = `Bearer ${token}`
          resolve(request(original))
        })
      })
    }

    return Promise.reject(error)
  }
)

export default request