<template>
  <div class="role-list">
    <el-card shadow="never" class="table-card">
      <template #header>
        <div class="card-header">
          <span>角色管理</span>
          <el-button type="primary" @click="dialogVisible = true">
            <el-icon><Plus /></el-icon> 新建角色
          </el-button>
        </div>
      </template>

      <el-table :data="tableData" v-loading="loading" border stripe style="width: 100%">
        <el-table-column type="index" label="序号" width="60" align="center" />
        <el-table-column prop="name" label="角色名" min-width="180">
          <template #default="{ row }">
            <el-tag :type="roleTagType(row.name)">{{ roleLabel(row.name) }}</el-tag>
            <el-tag v-if="!row.isActive" type="info" size="small" style="margin-left: 6px">已停用</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="userCount" label="用户数" width="120" align="center" />
        <el-table-column label="操作" width="220" align="center">
          <template #default="{ row }">
            <!-- 内置角色不可操作 -->
            <el-button
              v-if="row.name === 'Admin' || row.name === 'SuperAdmin' || row.name === 'Operator'"
              type="info"
              size="small"
              disabled
            >
              系统内置
            </el-button>
            <template v-else>
              <!-- 启用/停用切换 -->
              <el-button
                v-if="row.isActive"
                type="warning"
                size="small"
                @click="handleDelete(row)"
              >
                停用
              </el-button>
              <el-button
                v-else
                type="success"
                size="small"
                @click="handleActivate(row)"
              >
                启用
              </el-button>
              <!-- 硬删除 -->
              <el-button type="danger" size="small" @click="handleHardDelete(row)">
                删除
              </el-button>
            </template>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 新建角色弹窗 -->
    <el-dialog v-model="dialogVisible" title="新建角色" width="400px">
      <el-form label-width="80px" @submit.prevent>
        <el-form-item label="角色名">
          <el-input v-model="roleName" placeholder="请输入角色名" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleCreate" :loading="submitting">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Delete } from '@element-plus/icons-vue'
import { getRoles, createRole, deleteRole, activateRole, deleteRoleHard, type RoleDto } from '@/api/user'

const tableData = ref<RoleDto[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const roleName = ref('')
const submitting = ref(false)

const roleLabel = (r: string) => {
  const map: Record<string, string> = { SuperAdmin: '超级管理员', Admin: '管理员', Operator: '运营' }
  return map[r] || r
}
const roleTagType = (r: string) => {
  // 角色颜色统一：超管红、管理员/运营橙、自定义蓝
  if (r === 'SuperAdmin') return 'danger'
  if (r === 'Admin' || r === 'Operator') return 'warning'
  return 'primary'
}

const fetchRoles = async () => {
  loading.value = true
  try {
    tableData.value = await getRoles()
  } catch (e) {
    ElMessage.error('加载角色失败')
    console.error(e)
  } finally {
    loading.value = false
  }
}

const handleCreate = async () => {
  if (!roleName.value.trim()) {
    ElMessage.warning('请输入角色名')
    return
  }
  submitting.value = true
  try {
    await createRole(roleName.value.trim())
    ElMessage.success('创建成功')
    dialogVisible.value = false
    roleName.value = ''
    fetchRoles()
  } catch (e: any) {
    ElMessage.error(e.message || '创建失败')
    console.error(e)
  } finally {
    submitting.value = false
  }
}

const handleDelete = async (row: RoleDto) => {
  try {
    await ElMessageBox.confirm(`确定停用角色「${row.name}」吗？停用后不可再分配`, '警告', { type: 'error' })
    await deleteRole(row.name)
    ElMessage.success('角色已停用')
    fetchRoles()
  } catch (e) {
    if (e !== 'cancel') console.error(e)
  }
}

const handleActivate = async (row: RoleDto) => {
  try {
    await activateRole(row.name)
    ElMessage.success('角色已启用')
    fetchRoles()
  } catch (e: any) {
    ElMessage.error(e.message || '启用失败')
    console.error(e)
  }
}

const handleHardDelete = async (row: RoleDto) => {
  try {
    await ElMessageBox.confirm(
      `确定永久删除角色「${row.name}」吗？\n删除后不可恢复！`,
      '危险操作',
      { type: 'error', confirmButtonText: '确认删除', confirmButtonClass: 'el-button--danger' }
    )
    await deleteRoleHard(row.name)
    ElMessage.success('角色已删除')
    fetchRoles()
  } catch (e: any) {
    if (e !== 'cancel') ElMessage.error(e.message || '删除失败')
    console.error(e)
  }
}

onMounted(fetchRoles)
</script>

<style scoped>
.table-card { min-height: 400px; }
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
</style>
