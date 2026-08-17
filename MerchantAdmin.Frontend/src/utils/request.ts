import axios from 'axios'

const request = axios.create({
  // 以 /api 开头，配合 vite 代理 → nginx → 后端服务
  baseURL: '/api',
  timeout: 10000
})

// 请求拦截器：附加 JWT
request.interceptors.request.use(config => {
  const token = localStorage.getItem('token')
  if (token && config.headers) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// 响应拦截器：统一解包后端响应 + 错误处理
request.interceptors.response.use(
  res => {
    const body = res.data

    // 后端统一响应格式 { code, message, data, success }
    if (body && typeof body === 'object' && 'code' in body) {
      if (body.code === 0) {
        // 成功：直接返回业务数据 data
        return body.data
      }
      // 业务失败：抛出 message，由调用方 catch 处理
      return Promise.reject(new Error(body.message || '请求失败'))
    }

    // 非统一响应（如 Identity 登录返回 { token }）：原样返回
    return body
  },
  error => {
    const status = error.response?.status

    // 未认证：清空 token 并跳转登录页（登录接口自身的 401 不跳转，交由登录页提示）
    if (status === 401) {
      const isLoginRequest = error.config?.url?.includes('/auth/login')
      if (!isLoginRequest) {
        localStorage.clear()
        if (window.location.pathname !== '/login') {
          window.location.href = '/login'
        }
      }
    }

    // 提取后端统一响应的错误消息（如"库存不足"、"订单状态错误"等）
    const body = error.response?.data
    if (body && typeof body === 'object' && 'message' in body && body.message) {
      return Promise.reject(new Error(body.message))
    }
    // 后端可能返回纯文本错误（如 Identity 的 401）
    if (typeof body === 'string' && body.trim()) {
      return Promise.reject(new Error(body.trim()))
    }

    return Promise.reject(error)
  }
)

// 类型断言：响应拦截器已解包出 data，让 api 层直接拿到 Promise<T>
export default request as unknown as {
  get<T>(url: string, config?: Record<string, unknown>): Promise<T>
  post<T>(url: string, data?: unknown, config?: Record<string, unknown>): Promise<T>
  put<T>(url: string, data?: unknown, config?: Record<string, unknown>): Promise<T>
  delete<T>(url: string, config?: Record<string, unknown>): Promise<T>
}
