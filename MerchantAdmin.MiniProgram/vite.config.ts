import { defineConfig } from 'vite'
import uni from '@dcloudio/vite-plugin-uni'

export default defineConfig({
  plugins: [uni()],
  build: {
    watch: {
      // 排除输出目录与依赖目录，避免差量编译时 write 到 dist 又被 watch 捕获形成循环卡顿
      exclude: [
        '**/node_modules/**',
        '**/.git/**',
        '**/dist/**',
        '**/unpackage/**',
        '**/.hbuilderx/**'
      ]
    }
  }
})