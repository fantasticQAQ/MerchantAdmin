import { resolveUrl } from './config'

type Method = 'GET' | 'POST' | 'PUT' | 'DELETE'

interface RequestOptions {
  url: string
  method?: Method
  data?: any
}

/** 后端统一响应格式 { code, message, data, success } */
interface ApiResponse<T = any> {
  code: number
  message: string
  data: T
  success: boolean
}

/** 过滤 query 参数中的空值，避免 undefined/null/空字符串被序列化成 "undefined" 传给后端 */
function cleanParams(data?: any) {
  if (!data || typeof data !== 'object') return data
  const cleaned: Record<string, any> = {}
  for (const key in data) {
    const val = data[key]
    if (val === undefined || val === null || val === '') continue
    cleaned[key] = val
  }
  return cleaned
}

/** 从各种后端错误响应中提取可读的错误消息 */
function extractError(body: any, statusCode: number): string {
  // 纯文本错误（Identity 的 BadRequest("角色已存在")）
  if (typeof body === 'string' && body.trim()) {
    return body.trim()
  }
  // IdentityError 数组（Identity 的 BadRequest(result.Errors) → [{ code, description }]）
  if (Array.isArray(body) && body.length > 0) {
    const desc = body
      .map((e: any) => e?.description || e?.message || '')
      .filter((s: string) => s)
      .join('；')
    if (desc) return desc
  }
  if (body && typeof body === 'object' && body.message) {
    return body.message
  }
  return `请求失败(${statusCode})`
}

function request<T>(options: RequestOptions): Promise<T> {
  const token = uni.getStorageSync('token')
  const method = options.method || 'GET'

  // GET/DELETE 走 query 参数，清洗掉空值
  const data =
    method === 'GET' || method === 'DELETE'
      ? cleanParams(options.data)
      : options.data

  return new Promise<T>((resolve, reject) => {
    uni.request({
      url: resolveUrl(options.url),
      method,
      data,
      header: {
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {})
      },
      success: (res) => {
        const statusCode = res.statusCode

        // 未认证：清空 token 并跳转登录页
        // 但登录接口自身返回 401（如账号密码错误）不跳转，直接透传后端错误提示
        if (statusCode === 401) {
          const isLoginRequest =
            options.url.includes('/auth/login') ||
            options.url.includes('/wxauth/login')
          if (!isLoginRequest) {
            uni.removeStorageSync('token')
            uni.reLaunch({ url: '/pages/login/index' })
            reject(new Error('登录已过期，请重新登录'))
            return
          }
          reject(new Error(extractError(res.data, statusCode)))
          return
        }

        const body = res.data as any

        // 统一响应格式（MerchantAdmin.API 返回 { code, message, data, success }）
        if (body && typeof body === 'object' && 'code' in body) {
          if (body.code === 0) {
            resolve(body.data as T)
          } else {
            reject(new Error(body.message || '请求失败'))
          }
          return
        }

        // 非统一响应（如 Identity 登录返回 { token, userName, roles }、用户/角色返回裸数组或纯文本）
        if (statusCode >= 200 && statusCode < 300) {
          resolve(body as T)
        } else {
          reject(new Error(extractError(body, statusCode)))
        }
      },
      fail: (err) => {
        reject(new Error(err.errMsg || '网络错误'))
      }
    })
  })
}

export const http = {
  get<T>(url: string, data?: any) {
    return request<T>({ url, method: 'GET', data })
  },
  post<T>(url: string, data?: any) {
    return request<T>({ url, method: 'POST', data })
  },
  put<T>(url: string, data?: any) {
    return request<T>({ url, method: 'PUT', data })
  },
  delete<T>(url: string, data?: any) {
    return request<T>({ url, method: 'DELETE', data })
  }
}

export default http