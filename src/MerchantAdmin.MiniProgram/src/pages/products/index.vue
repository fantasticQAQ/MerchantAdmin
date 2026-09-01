<template>
  <view class="page">
    <TabBar current="/pages/products/index" />
    <!-- 搜索栏 -->
    <view class="search-bar">
      <input
        class="search-input"
        v-model="keyword"
        placeholder="请输入商品名称"
        confirm-type="search"
        @confirm="handleSearch"
      />
      <button class="search-btn" size="mini" @click="handleSearch">搜索</button>
    </view>

    <!-- 商品列表 -->
    <view class="product-list">
      <view v-for="item in list" :key="item.productId" class="product-card">
        <view class="product-main">
          <view class="product-name">{{ item.name }}</view>
          <view class="product-meta">
            <text class="price">¥{{ formatPrice(item.price) }}</text>
            <text class="stock">库存 {{ item.stock }}</text>
            <text :class="['status', item.isActive ? 'on' : 'off']">
              {{ item.isActive ? '上架' : '下架' }}
            </text>
          </view>
        </view>
        <view class="product-actions">
          <button size="mini" @click="openEdit(item)">编辑</button>
          <button size="mini" @click="openStock(item)">库存</button>
          <button size="mini" @click="handleToggle(item)">
            {{ item.isActive ? '下架' : '上架' }}
          </button>
          <button v-if="isAdminUser" size="mini" type="warn" @click="handleDelete(item)">删除</button>
        </view>
      </view>

      <view v-if="!loading && list.length === 0" class="empty">暂无商品</view>
      <view v-if="loading && list.length === 0" class="loading-mask">
        <view class="loading-spinner"></view>
        <text class="loading-text">加载中...</text>
      </view>
      <view v-if="loading && list.length > 0" class="loading">加载中...</view>
      <view v-if="!loading && list.length > 0 && list.length >= total" class="loading">没有更多了</view>
    </view>

    <!-- 新增按钮 -->
    <view class="fab" @click="openCreate">+</view>

    <!-- 新增/编辑弹窗 -->
    <view v-if="dialogVisible" class="mask" @click="dialogVisible = false">
      <view class="dialog" @click.stop>
        <view class="dialog-title">{{ editingId ? '编辑商品' : '新增商品' }}</view>
        <view class="form-item">
          <text class="label">名称</text>
          <input class="input" v-model="form.name" placeholder="请输入商品名称" />
        </view>
        <view class="form-item">
          <text class="label">价格</text>
          <input class="input" v-model="form.price" type="digit" placeholder="请输入价格" />
        </view>
        <view v-if="!editingId" class="form-item">
          <text class="label">库存</text>
          <input class="input" v-model="form.stock" type="number" placeholder="请输入库存" />
        </view>
        <view class="dialog-actions">
          <button class="dialog-btn" @click="dialogVisible = false">取消</button>
          <button class="dialog-btn primary" :loading="submitting" @click="handleSubmit">确定</button>
        </view>
      </view>
    </view>

    <!-- 库存调整弹窗 -->
    <view v-if="stockVisible" class="mask" @click="stockVisible = false">
      <view class="dialog" @click.stop>
        <view class="dialog-title">调整库存</view>
        <view class="stock-info">商品：{{ stockTarget?.name }}，当前库存：{{ stockTarget?.stock }}</view>
        <view class="form-item">
          <text class="label">调整数量</text>
          <input class="input" v-model="stockDelta" placeholder="正数补货，负数扣减" />
        </view>
        <view class="stock-tip">提示：正数表示补货，负数表示扣减库存</view>
        <view class="dialog-actions">
          <button class="dialog-btn" @click="stockVisible = false">取消</button>
          <button class="dialog-btn primary" :loading="stockSubmitting" @click="handleStockSubmit">确定</button>
        </view>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { onShow, onReachBottom } from '@dcloudio/uni-app'
import { getProducts, createProduct, updateProduct, deleteProduct, type ProductDto } from '@/api/product'
import { isAdmin } from '@/utils/auth'
import TabBar from '@/components/tab-bar.vue'

const list = ref<ProductDto[]>([])
const isAdminUser = isAdmin()
const keyword = ref('')
const page = ref(1)
const pageSize = 10
const total = ref(0)
const loading = ref(false)
const loaded = ref(false)

// 新增/编辑弹窗
const dialogVisible = ref(false)
const editingId = ref(0)
const submitting = ref(false)
const form = reactive({ name: '', price: '' as string | number, stock: '' as string | number })

// 库存调整弹窗
const stockVisible = ref(false)
const stockTarget = ref<ProductDto | null>(null)
const stockDelta = ref('')
const stockSubmitting = ref(false)

const formatPrice = (p: number) => (typeof p === 'number' ? p.toFixed(2) : '0.00')

const fetchProducts = async (reset = false) => {
  if (loading.value) return
  loading.value = true
  try {
    const params = {
      name: keyword.value.trim() || undefined,
      page: page.value,
      pageSize
    }
    const res = await getProducts(params)
    list.value = reset ? res.items : [...list.value, ...res.items]
    total.value = res.total
  } catch (e: any) {
    uni.showToast({ title: e.message || '加载失败', icon: 'none' })
  } finally {
    loading.value = false
  }
}

const handleSearch = () => {
  page.value = 1
  fetchProducts(true)
}

onShow(() => {
  if (loaded.value) return
  loaded.value = true
  page.value = 1
  fetchProducts(true)
})

onReachBottom(() => {
  if (list.value.length >= total.value) return
  page.value++
  fetchProducts(false)
})

// 新增
const openCreate = () => {
  editingId.value = 0
  form.name = ''
  form.price = ''
  form.stock = ''
  dialogVisible.value = true
}

// 编辑
const openEdit = (row: ProductDto) => {
  editingId.value = row.productId
  form.name = row.name
  form.price = String(row.price)
  form.stock = String(row.stock)
  dialogVisible.value = true
}

// 提交新增/编辑
const handleSubmit = async () => {
  const name = form.name.trim()
  const price = Number(form.price)
  if (!name) {
    uni.showToast({ title: '请输入商品名称', icon: 'none' })
    return
  }
  if (isNaN(price) || price < 0) {
    uni.showToast({ title: '请输入正确的价格', icon: 'none' })
    return
  }

  submitting.value = true
  try {
    if (editingId.value) {
      await updateProduct(editingId.value, { name, price })
      uni.showToast({ title: '修改成功', icon: 'success' })
    } else {
      const stock = Number(form.stock)
      if (isNaN(stock) || stock < 0) {
        uni.showToast({ title: '请输入正确的库存', icon: 'none' })
        return
      }
      await createProduct({ productDto: { productId: 0, name, price, stock } })
      uni.showToast({ title: '创建成功', icon: 'success' })
    }
    dialogVisible.value = false
    handleSearch()
  } catch (e: any) {
    uni.showToast({ title: e.message || '操作失败', icon: 'none' })
  } finally {
    submitting.value = false
  }
}

// 库存调整
const openStock = (row: ProductDto) => {
  stockTarget.value = row
  stockDelta.value = ''
  stockVisible.value = true
}

const handleStockSubmit = async () => {
  const delta = Number(stockDelta.value)
  if (isNaN(delta) || delta === 0) {
    uni.showToast({ title: '请输入非零的调整数量', icon: 'none' })
    return
  }
  stockSubmitting.value = true
  try {
    await updateProduct(stockTarget.value!.productId, { stockDelta: delta })
    uni.showToast({ title: '库存已调整', icon: 'success' })
    stockVisible.value = false
    handleSearch()
  } catch (e: any) {
    uni.showToast({ title: e.message || '调整失败', icon: 'none' })
  } finally {
    stockSubmitting.value = false
  }
}

// 上下架
const handleToggle = (row: ProductDto) => {
  const action = row.isActive ? '下架' : '上架'
  uni.showModal({
    title: '提示',
    content: `确定${action}商品「${row.name}」吗？`,
    success: async (res) => {
      if (!res.confirm) return
      try {
        await updateProduct(row.productId, { isActive: !row.isActive })
        uni.showToast({ title: `${action}成功`, icon: 'success' })
        handleSearch()
      } catch (e: any) {
        uni.showToast({ title: e.message || '操作失败', icon: 'none' })
      }
    }
  })
}

// 删除
const handleDelete = (row: ProductDto) => {
  uni.showModal({
    title: '警告',
    content: `确定删除商品「${row.name}」吗？`,
    success: async (res) => {
      if (!res.confirm) return
      try {
        await deleteProduct(row.productId)
        uni.showToast({ title: '删除成功', icon: 'success' })
        handleSearch()
      } catch (e: any) {
        uni.showToast({ title: e.message || '删除失败', icon: 'none' })
      }
    }
  })
}
</script>

<style>
.page {
  padding: 20rpx;
  padding-bottom: 160rpx;
}

.search-bar {
  display: flex;
  align-items: center;
  margin-bottom: 20rpx;
}

.search-input {
  flex: 1;
  height: 72rpx;
  padding: 0 24rpx;
  background: #fff;
  border-radius: 12rpx;
  font-size: 28rpx;
}

.search-btn {
  margin-left: 16rpx;
}

.product-card {
  background: #fff;
  border-radius: 16rpx;
  padding: 24rpx;
  margin-bottom: 20rpx;
}

.product-main {
  margin-bottom: 20rpx;
}

.product-name {
  font-size: 32rpx;
  font-weight: bold;
  margin-bottom: 12rpx;
}

.product-meta {
  display: flex;
  align-items: center;
  gap: 24rpx;
}

.price {
  color: #f56c6c;
  font-size: 30rpx;
  font-weight: bold;
}

.stock {
  color: #666;
  font-size: 26rpx;
}

.status {
  font-size: 24rpx;
  padding: 4rpx 16rpx;
  border-radius: 8rpx;
}

.status.on {
  color: #07c160;
  background: #e8f8ef;
}

.status.off {
  color: #999;
  background: #f2f2f2;
}

.product-actions {
  display: flex;
  gap: 12rpx;
}

.empty,
.loading {
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

.stock-info {
  font-size: 26rpx;
  color: #666;
  margin-bottom: 24rpx;
}

.stock-tip {
  font-size: 24rpx;
  color: #999;
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