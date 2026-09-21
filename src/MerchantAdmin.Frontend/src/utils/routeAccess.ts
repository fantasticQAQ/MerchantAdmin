import type { RouteMeta } from 'vue-router'

/**
 * 仅 Admin / SuperAdmin 可访问的页面。
 * 与后端 UsersController / RolesController / LogsController 上的
 * [Authorize(Roles = "Admin,SuperAdmin")] 对齐。
 */
export const ADMIN_ONLY_ROLES = ['Admin', 'SuperAdmin']

/**
 * 当前角色是否满足路由的角色要求。
 * 路由没声明 meta.roles 视为不限制；声明了则至少要命中其中一个。
 *
 * 单独放在这里是为了让 router 和 layout 都能用，同时避免两者互相 import 成环
 * （router 静态引用了 layout 组件）。
 */
export function canAccess(meta: RouteMeta, roles: string[]): boolean {
  const required = meta.roles as string[] | undefined
  return !required?.length || roles.some(role => required.includes(role))
}
