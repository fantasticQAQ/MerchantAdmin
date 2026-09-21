import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios'

/**
 * 生成幂等键（UUID v4），用于写操作的 x-requestid 请求头。
 * 用 getRandomValues 而不是 crypto.randomUUID：后者只在 https / localhost 等安全上下文可用，
 * 生产环境经 nginx 用 http + IP 访问时会取不到。
 */
export function newRequestId(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(16))
  const chars = Array.from(bytes, x => x.toString(16).padStart(2, '0')).join('').split('')
  chars[12] = '4'
  chars[16] = '89ab'.charAt(Math.floor(Math.random() * 4))
  const hex = chars.join('')
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`
}

type RetriableConfig = InternalAxiosRequestConfig & { __retried?: boolean }

type RefreshOutcome =
  // 续期成功
  | { status: 'ok'; token: string }
  // refresh token 确实失效了（被撤销 / 过期 / 改过密码）→ 该登出
  | { status: 'expired' }
  // 网络中断、超时、服务端 5xx 等临时故障 → 不能据此登出
  | { status: 'unavailable' }

/**
 * 把刚写入 localStorage 的登录态同步到 pinia store。
 * 续期可能带回新角色（管理员刚改过角色），只改 localStorage 的话，侧边栏菜单和按钮
 * 仍会停在旧角色上，要等刷新页面才更新。
 * 这里用动态 import 打破 store → api/auth → utils/request 的循环依赖。
 */
async function syncUserStore() {
  const { useUserStore } = await import('@/store/user')
  useUserStore().syncFromStorage()
}

/**
 * 用 refresh token 换一对新的 token 并写回 localStorage。
 */
async function refreshAccessToken(): Promise<RefreshOutcome> {
  let refreshToken = localStorage.getItem('refreshToken')

  // 最多两次：第一次失败可能是多标签页竞态——另一个标签页已经把 refresh token 用掉并换了新的，
  // 这时存储里的值会变，用新的再试一次即可；值没变则说明是真的失效了。
  for (let attempt = 0; attempt < 2; attempt++) {
    if (!refreshToken) return { status: 'expired' }

    try {
      // 用裸 axios：走 request 实例会被下面的拦截器再次拦截，形成递归
      const { data } = await axios.post('/api/identity/auth/refresh', { refreshToken })
      if (!data?.token) return { status: 'expired' }

      localStorage.setItem('token', data.token)
      localStorage.setItem('refreshToken', data.refreshToken)
      localStorage.setItem('userName', data.userName)
      localStorage.setItem('roles', JSON.stringify(data.roles))
      await syncUserStore()
      return { status: 'ok', token: data.token as string }
    } catch (err) {
      const status = (err as AxiosError)?.response?.status

      // 没有响应体（网络断了、超时）或服务端 5xx：属于临时故障。
      // 这种情况下一旦清登录态，用户会因为一次抖动被踢出去——保留登录态，等下一个请求再试。
      if (!status || status >= 500) return { status: 'unavailable' }

      // 明确被拒（401/400）：可能只是多标签页竞态，也可能真的失效，用存储里的最新值判断
      const latest = localStorage.getItem('refreshToken')
      if (!latest || latest === refreshToken) return { status: 'expired' }
      refreshToken = latest
    }
  }

  return { status: 'expired' }
}

// 单飞：access token 只有十几分钟，多个请求可能同时撞上 401。
// 必须保证只发一次续期请求，否则并发的续期会互相把对方的 refresh token 轮换掉。
let refreshing: Promise<RefreshOutcome> | null = null

function refreshOnce(): Promise<RefreshOutcome> {
  refreshing ??= refreshAccessToken().finally(() => {
    refreshing = null
  })
  return refreshing
}

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

// 响应拦截器：统一解包后端响应 + 401 自动续期（区分"真失效"与"临时故障"）+ 错误处理
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
  async error => {
    const status = error.response?.status
    const config = error.config as RetriableConfig | undefined
    // 登录接口自身的 401 是"密码错误"，刷新接口自身的 401 说明续期也失败了，都不该再触发续期
    const isLoginRequest = config?.url?.includes('/auth/login')
    const isRefreshRequest = config?.url?.includes('/auth/refresh')

    // access token 过期：先静默续期并重放原请求，成功则调用方完全无感
    if (status === 401 && config && !isLoginRequest && !isRefreshRequest && !config.__retried) {
      config.__retried = true
      const outcome = await refreshOnce()
      if (outcome.status === 'ok') {
        // 重放时会重新走请求拦截器，Authorization 从 localStorage 取到刚写入的新 token
        return request(config)
      }
      // 续期只是暂时不可用（网络/5xx）：保留登录态，只让这一个请求失败，下次请求会再试一次
      if (outcome.status === 'unavailable') {
        return Promise.reject(error)
      }
    }

    // 未认证且无法续期：清空登录态并跳转登录页（登录接口自身的 401 不跳转，交由登录页提示）
    if (status === 401 && !isLoginRequest) {
      localStorage.clear()
      if (window.location.pathname !== '/login') {
        window.location.href = '/login'
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
