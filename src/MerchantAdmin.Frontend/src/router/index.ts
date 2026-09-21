import { createRouter, createWebHistory } from 'vue-router'
import Layout from '@/layout/index.vue'
import { readAuthState } from '@/store/user'
import { ADMIN_ONLY_ROLES, canAccess } from '@/utils/routeAccess'

/** 仅 Admin / SuperAdmin 可访问的页面（对应 UsersController / RolesController / LogsController） */
const adminOnly = { roles: ADMIN_ONLY_ROLES }

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/login',
      name: 'Login',
      component: () => import('@/views/login/index.vue')
    },
    {
      path: '/register',
      name: 'Register',
      component: () => import('@/views/register/index.vue')
    },
    {
      path: '/',
      component: Layout,
      redirect: '/dashboard',
      children: [
        {
          path: 'dashboard',
          name: 'Dashboard',
          component: () => import('@/views/dashboard/index.vue')
        },
        {
          path: 'products',
          name: 'Products',
          component: () => import('@/views/product/index.vue')
        },
        {
          path: 'orders',
          name: 'Orders',
          component: () => import('@/views/order/index.vue')
        },
        {
          path: 'users',
          name: 'Users',
          component: () => import('@/views/user/index.vue'),
          meta: adminOnly
        },
        {
          path: 'roles',
          name: 'Roles',
          component: () => import('@/views/role/index.vue'),
          meta: adminOnly
        },
        {
          path: 'logs',
          name: 'Logs',
          component: () => import('@/views/log/index.vue'),
          meta: adminOnly
        }
      ]
    }
  ]
})

// 导航守卫：未登录跳登录页；角色不足跳仪表盘
router.beforeEach(to => {
  const { token, roles } = readAuthState()
  const isPublicPage = to.path === '/login' || to.path === '/register'

  if (!isPublicPage && !token) {
    return { path: '/login' }
  }
  // 已登录访问登录/注册页时直接进入首页
  if (isPublicPage && token) {
    return { path: '/' }
  }

  // 用户被降级后，下一次跳转就会被这里拦下来（而不是留在页面上拿到一堆 403）。
  // 停留在当前页被降级的情况由 layout 里的 watch 处理。
  if (!canAccess(to.meta, roles)) {
    return { path: '/dashboard' }
  }
})

export default router
