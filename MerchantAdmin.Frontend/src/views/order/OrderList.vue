<template>
  <div class="order-list">
    <!-- 搜索栏 -->
    <el-card shadow="never" class="search-card">
      <el-form :inline="true" :model="searchForm" ref="searchRef">
        <el-form-item label="订单号">
          <el-input
            v-model="searchForm.orderNo"
            placeholder="请输入订单号"
            clearable
            @keyup.enter="handleSearch"
          />
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="searchForm.status" placeholder="全部" clearable>
            <el-option label="待支付" value="pending" />
            <el-option label="已支付" value="paid" />
            <el-option label="已取消" value="cancelled" />
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
        <el-button type="primary" @click="openCreateDialog">
          <el-icon><Plus /></el-icon> 创建订单
        </el-button>
      </div>

      <el-table :data="tableData" v-loading="loading" border stripe style="width: 100%">
        <el-table-column type="index" label="序号" width="60" align="center" />
        <el-table-column prop="orderId" label="订单ID" width="80" align="center" />
        <el-table-column prop="totalAmount" label="总金额" width="120" align="right">
          <template #default="{ row }">
            ¥{{ calculateTotalAmount(row.orderItems)?.toFixed(2) }}
          </template>
        </el-table-column>
        <el-table-column prop="orderStatus" label="状态" width="100" align="center">
          <template #default="{ row }">
            <el-tag :type="statusTagType(row.orderStatus)">{{ statusLabel(row.orderStatus) }}</el-tag>  
          </template>
        </el-table-column>
        <el-table-column prop="createdAt" label="创建时间" width="180" />
        <el-table-column label="操作" width="240" align="center" fixed="right">
          <template #default="{ row }">
            <el-button
              type="info"
              size="small"
              @click="handlelooking(row)"
              :loading="row.looking"
            >
              <el-icon><Check /></el-icon> 支付
            </el-button>
            <el-button
              v-if="row.orderStatus === 'Created'"
              type="success"
              size="small"
              @click="handlePay(row)"
              :loading="row.paying"
            >
              <el-icon><Check /></el-icon> 支付
            </el-button>
            <el-button
              v-if="row.orderStatus === 'Created'"
              type="warning"
              size="small"
              @click="handleCancel(row)"
              :loading="row.cancelling"
            >
              <el-icon><Close /></el-icon> 取消
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

    <!-- 创建订单弹窗 -->
    <el-dialog v-model="dialogVisible" title="创建订单" width="500px" @close="resetForm">
      <el-form :model="formData" :rules="rules" ref="formRef" label-width="100px">
        <el-form-item label="商品" prop="productId">
          <el-select
            v-model="formData.productId"
            placeholder="请选择商品"
            filterable
            style="width: 100%"
            @change="onProductChange"
          >
            <el-option
              v-for="p in productList"
              :key="p.productId"
              :label="p.name"
              :value="p.productId"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="数量" prop="Quantity">
          <el-input-number
            v-model="formData.quantity"
            :min="1"
            :max="selectedProduct?.stock || 9999"
            placeholder="请输入数量"
            style="width: 100%"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSubmit" :loading="submitting">
          确定
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, reactive } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Search, Refresh, Plus, Check, Close } from '@element-plus/icons-vue'
import { getOrders, createOrder, cancelOrder, payOrder } from '@/api/order'
import { getProducts, type ProductDto } from '@/api/product'
import type { OrderItemDto ,OrderDto,OrderStatus} from '@/api/order'

// 搜索
const searchForm = reactive({ orderNo: '', status: '' })
const searchRef = ref()

// 表格
const tableData = ref<OrderDto[]>([])
const loading = ref(false)

// 分页
const pagination = reactive({ page: 1, pageSize: 10, total: 0 })

// 弹窗
const dialogVisible = ref(false)
const formRef = ref()
const submitting = ref(false)
const productList = ref<ProductDto[]>([])
const selectedProduct = ref<ProductDto | null>(null)
const formData = reactive<OrderItemDto>({
  productId: 0,
  productName: '',
  price: 0,
  quantity: 0
})
const formLoadingData = reactive<OrderItemDto>({
  productId: 0,
  productName: '',
  price: 0,
  quantity: 0
})

const rules = {
  productId: [{ required: true, message: '请选择商品', trigger: 'change' }],
  quantity: [{ required: true, message: '请输入数量', trigger: 'blur' }]
}

// 状态映射
const statusLabel = (s) => {
  const map = { Created: '待支付', Paid: '已支付', Cancelled: '已取消' }
  return map[s] || s
}
const statusTagType = (s) => {
  const map = { Created: 'info', Paid: 'success', Cancelled: 'info' }
  return map[s] || ''
}

//计算订单总金额
const calculateTotalAmount = (items: OrderItemDto[]) => {
  return items.reduce((sum, item) => sum + item.price * item.quantity, 0)
}

// 获取订单列表
const fetchOrders = async () => {
  loading.value = true
  try {
    const params = {
      page: pagination.page,
      pageSize: pagination.pageSize,
      ...searchForm
    }
    const res = await getOrders()
    tableData.value =  res
    console.log(res)
    pagination.total = res.total || tableData.value.length
  } catch (e) {
    console.error(e)
  } finally {
    loading.value = false
  }
}

// 搜索 & 重置
const handleSearch = () => { pagination.page = 1; fetchOrders() }
const resetSearch = () => { searchRef.value?.resetFields(); handleSearch() }

// 商品列表（弹窗内下拉）
const fetchProductList = async () => {
  try {
    const res = await getProducts({ pageSize: 9999 })
    productList.value = res.list || res.items || res
  } catch (e) {
    console.error(e)
  }
}

// 选择商品
const onProductChange = (id) => {
  selectedProduct.value = productList.value.find((p) => p.productId === id) || null
}

// 打开创建弹窗
const openCreateDialog = async () => {
  await fetchProductList()
  formData.productId = 0
  formData.quantity = 1
  selectedProduct.value = null
  dialogVisible.value = true
}

// 提交创建
const handleSubmit = async () => {
  await formRef.value?.validate()
  submitting.value = true
  try {
    await createOrder({
      orderItems: [
        {
          productId: selectedProduct.value?.productId || formData.productId,
          quantity: formData.quantity,
          productName:  '',
          price: 0
        }
      ]
    })
    ElMessage.success('订单创建成功')
    dialogVisible.value = false
    fetchOrders()
  } catch (e) {
    console.error(e)
  } finally {
    submitting.value = false
  }
}

// 查看订单
const handleLooking = async (row) => {
  try {
    row.looking = true
    ElMessage.success('查看订单成功')
    fetchOrders()
  } catch (e) {
    if (e !== 'cancel') console.error(e)
  } finally {
    row.looking = false
  }
}

// 支付
const handlePay = async (row) => {
  try {
    await ElMessageBox.confirm(`确定支付订单「${row.orderId}」吗？`, '提示', { type: 'info' })
    row.paying = true
    await payOrder(row.orderId)
    ElMessage.success('支付成功')
    fetchOrders()
  } catch (e) {
    if (e !== 'cancel') console.error(e)
  } finally {
    row.paying = false
  }
}

// 取消
const handleCancel = async (row) => {
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

const resetForm = () => formRef.value?.resetFields()

onMounted(fetchOrders)
</script>

<style scoped>
.search-card { margin-bottom: 16px; }
.table-card { min-height: 500px; }
.toolbar { margin-bottom: 16px; display: flex; justify-content: flex-end; }
.pagination { margin-top: 16px; display: flex; justify-content: flex-end; }
</style>