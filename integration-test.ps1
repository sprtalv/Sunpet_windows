$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$env:TEMP = Join-Path $root '.build-cache/temp'
$env:TMP = $env:TEMP
$env:DOTNET_ROOT = ''
$results = Join-Path $root 'artifacts/test-results/integration'
New-Item -ItemType Directory -Force $results | Out-Null
$apps = @(
    @{Name='SunPet'; Repo='SunPet-Windows'},
    @{Name='FengGe'; Repo='FengGe-Windows'},
    @{Name='Shaolan'; Repo='Shaolan-Windows'}
)
$summary = @()
foreach($source in @('Shaolan','FengGe','SunPet')) {
    $scope = 'acceptance-' + $source + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
    $processes = @()
    foreach($app in $apps) {
        $exe = (Get-Content (Join-Path $root 'artifacts/current.json') -Raw | ConvertFrom-Json).($app.Name)
        $log = Join-Path $results "$source-$($app.Name).log"
        $arguments = @('--integration-test','--ipc-scope',$scope,'--trace','--trace-file',$log)
        if($app.Name -eq $source) { $arguments += @('--play','7') }
        $process = Start-Process $exe -ArgumentList $arguments -PassThru
        $processes += @{Process=$process;Name=$app.Name;Exe=$exe;Log=$log}
    }
    Start-Sleep -Seconds 2
    foreach($item in $processes) {
        $duplicate = Start-Process $item.Exe -ArgumentList @('--ipc-scope',$scope,'--trace','--trace-file',(Join-Path $results "duplicate-$source-$($item.Name).log")) -PassThru
        if (!$duplicate.WaitForExit(4000) -or $duplicate.ExitCode -ne 0) { throw "Duplicate did not exit: $($item.Name)" }
    }
    foreach($item in $processes) {
        if (!$item.Process.WaitForExit(18000) -or $item.Process.ExitCode -ne 0) { throw "Process failed: $($item.Name)" }
        $text = Get-Content $item.Log -Raw
        if ($text -notmatch 'closed: audio, tray, ipc disposed') { throw 'Missing cleanup confirmation' }
    }
    $sun = Get-Content (Join-Path $results "$source-SunPet.log") -Raw
    $feng = Get-Content (Join-Path $results "$source-FengGe.log") -Raw
    $shao = Get-Content (Join-Path $results "$source-Shaolan.log") -Raw
    $counts = @([regex]::Matches($sun,'play clip=').Count,[regex]::Matches($feng,'play clip=').Count,[regex]::Matches($shao,'play clip=').Count)
    $expected = switch($source) { 'Shaolan' { '2,1,1' }; 'FengGe' { '1,1,0' }; 'SunPet' { '1,0,0' } }
    if (($counts -join ',') -ne $expected) { throw "$source wrong chain counts $($counts -join ',') expected $expected" }
    if($source -eq 'Shaolan' -and ($sun -notmatch 'hop=1 root=local.shaolan' -or $sun -notmatch 'hop=3 root=local.shaolan' -or $feng -notmatch 'hop=2 root=local.shaolan')) { throw 'Wrong hop/root propagation' }
    $summary += "PASS source=$source counts(Sun,Feng,Shao)=$($counts -join ','); real processes; duplicate exits; cleanup"
}
$summary += 'PASS'
$summary | Set-Content (Join-Path $results 'integration.txt') -Encoding utf8
$summary

