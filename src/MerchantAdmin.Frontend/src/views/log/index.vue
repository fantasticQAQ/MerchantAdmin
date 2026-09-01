<template>
  <div class="log-list">
    <el-card shadow="never" class="table-card">
      <template #header>
        <span>操作日志</span>
      </template>

      <el-table :data="tableData" v-loading="loading" border stripe style="width: 100%">
        <el-table-column prop="id" label="ID" width="80" align="center" />
        <el-table-column prop="userName" label="操作人" width="140" />
        <el-table-column label="操作" width="220">
          <template #default="{ row }">{{ actionLabel(row.action) }}</template>
        </el-table-column>
        <el-table-column prop="detail" label="详情" min-width="300" show-overflow-tooltip />
        <el-table-column prop="createdAt" label="时间" width="180">
          <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
        </el-table-column>
      </el-table>

      <div class="pagination">
        <el-pagination
          v-model:current-page="pagination.page"
          v-model:page-size="pagination.pageSize"
          :total="pagination.total"
          :page-sizes="[10, 20, 50]"
          layout="total, sizes, prev, pager, next, jumper"
          @change="fetchLogs"
        />
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, reactive } from 'vue'
import { ElMessage } from 'element-plus'
import { getLogs, type LogDto } from '@/api/log'

const tableData = ref<LogDto[]>([])
const loading = ref(false)
const pagination = reactive({ page: 1, pageSize: 20, total: 0 })

const actionLabel = (action: string) => {
  const map: Record<string, string> = {
    CreateProductCommand: '创建商品',
    UpdateProductCommand: '更新商品',
    DeleteProductCommand: '删除商品',
    CreateOrderCommand: '创建订单',
    PayOrderCommand: '发起支付',
    CancelOrderCommand: '取消订单',
    RefundOrderCommand: '订单退款',
    DeleteOrderCommand: '删除订单'
  }
  return map[action] || action
}

const formatTime = (iso: string) => {
  if (!iso) return '-'
  return new Date(iso).toLocaleString('zh-CN', { hour12: false })
}

const fetchLogs = async () => {
  loading.value = true
  try {
    const res = await getLogs({ page: pagination.page, pageSize: pagination.pageSize })
    tableData.value = res.items
    pagination.total = res.total
  } catch (e) {
    ElMessage.error('加载日志失败')
    console.error(e)
  } finally {
    loading.value = false
  }
}

onMounted(fetchLogs)
</script>

<style scoped>
.table-card { min-height: 400px; }
.pagination { margin-top: 16px; display: flex; justify-content: flex-end; }
</style>
