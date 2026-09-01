<template>
  <view class="page">
    <!-- 角色列表 -->
    <view class="role-list">
      <view v-for="item in list" :key="item.name" class="role-card">
        <view class="role-main">
          <view class="role-name-line">
            <text class="role-name">{{ item.name }}</text>
            <text class="role-label">{{ roleLabel(item.name) }}</text>
            <text v-if="isBuiltin(item.name)" class="builtin-tag">内置</text>
            <text :class="item.isActive ? 'status-on' : 'status-off'">
              {{ item.isActive ? '启用' : '停用' }}
            </text>
          </view>
          <view class="role-count">关联用户：{{ item.userCount }} 个</view>
        </view>
        <view class="role-actions">
          <button
            v-if="!isBuiltin(item.name) && item.isActive"
            size="mini"
            @click="handleDeactivate(item)"
          >停用</button>
          <button
            v-if="!isBuiltin(item.name) && !item.isActive"
            size="mini"
            type="primary"
            @click="handleActivate(item)"
          >启用</button>
          <button
            v-if="!isBuiltin(item.name)"
            size="mini"
            type="warn"
            @click="handleHardDelete(item)"
          >删除</button>
        </view>
      </view>

      <view v-if="!loading && list.length === 0" class="empty">暂无角色</view>
      <view v-if="loading && list.length === 0" class="loading-mask">
        <view class="loading-spinner"></view>
        <text class="loading-text">加载中...</text>
      </view>
    </view>

    <!-- 新增按钮 -->
    <view class="fab" @click="openCreate">+</view>

    <!-- 新增角色弹窗 -->
    <view v-if="createVisible" class="mask" @click="createVisible = false">
      <view class="dialog" @click.stop>
        <view class="dialog-title">新增角色</view>
        <view class="form-item">
          <text class="label">角色名</text>
          <input class="input" v-model="createName" placeholder="请输入角色名" />
        </view>
        <view class="dialog-actions">
          <button class="dialog-btn" @click="createVisible = false">取消</button>
          <button class="dialog-btn primary" :loading="submitting" @click="handleCreate">确定</button>
        </view>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import {
  getRoles,
  createRole,
  deactivateRole,
  activateRole,
  hardDeleteRole,
  type RoleDto
} from '@/api/role'
import { isAdmin } from '@/utils/auth'

const props = defineProps<{ active: boolean }>()

const list = ref<RoleDto[]>([])
const loading = ref(false)
const submitting = ref(false)
const loaded = ref(false)

const createVisible = ref(false)
const createName = ref('')

const BUILTIN = ['Admin', 'SuperAdmin', 'Operator']

const roleLabel = (r: string) => {
  const map: Record<string, string> = {
    Admin: '管理员',
    Operator: '操作员',
    SuperAdmin: '超级管理员'
  }
  return map[r] || ''
}

const isBuiltin = (name: string) => BUILTIN.includes(name)

const fetchRoles = async () => {
  loading.value = true
  try {
    list.value = await getRoles()
  } catch (e: any) {
    uni.showToast({ title: e.message || '加载失败', icon: 'none' })
  } finally {
    loading.value = false
  }
}

watch(
  () => props.active,
  (v) => {
    if (v && !loaded.value) {
      if (!isAdmin()) return // 上层tab-bar已过滤非管理员入口，此处防御性跳过
      loaded.value = true
      fetchRoles()
    }
  },
  { immediate: true }
)

const onReachBottom = () => { /* 角色列表无分页 */ }

// 下拉刷新：重新加载角色列表
const refresh = () => fetchRoles()

defineExpose({ onReachBottom, refresh })

const openCreate = () => {
  createName.value = ''
  createVisible.value = true
}

const handleCreate = async () => {
  const name = createName.value.trim()
  if (!name) {
    uni.showToast({ title: '请输入角色名', icon: 'none' })
    return
  }

  submitting.value = true
  try {
    await createRole(name)
    uni.showToast({ title: '创建成功', icon: 'success' })
    createVisible.value = false
    fetchRoles()
  } catch (e: any) {
    uni.showToast({ title: e.message || '创建失败', icon: 'none' })
  } finally {
    submitting.value = false
  }
}

const handleDeactivate = (row: RoleDto) => {
  uni.showModal({
    title: '提示',
    content: `确定停用角色「${row.name}」吗？`,
    success: async (res) => {
      if (!res.confirm) return
      try {
        await deactivateRole(row.name)
        uni.showToast({ title: '已停用', icon: 'success' })
        fetchRoles()
      } catch (e: any) {
        uni.showToast({ title: e.message || '操作失败', icon: 'none' })
      }
    }
  })
}

const handleActivate = (row: RoleDto) => {
  uni.showModal({
    title: '提示',
    content: `确定启用角色「${row.name}」吗？`,
    success: async (res) => {
      if (!res.confirm) return
      try {
        await activateRole(row.name)
        uni.showToast({ title: '已启用', icon: 'success' })
        fetchRoles()
      } catch (e: any) {
        uni.showToast({ title: e.message || '操作失败', icon: 'none' })
      }
    }
  })
}

const handleHardDelete = (row: RoleDto) => {
  uni.showModal({
    title: '警告',
    content: `确定删除角色「${row.name}」吗？删除后不可恢复`,
    success: async (res) => {
      if (!res.confirm) return
      try {
        await hardDeleteRole(row.name)
        uni.showToast({ title: '删除成功', icon: 'success' })
        fetchRoles()
      } catch (e: any) {
        uni.showToast({ title: e.message || '删除失败', icon: 'none' })
      }
    }
  })
}
</script>

<style scoped>
.page {
  padding: 20rpx;
  padding-bottom: 160rpx;
}

.role-card {
  background: #fff;
  border-radius: 16rpx;
  padding: 24rpx;
  margin-bottom: 20rpx;
}

.role-main {
  margin-bottom: 20rpx;
}

.role-name-line {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 12rpx;
  margin-bottom: 8rpx;
}

.role-name {
  font-size: 32rpx;
  font-weight: bold;
}

.role-label {
  font-size: 24rpx;
  color: #666;
}

.builtin-tag {
  font-size: 22rpx;
  color: #e6a23c;
  background: #fdf6ec;
  padding: 4rpx 16rpx;
  border-radius: 8rpx;
}

.status-on {
  font-size: 22rpx;
  color: #07c160;
  background: #e8f8ee;
  padding: 4rpx 16rpx;
  border-radius: 8rpx;
}

.status-off {
  font-size: 22rpx;
  color: #909399;
  background: #f2f3f5;
  padding: 4rpx 16rpx;
  border-radius: 8rpx;
}

.role-count {
  font-size: 26rpx;
  color: #999;
}

.role-actions {
  display: flex;
  gap: 12rpx;
}

.empty {
  text-align: center;
  color: #999;
  font-size: 26rpx;
  padding: 40rpx 0;
}

.fab {
  position: fixed;
  right: 40rpx;
  bottom: 160rpx;
  width: 100rpx;
  height: 100rpx;
  border-radius: 50%;
  background: #1989fa;
  color: #fff;
  font-size: 56rpx;
  line-height: 100rpx;
  text-align: center;
  box-shadow: 0 8rpx 24rpx rgba(25, 137, 250, 0.4);
}

.mask {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 999;
}

.dialog {
  width: 640rpx;
  max-height: 80vh;
  overflow-y: auto;
  background: #fff;
  border-radius: 16rpx;
  padding: 32rpx;
}

.dialog-title {
  font-size: 34rpx;
  font-weight: bold;
  text-align: center;
  margin-bottom: 32rpx;
}

.form-item {
  display: flex;
  align-items: center;
  margin-bottom: 24rpx;
}

.label {
  width: 140rpx;
  font-size: 28rpx;
  color: #333;
}

.input {
  flex: 1;
  height: 72rpx;
  padding: 0 20rpx;
  border: 1rpx solid #eee;
  border-radius: 8rpx;
  font-size: 28rpx;
}

.dialog-actions {
  display: flex;
  gap: 20rpx;
  margin-top: 8rpx;
}

.dialog-btn {
  flex: 1;
  height: 72rpx;
  line-height: 72rpx;
  font-size: 28rpx;
  border-radius: 8rpx;
}

.dialog-btn.primary {
  background: #1989fa;
  color: #fff;
}
</style>
