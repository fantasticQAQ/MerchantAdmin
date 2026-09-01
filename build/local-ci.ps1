$ErrorActionPreference = "Stop"

# 仓库根目录（本脚本位于 build/ 下）
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot

$progressFile = Join-Path $repoRoot "build\ci-progress.txt"
$SHA = git rev-parse --short HEAD
Write-Host "Commit: $SHA"

function Set-Progress {
    param([string]$msg)
    Write-Host "`n[进度] $msg"
    Set-Content -Path $progressFile -Value $msg -Encoding UTF8
}

# ---------------------------------------------------------------------------
# 运行外部可执行程序（dotnet、docker、git 等）。
# 关键：这些工具会把"正常输出"写到 stderr（比如 docker compose 启动过程），
# 在 PowerShell $ErrorActionPreference = "Stop" 下会被误判为异常终止。
# 这里单独包一层：临时把 ErrorAction 切回 Continue，用 $LASTEXITCODE 判断真实成败。
# ---------------------------------------------------------------------------
function Invoke-External {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )
    if ([string]::IsNullOrWhiteSpace($FilePath)) { throw "FilePath 不能为空" }
    $prevEAP = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        if ($Arguments -and $Arguments.Count -gt 0) {
            & $FilePath @Arguments
        } else {
            & $FilePath
        }
        if ($LASTEXITCODE -ne 0) {
            throw "命令执行失败 (退出码 $LASTEXITCODE): $FilePath $($Arguments -join ' ')"
        }
    } finally {
        $ErrorActionPreference = $prevEAP
    }
}

# ---------------------------------------------------------------------------
# 如果指定项目下还没有任何迁移记录（dotnet ef migrations list 输出为空），
# 自动执行一次 migrations add InitialCreate，
# 这样后续 dotnet ef migrations script 不会只吐出一个 __EFMigrationsHistory 空壳。
# 注：Identity 项目可能把迁移放到 Data/Migrations 子目录，
#     所以不能简单看 Migrations/ 目录下的 .cs 数量，必须通过 migrations list 判定。
# ---------------------------------------------------------------------------
function Ensure-InitialMigration {
    param(
        [string]$ProjectRelPath,
        [string]$StartupRelPath,
        [string]$DbName
    )
    if ([string]::IsNullOrWhiteSpace($ProjectRelPath)) { throw "ProjectRelPath 不能为空" }
    if ([string]::IsNullOrWhiteSpace($StartupRelPath)) { throw "StartupRelPath 不能为空" }
    if ([string]::IsNullOrWhiteSpace($DbName))           { throw "DbName 不能为空" }

    $listArgs = @(
        "ef", "migrations", "list",
        "--project", $ProjectRelPath,
        "--startup-project", $StartupRelPath
    )

    # 用 --no-build 能省一次编译，但万一没 build 可能失败，所以默认还是让它自己 build
    $listLines = @(& dotnet @listArgs 2>&1)

    # 去除 Build started / Build succeeded / Build failed 这类构建输出头
    $migNames = @($listLines | ForEach-Object {
        $line = "$_"
        if ($line -match '^\s*(Build started|Build succeeded|Build failed|Done\.|To undo this action)') { return }
        if ($line -match 'error|fatal') { return }
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) { return }
        $trimmed
    })

    if ($migNames.Count -eq 0) {
        Write-Host ("  - [{0}] 无任何迁移，自动执行 migrations add InitialCreate ..." -f $DbName) -ForegroundColor Yellow
        $cmdArgs = @(
            "ef", "migrations", "add", "InitialCreate",
            "--project", $ProjectRelPath,
            "--startup-project", $StartupRelPath
        )
        Invoke-External -FilePath "dotnet" -Arguments $cmdArgs
        Write-Host ("  - [{0}] InitialCreate 迁移已生成" -f $DbName) -ForegroundColor Green
    } else {
        Write-Host ("  - [{0}] 已检测到 {1} 个迁移：{2}，跳过 InitialCreate 自动生成" `
            -f $DbName, $migNames.Count, ($migNames -join ', '))
    }
}

# ---------------------------------------------------------------------------
# 给 dotnet ef migrations script 生成的 SQL 文件头部注入：
#   1. CREATE DATABASE [xxx] （如果库不存在）
#   2. USE [xxx]
# 这样 sqlcmd 直接连 master 也能跑通，无需在调用侧提前建库。
# 另外修正 idempotent 输出的编码：EF 默认 UTF-8 no BOM，文件要以 UTF-8 BOM 写入避免 sqlcmd 乱码。
# ---------------------------------------------------------------------------
function Add-CreateDatabasePrologue {
    param(
        [string]$SqlFile,
        [string]$DatabaseName
    )
    if ([string]::IsNullOrWhiteSpace($SqlFile))      { throw "SqlFile 不能为空" }
    if ([string]::IsNullOrWhiteSpace($DatabaseName)) { throw "DatabaseName 不能为空" }
    if (-not (Test-Path $SqlFile)) {
        throw "SQL 文件不存在，无法注入 CREATE DATABASE：$SqlFile"
    }

    # EF 生成的 sql 默认 UTF-8 无 BOM
    $body = [System.IO.File]::ReadAllText($SqlFile, [System.Text.Encoding]::UTF8)

    $timeStr  = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $prologue = @"
-- =====================================================
-- 自动注入：库不存在则创建 + 切换上下文
-- 生成时间：$timeStr
-- -----------------------------------------------------
-- 说明：
--  1) 先把 QUOTED_IDENTIFIER / ANSI_NULLS 等 EF Core 要求的 SET 选项打开。
--     sqlcmd 默认 QUOTED_IDENTIFIER=OFF，EF 生成的建表脚本若用到主键索引
--     超长键、XML 类型方法、筛选索引等会报 Msg 1934。
--  2) 库不存在则 CREATE DATABASE。
--  3) USE [DatabaseName]。
-- =====================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
IF DB_ID(N'$DatabaseName') IS NULL
BEGIN
    CREATE DATABASE [$DatabaseName];
END;
GO
USE [$DatabaseName];
GO

"@

    # 如果文件开头已经是这次写的"自动注入"prologue（特征：SET QUOTED_IDENTIFIER ON + IF DB_ID(N'DbName')），
    # 先剥离旧注入，再写新的，避免重复或遗漏最新的 SET 选项。
    # （匹配到第 2 条 GO + USE 之后的空行作为注入块结束锚点。）
    $markerPattern = @"
(?s)^-- =====================================================\s*?
-- 自动注入：库不存在则创建 \+ 切换上下文.*?
USE \[[^\]]+\];\s*?
GO\s*?
\s*?
"@

    if ($body -match [regex]$markerPattern) {
        # 已注入过 prologue，先剥掉
        $body = [regex]::Replace($body, $markerPattern, "", 1)
        Write-Host "  - 检测到旧的自动注入头部，已剥离后重新注入"
    } elseif ($body -match "IF DB_ID\(N'$([regex]::Escape($DatabaseName))'\)") {
        # 旧版 prologue（不带 SET 选项），按同样规则尽量剥掉；如果剥不掉，就直接在更老的兼容模式下截断到 --idempotent 输出开始行
        Write-Host "  - 检测到旧版注入头（无 SET 选项），尝试剥离..."
        $body = [regex]::Replace($body, $markerPattern, "", 1)
    }

    $final    = $prologue + $body
    $utf8Bom  = New-Object System.Text.UTF8Encoding($true)
    [System.IO.File]::WriteAllText($SqlFile, $final, $utf8Bom)
    Write-Host ("  - 已注入 SET QUOTED_IDENTIFIER + CREATE DATABASE [{0}] + USE 头部（重写后长度 {1}）" -f $DatabaseName, $final.Length)
}

# ===========================================================================
# 1. 生成迁移 SQL 脚本（幂等，可重复执行）
# ===========================================================================
Set-Progress "1/3 生成迁移 SQL 脚本（Merchant + Identity）..."
Write-Host "`n[1/3] 生成迁移 SQL 脚本..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path "build\sql" | Out-Null

# 1.1 如果项目还没有首次迁移，自动生成 InitialCreate
$merchantProj  = "src\MerchantAdmin.Infrastructure"
$merchantStart = "src\MerchantAdmin.API"
$merchantDb    = "MerchantAdmin.Merchant"
Ensure-InitialMigration -ProjectRelPath $merchantProj -StartupRelPath $merchantStart -DbName $merchantDb

$identProj     = "src\Identity.API"
$identStart    = "src\Identity.API"
$identDb       = "MerchantAdmin.Identity"
Ensure-InitialMigration -ProjectRelPath $identProj -StartupRelPath $identStart -DbName $identDb

# 1.2 生成脚本（商家库）
$merchantArgs = @(
  "ef", "migrations", "script",
  "--project", "src/MerchantAdmin.Infrastructure",
  "--startup-project", "src/MerchantAdmin.API",
  "--idempotent",
  "--output", "build/sql/MerchantAdmin.sql"
)
Invoke-External -FilePath "dotnet" -Arguments $merchantArgs
Add-CreateDatabasePrologue -SqlFile "build\sql\MerchantAdmin.sql" -DatabaseName "MerchantAdmin.Merchant"
Write-Host "  - MerchantAdmin.sql 已生成（含 CREATE DATABASE 头部）"

# 1.3 生成脚本（Identity 库）
$identityArgs = @(
  "ef", "migrations", "script",
  "--project", "src/Identity.API",
  "--startup-project", "src/Identity.API",
  "--idempotent",
  "--output", "build/sql/Identity.sql"
)
Invoke-External -FilePath "dotnet" -Arguments $identityArgs
Add-CreateDatabasePrologue -SqlFile "build\sql\Identity.sql" -DatabaseName "MerchantAdmin.Identity"
Write-Host "  - Identity.sql 已生成（含 CREATE DATABASE 头部）"

# ===========================================================================
# 2. 迁移数据库（docker-compose 的 migrate profile 用 sqlcmd 执行 SQL）
# ===========================================================================
Set-Progress "2/3 迁移数据库（docker compose + sqlcmd）..."
Write-Host "`n[2/3] 迁移数据库（sqlcmd 执行 SQL）..." -ForegroundColor Cyan

# 迁移镜像每次都 --build --no-cache，避免 SQL 更新了但 COPY 还是旧的缓存
$buildMigArgs = @(
  "compose", "--project-directory", ".",
  "-f", "build/docker-compose.yml",
  "--profile", "migrate",
  "build", "--no-cache", "merchant-migrator", "identity-migrator"
)
Invoke-External -FilePath "docker" -Arguments $buildMigArgs

$upMigArgs = @(
  "compose", "--project-directory", ".",
  "-f", "build/docker-compose.yml",
  "--profile", "migrate",
  "up", "--abort-on-container-exit", "merchant-migrator", "identity-migrator"
)
Invoke-External -FilePath "docker" -Arguments $upMigArgs
Write-Host "  - merchant-migrator / identity-migrator 执行完毕"

# ===========================================================================
# 3. 构建生产镜像
# ===========================================================================
Set-Progress "3/3 构建生产镜像（4 个镜像）..."
Write-Host "`n[3/3] 构建生产镜像..." -ForegroundColor Cyan

Write-Host "  - 构建 merchant-api:latest ..."
Invoke-External -FilePath "docker" -Arguments @("build", "-t", "merchant-api:latest", "-f", "src/MerchantAdmin.API/Dockerfile", ".")

Write-Host "  - 构建 identity-api:latest ..."
Invoke-External -FilePath "docker" -Arguments @("build", "-t", "identity-api:latest", "-f", "src/Identity.API/Dockerfile", ".")

Write-Host "  - 构建 payment-api:latest ..."
Invoke-External -FilePath "docker" -Arguments @("build", "-t", "payment-api:latest", "-f", "src/Payment.API/Dockerfile", ".")

Write-Host "  - 构建 merchant-admin-frontend:latest ..."
Invoke-External -FilePath "docker" -Arguments @("build", "-t", "merchant-admin-frontend:latest", "-f", "src/MerchantAdmin.Frontend/Dockerfile", ".")

Set-Content -Path $progressFile -Value "3/3 全部镜像构建完成" -Encoding UTF8
Write-Host "`n本地 CI/CD 完成：迁移 SQL 已生成、数据库已迁移、生产镜像已构建（tag = latest）" -ForegroundColor Green
