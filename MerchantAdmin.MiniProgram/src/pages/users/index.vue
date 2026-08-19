<template>
  <view class="page">
    <TabBar current="/pages/users/index" />
    <!-- 用户列表 -->
    <view class="user-list">
      <view v-for="item in list" :key="item.id" class="user-card">
        <view class="user-main">
          <view class="user-name">{{ item.userName }}</view>
          <view class="user-email">{{ item.email || '—' }}</view>
          <view class="user-roles">
            <text v-for="r in item.roles" :key="r" class="role-tag">{{ roleLabel(r) }}</text>
          </view>
        </view>
        <view class="user-actions">
          <button size="mini" @click="openEdit(item)">编辑</button>
          <button size="mini" @click="openResetPwd(item)">重置密码</button>
          <button size="mini" type="warn" @click="handleDelete(item)">删除</button>
        </view>
      </view>

      <view v-if="!loading && list.length === 0" class="empty">暂无用户</view>
      <view v-if="loading && list.length === 0" class="loading-mask">
        <view class="loading-spinner"></view>
        <text class="loading-text">加载中...</text>
      </view>
    </view>

    <!-- 新增按钮 -->
    <view class="fab" @click="openCreate">+</view>

    <!-- 新增用户弹窗 -->
    <view v-if="createVisible" class="mask" @click="createVisible = false">
      <view class="dialog" @click.stop>
        <view class="dialog-title">新增用户</view>
        <view class="form-item">
          <text class="label">用户名</text>
          <input class="input" v-model="createForm.userName" placeholder="请输入用户名" />
        </view>
        <view class="form-item">
          <text class="label">邮箱</text>
          <input class="input" v-model="createForm.email" placeholder="请输入邮箱" />
        </view>
        <view class="form-item">
          <text class="label">密码</text>
          <input class="input" v-model="createForm.password" password placeholder="请输入密码" />
        </view>
        <view class="role-select">
          <text class="role-select-title">分配角色</text>
          <checkbox-group @change="onCreateRoleChange">
            <label v-for="r in activeRoles" :key="r.name" class="role-check">
              <checkbox :value="r.name" :checked="createForm.roles.includes(r.name)" />
              <text class="role-check-text">{{ roleLabel(r.name) }}</text>
            </label>
          </checkbox-group>
        </view>
        <view class="dialog-actions">
          <button class="dialog-btn" @click="createVisible = false">取消</button>
          <button class="dialog-btn primary" :loading="submitting" @click="handleCreate">确定</button>
        </view>
      </view>
    </view>

    <!-- 编辑用户弹窗 -->
    <view v-if="editVisible" class="mask" @click="editVisible = false">
      <view class="dialog" @click.stop>
        <view class="dialog-title">编辑用户</view>
        <view class="form-item">
          <text class="label">用户名</text>
          <input class="input" :value="editTarget?.userName" disabled />
        </view>
        <view class="form-item">
          <text class="label">邮箱</text>
          <input class="input" v-model="editForm.email" placeholder="请输入邮箱" />
        </view>
        <view class="role-select">
          <text class="role-select-title">分配角色</text>
          <checkbox-group @change="onEditRoleChange">
            <label v-for="r in activeRoles" :key="r.name" class="role-check">
              <checkbox :value="r.name" :checked="editForm.roles.includes(r.name)" />
              <text class="role-check-text">{{ roleLabel(r.name) }}</text>
            </label>
          </checkbox-group>
        </view>
        <view class="dialog-actions">
          <button class="dialog-btn" @click="editVisible = false">取消</button>
          <button class="dialog-btn primary" :loading="submitting" @click="handleEdit">确定</button>
        </view>
      </view>
    </view>

    <!-- 重置密码弹窗 -->
    <view v-if="resetVisible" class="mask" @click="resetVisible = false">
      <view class="dialog" @click.stop>
        <view class="dialog-title">重置密码</view>
        <view class="stock-info">用户：{{ resetTarget?.userName }}</view>
        <view class="form-item">
          <text class="label">新密码</text>
          <input class="input" v-model="resetPwd" password placeholder="请输入新密码" />
        </view>
        <view class="dialog-actions">
          <button class="dialog-btn" @click="resetVisible = false">取消</button>
          <button class="dialog-btn primary" :loading="submitting" @click="handleResetPwd">确定</button>
        </view>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { getUsers, createUser, updateUser, deleteUser, resetPassword, type UserDto } from '@/api/user'
import { getRoles, type RoleDto } from '@/api/role'
import { isAdmin } from '@/utils/auth'
import TabBar from '@/components/tab-bar.vue'

const list = ref<UserDto[]>([])
const loading = ref(false)
const submitting = ref(false)
const loaded = ref(false)

// 角色列表（用于复选，只展示启用中的角色）
const allRoles = ref<RoleDto[]>([])
const activeRoles = ref<RoleDto[]>([])

const roleLabel = (r: string) => {
  const map: Record<string, string> = {
    Admin: '管理员',
    Operator: '操作员',
    SuperAdmin: '超级管理员'
  }
  return map[r] || r
}

// 新增
const createVisible = ref(false)
const createForm = reactive({ userName: '', email: '', password: '', roles: [] as string[] })

// 编辑
const editVisible = ref(false)
const editTarget = ref<UserDto | null>(null)
const editForm = reactive({ email: '', roles: [] as string[] })

// 重置密码
const resetVisible = ref(false)
const resetTarget = ref<UserDto | null>(null)
const resetPwd = ref('')

const fetchUsers = async () => {
  loading.value = true
  try {
    list.value = await getUsers()
  } catch (e: any) {
    uni.showToast({ title: e.message || '加载失败', icon: 'none' })
  } finally {
    loading.value = false
  }
}

const fetchRoles = async () => {
  try {
    allRoles.value = await getRoles()
    activeRoles.value = allRoles.value.filter((r) => r.isActive)
  } catch (e: any) {
    uni.showToast({ title: e.message || '加载角色失败', icon: 'none' })
  }
}

onShow(async () => {
  if (!isAdmin()) {
    uni.showToast({ title: '无权限访问', icon: 'none' })
    uni.reLaunch({ url: '/pages/products/index' })
    return
  }
  if (loaded.value) return
  loaded.value = true
  await Promise.all([fetchUsers(), fetchRoles()])
})

// 新增
const openCreate = () => {
  createForm.userName = ''
  createForm.email = ''
  createForm.password = ''
  createForm.roles = []
  createVisible.value = true
}

const onCreateRoleChange = (e: any) => {
  createForm.roles = e.detail.value
}

const handleCreate = async () => {
  const userName = createForm.userName.trim()
  const email = createForm.email.trim()
  const password = createForm.password
  if (!userName) {
    uni.showToast({ title: '请输入用户名', icon: 'none' })
    return
  }
  if (!password) {
    uni.showToast({ title: '请输入密码', icon: 'none' })
    return
  }

  submitting.value = true
  try {
    const data: any = { userName, email, password }
    if (createForm.roles.length > 0) data.roles = createForm.roles
    await createUser(data)
    uni.showToast({ title: '创建成功', icon: 'success' })
    createVisible.value = false
    fetchUsers()
  } catch (e: any) {
    uni.showToast({ title: e.message || '创建失败', icon: 'none' })
  } finally {
    submitting.value = false
  }
}

// 编辑
const openEdit = (row: UserDto) => {
  editTarget.value = row
  editForm.email = row.email || ''
  editForm.roles = [...row.roles]
  editVisible.value = true
}

const onEditRoleChange = (e: any) => {
  editForm.roles = e.detail.value
}

const handleEdit = async () => {
  if (!editTarget.value) return
  submitting.value = true
  try {
    const data: any = { email: editForm.email.trim() }
    data.roles = editForm.roles
    await updateUser(editTarget.value.id, data)
    uni.showToast({ title: '更新成功', icon: 'success' })
    editVisible.value = false
    fetchUsers()
  } catch (e: any) {
    uni.showToast({ title: e.message || '更新失败', icon: 'none' })
  } finally {
    submitting.value = false
  }
}

// 删除
const handleDelete = (row: UserDto) => {
  uni.showModal({
    title: '警告',
    content: `确定删除用户「${row.userName}」吗？`,
    success: async (res) => {
      if (!res.confirm) return
      try {
        await deleteUser(row.id)
        uni.showToast({ title: '删除成功', icon: 'success' })
        fetchUsers()
      } catch (e: any) {
        uni.showToast({ title: e.message || '删除失败', icon: 'none' })
      }
    }
  })
}

// 重置密码
const openResetPwd = (row: UserDto) => {
  resetTarget.value = row
  resetPwd.value = ''
  resetVisible.value = true
}

const handleResetPwd = async () => {
  if (!resetTarget.value) return
  if (!resetPwd.value) {
    uni.showToast({ title: '请输入新密码', icon: 'none' })
    return
  }
  submitting.value = true
  try {
    await resetPassword(resetTarget.value.id, resetPwd.value)
    uni.showToast({ title: '密码已重置', icon: 'success' })
    resetVisible.value = false
  } catch (e: any) {
    uni.showToast({ title: e.message || '重置失败', icon: 'none' })
  } finally {
    submitting.value = false
  }
}
</script>

<style>
.page {
  padding: 20rpx;
  padding-bottom: 160rpx;
}

.user-card {
  background: #fff;
  border-radius: 16rpx;
  padding: 24rpx;
  margin-bottom: 20rpx;
}

.user-main {
  margin-bottom: 20rpx;
}

.user-name {
  font-size: 32rpx;
  font-weight: bold;
  margin-bottom: 8rpx;
}

.user-email {
  font-size: 26rpx;
  color: #999;
  margin-bottom: 12rpx;
}

.user-roles {
  display: flex;
  flex-wrap: wrap;
  gap: 12rpx;
}

.role-tag {
  font-size: 22rpx;
  color: #1989fa;
  background: #e8f3ff;
  padding: 4rpx 16rpx;
  border-radius: 8rpx;
}

.user-actions {
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

.role-select {
  margin-bottom: 24rpx;
}

.role-select-title {
  display: block;
  font-size: 28rpx;
  color: #333;
  margin-bottom: 16rpx;
}

.role-check {
  display: flex;
  align-items: center;
  padding: 12rpx 0;
}

.role-check-text {
  font-size: 28rpx;
  color: #333;
}

.stock-info {
  font-size: 26rpx;
  color: #666;
  margin-bottom: 24rpx;
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