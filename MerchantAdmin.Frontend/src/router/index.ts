import { createRouter, createWebHistory } from 'vue-router'
import Layout from '@/layout/index.vue'

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
          component: () => import('@/views/user/index.vue')
        },
        {
          path: 'roles',
          name: 'Roles',
          component: () => import('@/views/role/index.vue')
        },
        {
          path: 'logs',
          name: 'Logs',
          component: () => import('@/views/log/index.vue')
        }
      ]
    }
  ]
})

// 导航守卫：未登录访问受保护页面时跳转登录页
router.beforeEach(to => {
  const token = localStorage.getItem('token')
  const isPublicPage = to.path === '/login' || to.path === '/register'

  if (!isPublicPage && !token) {
    return { path: '/login' }
  }
  // 已登录访问登录/注册页时直接进入首页
  if (isPublicPage && token) {
    return { path: '/' }
  }
})

export default router
