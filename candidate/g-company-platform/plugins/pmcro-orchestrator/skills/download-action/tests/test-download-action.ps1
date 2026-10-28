$ErrorActionPreference='Stop'
$script=Join-Path $PSScriptRoot '..\scripts\download-action.ps1'
$pass=0
function Expect([bool]$condition,[string]$name) {
  if (-not $condition) { throw ("FAIL "+$name) }
  $script:pass++
  Write-Output ("PASS "+$name)
}
function Run([string[]]$a) {
  $output=& pwsh -NoProfile -File $script @a 2>&1 | Out-String
  return [pscustomobject]@{ Exit=$LASTEXITCODE; Text=$output }
}
$x=Run @()
$data=$x.Text | ConvertFrom-Json
Expect ($x.Exit -eq 0 -and -not $data.downloaded -and -not $data.registered -and
  -not $data.granted -and -not $data.activated -and -not $data.executed) 'inspect-no-authority'
$root=Join-Path ([IO.Path]::GetTempPath()) ('pmcro-candidate-test-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root) | Out-Null
try {
  $sample=Join-Path $root 'sample.txt'
  [IO.File]::WriteAllText($sample,'fixture')
  $s=[Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($sample))
  $digest=[Convert]::ToHexString($s).ToLowerInvariant()
  $x=Run @('-Mode','verify','-File',$sample,'-Sha256',$digest)
  Expect ($x.Exit -eq 0 -and $x.Text.Contains('verified-local-only')) 'verify-local-pinned-digest'
  $x=Run @('-Mode','verify','-File',$sample,'-Sha256',('0'*64))
  Expect ($x.Exit -ne 0 -and $x.Text.Contains('sha256-mismatch')) 'reject-wrong-sha256'
  $stage=Join-Path $root 'candidate-action-downloads'
  [IO.Directory]::CreateDirectory($stage) | Out-Null
  $x=Run @('-Mode','stage','-Url','https://github.com/owner/repo/a','-Sha256',$digest,
    '-Filename','a.zip','-StageDir',$stage)
  Expect ($x.Exit -ne 0 -and $x.Text.Contains('explicit-owner-stage-approval-required')) 'deny-without-explicit-approval'
  $x=Run @('-Mode','stage','-Url','file:///tmp/a','-Sha256',$digest,
    '-Filename','a.zip','-StageDir',$stage,'-ApproveStage')
  Expect ($x.Exit -ne 0 -and $x.Text.Contains('source-not-approved-https-github-host')) 'reject-file-url'
  $x=Run @('-Mode','stage','-Url','https://github.com/owner/repo/a','-Sha256',$digest,
    '-Filename','../a.zip','-StageDir',$stage,'-ApproveStage')
  Expect ($x.Exit -ne 0 -and $x.Text.Contains('unsafe-filename') -and
    @(Get-ChildItem -LiteralPath $stage).Count -eq 0) 'reject-unsafe-filename'
} finally { [IO.Directory]::Delete($root,$true) }
Write-Output ("RESULT: "+$pass+"/"+$pass+" local security checks passed; 0 network downloads")