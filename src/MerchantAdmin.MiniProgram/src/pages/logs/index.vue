<template>
  <view class="page">
    <TabBar current="/pages/logs/index" />
    <!-- 日志列表 -->
    <view class="log-list">
      <view v-for="item in list" :key="item.id" class="log-card">
        <view class="log-head">
          <text class="log-user">{{ item.userName }}</text>
          <text class="log-action">{{ actionLabel(item.action) }}</text>
        </view>
        <scroll-view class="log-detail-scroll" scroll-x>
          <text class="log-detail">{{ item.detail }}</text>
        </scroll-view>
        <view class="log-time">{{ formatTime(item.createdAt) }}</view>
      </view>

      <view v-if="!loading && list.length === 0" class="empty">暂无日志</view>
      <view v-if="loading && list.length === 0" class="loading-mask">
        <view class="loading-spinner"></view>
        <text class="loading-text">加载中...</text>
      </view>
      <view v-if="loading && list.length > 0" class="loading">加载中...</view>
      <view v-if="!loading && list.length > 0 && list.length >= total" class="loading">没有更多了</view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { onShow, onReachBottom } from '@dcloudio/uni-app'
import { getLogs, type LogDto } from '@/api/log'
import { isAdmin } from '@/utils/auth'
import TabBar from '@/components/tab-bar.vue'

const list = ref<LogDto[]>([])
const page = ref(1)
const pageSize = 20
const total = ref(0)
const loading = ref(false)
const loaded = ref(false)

const actionLabel = (a: string) => {
  const map: Record<string, string> = {
    Create: '新增',
    Update: '修改',
    Delete: '删除',
    Pay: '支付',
    Refund: '退款'
  }
  return map[a] || a
}

const formatTime = (iso: string) => {
  if (!iso) return '-'
  const d = new Date(iso)
  const pad = (n: number) => (n < 10 ? '0' + n : '' + n)
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

const fetchLogs = async (reset = false) => {
  if (loading.value) return
  loading.value = true
  try {
    const res = await getLogs(page.value, pageSize)
    list.value = reset ? res.items : [...list.value, ...res.items]
    total.value = res.total
  } catch (e: any) {
    uni.showToast({ title: e.message || '加载失败', icon: 'none' })
  } finally {
    loading.value = false
  }
}

onShow(() => {
  if (!isAdmin()) {
    uni.showToast({ title: '无权限访问', icon: 'none' })
    uni.reLaunch({ url: '/pages/products/index' })
    return
  }
  if (loaded.value) return
  loaded.value = true
  page.value = 1
  fetchLogs(true)
})

onReachBottom(() => {
  if (list.value.length >= total.value) return
  page.value++
  fetchLogs(false)
})
</script>

<style>
.page {
  padding: 20rpx;
  padding-bottom: 160rpx;
}

.log-card {
  background: #fff;
  border-radius: 16rpx;
  padding: 24rpx;
  margin-bottom: 20rpx;
}

.log-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12rpx;
}

.log-user {
  font-size: 30rpx;
  font-weight: bold;
}

.log-action {
  font-size: 24rpx;
  color: #1989fa;
  background: #e8f3ff;
  padding: 4rpx 16rpx;
  border-radius: 8rpx;
}

.log-detail-scroll {
  width: 100%;
  margin-bottom: 12rpx;
  white-space: nowrap;
}

.log-detail {
  font-size: 28rpx;
  color: #333;
  line-height: 1.5;
  white-space: nowrap;
}

.log-time {
  font-size: 24rpx;
  color: #999;
}

.empty,
.loading {
  text-align: center;
  color: #999;
  font-size: 26rpx;
  padding: 40rpx 0;
}
</style>