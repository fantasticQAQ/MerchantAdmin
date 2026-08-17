using Identity.API.Entities;
using Identity.API;
using Identity.API.Entities;
using Identity.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. 控制器 + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo { Title = "Identity API", Version = "v3" });

    // ✅ 1. 定义 JWT 认证
    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "请输入 JWT Token，格式：Bearer {token}"
    });

    // ✅ 2. 全局要求认证
    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 2. 数据库上下文
builder.Services.AddDbContext<IdentityDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// 3. Identity（用户管理）
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>()
    .AddEntityFrameworkStores<IdentityDbContext>()
    .AddDefaultTokenProviders();

// ✅ 配置密码规则
builder.Services.Configure<IdentityOptions>(options =>
{
    // 密码设置
    options.Password.RequireDigit = false;           // 不需要数字
    options.Password.RequireLowercase = false;      // 不需要小写字母
    options.Password.RequireUppercase = false;      // 不需要大写字母
    options.Password.RequireNonAlphanumeric = false; // 不需要特殊字符
    options.Password.RequiredLength = 6;             // 最小长度 6
    options.Password.RequiredUniqueChars = 1;         // 唯一字符数

    // 锁定设置（防止暴力破解）
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // 用户设置
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    options.User.RequireUniqueEmail = true;
});

// 4. JWT 认证
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine("❌ JWT 校验失败：" + context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            // 校验 SecurityStamp：用户改密码等安全凭证变更后，旧 token 立即失效
            var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var securityStamp = context.Principal?.FindFirstValue("securityStamp");
            if (userId != null)
            {
                using var scope = context.HttpContext.RequestServices.CreateScope();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userManager.FindByIdAsync(userId);
                if (user is null || user.SecurityStamp != securityStamp)
                {
                    context.Fail("安全凭证已变更，请重新登录");
                    return;
                }
            }
            Console.WriteLine("✅ JWT 校验成功");
        }
    };

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = false,
        ValidateLifetime = true,

        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
});

// 5. 授权服务（必须加！）
builder.Services.AddAuthorization();

// 6. Token 服务
builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health").AllowAnonymous();

// 初始化角色与管理员（种子数据）
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    foreach (var roleName in new[] { "SuperAdmin", "Admin", "Operator" })
    {
        var role = await roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            await roleManager.CreateAsync(new ApplicationRole(roleName) { IsActive = true });
        }
    }

    var admin = await userManager.FindByNameAsync("admin");
    if (admin is null)
    {
        admin = new ApplicationUser("admin", "admin@qq.com");
        var createResult = await userManager.CreateAsync(admin, "123456");
        Console.WriteLine($"[SEED] CreateAsync Succeeded={createResult.Succeeded} Errors={string.Join(";", createResult.Errors.Select(e => e.Description))}");
        if (createResult.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "SuperAdmin");
        }
    }
}

//多个 Pod 同时启动时可能并发迁移（SQL Server 会锁表，一般不会炸，但会报错） 放部署文件中
//using (var scope = app.Services.CreateScope())
//{
//    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
//    db.Database.Migrate();
//}

app.Use(async (context, next) =>
{
    Console.WriteLine($"REQ {context.Request.Method} {context.Request.Path} AuthHeader: {context.Request.Headers["Authorization"]}");
    await next();
});

// 7. Swagger（仅开发环境启用，生产关闭避免泄露接口）
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 8. 认证 & 授权（顺序不能错）
app.UseAuthentication(); // ✅ 必须加！
app.UseAuthorization();

app.MapControllers();

app.Run();
