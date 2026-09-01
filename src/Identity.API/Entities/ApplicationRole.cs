namespace Identity.API.Entities
{
    /// <summary>自定义角色：在 Identity 角色基础上扩展软删除字段。</summary>
    public class ApplicationRole : IdentityRole<long>
    {
        /// <summary>是否启用：停用（软删除）后不可再分配给新用户，历史数据保留。</summary>
        public bool IsActive { get; set; } = true;

        public ApplicationRole() { }

        public ApplicationRole(string name) : base(name) { }
    }
}
