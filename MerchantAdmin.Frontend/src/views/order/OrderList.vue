<template>
  <div class="order-list">
    <!-- 搜索栏 -->
    <el-card shadow="never" class="search-card">
      <el-form :inline="true" :model="searchForm" ref="searchRef">
        <el-form-item label="订单号" prop="orderNo">
          <el-input
            v-model="searchForm.orderNo"
            placeholder="请输入订单号"
            clearable
            @keyup.enter="handleSearch"
          />
        </el-form-item>
        <el-form-item label="状态" prop="status">
          <el-select v-model="searchForm.status" placeholder="全部" clearable style="width: 140px">
            <el-option label="待支付" value="Created" />
            <el-option label="支付处理中" value="PaymentProcessing" />
            <el-option label="已支付" value="Paid" />
            <el-option label="已退款" value="Refunded" />
            <el-option label="已取消" value="Cancelled" />
            <el-option label="超时关闭" value="TimedOut" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="handleSearch" :loading="loading">
            <el-icon><Search /></el-icon> 搜索
          </el-button>
          <el-button @click="resetSearch">
            <el-icon><Refresh /></el-icon> 重置
          </el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <!-- 操作栏 + 表格 -->
    <el-card shadow="never" class="table-card">
      <div class="toolbar">
        <el-button v-if="userStore.canManage" type="primary" @click="openCreateDialog">
          <el-icon><Plus /></el-icon> 创建订单
        </el-button>
        <el-button v-if="userStore.canManage" type="success" plain @click="handleExport" :loading="exporting">
          <el-icon><Download /></el-icon> 导出订单
        </el-button>
      </div>

      <el-table :data="tableData" v-loading="loading" border stripe style="width: 100%">
        <el-table-column type="index" label="序号" width="60" align="center" />
        <el-table-column prop="orderId" label="订单ID" width="80" align="center" />
        <el-table-column label="商品数" width="80" align="center">
          <template #default="{ row }">
            {{ row.orderItems.length }}
          </template>
        </el-table-column>
        <el-table-column label="总金额" width="120" align="right">
          <template #default="{ row }">
            ¥{{ calculateTotalAmount(row.orderItems).toFixed(2) }}
          </template>
        </el-table-column>
        <el-table-column prop="orderStatus" label="状态" width="110" align="center">
          <template #default="{ row }">
            <el-tag :type="statusTagType(row.orderStatus)">{{ statusLabel(row.orderStatus) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="createdAt" label="创建时间" min-width="180">
          <template #default="{ row }">
            {{ formatTime(row.createdAt) }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="300" align="center" fixed="right">
          <template #default="{ row }">
            <el-button type="info" size="small" @click="openDetail(row)">
              <el-icon><View /></el-icon> 查看
            </el-button>
            <el-button
              v-if="row.orderStatus === 'Created' && userStore.canManage"
              type="success"
              size="small"
              @click="handlePay(row)"
              :loading="row.paying"
            >
              <el-icon><Check /></el-icon> 支付
            </el-button>
            <el-button
              v-if="row.orderStatus === 'Created' && userStore.canManage"
              type="warning"
              size="small"
              @click="handleCancel(row)"
              :loading="row.cancelling"
            >
              <el-icon><Close /></el-icon> 取消
            </el-button>
            <el-button
              v-if="row.orderStatus === 'Paid' && userStore.canManage"
              type="danger"
              size="small"
              @click="handleRefund(row)"
              :loading="row.refunding"
            >
              <el-icon><Money /></el-icon> 退款
            </el-button>
            <el-button
              v-if="['Cancelled', 'Paid', 'Refunded', 'TimedOut'].includes(row.orderStatus) && userStore.isAdmin"
              type="danger"
              size="small"
              @click="handleDelete(row)"
              :loading="row.deleting"
            >
              <el-icon><Delete /></el-icon> 删除
            </el-button>
          </template>
        </el-table-column>
      </el-table>

      <div class="pagination">
        <el-pagination
          v-model:current-page="pagination.page"
          v-model:page-size="pagination.pageSize"
          :total="pagination.total"
          :page-sizes="[10, 20, 50]"
          layout="total, sizes, prev, pager, next, jumper"
          @change="fetchOrders"
        />
      </div>
    </el-card>

    <!-- 创建订单弹窗（支持多商品） -->
    <el-dialog v-model="dialogVisible" title="创建订单" width="640px" @close="resetForm">
      <el-form label-width="80px">
        <div v-for="(item, index) in orderItems" :key="index" class="order-item-row">
          <el-form-item label="商品">
            <el-select
              v-model="item.productId"
              placeholder="请选择商品"
              filterable
              style="width: 280px"
            >
              <el-option
                v-for="p in productList"
                :key="p.productId"
                :label="`${p.name}（库存 ${p.stock}）${p.isActive ? '' : '（已下架）'}`"
                :value="p.productId"
                :disabled="!p.isActive"
              />
            </el-select>
          </el-form-item>
          <el-form-item label="数量" label-width="50px">
            <el-input-number
              v-model="item.quantity"
              :min="1"
              :max="getProductStock(item.productId)"
              style="width: 130px"
            />
          </el-form-item>
          <el-button
            v-if="orderItems.length > 1"
            type="danger"
            plain
            @click="removeOrderItem(index)"
          >
            <el-icon><Delete /></el-icon>
          </el-button>
        </div>

        <el-button type="primary" plain @click="addOrderItem">
          <el-icon><Plus /></el-icon> 添加商品
        </el-button>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSubmit" :loading="submitting">
          确定下单
        </el-button>
      </template>
    </el-dialog>

    <!-- 订单详情弹窗 -->
    <el-dialog v-model="detailVisible" title="订单详情" width="560px">
      <template v-if="detailOrder">
        <el-descriptions :column="2" border>
          <el-descriptions-item label="订单号">{{ detailOrder.orderId }}</el-descriptions-item>
          <el-descriptions-item label="状态">
            <el-tag :type="statusTagType(detailOrder.orderStatus)">
              {{ statusLabel(detailOrder.orderStatus) }}
            </el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="创建时间" :span="2">
            {{ formatTime(detailOrder.createdAt) }}
          </el-descriptions-item>
        </el-descriptions>

        <el-table :data="detailOrder.orderItems" border size="small" style="margin-top: 16px">
          <el-table-column prop="productName" label="商品名称" />
          <el-table-column prop="price" label="单价" width="100" align="right">
            <template #default="{ row }">¥{{ row.price.toFixed(2) }}</template>
          </el-table-column>
          <el-table-column prop="quantity" label="数量" width="80" align="center" />
          <el-table-column label="小计" width="110" align="right">
            <template #default="{ row }">¥{{ (row.price * row.quantity).toFixed(2) }}</template>
          </el-table-column>
        </el-table>

        <div class="order-total">
          总金额：<span class="total-amount">¥{{ calculateTotalAmount(detailOrder.orderItems).toFixed(2) }}</span>
        </div>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, reactive } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Search, Refresh, Plus, Check, Close, View, Delete, Money, Download } from '@element-plus/icons-vue'
import {
  getOrders, createOrder, cancelOrder, payOrder, refundOrder, deleteOrder, exportOrders,
  type OrderDto, type OrderItemDto, type OrderQueryParams
} from '@/api/order'
import { getProducts, type ProductDto } from '@/api/product'
import { useUserStore } from '@/store/user'

const userStore = useUserStore()

// 表格行类型：在 OrderDto 基础上附加 UI 临时 loading 状态
type OrderRow = OrderDto & {
  paying?: boolean
  cancelling?: boolean
  refunding?: boolean
  deleting?: boolean
}

// 创建订单的明细行
interface OrderItemRow {
  productId: number
  quantity: number
}

// 搜索
const searchForm = reactive({ orderNo: '', status: '' })
const searchRef = ref()

// 表格
const tableData = ref<OrderRow[]>([])
const loading = ref(false)
const pagination = reactive({ page: 1, pageSize: 10, total: 0 })

// 创建订单弹窗
const dialogVisible = ref(false)
const submitting = ref(false)
const productList = ref<ProductDto[]>([])
const orderItems = ref<OrderItemRow[]>([{ productId: 0, quantity: 1 }])

// 详情弹窗
const detailVisible = ref(false)
const detailOrder = ref<OrderDto | null>(null)

// 状态映射
const statusLabel = (s: string) => {
  const map: Record<string, string> = { Created: '待支付', PaymentProcessing: '支付处理中', Paid: '已支付', Refunded: '已退款', Cancelled: '已取消', TimedOut: '超时关闭' }
  return map[s] || s
}
const statusTagType = (s: string) => {
  const map: Record<string, string> = { Created: 'info', PaymentProcessing: 'warning', Paid: 'success', Refunded: 'danger', Cancelled: 'info', TimedOut: 'info' }
  return map[s] || ''
}

// 计算订单总金额
const calculateTotalAmount = (items: OrderItemDto[]) => {
  return items.reduce((sum, item) => sum + item.price * item.quantity, 0)
}

// 格式化时间
const formatTime = (iso: string) => {
  if (!iso) return '-'
  const d = new Date(iso)
  return d.toLocaleString('zh-CN', { hour12: false })
}

// 获取订单列表（分页 + 搜索）
const fetchOrders = async () => {
  loading.value = true
  try {
    const params: OrderQueryParams = {
      page: pagination.page,
      pageSize: pagination.pageSize
    }
    if (searchForm.orderNo) {
      const num = Number(searchForm.orderNo)
      if (!isNaN(num)) params.orderId = num
    }
    if (searchForm.status) params.status = searchForm.status

    const res = await getOrders(params)
    tableData.value = res.items
    pagination.total = res.total
  } catch (e) {
    ElMessage.error('加载订单失败')
    console.error(e)
  } finally {
    loading.value = false
  }
}

const handleSearch = () => { pagination.page = 1; fetchOrders() }
const resetSearch = () => { searchRef.value?.resetFields(); handleSearch() }

// 导出订单
const exporting = ref(false)
const handleExport = async () => {
  exporting.value = true
  try {
    const ok = await exportOrders(searchForm.status || undefined)
    if (ok) ElMessage.success('导出成功')
  } catch (e: any) {
    ElMessage.error(e.message || '导出失败')
    console.error(e)
  } finally {
    exporting.value = false
  }
}

// 商品列表（弹窗内下拉）
const fetchProductList = async () => {
  try {
    const res = await getProducts({ page: 1, pageSize: 100 })
    productList.value = res.items
  } catch (e) {
    console.error(e)
  }
}

const getProductStock = (productId: number) => {
  const p = productList.value.find(x => x.productId === productId)
  return p?.stock ?? 9999
}

// 创建订单弹窗操作
const addOrderItem = () => orderItems.value.push({ productId: 0, quantity: 1 })
const removeOrderItem = (index: number) => orderItems.value.splice(index, 1)

const openCreateDialog = async () => {
  await fetchProductList()
  orderItems.value = [{ productId: 0, quantity: 1 }]
  dialogVisible.value = true
}

const resetForm = () => { orderItems.value = [{ productId: 0, quantity: 1 }] }

// 提交创建订单
const handleSubmit = async () => {
  const items = orderItems.value
    .filter(i => i.productId > 0 && i.quantity > 0)

  if (items.length === 0) {
    ElMessage.warning('请至少选择一个商品')
    return
  }

  const invalid = orderItems.value.some(i => i.productId <= 0 || i.quantity <= 0)
  if (invalid) {
    ElMessage.warning('请完善所有商品和数量')
    return
  }

  submitting.value = true
  try {
    const orderItemsDto = items.map(i => {
      const p = productList.value.find(x => x.productId === i.productId)!
      return { productId: p.productId, productName: p.name, price: p.price, quantity: i.quantity }
    })

    await createOrder({ orderItems: orderItemsDto })
    ElMessage.success('订单创建成功')
    dialogVisible.value = false
    fetchOrders()
  } catch (e: any) {
    ElMessage.error(e.message || '创建订单失败')
    console.error(e)
  } finally {
    submitting.value = false
  }
}

// 查看订单详情
const openDetail = (row: OrderRow) => {
  detailOrder.value = row
  detailVisible.value = true
}

// 轮询订单状态直到变为终态（Paid/Refunded/Cancelled）或超时
const pollOrderStatus = async (orderId: number) => {
  const maxSeconds = 20
  const intervalMs = 2000
  for (let i = 0; i < maxSeconds * 1000 / intervalMs; i++) {
    await new Promise(r => setTimeout(r, intervalMs))
    try {
      const res = await getOrders({ orderId, page: 1, pageSize: 1 })
      const order = res.items[0]
      if (order && order.orderStatus !== 'PaymentProcessing' && order.orderStatus !== 'Created') {
        return order.orderStatus
      }
    } catch {
      // 轮询失败继续下一次
    }
  }
  return null
}

// 支付
const handlePay = async (row: OrderRow) => {
  try {
    await ElMessageBox.confirm(`确定支付订单「${row.orderId}」吗？`, '提示', { type: 'info' })
    row.paying = true
    await payOrder(row.orderId)
    ElMessage.success('支付成功，正在处理中...')

    // 轮询等待支付闭环完成，自动更新订单状态
    const finalStatus = await pollOrderStatus(row.orderId)
    if (finalStatus) {
      ElMessage.success(`支付完成，订单状态：${statusLabel(finalStatus)}`)
    }
    fetchOrders()
  } catch (e) {
    if (e !== 'cancel') console.error(e)
  } finally {
    row.paying = false
  }
}

// 退款
const handleRefund = async (row: OrderRow) => {
  try {
    await ElMessageBox.confirm(`确定对订单「${row.orderId}」发起退款吗？退款后库存将回补`, '退款确认', { type: 'warning' })
    row.refunding = true
    await refundOrder(row.orderId)
    ElMessage.success('退款成功')
    fetchOrders()
  } catch (e) {
    if (e !== 'cancel') console.error(e)
  } finally {
    row.refunding = false
  }
}

// 取消
const handleCancel = async (row: OrderRow) => {
  try {
    await ElMessageBox.confirm(`确定取消订单「${row.orderId}」吗？`, '提示', { type: 'warning' })
    row.cancelling = true
    await cancelOrder(row.orderId)
    ElMessage.success('订单已取消')
    fetchOrders()
  } catch (e) {
    if (e !== 'cancel') console.error(e)
  } finally {
    row.cancelling = false
  }
}

// 删除
const handleDelete = async (row: OrderRow) => {
  const tip = row.orderStatus === 'Paid' ? '，该订单已支付，请确认' : ''
  try {
    await ElMessageBox.confirm(`确定删除订单「${row.orderId}」吗${tip}？`, '警告', { type: 'error' })
    row.deleting = true
    await deleteOrder(row.orderId)
    ElMessage.success('订单已删除')
    fetchOrders()
  } catch (e) {
    if (e !== 'cancel') console.error(e)
  } finally {
    row.deleting = false
  }
}

onMounted(fetchOrders)
</script>

<style scoped>
.search-card { margin-bottom: 16px; }
.table-card { min-height: 500px; }
.toolbar { margin-bottom: 16px; display: flex; justify-content: flex-end; }
.pagination { margin-top: 16px; display: flex; justify-content: flex-end; }
.order-item-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}
.order-total {
  margin-top: 16px;
  text-align: right;
  font-size: 15px;
}
.total-amount {
  color: #f56c6c;
  font-size: 20px;
  font-weight: bold;
}
</style>
