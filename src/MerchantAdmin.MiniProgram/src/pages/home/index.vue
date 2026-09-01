<template>
  <view class="home">
    <!-- 顶部用户栏：显示当前用户 + 退出登录 -->
    <view class="home-header">
      <text class="greet">{{ userName || '已登录' }}</text>
      <text class="logout-btn" @click="handleLogout">退出登录</text>
    </view>

    <!-- tab内容：用 v-show 切换，所有组件常驻DOM，切换瞬间完成 -->
    <tab-products ref="productsRef" :active="current === 'products'" v-show="current === 'products'" />
    <tab-orders ref="ordersRef" :active="current === 'orders'" v-show="current === 'orders'" />
    <tab-users ref="usersRef" :active="current === 'users'" v-show="current === 'users'" />
    <tab-roles ref="rolesRef" :active="current === 'roles'" v-show="current === 'roles'" />
    <tab-logs ref="logsRef" :active="current === 'logs'" v-show="current === 'logs'" />

    <!-- 底部导航：高亮与v-show绑定的是同一个current，视觉同步 -->
    <TabBar :current="current" @change="onTabChange" />
  </view>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { onShow, onReachBottom, onPullDownRefresh } from '@dcloudio/uni-app'
import TabBar from '@/components/tab-bar.vue'
import TabProducts from '@/components/tab-products.vue'
import TabOrders from '@/components/tab-orders.vue'
import TabUsers from '@/components/tab-users.vue'
import TabRoles from '@/components/tab-roles.vue'
import TabLogs from '@/components/tab-logs.vue'
import { isAdmin, refreshCurrentUser, clearAuth, userName } from '@/utils/auth'

type TabKey = 'products' | 'orders' | 'users' | 'roles' | 'logs'

interface TabExposed {
  onReachBottom: () => void
  refresh: () => void | Promise<void>
}

const productsRef = ref<TabExposed | null>(null)
const ordersRef = ref<TabExposed | null>(null)
const usersRef = ref<TabExposed | null>(null)
const rolesRef = ref<TabExposed | null>(null)
const logsRef = ref<TabExposed | null>(null)

const DEFAULT_TAB: TabKey = 'products'
const ADMIN_TABS: TabKey[] = ['users', 'roles', 'logs']

const current = ref<TabKey>(DEFAULT_TAB)

const TITLE_MAP: Record<TabKey, string> = {
  products: '商品管理',
  orders: '订单管理',
  users: '用户管理',
  roles: '角色管理',
  logs: '操作日志'
}

const setNavTitle = (key: TabKey) => {
  uni.setNavigationBarTitle({ title: TITLE_MAP[key] })
}

setNavTitle(current.value)

const refMap: Record<TabKey, typeof productsRef> = {
  products: productsRef,
  orders: ordersRef,
  users: usersRef,
  roles: rolesRef,
  logs: logsRef
}

// 每次进入主页都刷新最新角色，保证「角色变更后 tab/按钮即时生效」
// 注意：必须 await 同步完成后展示，否则旧的角色缓存（如管理员残留）会让 Operator 短暂看到日志 tab
onShow(async () => {
  await refreshCurrentUser()
})

const handleLogout = () => {
  uni.showModal({
    title: '提示',
    content: '确定要退出登录吗？',
    success: (res) => {
      if (res.confirm) {
        clearAuth()
        uni.reLaunch({ url: '/pages/login/index' })
      }
    }
  })
}

const onTabChange = (key: string) => {
  const k = key as TabKey
  if (ADMIN_TABS.includes(k) && !isAdmin()) {
    uni.showToast({ title: '无权限访问', icon: 'none' })
    return
  }
  current.value = k
  setNavTitle(k)
  uni.pageScrollTo({ scrollTop: 0, duration: 0 })
}

// 统一分发触底加载事件到当前激活的 tab
onReachBottom(() => {
  const r = refMap[current.value].value
  if (r && typeof r.onReachBottom === 'function') {
    r.onReachBottom()
  }
})

// 下拉刷新：刷新当前 tab 数据 + 同步最新角色
onPullDownRefresh(async () => {
  await refreshCurrentUser()
  const r = refMap[current.value].value
  if (r && typeof r.refresh === 'function') {
    await r.refresh()
  }
  uni.stopPullDownRefresh()
})
</script>

<style>
.home {
  min-height: 100vh;
  background: #f5f6f8;
}

.home-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 20rpx 24rpx;
  background: #fff;
  border-bottom: 1rpx solid #eee;
}

.greet {
  font-size: 28rpx;
  color: #333;
}

.logout-btn {
  font-size: 26rpx;
  color: #f56c6c;
}
</style>