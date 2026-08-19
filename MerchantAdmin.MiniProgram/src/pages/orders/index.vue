<template>
  <view class="page">
    <TabBar current="/pages/orders/index" />
    <!-- 搜索栏 -->
    <view class="search-bar">
      <input
        class="search-input"
        v-model="keyword"
        placeholder="请输入订单ID"
        type="number"
        confirm-type="search"
        @confirm="handleSearch"
      />
      <picker class="status-picker" :range="statusLabels" :value="statusIndex" @change="onStatusChange">
        <view class="picker-display">{{ statusLabels[statusIndex] }}</view>
      </picker>
      <button class="search-btn" size="mini" @click="handleSearch">搜索</button>
    </view>

    <!-- 订单列表 -->
    <view class="order-list">
      <view v-for="item in list" :key="item.orderId" class="order-card">
        <view class="order-head">
          <text class="order-id">订单 #{{ item.orderId }}</text>
          <text :style="{ color: statusMap[item.orderStatus]?.color || '#333' }" class="order-status">
            {{ statusMap[item.orderStatus]?.label || item.orderStatus }}
          </text>
        </view>
        <view class="order-body">
          <text class="order-count">{{ item.orderItems.length }} 种商品</text>
          <text class="order-time">{{ formatTime(item.createdAt) }}</text>
        </view>
        <view class="order-foot">
          <text class="order-total">合计：¥{{ calcTotal(item.orderItems).toFixed(2) }}</text>
          <view class="order-actions">
            <button size="mini" @click="openDetail(item)">详情</button>
            <button v-if="item.orderStatus === 'Created'" size="mini" type="primary" @click="handlePay(item)">支付</button>
            <button v-if="item.orderStatus === 'Created'" size="mini" @click="handleCancel(item)">取消</button>
            <button v-if="item.orderStatus === 'Paid'" size="mini" type="warn" @click="handleRefund(item)">退款</button>
            <button
              v-if="isAdminUser && ['Cancelled', 'Paid', 'Refunded', 'TimedOut'].includes(item.orderStatus)"
              size="mini"
              type="warn"
              @click="handleDelete(item)"
            >删除</button>
          </view>
        </view>
      </view>

      <view v-if="!loading && list.length === 0" class="empty">暂无订单</view>
      <view v-if="loading && list.length === 0" class="loading-mask">
        <view class="loading-spinner"></view>
        <text class="loading-text">加载中...</text>
      </view>
      <view v-if="loading && list.length > 0" class="loading">加载中...</view>
      <view v-if="!loading && list.length > 0 && list.length >= total" class="loading">没有更多了</view>
    </view>

    <!-- 创建订单按钮 -->
    <view class="fab" @click="openCreate">+</view>

    <!-- 创建订单弹窗 -->
    <view v-if="dialogVisible" class="mask" @click="dialogVisible = false">
      <view class="dialog" @click.stop>
        <view class="dialog-title">创建订单</view>
        <view v-for="(item, index) in orderItems" :key="index" class="order-item-row">
          <picker class="product-picker" :range="productNames" :value="getProductIndex(item.productId)" @change="onProductChange(index, $event)">
            <view class="picker-value">{{ getProductName(item.productId) }}</view>
          </picker>
          <input class="qty-input" v-model="item.quantity" type="number" placeholder="数量" />
          <text v-if="orderItems.length > 1" class="remove-btn" @click="removeItem(index)">✕</text>
        </view>
        <button class="add-btn" size="mini" @click="addItem">+ 添加商品</button>
        <view class="dialog-actions">
          <button class="dialog-btn" @click="dialogVisible = false">取消</button>
          <button class="dialog-btn primary" :loading="submitting" @click="handleCreateSubmit">下单</button>
        </view>
      </view>
    </view>

    <!-- 订单详情弹窗 -->
    <view v-if="detailVisible" class="mask" @click="detailVisible = false">
      <view class="dialog" @click.stop>
        <view class="dialog-title">订单详情</view>
        <template v-if="detailOrder">
          <view class="detail-row">
            <text class="detail-label">订单号</text>
            <text>{{ detailOrder.orderId }}</text>
          </view>
          <view class="detail-row">
            <text class="detail-label">状态</text>
            <text :style="{ color: statusMap[detailOrder.orderStatus]?.color || '#333' }">
              {{ statusMap[detailOrder.orderStatus]?.label || detailOrder.orderStatus }}
            </text>
          </view>
          <view class="detail-row">
            <text class="detail-label">时间</text>
            <text>{{ formatTime(detailOrder.createdAt) }}</text>
          </view>
          <view class="detail-items">
            <view v-for="(it, i) in detailOrder.orderItems" :key="i" class="detail-item">
              <text class="detail-item-name">{{ it.productName }}</text>
              <text class="detail-item-qty">x{{ it.quantity }}</text>
              <text class="detail-item-price">¥{{ (it.price * it.quantity).toFixed(2) }}</text>
            </view>
          </view>
          <view class="detail-total">总金额：¥{{ calcTotal(detailOrder.orderItems).toFixed(2) }}</view>
        </template>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { onShow, onReachBottom } from '@dcloudio/uni-app'
import { getOrders, createOrder, cancelOrder, payOrder, refundOrder, deleteOrder, type OrderDto, type OrderItemDto } from '@/api/order'
import { getProducts, type ProductDto } from '@/api/product'
import { isAdmin } from '@/utils/auth'
import TabBar from '@/components/tab-bar.vue'

const list = ref<OrderDto[]>([])
const isAdminUser = isAdmin()
const keyword = ref('')
const page = ref(1)
const pageSize = 10
const total = ref(0)
const loading = ref(false)
const loaded = ref(false)

// 状态筛选
const statusLabels = ['全部', '待支付', '支付处理中', '已支付', '已退款', '已取消', '超时关闭']
const statusValues = ['', 'Created', 'PaymentProcessing', 'Paid', 'Refunded', 'Cancelled', 'TimedOut']
const statusIndex = ref(0)
const status = ref('')

const statusMap: Record<string, { label: string; color: string }> = {
  Created: { label: '待支付', color: '#909399' },
  PaymentProcessing: { label: '支付处理中', color: '#e6a23c' },
  Paid: { label: '已支付', color: '#07c160' },
  Refunded: { label: '已退款', color: '#f56c6c' },
  Cancelled: { label: '已取消', color: '#909399' },
  TimedOut: { label: '超时关闭', color: '#909399' }
}

// 创建订单
interface OrderItemRow {
  productId: number
  quantity: string
}
const dialogVisible = ref(false)
const submitting = ref(false)
const productList = ref<ProductDto[]>([])
const orderItems = ref<OrderItemRow[]>([{ productId: 0, quantity: '1' }])

const productNames = computed(() =>
  productList.value.map((p) => `${p.name}（库存${p.stock}）${p.isActive ? '' : '（已下架）'}`)
)

// 订单详情
const detailVisible = ref(false)
const detailOrder = ref<OrderDto | null>(null)

const calcTotal = (items: OrderItemDto[]) => items.reduce((s, i) => s + i.price * i.quantity, 0)

const formatTime = (iso: string) => {
  if (!iso) return '-'
  const d = new Date(iso)
  const pad = (n: number) => (n < 10 ? '0' + n : '' + n)
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

const fetchOrders = async (reset = false) => {
  if (loading.value) return
  loading.value = true
  try {
    const params: Record<string, any> = { page: page.value, pageSize }
    if (keyword.value) {
      const num = Number(keyword.value)
      if (!isNaN(num)) params.orderId = num
    }
    if (status.value) params.status = status.value

    const res = await getOrders(params)
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
  fetchOrders(true)
}

const onStatusChange = (e: any) => {
  statusIndex.value = Number(e.detail.value)
  status.value = statusValues[statusIndex.value]
  handleSearch()
}

onShow(() => {
  if (loaded.value) return
  loaded.value = true
  page.value = 1
  fetchOrders(true)
})

onReachBottom(() => {
  if (list.value.length >= total.value) return
  page.value++
  fetchOrders(false)
})

// 创建订单
const fetchProductList = async () => {
  try {
    const res = await getProducts({ page: 1, pageSize: 100 })
    productList.value = res.items.filter((p) => p.isActive)
  } catch (e: any) {
    uni.showToast({ title: e.message || '加载商品失败', icon: 'none' })
  }
}

const getProductIndex = (productId: number) => {
  const idx = productList.value.findIndex((p) => p.productId === productId)
  return idx >= 0 ? idx : 0
}

const getProductName = (productId: number) => {
  const p = productList.value.find((x) => x.productId === productId)
  return p ? p.name : '请选择商品'
}

const onProductChange = (index: number, e: any) => {
  const value = Number(e.detail.value)
  const p = productList.value[value]
  if (p) orderItems.value[index].productId = p.productId
}

const addItem = () => orderItems.value.push({ productId: 0, quantity: '1' })
const removeItem = (index: number) => orderItems.value.splice(index, 1)

const openCreate = () => {
  // 先立即打开弹窗，商品列表异步加载，避免等待网络导致"点了没反应"
  orderItems.value = [{ productId: 0, quantity: '1' }]
  dialogVisible.value = true
  fetchProductList()
}

const handleCreateSubmit = async () => {
  if (productList.value.length === 0) {
    uni.showToast({ title: '暂无可下单商品', icon: 'none' })
    return
  }

  const items = orderItems.value.filter((i) => i.productId > 0 && Number(i.quantity) > 0)
  const hasInvalid = orderItems.value.some((i) => i.productId <= 0 || Number(i.quantity) <= 0)

  if (items.length === 0) {
    uni.showToast({ title: '请至少选择一个商品', icon: 'none' })
    return
  }
  if (hasInvalid) {
    uni.showToast({ title: '请完善所有商品和数量', icon: 'none' })
    return
  }

  submitting.value = true
  try {
    const orderItemsDto = items.map((i) => {
      const p = productList.value.find((x) => x.productId === i.productId)!
      return { productId: p.productId, productName: p.name, price: p.price, quantity: Number(i.quantity) }
    })
    await createOrder({ orderItems: orderItemsDto })
    uni.showToast({ title: '订单创建成功', icon: 'success' })
    dialogVisible.value = false
    handleSearch()
  } catch (e: any) {
    uni.showToast({ title: e.message || '创建失败', icon: 'none' })
  } finally {
    submitting.value = false
  }
}

// 详情
const openDetail = (row: OrderDto) => {
  detailOrder.value = row
  detailVisible.value = true
}

// 支付
const handlePay = (row: OrderDto) => {
  uni.showModal({
    title: '提示',
    content: `确定支付订单「${row.orderId}」吗？`,
    success: async (res) => {
      if (!res.confirm) return
      try {
        await payOrder(row.orderId)
        uni.showToast({ title: '支付请求已提交', icon: 'success' })
        setTimeout(() => handleSearch(), 800)
      } catch (e: any) {
        uni.showToast({ title: e.message || '支付失败', icon: 'none' })
      }
    }
  })
}

// 退款
const handleRefund = (row: OrderDto) => {
  uni.showModal({
    title: '退款确认',
    content: `确定对订单「${row.orderId}」发起退款吗？退款后库存将回补`,
    success: async (res) => {
      if (!res.confirm) return
      try {
        await refundOrder(row.orderId)
        uni.showToast({ title: '退款成功', icon: 'success' })
        handleSearch()
      } catch (e: any) {
        uni.showToast({ title: e.message || '退款失败', icon: 'none' })
      }
    }
  })
}

// 取消
const handleCancel = (row: OrderDto) => {
  uni.showModal({
    title: '提示',
    content: `确定取消订单「${row.orderId}」吗？`,
    success: async (res) => {
      if (!res.confirm) return
      try {
        await cancelOrder(row.orderId)
        uni.showToast({ title: '订单已取消', icon: 'success' })
        handleSearch()
      } catch (e: any) {
        uni.showToast({ title: e.message || '操作失败', icon: 'none' })
      }
    }
  })
}

// 删除
const handleDelete = (row: OrderDto) => {
  uni.showModal({
    title: '警告',
    content: `确定删除订单「${row.orderId}」吗？`,
    success: async (res) => {
      if (!res.confirm) return
      try {
        await deleteOrder(row.orderId)
        uni.showToast({ title: '订单已删除', icon: 'success' })
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

.status-picker {
  margin-left: 16rpx;
  background: #fff;
  border-radius: 12rpx;
  height: 72rpx;
  display: flex;
  align-items: center;
  padding: 0 20rpx;
}

.picker-display {
  font-size: 28rpx;
  color: #333;
}

.search-btn {
  margin-left: 16rpx;
}

.order-card {
  background: #fff;
  border-radius: 16rpx;
  padding: 24rpx;
  margin-bottom: 20rpx;
}

.order-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16rpx;
}

.order-id {
  font-size: 30rpx;
  font-weight: bold;
}

.order-status {
  font-size: 26rpx;
}

.order-body {
  display: flex;
  justify-content: space-between;
  margin-bottom: 16rpx;
}

.order-count,
.order-time {
  font-size: 26rpx;
  color: #666;
}

.order-foot {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.order-total {
  font-size: 28rpx;
  color: #f56c6c;
  font-weight: bold;
}

.order-actions {
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

.order-item-row {
  display: flex;
  align-items: center;
  gap: 16rpx;
  margin-bottom: 20rpx;
}

.product-picker {
  flex: 1;
  min-width: 0;
}

.picker-value {
  width: 100%;
  height: 72rpx;
  line-height: 72rpx;
  padding: 0 20rpx;
  box-sizing: border-box;
  background: #f7f7f7;
  border-radius: 8rpx;
  font-size: 28rpx;
  color: #333;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.qty-input {
  width: 120rpx;
  height: 72rpx;
  background: #f7f7f7;
  border-radius: 8rpx;
  text-align: center;
  font-size: 28rpx;
}

.remove-btn {
  font-size: 32rpx;
  color: #f56c6c;
  padding: 0 8rpx;
}

.add-btn {
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

.detail-row {
  display: flex;
  justify-content: space-between;
  padding: 12rpx 0;
  font-size: 28rpx;
}

.detail-label {
  color: #999;
}

.detail-items {
  margin-top: 16rpx;
  border-top: 1rpx solid #eee;
  padding-top: 16rpx;
}

.detail-item {
  display: flex;
  align-items: center;
  gap: 16rpx;
  padding: 12rpx 0;
  font-size: 28rpx;
}

.detail-item-name {
  flex: 1;
}

.detail-item-qty {
  color: #666;
  width: 80rpx;
  text-align: center;
}

.detail-item-price {
  color: #f56c6c;
  width: 160rpx;
  text-align: right;
}

.detail-total {
  margin-top: 16rpx;
  text-align: right;
  font-size: 30rpx;
  color: #f56c6c;
  font-weight: bold;
}
</style>