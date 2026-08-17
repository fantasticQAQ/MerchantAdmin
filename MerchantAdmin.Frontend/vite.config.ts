import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'

import AutoImport from 'unplugin-auto-import/vite'
import Components from 'unplugin-vue-components/vite'
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    vueDevTools(),
    AutoImport({
      resolvers: [ElementPlusResolver()],
    }),
    Components({
      resolvers: [ElementPlusResolver()],
    }),
  ],
  server: {
    port: 5173,  // 开发服务器端口
    proxy: {
      // 身份认证服务（本地开发直连，生产走 nginx）
      '/api/identity': {
        target: 'http://localhost:5034',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api\/identity/, '/api')
      },
      // 订单/商品服务（本地开发直连，生产走 nginx）
      '/api/merchant': {
        target: 'http://localhost:5243',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api\/merchant/, '/api')
      }
    }
  },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    },
  },
})
