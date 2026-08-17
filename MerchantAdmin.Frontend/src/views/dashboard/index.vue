<template>
  <div class="dashboard">
    <el-card shadow="never" class="dashboard-card">
      <template #header>
        <span>数据概览</span>
      </template>

      <div class="stat-grid">
        <div class="stat-item">
          <div class="stat-label">商品总数</div>
          <div class="stat-value">{{ dashboard?.productCount ?? 0 }}</div>
        </div>
        <div class="stat-item">
          <div class="stat-label">订单总数</div>
          <div class="stat-value">{{ dashboard?.orderCount ?? 0 }}</div>
        </div>
        <div class="stat-item">
          <div class="stat-label">已支付订单</div>
          <div class="stat-value" style="color: #67c23a">{{ dashboard?.paidOrderCount ?? 0 }}</div>
        </div>
        <div class="stat-item">
          <div class="stat-label">待处理订单</div>
          <div class="stat-value" style="color: #e6a23c">{{ dashboard?.pendingOrderCount ?? 0 }}</div>
        </div>
        <div class="stat-item">
          <div class="stat-label">总销售额</div>
          <div class="stat-value" style="color: #f56c6c">¥{{ (dashboard?.totalSales ?? 0).toFixed(2) }}</div>
        </div>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { getDashboard, type DashboardDto } from '@/api/dashboard'

const dashboard = ref<DashboardDto | null>(null)

const fetchDashboard = async () => {
  try {
    dashboard.value = await getDashboard()
  } catch (e) {
    console.error(e)
  }
}

onMounted(fetchDashboard)
</script>

<style scoped>
.stat-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 20px;
}
.stat-item {
  background: #f5f7fa;
  border-radius: 8px;
  padding: 24px;
  text-align: center;
}
.stat-label {
  color: #909399;
  font-size: 14px;
  margin-bottom: 12px;
}
.stat-value {
  font-size: 28px;
  font-weight: bold;
  color: #303133;
}
</style>
