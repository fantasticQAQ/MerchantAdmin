import { ref } from 'vue'
import { http } from '@/utils/request'

/** 响应式角色状态：跨组件共享，角色变更后全局即时生效 */
const roles = ref<string[]>(uni.getStorageSync('roles') || [])
export const userName = ref<string>(uni.getStorageSync('userName') || '')

/** 判断当前登录用户是否为管理员（Admin / SuperAdmin），可被 computed 追踪 */
export function isAdmin(): boolean {
  return roles.value.some((r) => r === 'Admin' || r === 'SuperAdmin')
}

/** 登录成功后写入 token 与用户信息 */
export function setAuth(auth: { token: string; userName: string; roles: string[] }) {
  uni.setStorageSync('token', auth.token)
  uni.setStorageSync('userName', auth.userName)
  uni.setStorageSync('roles', auth.roles)
  roles.value = auth.roles
  userName.value = auth.userName
}

/** 清空本地身份信息 */
export function clearAuth() {
  uni.removeStorageSync('token')
  uni.removeStorageSync('userName')
  uni.removeStorageSync('roles')
  roles.value = []
  userName.value = ''
}

/** 从后端拉取当前用户最新角色，用于「角色变更即时生效」 */
export async function refreshCurrentUser() {
  const token = uni.getStorageSync('token')
  if (!token) return
  try {
    const res = await http.get<{ userName: string; roles: string[] }>('/identity/auth/me')
    uni.setStorageSync('userName', res.userName)
    uni.setStorageSync('roles', res.roles)
    roles.value = res.roles
    userName.value = res.userName
  } catch (e) {
    // 静默失败：保留本地缓存的角色，避免打断正常使用
  }
}