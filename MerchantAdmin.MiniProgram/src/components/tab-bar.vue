<template>
  <view class="nav-bar">
    <view v-for="item in tabs" :key="item.key" class="nav-item" @click="go(item)">
      <text :class="['nav-text', item.key === current ? 'active' : '']">{{ item.text }}</text>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { isAdmin } from '@/utils/auth'

export interface NavItem {
  key: string
  text: string
}

const props = defineProps<{ current: string }>()
const emit = defineEmits<{ (e: 'change', key: string): void }>()

const ALL_NAVS: NavItem[] = [
  { key: 'products', text: '商品' },
  { key: 'orders', text: '订单' },
  { key: 'users', text: '用户' },
  { key: 'roles', text: '角色' },
  { key: 'logs', text: '日志' }
]

const ADMIN_ONLY = ['users', 'roles', 'logs']

// 响应式过滤：角色变更后 isAdmin() 变化会自动重算，即时隐藏/显示入口
const tabs = computed<NavItem[]>(() =>
  ALL_NAVS.filter((n) => !ADMIN_ONLY.includes(n.key) || isAdmin())
)

const go = (item: NavItem) => {
  if (item.key === props.current) return
  emit('change', item.key)
}
</script>

<style>
.nav-bar {
  position: fixed;
  left: 0;
  right: 0;
  bottom: 0;
  display: flex;
  height: 100rpx;
  background: #fff;
  border-top: 1rpx solid #eee;
  padding-bottom: env(safe-area-inset-bottom);
  z-index: 99;
}

.nav-item {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
}

.nav-text {
  font-size: 26rpx;
  color: #999;
}

.nav-text.active {
  color: #1989fa;
}
</style>
