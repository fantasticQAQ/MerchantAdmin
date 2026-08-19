<template>
  <view class="login-page">
    <view class="logo">商户管理</view>
    <view class="subtitle">{{ mode === 'login' ? '微信小程序版' : '注册账号' }}</view>

    <!-- 账号密码登录 -->
    <view v-if="mode === 'login'" class="form">
      <view class="field">
        <input
          class="input"
          v-model="userName"
          placeholder="请输入账号"
          placeholder-class="ph"
        />
      </view>
      <view class="field">
        <input
          class="input"
          v-model="password"
          password
          placeholder="请输入密码"
          placeholder-class="ph"
        />
      </view>

      <button
        class="login-btn"
        type="primary"
        :loading="loading"
        :disabled="loading || wxLoading"
        @click="handlePasswordLogin"
      >
        登录
      </button>

      <view class="divider">
        <view class="line"></view>
        <text class="divider-text">其他登录方式</text>
        <view class="line"></view>
      </view>

      <button
        class="wx-btn"
        :loading="wxLoading"
        :disabled="loading || wxLoading"
        @click="handleWxLogin"
      >
        微信一键登录
      </button>

      <view class="switch" @click="switchMode('register')">没有账号？立即注册</view>
    </view>

    <!-- 注册账号 -->
    <view v-else class="form">
      <view class="field">
        <input
          class="input"
          v-model="regUserName"
          placeholder="请输入账号"
          placeholder-class="ph"
        />
      </view>
      <view class="field">
        <input
          class="input"
          v-model="regPassword"
          password
          placeholder="请输入密码（至少 6 位）"
          placeholder-class="ph"
        />
      </view>
      <view class="field">
        <input
          class="input"
          v-model="regConfirm"
          password
          placeholder="请再次输入密码"
          placeholder-class="ph"
        />
      </view>
      <view class="field">
        <input
          class="input"
          v-model="regEmail"
          placeholder="邮箱（可选）"
          placeholder-class="ph"
        />
      </view>

      <button
        class="login-btn"
        type="primary"
        :loading="registering"
        @click="handleRegister"
      >
        注册
      </button>

      <view class="switch" @click="switchMode('login')">已有账号？返回登录</view>
    </view>

    <view class="tip">登录即表示授权本小程序获取您的微信身份</view>
  </view>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { wxLogin, login, register } from '@/api/auth'
import { setAuth } from '@/utils/auth'

type Mode = 'login' | 'register'

const mode = ref<Mode>('login')

const userName = ref('')
const password = ref('')
const loading = ref(false)
const wxLoading = ref(false)

const regUserName = ref('')
const regPassword = ref('')
const regConfirm = ref('')
const regEmail = ref('')
const registering = ref(false)

// 已登录则直接进入首页
onShow(() => {
  const token = uni.getStorageSync('token')
  if (token) {
    uni.reLaunch({ url: '/pages/home/index' })
  }
})

const switchMode = (m: Mode) => {
  mode.value = m
}

const goHome = () => {
  uni.showToast({ title: '登录成功', icon: 'success' })
  setTimeout(() => {
    uni.reLaunch({ url: '/pages/home/index' })
  }, 500)
}

const handlePasswordLogin = async () => {
  if (!userName.value.trim() || !password.value) {
    uni.showToast({ title: '请输入账号和密码', icon: 'none' })
    return
  }
  if (loading.value) return
  loading.value = true
  try {
    const result = await login(userName.value.trim(), password.value)
    setAuth(result)
    goHome()
  } catch (e: any) {
    uni.showToast({ title: e.message || '登录失败', icon: 'none' })
  } finally {
    loading.value = false
  }
}

const handleRegister = async () => {
  const name = regUserName.value.trim()
  if (!name || !regPassword.value) {
    uni.showToast({ title: '请输入账号和密码', icon: 'none' })
    return
  }
  if (regPassword.value.length < 6) {
    uni.showToast({ title: '密码至少 6 位', icon: 'none' })
    return
  }
  if (regPassword.value !== regConfirm.value) {
    uni.showToast({ title: '两次输入的密码不一致', icon: 'none' })
    return
  }
  if (registering.value) return
  registering.value = true
  try {
    await register(name, regPassword.value, regEmail.value.trim())
    uni.showToast({ title: '注册成功，请登录', icon: 'success' })
    // 切回登录页并预填账号，方便直接登录
    userName.value = name
    password.value = ''
    regPassword.value = ''
    regConfirm.value = ''
    switchMode('login')
  } catch (e: any) {
    uni.showToast({ title: e.message || '注册失败', icon: 'none' })
  } finally {
    registering.value = false
  }
}

const handleWxLogin = () => {
  if (wxLoading.value) return
  wxLoading.value = true

  uni.login({
    success: async (loginRes) => {
      try {
        const code = loginRes.code
        if (!code) {
          throw new Error('未获取到微信登录凭证')
        }
        const result = await wxLogin(code)
        setAuth(result)
        goHome()
      } catch (e: any) {
        uni.showToast({ title: e.message || '登录失败', icon: 'none' })
      } finally {
        wxLoading.value = false
      }
    },
    fail: () => {
      wxLoading.value = false
      uni.showToast({ title: '微信登录失败', icon: 'none' })
    }
  })
}
</script>

<style>
.login-page {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  padding: 40rpx;
  box-sizing: border-box;
}

.logo {
  font-size: 56rpx;
  font-weight: bold;
  color: #1989fa;
  margin-bottom: 16rpx;
}

.subtitle {
  font-size: 28rpx;
  color: #999;
  margin-bottom: 80rpx;
}

.form {
  width: 80%;
}

.field {
  background: #fff;
  border-radius: 16rpx;
  margin-bottom: 24rpx;
  padding: 0 24rpx;
  border: 1rpx solid #eee;
}

.input {
  height: 88rpx;
  line-height: 88rpx;
  font-size: 30rpx;
}

.ph {
  color: #bbb;
}

.login-btn {
  width: 100%;
  height: 88rpx;
  line-height: 88rpx;
  border-radius: 44rpx;
  font-size: 32rpx;
  margin-top: 8rpx;
}

.divider {
  width: 100%;
  display: flex;
  align-items: center;
  margin: 48rpx 0 32rpx;
}

.line {
  flex: 1;
  height: 1rpx;
  background: #e5e5e5;
}

.divider-text {
  margin: 0 24rpx;
  font-size: 24rpx;
  color: #999;
}

.wx-btn {
  width: 100%;
  height: 88rpx;
  line-height: 88rpx;
  border-radius: 44rpx;
  font-size: 30rpx;
  color: #07c160;
  background: #f0faf4;
  border: 1rpx solid #07c160;
}

.switch {
  margin-top: 32rpx;
  text-align: center;
  font-size: 26rpx;
  color: #1989fa;
}

.tip {
  margin-top: 40rpx;
  font-size: 24rpx;
  color: #bbb;
}
</style>
