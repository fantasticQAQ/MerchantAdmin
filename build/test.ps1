# 库存扣减压测：$c 并发 / $n 总请求，每个请求用独立幂等键（x-requestid）
# 【重要】本文件必须保存为 "UTF-8 with BOM"，否则 PowerShell 5.1 会把中文读成乱码
Add-Type -AssemblyName System.Net.Http

$base  = 'http://localhost:8080'
$url   = "$base/api/merchant/products/1066"
$body  = '{"stockDelta":-1}'
$c     = 200    # 并发
$n     = 200    # 总请求数

# 运行时登录拿 token（别硬编码：会过期）
$login = Invoke-RestMethod -Uri "$base/api/identity/auth/login" -Method Post `
         -Body '{"userName":"fantastic","password":"123456"}' -ContentType 'application/json'
$token = $login.token
if (-not $token) { throw '登录失败：拿不到 token' }

[System.Net.ServicePointManager]::DefaultConnectionLimit = $c   # PS 5.1 必需，不加就退化成 2 并发

$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(30)
$client.DefaultRequestHeaders.Authorization =
    [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $token)

$all = New-Object System.Collections.ArrayList
$sw  = [System.Diagnostics.Stopwatch]::StartNew()

# 分批：每批 $c 个并发发出，本批完成再发下一批 —— 等价 bombardier 的 -c/-n 语义
for ($sent = 0; $sent -lt $n; $sent += $c) {
    $batch = 1..([Math]::Min($c, $n - $sent)) | ForEach-Object {
        $req = [System.Net.Http.HttpRequestMessage]::new('PUT', $url)
        $req.Headers.Add('x-requestid', [guid]::NewGuid().ToString())   # 每请求独立幂等键
        $req.Content = [System.Net.Http.StringContent]::new($body, [System.Text.Encoding]::UTF8, 'application/json')
        $client.SendAsync($req)
    }
    foreach ($t in $batch) { try { [void]$t.Wait() } catch {}; [void]$all.Add($t) }
}

$el = $sw.Elapsed.TotalSeconds
Write-Host ("总请求 {0}  并发 {1}  耗时 {2:N2}s  吞吐 {3:N1} req/s" -f $n, $c, $el, ($n / $el))

Write-Host '--- 状态码分布（0 = 传输层失败，没拿到响应）---'
$all | Where-Object { $_.Status -eq 'RanToCompletion' } |
    Group-Object { [int]$_.Result.StatusCode } | Select-Object Count, Name

$bad = $all | Where-Object { $_.Status -ne 'RanToCompletion' }
Write-Host ("任务异常数: {0}" -f $bad.Count)
$bad | Select-Object -First 3 | ForEach-Object {
    $e = $_.Exception; while ($e.InnerException) { $e = $e.InnerException }
    "$($e.GetType().Name): $($e.Message)"
}
