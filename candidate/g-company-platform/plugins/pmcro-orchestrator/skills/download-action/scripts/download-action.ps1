# PMCR-O candidate-only pinned action artifact staging. No install, extraction or execution.
[CmdletBinding()]
param(
  [ValidateSet('inspect','verify','stage')][string]$Mode='inspect',
  [string]$Url,
  [string]$Sha256,
  [string]$Filename,
  [string]$StageDir,
  [string]$File,
  [switch]$ApproveStage
)
$ErrorActionPreference='Stop'
$Hosts=@('github.com','api.github.com','raw.githubusercontent.com','objects.githubusercontent.com','release-assets.githubusercontent.com')
$MaxBytes=100MB

function Assert-Url([string]$InputUrl) {
  $u=[uri]$InputUrl
  if ($u.Scheme -cne 'https' -or $u.Host -notin $Hosts -or
    $u.UserInfo.Length -ne 0 -or -not $u.IsDefaultPort -or $u.AbsolutePath -eq '/') {
    throw 'source-not-approved-https-github-host'
  }
  return $u
}
function Assert-Digest([string]$Value) {
  if ($Value -cnotmatch '^[0-9a-fA-F]{64}$') { throw 'sha256-required' }
}
function File-Digest([string]$Path) {
  $s=[IO.File]::OpenRead($Path)
  try { $alg=[Security.Cryptography.SHA256]::Create()
    try { return [Convert]::ToHexString($alg.ComputeHash($s)).ToLowerInvariant() }
    finally { $alg.Dispose() }
  } finally { $s.Dispose() }
}
try {
  if ($Mode -eq 'inspect') {
    [pscustomobject]@{ status='candidate-only'; allowedHosts=$Hosts; downloaded=$false;
      registered=$false; granted=$false; activated=$false; executed=$false } |
      ConvertTo-Json -Compress
    exit 0
  }
  Assert-Digest $Sha256
  if ($Mode -eq 'verify') {
    if ([string]::IsNullOrWhiteSpace($File)) { throw 'file-required' }
    $actual=File-Digest $File
    if ($actual -cne $Sha256.ToLowerInvariant()) { throw 'sha256-mismatch' }
    [pscustomobject]@{status='verified-local-only';sha256=$actual;bytes=([IO.FileInfo]::new($File).Length)} |
      ConvertTo-Json -Compress
    exit 0
  }
  if (-not $ApproveStage) { throw 'explicit-owner-stage-approval-required' }
  if ([string]::IsNullOrWhiteSpace($Url) -or [string]::IsNullOrWhiteSpace($Filename) -or
    [string]::IsNullOrWhiteSpace($StageDir)) { throw 'stage-requires-url-filename-directory' }
  $current=Assert-Url $Url
  if ($Filename -eq '.' -or $Filename -eq '..' -or
      $Filename -cnotmatch '^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}$') { throw 'unsafe-filename' }
  $root=[IO.DirectoryInfo]::new([IO.Path]::GetFullPath($StageDir))
  if (-not $root.Exists -or $root.Name -cne 'candidate-action-downloads' -or
      (($root.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
    throw 'preexisting-candidate-action-downloads-directory-required'
  }
  $target=[IO.Path]::Combine($root.FullName,$Filename)
  if ([IO.File]::Exists($target) -or [IO.Directory]::Exists($target)) { throw 'target-exists-no-overwrite' }
  $temp=[IO.Path]::Combine($root.FullName,('.candidate-'+[Guid]::NewGuid().ToString('N')+'.part'))
  $handler=[Net.Http.HttpClientHandler]::new()
  $handler.AllowAutoRedirect=$false
  $client=[Net.Http.HttpClient]::new($handler)
  $client.Timeout=[TimeSpan]::FromSeconds(40)
  $response=$null
  try {
    for ($hops=0;$hops -lt 6;$hops++) {
      if ($null -ne $response) { $response.Dispose() }
      $response=$client.GetAsync($current,[Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
      $status=[int]$response.StatusCode
      if ($status -in @(301,302,303,307,308)) {
        if ($hops -eq 5 -or $null -eq $response.Headers.Location) { throw 'too-many-redirects' }
        $current=Assert-Url ([uri]::new($current,$response.Headers.Location).AbsoluteUri)
        continue
      }
      if (-not $response.IsSuccessStatusCode) { throw ('http-status-'+$status) }
      break
    }
    if ($response.Content.Headers.ContentLength -gt $MaxBytes) { throw 'artifact-too-large' }
    $inputStream=$response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
    try {
      $output=[IO.FileStream]::new($temp,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
      try {
        $buffer=[byte[]]::new(1048576)
        [long]$total=0
        while (($count=$inputStream.Read($buffer,0,$buffer.Length)) -gt 0) {
          $total+=$count
          if ($total -gt $MaxBytes) { throw 'artifact-too-large' }
          $output.Write($buffer,0,$count)
        }
        $output.Flush($true)
      } finally { $output.Dispose() }
    } finally { $inputStream.Dispose() }
    $observed=File-Digest $temp
    if ($observed -cne $Sha256.ToLowerInvariant()) { throw 'sha256-mismatch' }
    [IO.File]::Move($temp,$target) # no overwrite: atomic move on same filesystem
    [pscustomobject]@{status='staged-only';path=$target;sha256=$observed;bytes=$total;
      registered=$false;activated=$false;executed=$false} | ConvertTo-Json -Compress
  } finally {
    if ($null -ne $response) { $response.Dispose() }
    $client.Dispose()
    $handler.Dispose()
    if ([IO.File]::Exists($temp)) { [IO.File]::Delete($temp) }
  }
  exit 0
} catch {
  [Console]::Error.WriteLine(('BLOCKED: '+$_.Exception.Message))
  exit 2
}