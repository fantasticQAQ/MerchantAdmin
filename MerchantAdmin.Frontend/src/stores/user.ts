import { defineStore } from 'pinia'
import { login, type LoginParams } from '@/api/auth'

export const useUserStore = defineStore('user', {
  state: () => ({
    token: localStorage.getItem('token') || '',
    refreshToken: localStorage.getItem('refreshToken') || ''
  }),

  actions: {
    async login({ username, password }) {
      const res = await login({ username, password })
      this.token = res.accessToken
      this.refreshToken = res.refreshToken

      localStorage.setItem('token', res.accessToken)
      localStorage.setItem('refreshToken', res.refreshToken)
    },

    logout() {
      this.token = ''
      this.refreshToken = ''
      localStorage.clear()
    }
  }
})