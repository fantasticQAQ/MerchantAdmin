import { defineStore } from 'pinia'
import { login, type LoginParams } from '@/api/auth'

export const useUserStore = defineStore('user', {
  state: () => ({
    token: localStorage.getItem('token') || '',
    userName: localStorage.getItem('userName') || '',
    roles: JSON.parse(localStorage.getItem('roles') || '[]') as string[]
  }),

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
      this.userName = res.userName
      this.roles = res.roles

      localStorage.setItem('token', res.token)
      localStorage.setItem('userName', res.userName)
      localStorage.setItem('roles', JSON.stringify(res.roles))
    },

    logout() {
      this.token = ''
      this.userName = ''
      this.roles = []
      localStorage.clear()
    }
  }
})
