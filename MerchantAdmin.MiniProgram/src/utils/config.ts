// ==================== 后端地址配置 ====================
//
// 通过 MODE 一键切换两种模式：
//   MODE = 'proxy'   电脑上开发：先 `npm run proxy` 启动本地代理（走 8080）
//   MODE = 'direct'  真机调试：手机与电脑连同一 WiFi，直连各后端服务端口（无需代理）
//
// 直连模式下，把 LAN_HOST 换成你电脑的局域网 IP 即可。
// 下面填的是本机以太网 IPv4（192.168.1.2，已用 ipconfig 查到）。

export const MODE: 'proxy' | 'direct' = 'proxy'

/** 电脑局域网 IP（真机调试用，改这里即可）。手机必须用这个 IP 才能连到电脑，不能用 localhost */
const LAN_HOST = 'http://192.168.1.2'

/** 本地代理地址（MODE = 'proxy' 时使用） */
const PROXY_BASE = 'http://localhost:8080/api'

/** 直连模式：请求路径前缀 → 后端服务地址 */
const DIRECT_BASES: Record<string, string> = {
  '/identity': `${LAN_HOST}:5001/api`, // 身份认证服务（用户 / 角色 / 登录）
  '/merchant': `${LAN_HOST}:5002/api` // 商品 / 订单 / 日志服务
}

/**
 * 把前端请求路径解析为最终请求地址。
 * direct：剥掉业务前缀、直连对应端口（/identity/wxauth/login → http://host:5001/api/wxauth/login）
 * proxy ：原样拼接本地代理地址
 */
export function resolveUrl(url: string): string {
  if (MODE === 'direct') {
    for (const prefix of Object.keys(DIRECT_BASES)) {
      if (url.startsWith(prefix)) {
        return DIRECT_BASES[prefix] + url.slice(prefix.length)
      }
    }
  }
  return PROXY_BASE + url
}