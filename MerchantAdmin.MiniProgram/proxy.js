/**
 * 本地开发反向代理（给微信小程序 / 本地联调用）
 *
 * 作用：替代生产环境的 nginx 网关，把不同路径前缀分别转发到本地两个后端服务，
 *       并剥掉 /api/identity、/api/merchant 前缀（和生产 nginx 行为一致）。
 *
 * 用法：
 *   node proxy.js
 *   或
 *   npm run proxy
 *
 * 启动后，小程序 BASE_URL 填： http://localhost:8080/api
 */

const http = require('http')

// ==================== 配置区（按需修改） ====================

/** 代理自身监听的端口（小程序 BASE_URL 要用这个端口） */
const PROXY_PORT = 8080

/** 路由表：路径前缀 → 目标后端服务 */
const ROUTES = [
  {
    prefix: '/api/identity',
    // 身份认证服务（对应 vite proxy 里的 target）
    target: { host: '127.0.0.1', port: 5001 },
    rewrite: (path) => path.replace(/^\/api\/identity/, '/api')
  },
  {
    prefix: '/api/merchant',
    // 商品/订单服务
    target: { host: '127.0.0.1', port: 5002 },
    rewrite: (path) => path.replace(/^\/api\/merchant/, '/api')
  }
]

// ============================================================

const server = http.createServer((req, res) => {
  const route = ROUTES.find((r) => req.url.startsWith(r.prefix))

  // 没有匹配的路由
  if (!route) {
    res.writeHead(404, { 'Content-Type': 'application/json; charset=utf-8' })
    res.end(JSON.stringify({ message: `没有匹配的代理路由: ${req.method} ${req.url}` }))
    return
  }

  const targetPath = route.rewrite(req.url)

  // 改写 Host 头，模拟 vite proxy 的 changeOrigin
  const headers = { ...req.headers, host: `${route.target.host}:${route.target.port}` }

  // 转发请求到后端
  const proxyReq = http.request(
    {
      host: route.target.host,
      port: route.target.port,
      path: targetPath,
      method: req.method,
      headers
    },
    (proxyRes) => {
      res.writeHead(proxyRes.statusCode, proxyRes.headers)
      proxyRes.pipe(res)
    }
  )

  // 后端没启动 / 连不上时给出清晰提示，而不是让小程序一直转圈
  proxyReq.on('error', (err) => {
    console.error(`[proxy] 转发失败 → http://${route.target.host}:${route.target.port}${targetPath}\n        原因: ${err.message}`)
    res.writeHead(502, { 'Content-Type': 'application/json; charset=utf-8' })
    res.end(JSON.stringify({
      message: `后端服务未启动或无法连接: http://${route.target.host}:${route.target.port}`
    }))
  })

  req.pipe(proxyReq)
})

server.listen(PROXY_PORT, () => {
  console.log('==================================================')
  console.log(`  本地开发代理已启动: http://localhost:${PROXY_PORT}`)
  console.log('  小程序 BASE_URL 请设置为:')
  console.log(`    http://localhost:${PROXY_PORT}/api`)
  console.log('--------------------------------------------------')
  console.log('  路由映射:')
  ROUTES.forEach((r) => {
    console.log(`    ${r.prefix}/*  →  http://${r.target.host}:${r.target.port}/api/*`)
  })
  console.log('==================================================')
})