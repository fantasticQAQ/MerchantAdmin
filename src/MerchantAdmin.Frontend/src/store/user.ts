import { defineStore } from 'pinia'
import { login, type LoginParams } from '@/api/auth'

/**
 * 登录态在 localStorage 里的读取。静默续期（utils/request）也会改写它，
 * 所以读写逻辑集中在一处，避免两边格式漂移。
 * 路由守卫也用它——守卫在导航时做一次性判断，直接读存储比依赖 pinia 更稳。
 */
export function readAuthState() {
  return {
    token: localStorage.getItem('token') || '',
    // access token 只有十几分钟；过期后由 utils/request 的拦截器用 refresh token 静默续期
    refreshToken: localStorage.getItem('refreshToken') || '',
    userName: localStorage.getItem('userName') || '',
    roles: JSON.parse(localStorage.getItem('roles') || '[]') as string[]
  }
}

export const useUserStore = defineStore('user', {
  state: readAuthState,

  getters: {
    isLoggedIn: state => !!state.token,
    isSuperAdmin: state => state.roles.includes('SuperAdmin'),
    // Admin 或超管都视为管理员（超管拥有全部权限）
    isAdmin: state => state.roles.includes('Admin') || state.roles.includes('SuperAdmin'),
    isOperator: state => state.roles.includes('Operator'),
    // 有管理权限（Admin 或 Operator 或超管）
    canManage: state => state.roles.some(r => r === 'Admin' || r === 'Operator' || r === 'SuperAdmin')
  },

  actions: {
    async login({ username, password }: LoginParams) {
      const res = await login({ username, password })
      this.token = res.token
      this.refreshToken = res.refreshToken
      this.userName = res.userName
      this.roles = res.roles

      localStorage.setItem('token', res.token)
      localStorage.setItem('refreshToken', res.refreshToken)
      localStorage.setItem('userName', res.userName)
      localStorage.setItem('roles', JSON.stringify(res.roles))
    },

    /**
     * 从 localStorage 重新同步登录态。
     * 静默续期成功后角色可能已经变了（管理员刚改过），而拦截器只改了 localStorage，
     * 不同步的话侧边栏菜单和按钮会停在旧角色上——用户会看到点不动的按钮（点了才 403）。
     */
    syncFromStorage() {
      const state = readAuthState()
      this.token = state.token
      this.refreshToken = state.refreshToken
      this.userName = state.userName
      this.roles = state.roles
    },

    logout() {
      this.token = ''
      this.refreshToken = ''
      this.userName = ''
      this.roles = []
      localStorage.clear()
    }
  }
})
