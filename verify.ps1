# Builds the same code twice into separate folders and compares the results:
#   classic   -p:RuntimeAsync=false   compiler-generated state machines
#   runtime   default                 <Features>$(Features);runtime-async=on</Features>
# Run: pwsh ./verify.ps1  (or powershell -ExecutionPolicy Bypass -File verify.ps1)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$failed = 0

function Check([string] $claim, [bool] $ok, [string] $detail) {
    $label = if ($ok) { 'PASS' } else { 'FAIL' }
    Write-Host "[$label] $claim"
    Write-Host "       $detail"
    if (-not $ok) { $script:failed++ }
}

function Run([string] $name, [string[]] $properties) {
    $out = Join-Path $PSScriptRoot "artifacts/$name"
    & dotnet build -c Release --no-incremental -o $out @properties 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "build $name failed" }
    $output = & dotnet (Join-Path $out 'DotnetRuntimeAsync.dll') 2>&1 | Out-String
    $exit = $LASTEXITCODE
    Write-Host "----- $name -----"
    Write-Host $output.TrimEnd()
    Write-Host ''
    return @{
        Exit   = $exit
        Mode   = [regex]::Match($output, 'Build: (.+)').Groups[1].Value.Trim()
        Frames = [int][regex]::Match($output, '(\d+) frames').Groups[1].Value
        Ns     = [double][regex]::Match($output, '([\d.]+) ns and').Groups[1].Value
        Bytes  = [int][regex]::Match($output, 'ns and (\d+) B').Groups[1].Value
        Us     = [double][regex]::Match($output, '([\d.]+) us per throw').Groups[1].Value
    }
}

Write-Host "SDK $(dotnet --version)"
Write-Host ''
$classic = Run 'classic' @('-p:RuntimeAsync=false')
$runtime = Run 'runtime' @()

Write-Host '----- comparison -----'
Check 'Both builds pass their own checks' (($classic.Exit + $runtime.Exit) -eq 0) "classic exit $($classic.Exit), runtime exit $($runtime.Exit)"
Check 'The classic build uses state machines, the runtime build does not' ($classic.Mode -eq 'classic state machines' -and $runtime.Mode -eq 'runtime async') "classic: $($classic.Mode); runtime: $($runtime.Mode)"
Check 'Live stack trace: fewer frames with runtime async' ($runtime.Frames -lt $classic.Frames) "$($classic.Frames) frames -> $($runtime.Frames) frames"
Check 'Sync-completing chain: faster and allocation-free' ($runtime.Ns -lt $classic.Ns -and $runtime.Bytes -eq 0 -and $classic.Bytes -gt 0) "$($classic.Ns) ns, $($classic.Bytes) B -> $($runtime.Ns) ns, $($runtime.Bytes) B"
Check 'Exception through 30 awaits: faster with runtime async' ($runtime.Us -lt $classic.Us) "$($classic.Us) us -> $($runtime.Us) us ($([math]::Round($classic.Us / $runtime.Us, 1))x)"

Write-Host ''
if ($failed -eq 0) { Write-Host 'verify.ps1: all checks passed.'; exit 0 }
Write-Host "verify.ps1: $failed check(s) failed."; exit 1
