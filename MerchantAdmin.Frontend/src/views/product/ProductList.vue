<template>
  <div class="product-list">
    <!-- 搜索栏 -->
    <el-card shadow="never" class="search-card">
      <el-form :inline="true" :model="searchForm" ref="searchRef">
        <el-form-item label="商品名称" prop="name">
          <el-input
            v-model="searchForm.name"
            placeholder="请输入商品名称"
            clearable
            @keyup.enter="handleSearch"
          />
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
          <el-icon><Plus /></el-icon> 新增商品
        </el-button>
      </div>

      <el-table :data="tableData" v-loading="loading" border stripe style="width: 100%">
        <el-table-column type="index" label="序号" width="60" align="center" />
        <el-table-column prop="name" label="商品名称" min-width="140" />
        <el-table-column prop="price" label="价格" width="110" align="right">
          <template #default="{ row }">
            ¥{{ row.price?.toFixed(2) }}
          </template>
        </el-table-column>
        <el-table-column prop="stock" label="库存" width="100" align="center" />
        <el-table-column label="状态" width="90" align="center">
          <template #default="{ row }">
            <el-tag :type="row.isActive ? 'success' : 'info'">
              {{ row.isActive ? '上架' : '下架' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="320" align="center" fixed="right">
          <template #default="{ row }">
            <el-button
              v-if="userStore.canManage"
              type="primary"
              size="small"
              @click="openEditDialog(row)"
            >
              <el-icon><Edit /></el-icon> 编辑
            </el-button>
            <el-button
              v-if="userStore.canManage"
              type="warning"
              size="small"
              @click="openStockDialog(row)"
            >
              <el-icon><Box /></el-icon> 库存
            </el-button>
            <el-button
              v-if="userStore.canManage"
              :type="row.isActive ? 'info' : 'success'"
              size="small"
              @click="handleToggleActive(row)"
            >
              {{ row.isActive ? '下架' : '上架' }}
            </el-button>
            <el-button
              v-if="userStore.isAdmin"
              type="danger"
              size="small"
              @click="handleDelete(row)"
              :loading="row.deleting"
            >
              <el-icon><Delete /></el-icon> 删除
            </el-button>
            <span v-if="!userStore.canManage" style="color: #909399">只读</span>
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
          @change="fetchProducts"
        />
      </div>
    </el-card>

    <!-- 新增/编辑弹窗 -->
    <el-dialog v-model="dialogVisible" :title="dialogTitle" width="500px" @close="resetForm">
      <el-form :model="formData" :rules="rules" ref="formRef" label-width="100px">
        <el-form-item label="商品名称" prop="name">
          <el-input v-model="formData.name" placeholder="请输入商品名称" />
        </el-form-item>
        <el-form-item label="价格" prop="price">
          <el-input-number
            v-model="formData.price"
            :min="0"
            :precision="2"
            placeholder="请输入价格"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item v-if="!editingId" label="库存" prop="stock">
          <el-input-number
            v-model="formData.stock"
            :min="0"
            placeholder="请输入库存"
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

    <!-- 库存调整弹窗 -->
    <el-dialog v-model="stockDialogVisible" title="调整库存" width="420px">
      <div v-if="stockTarget" class="stock-info">
        商品：{{ stockTarget.name }}，当前库存：{{ stockTarget.stock }}
      </div>
      <el-form label-width="100px" style="margin-top: 16px">
        <el-form-item label="调整数量">
          <el-input-number
            v-model="stockDelta"
            placeholder="正数补货，负数扣减"
            style="width: 100%"
          />
        </el-form-item>
        <div class="stock-tip">提示：正数表示补货，负数表示扣减库存</div>
      </el-form>
      <template #footer>
        <el-button @click="stockDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleStockSubmit" :loading="stockSubmitting">
          确定
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, reactive } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Search, Refresh, Plus, Delete, Edit, Box } from '@element-plus/icons-vue'
import { getProducts, createProduct, updateProduct, deleteProduct, type ProductDto } from '@/api/product'
import { useUserStore } from '@/store/user'

const userStore = useUserStore()

// 表格行类型：在 ProductDto 基础上附加 UI 临时 loading 状态
type ProductRow = ProductDto & {
  deleting?: boolean
}

const searchForm = reactive({ name: '' })
const searchRef = ref()

const tableData = ref<ProductRow[]>([])
const loading = ref(false)
const pagination = reactive({ page: 1, pageSize: 10, total: 0 })

// 新增/编辑弹窗
const dialogVisible = ref(false)
const dialogTitle = ref('新增商品')
const editingId = ref(0)
const formRef = ref()
const submitting = ref(false)
const formData = reactive({ name: '', price: 0, stock: 0 })

// 库存调整弹窗
const stockDialogVisible = ref(false)
const stockTarget = ref<ProductDto | null>(null)
const stockDelta = ref(0)
const stockSubmitting = ref(false)

const rules = {
  name: [{ required: true, message: '请输入商品名称', trigger: 'blur' }],
  price: [{ required: true, message: '请输入价格', trigger: 'blur' }]
}

const fetchProducts = async () => {
  loading.value = true
  try {
    const params = {
      name: searchForm.name?.trim() || undefined,
      page: pagination.page,
      pageSize: pagination.pageSize
    }
    const res = await getProducts(params)
    tableData.value = res.items
    pagination.total = res.total
  } catch (e) {
    ElMessage.error('加载商品失败')
    console.error(e)
  } finally {
    loading.value = false
  }
}

const handleSearch = () => { pagination.page = 1; fetchProducts() }
const resetSearch = () => { searchRef.value?.resetFields(); handleSearch() }

// 新增
const openCreateDialog = () => {
  dialogTitle.value = '新增商品'
  editingId.value = 0
  formData.name = ''
  formData.price = 0
  formData.stock = 0
  dialogVisible.value = true
}

// 编辑
const openEditDialog = (row: ProductRow) => {
  dialogTitle.value = '编辑商品'
  editingId.value = row.productId
  formData.name = row.name
  formData.price = row.price
  formData.stock = row.stock
  dialogVisible.value = true
}

// 新增/编辑提交
const handleSubmit = async () => {
  await formRef.value?.validate()
  submitting.value = true
  try {
    if (editingId.value) {
      await updateProduct(editingId.value, { name: formData.name, price: formData.price })
      ElMessage.success('修改成功')
    } else {
      await createProduct({ productDto: { productId: 0, ...formData } })
      ElMessage.success('创建成功')
    }
    dialogVisible.value = false
    fetchProducts()
  } catch (e: any) {
    ElMessage.error(e.message || '操作失败')
    console.error(e)
  } finally {
    submitting.value = false
  }
}

// 库存调整弹窗
const openStockDialog = (row: ProductRow) => {
  stockTarget.value = row
  stockDelta.value = 0
  stockDialogVisible.value = true
}

const handleStockSubmit = async () => {
  if (!stockTarget.value || stockDelta.value === 0) {
    ElMessage.warning('请输入非零的调整数量')
    return
  }
  stockSubmitting.value = true
  try {
    await updateProduct(stockTarget.value.productId, { stockDelta: stockDelta.value })
    ElMessage.success('库存已调整')
    stockDialogVisible.value = false
    fetchProducts()
  } catch (e: any) {
    ElMessage.error(e.message || '调整失败')
    console.error(e)
  } finally {
    stockSubmitting.value = false
  }
}

// 上下架
const handleToggleActive = async (row: ProductRow) => {
  const action = row.isActive ? '下架' : '上架'
  try {
    await ElMessageBox.confirm(`确定${action}商品「${row.name}」吗？`, '提示', { type: 'warning' })
    await updateProduct(row.productId, { isActive: !row.isActive })
    ElMessage.success(`${action}成功`)
    fetchProducts()
  } catch (e) {
    if (e !== 'cancel') console.error(e)
  }
}

// 删除
const handleDelete = async (row: ProductRow) => {
  try {
    await ElMessageBox.confirm(`确定删除商品「${row.name}」吗？`, '提示', { type: 'warning' })
    row.deleting = true
    await deleteProduct(row.productId)
    ElMessage.success('删除成功')
    fetchProducts()
  } catch (e) {
    if (e !== 'cancel') console.error(e)
  } finally {
    row.deleting = false
  }
}

const resetForm = () => { formRef.value?.resetFields() }

onMounted(fetchProducts)
</script>

<style scoped>
.search-card { margin-bottom: 16px; }
.table-card { min-height: 500px; }
.toolbar { margin-bottom: 16px; display: flex; justify-content: flex-end; }
.pagination { margin-top: 16px; display: flex; justify-content: flex-end; }
.stock-info { color: #606266; font-size: 14px; }
.stock-tip { color: #909399; font-size: 12px; margin-top: -8px; padding-left: 100px; }
</style>
