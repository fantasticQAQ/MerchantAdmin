<template>
  <div class="layout">
    <el-container>
      <!-- 顶部导航 -->
      <el-header class="layout-header">
        <div class="logo">🛒 商城管理系统</div>
        <div class="header-right">
          <el-dropdown>
            <span class="user-info">
              <el-icon><User /></el-icon>
              {{ userStore.userName || '管理员' }}
              <el-tag v-if="userStore.isSuperAdmin" size="small" type="danger" style="margin-left: 6px">超级管理员</el-tag>
              <el-tag v-else-if="userStore.isAdmin" size="small" type="danger" style="margin-left: 6px">管理员</el-tag>
              <el-tag v-else-if="userStore.isOperator" size="small" type="warning" style="margin-left: 6px">运营</el-tag>
              <el-icon><ArrowDown /></el-icon>
            </span>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item @click="openChangePassword">修改密码</el-dropdown-item>
                <el-dropdown-item divided @click="handleLogout">退出登录</el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </el-header>

      <el-container>
        <!-- 左侧菜单 -->
        <el-aside width="220px" class="layout-aside">
          <el-menu
            :default-active="route.path"
            router
            class="side-menu"
            background-color="#304156"
            text-color="#bfcbd9"
            active-text-color="#409eff"
          >
            <el-menu-item
              v-for="item in menuItems"
              :key="item.path"
              :index="item.path"
              @click="router.push(item.path)"
            >
              <el-icon><component :is="item.icon" /></el-icon>
              <span>{{ item.title }}</span>
            </el-menu-item>
          </el-menu>
        </el-aside>

        <!-- 主内容区 -->
        <el-main class="layout-main">
          <router-view />
        </el-main>
      </el-container>
    </el-container>

    <!-- 修改密码弹窗 -->
    <el-dialog v-model="pwdDialogVisible" title="修改密码" width="420px" @close="resetPwdForm">
      <el-form :model="pwdForm" :rules="pwdRules" ref="pwdFormRef" label-width="90px">
        <el-form-item label="原密码" prop="oldPassword">
          <el-input v-model="pwdForm.oldPassword" type="password" show-password />
        </el-form-item>
        <el-form-item label="新密码" prop="newPassword">
          <el-input v-model="pwdForm.newPassword" type="password" show-password />
        </el-form-item>
        <el-form-item label="确认新密码" prop="confirmPassword">
          <el-input v-model="pwdForm.confirmPassword" type="password" show-password />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="pwdDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleChangePassword" :loading="pwdSubmitting">
          确定
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { computed, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { DataAnalysis, Goods, Tickets, User, ArrowDown, Setting, Lock } from '@element-plus/icons-vue'
import { useUserStore } from '@/store/user'
import { changePassword } from '@/api/auth'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()

// 菜单按角色显示：用户管理/角色管理/操作日志仅 Admin 可见
const menuItems = computed(() => {
  const items = [
    { path: '/dashboard', title: '仪表盘', icon: 'DataAnalysis' },
    { path: '/products', title: '商品管理', icon: 'Goods' },
    { path: '/orders', title: '订单管理', icon: 'Tickets' }
  ]
  if (userStore.isAdmin) {
    items.push(
      { path: '/users', title: '用户管理', icon: 'User' },
      { path: '/roles', title: '角色管理', icon: 'Setting' },
      { path: '/logs', title: '操作日志', icon: 'Lock' }
    )
  }
  return items
})

const handleLogout = () => {
  userStore.logout()
  router.push('/login')
}

// 修改密码
const pwdDialogVisible = ref(false)
const pwdSubmitting = ref(false)
const pwdFormRef = ref()
const pwdForm = reactive({ oldPassword: '', newPassword: '', confirmPassword: '' })

const validateConfirm = (_rule, value, callback) => {
  if (value !== pwdForm.newPassword) {
    callback(new Error('两次输入的密码不一致'))
  } else {
    callback()
  }
}

const pwdRules = {
  oldPassword: [{ required: true, message: '请输入原密码', trigger: 'blur' }],
  newPassword: [{ required: true, min: 6, message: '新密码至少 6 位', trigger: 'blur' }],
  confirmPassword: [{ required: true, validator: validateConfirm, trigger: 'blur' }]
}

const openChangePassword = () => {
  pwdForm.oldPassword = ''
  pwdForm.newPassword = ''
  pwdForm.confirmPassword = ''
  pwdDialogVisible.value = true
}

const resetPwdForm = () => { pwdFormRef.value?.resetFields() }

const handleChangePassword = async () => {
  await pwdFormRef.value?.validate()
  pwdSubmitting.value = true
  try {
    await changePassword(pwdForm.oldPassword, pwdForm.newPassword)
    ElMessage.success('密码修改成功，请重新登录')
    pwdDialogVisible.value = false
    handleLogout()
  } catch (e) {
    ElMessage.error(e.message || '修改失败')
    console.error(e)
  } finally {
    pwdSubmitting.value = false
  }
}
</script>

<style scoped>
.layout {
  min-height: 100vh;
}
.layout-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: #409eff;
  color: #fff;
  padding: 0 20px;
  height: 60px;
}
.logo {
  font-size: 20px;
  font-weight: bold;
}
.user-info {
  color: #fff;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 4px;
}
.layout-aside {
  background: #304156;
  min-height: calc(100vh - 60px);
}
.side-menu {
  border-right: none;
}
.layout-main {
  background: #f0f2f5;
  padding: 20px;
  min-height: calc(100vh - 60px);
}
</style>
