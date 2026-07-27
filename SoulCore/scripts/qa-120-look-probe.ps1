# TASK-120 look / ParseAutonomyCommand probe
$ErrorActionPreference='Stop'
function Send-FrameR {
  param($Ws, [string]$Json)
  $bytes = [System.Text.Encoding]::UTF8.GetBytes($Json)
  $seg = [ArraySegment[byte]]::new($bytes)
  $cts = [System.Threading.CancellationTokenSource]::new(8000)
  $null = $Ws.SendAsync($seg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).Wait(8000)
  $cts.Dispose()
}
function Recv-FrameR {
  param($Ws, [int]$TimeoutMs=8000)
  $buf = New-Object byte[] 16384
  $seg=[ArraySegment[byte]]::new($buf)
  $cts=[System.Threading.CancellationTokenSource]::new($TimeoutMs)
  $t=$Ws.ReceiveAsync($seg,$cts.Token)
  if(-not $t.Wait($TimeoutMs)){ $cts.Cancel(); throw 'recv timeout'}
  $cts.Dispose()
  $r=$t.Result
  return ,[System.Text.Encoding]::UTF8.GetString($buf,0,$r.Count)
}
function Connect-WsR {
  param([string]$Url, [int]$TimeoutMs = 15000)
  $ws = [System.Net.WebSockets.ClientWebSocket]::new()
  $cts = [System.Threading.CancellationTokenSource]::new($TimeoutMs)
  $t = $ws.ConnectAsync([Uri]$Url, $cts.Token)
  if (-not $t.Wait($TimeoutMs)) { $cts.Cancel(); $ws.Dispose(); throw "Connect timeout to $Url" }
  $null = $t.GetAwaiter().GetResult()
  $cts.Dispose()
  if ($ws.State -ne [System.Net.WebSockets.WebSocketState]::Open) { $ws.Dispose(); throw "Connect failed (state=$($ws.State))" }
  return ,$ws
}

$ws = $null
try {
  $ws = Connect-WsR 'ws://127.0.0.1:8888'
  try { $null = Recv-FrameR $ws 3000 } catch {}
  Send-FrameR $ws '{"type":"command","payload":{"name":"autonomy","args":{"command":"look_at_player"}}}'
  Write-Output ("LOOK_REPLY=" + (Recv-FrameR $ws 10000))
  Send-FrameR $ws '{"type":"command","payload":{"name":"autonomy","args":{}}}'
  Write-Output ("BAD_ARGS_REPLY=" + (Recv-FrameR $ws 8000))
} finally {
  if ($ws -is [System.Net.WebSockets.ClientWebSocket]) {
    try { $c=[System.Threading.CancellationTokenSource]::new(1500); $null=$ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure,'bye',$c.Token).Wait(1500) } catch {}
    $ws.Dispose()
  }
}
