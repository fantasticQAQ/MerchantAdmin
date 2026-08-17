<template>
  <div class="user-list">
    <el-card shadow="never" class="table-card">
      <template #header>
        <div class="card-header">
          <span>用户管理</span>
          <el-button type="primary" @click="openCreateDialog">
            <el-icon><Plus /></el-icon> 新增用户
          </el-button>
        </div>
      </template>

      <el-table :data="tableData" v-loading="loading" border stripe style="width: 100%">
        <el-table-column type="index" label="序号" width="60" align="center" />
        <el-table-column prop="id" label="用户ID" width="100" align="center" />
        <el-table-column prop="userName" label="用户名" min-width="140" />
        <el-table-column prop="email" label="邮箱" min-width="180" />
        <el-table-column label="角色" min-width="140" align="center">
          <template #default="{ row }">
            <el-tag
              v-for="role in row.roles"
              :key="role"
              :type="roleTagType(role)"
              style="margin-right: 4px"
            >
              {{ roleLabel(role) }}
            </el-tag>
            <span v-if="row.roles.length === 0" style="color: #909399">-</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="300" align="center" fixed="right">
          <template #default="{ row }">
            <el-button
              type="primary"
              size="small"
              @click="openEditDialog(row)"
              :disabled="row.userName === 'admin'"
            >
              <el-icon><Edit /></el-icon> 编辑
            </el-button>
            <el-button
              type="warning"
              size="small"
              @click="openResetPwdDialog(row)"
              :disabled="row.userName === 'admin'"
            >
              <el-icon><Key /></el-icon> 重置密码
            </el-button>
            <el-button
              type="danger"
              size="small"
              @click="handleDelete(row)"
              :disabled="row.userName === 'admin'"
            >
              <el-icon><Delete /></el-icon> 删除
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 新增用户弹窗 -->
    <el-dialog v-model="dialogVisible" :title="dialogTitle" width="480px" @close="resetForm">
      <el-form :model="formData" :rules="rules" ref="formRef" label-width="90px">
        <el-form-item label="用户名" prop="userName">
          <el-input v-model="formData.userName" placeholder="请输入用户名" :disabled="!!editingId" />
        </el-form-item>
        <el-form-item label="邮箱" prop="email">
          <el-input v-model="formData.email" placeholder="请输入邮箱" />
        </el-form-item>
        <el-form-item v-if="!editingId" label="密码" prop="password">
          <el-input v-model="formData.password" type="password" placeholder="至少 6 位" show-password />
        </el-form-item>
        <el-form-item v-if="!editingId" label="确认密码" prop="confirmPassword">
          <el-input v-model="formData.confirmPassword" type="password" placeholder="请再次输入密码" show-password />
        </el-form-item>
        <el-form-item label="角色" prop="roles">
          <el-select v-model="formData.roles" placeholder="请选择角色（可多选）" multiple style="width: 100%">
            <el-option v-for="r in roleList" :key="r" :label="roleLabel(r)" :value="r" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSubmit" :loading="submitting">
          确定
        </el-button>
      </template>
    </el-dialog>

    <!-- 重置密码弹窗 -->
    <el-dialog v-model="resetPwdVisible" title="重置密码" width="400px" @close="resetPwdForm">
      <el-form :model="resetPwdForm" ref="resetPwdFormRef" label-width="90px">
        <el-form-item label="用户">{{ resetPwdTarget?.userName }}</el-form-item>
        <el-form-item label="新密码">
          <el-input v-model="resetPwdForm.newPassword" type="password" placeholder="至少 6 位" show-password />
        </el-form-item>
        <el-form-item label="确认密码">
          <el-input v-model="resetPwdForm.confirmPassword" type="password" placeholder="请再次输入密码" show-password />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="resetPwdVisible = false">取消</el-button>
        <el-button type="primary" @click="handleResetPwd" :loading="resetPwdSubmitting">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, reactive } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Edit, Delete, Key } from '@element-plus/icons-vue'
import { getUsers, getRoles, createUser, updateUser, deleteUser, resetPassword, type UserDto } from '@/api/user'

const tableData = ref<UserDto[]>([])
const roleList = ref<string[]>([])
const loading = ref(false)

// 弹窗
const dialogVisible = ref(false)
const dialogTitle = ref('新增用户')
const editingId = ref(0)
const formRef = ref()
const submitting = ref(false)
const formData = reactive({ userName: '', email: '', password: '', confirmPassword: '', roles: [] as string[] })

const validateConfirmPassword = (_rule: unknown, value: string, callback: (error?: Error) => void) => {
  if (value !== formData.password) {
    callback(new Error('两次输入的密码不一致'))
  } else {
    callback()
  }
}

const rules = {
  userName: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  email: [
    { required: true, message: '请输入邮箱', trigger: 'blur' },
    { type: 'email', message: '邮箱格式不正确', trigger: 'blur' }
  ],
  password: [{ required: true, min: 6, message: '密码至少 6 位', trigger: 'blur' }],
  confirmPassword: [{ required: true, validator: validateConfirmPassword, trigger: 'blur' }]
}

const roleLabel = (r: string) => {
  const map: Record<string, string> = { SuperAdmin: '超级管理员', Admin: '管理员', Operator: '运营' }
  return map[r] || r
}
// 角色颜色统一：超管红、管理员/运营橙、自定义蓝
const roleTagType = (r: string) => {
  if (r === 'SuperAdmin') return 'danger'
  if (r === 'Admin' || r === 'Operator') return 'warning'
  return 'primary'
}

const fetchUsers = async () => {
  loading.value = true
  try {
    tableData.value = await getUsers()
  } catch (e) {
    ElMessage.error('加载用户失败')
    console.error(e)
  } finally {
    loading.value = false
  }
}

const fetchRoles = async () => {
  try {
    const res = await getRoles()
    // 只显示启用中的角色；超级管理员不参与普通分配
    roleList.value = res
      .filter(r => r.isActive && r.name !== 'SuperAdmin')
      .map(r => r.name)
  } catch (e) {
    console.error(e)
  }
}

// 重置密码
const resetPwdVisible = ref(false)
const resetPwdTarget = ref<UserDto | null>(null)
const resetPwdFormRef = ref()
const resetPwdSubmitting = ref(false)
const resetPwdForm = reactive({ newPassword: '', confirmPassword: '' })

const openResetPwdDialog = (row: UserDto) => {
  resetPwdTarget.value = row
  resetPwdForm.newPassword = ''
  resetPwdForm.confirmPassword = ''
  resetPwdVisible.value = true
}

const resetPwdFormClear = () => { resetPwdFormRef.value?.resetFields() }

const handleResetPwd = async () => {
  if (!resetPwdTarget.value || resetPwdForm.newPassword.length < 6) {
    ElMessage.warning('新密码至少 6 位')
    return
  }
  if (resetPwdForm.newPassword !== resetPwdForm.confirmPassword) {
    ElMessage.warning('两次输入的密码不一致')
    return
  }
  resetPwdSubmitting.value = true
  try {
    await resetPassword(resetPwdTarget.value.id, resetPwdForm.newPassword)
    ElMessage.success('密码已重置')
    resetPwdVisible.value = false
  } catch (e: any) {
    ElMessage.error(e.message || '重置失败')
    console.error(e)
  } finally {
    resetPwdSubmitting.value = false
  }
}

const openCreateDialog = () => {
  dialogTitle.value = '新增用户'
  editingId.value = 0
  formData.userName = ''
  formData.email = ''
  formData.password = ''
  formData.confirmPassword = ''
  formData.roles = []
  dialogVisible.value = true
}

const openEditDialog = (row: UserDto) => {
  dialogTitle.value = '编辑用户'
  editingId.value = row.id
  formData.userName = row.userName
  formData.email = row.email
  formData.password = ''
  formData.roles = [...row.roles]
  dialogVisible.value = true
}

const handleSubmit = async () => {
  await formRef.value?.validate()
  submitting.value = true
  try {
    if (editingId.value) {
      await updateUser(editingId.value, { email: formData.email, roles: formData.roles })
      ElMessage.success('更新成功')
    } else {
      await createUser({
        userName: formData.userName,
        email: formData.email,
        password: formData.password,
        roles: formData.roles
      })
      ElMessage.success('创建成功')
    }
    dialogVisible.value = false
    fetchUsers()
  } catch (e: any) {
    ElMessage.error(e.message || '操作失败')
    console.error(e)
  } finally {
    submitting.value = false
  }
}

const handleDelete = async (row: UserDto) => {
  try {
    await ElMessageBox.confirm(`确定删除用户「${row.userName}」吗？`, '警告', { type: 'error' })
    await deleteUser(row.id)
    ElMessage.success('删除成功')
    fetchUsers()
  } catch (e: any) {
    if (e !== 'cancel') {
      ElMessage.error(e.message || '删除失败')
      console.error(e)
    }
  }
}

const resetForm = () => { formRef.value?.resetFields() }

onMounted(() => {
  fetchUsers()
  fetchRoles()
})
</script>

<style scoped>
.table-card { min-height: 400px; }
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
</style>
