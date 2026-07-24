using Microsoft.EntityFrameworkCore;
using Test;

var builder = WebApplication.CreateBuilder(args);

// ✅ 注册 DbContext（必须）
builder.Services.AddDbContext<TestDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// ❌ 删掉下面这些
// using var conn = new SqlConnection(...);
// await conn.OpenAsync();

var app = builder.Build();

app.MapGet("/", () => "ok");

app.Run();