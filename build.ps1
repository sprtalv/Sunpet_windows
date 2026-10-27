param(
    [ValidateSet('All','SunPet','FengGe','Shaolan')][string]$Pet = 'All',
    [string]$Dotnet = 'dotnet',
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$cache = Join-Path $root '.build-cache'
New-Item -ItemType Directory -Force "$cache/temp","$cache/nuget","$cache/http","$root/artifacts/packages","$root/artifacts/test-results" | Out-Null
if (!$env:NUGET_PACKAGES) { $env:NUGET_PACKAGES = "$cache/nuget" }
$env:NUGET_HTTP_CACHE_PATH = "$cache/http"
$env:TEMP = "$cache/temp"
$env:TMP = $env:TEMP
$names = if ($Pet -eq 'All') { @('SunPet','FengGe','Shaolan') } else { @($Pet) }
$paths = @{}
if (Test-Path "$root/artifacts/current.json") { $paths = Get-Content "$root/artifacts/current.json" -Raw | ConvertFrom-Json -AsHashtable }
foreach($name in $names) {
    $project = "$root/pets/$name/Windows/$name.Windows/$name.Windows.csproj"
    if (!$SkipTests) {
        & $Dotnet run --project "$root/pets/$name/Windows/$name.Windows.Tests" -c Release *> "$root/artifacts/test-results/$name-core.txt"
        if ($LASTEXITCODE -ne 0) { throw "$name core tests failed" }
    }
    # Fresh staging prevents a previously used UserData directory entering a release.
    $stage = Join-Path $root ('artifacts/staging/' + [Guid]::NewGuid().ToString('N'))
    $folder = Join-Path $stage "$name.Windows-win-x64"
    & $Dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None "-p:PathMap=$root=/_/" -o $folder
    if ($LASTEXITCODE -ne 0) { throw "$name publish failed" }
    Set-Content "$folder/portable.flag" 'Portable settings are stored in UserData/<current user SID>.' -Encoding utf8
    if (!$SkipTests) {
        $report = "$root/artifacts/test-results/$name-published-self-test.txt"
        $process = Start-Process "$folder/$name.Windows.exe" -ArgumentList @('--self-test','--report',('"' + $report + '"')) -PassThru
        if (!$process.WaitForExit(60000)) { $process.Kill(); throw "$name self-test timed out" }
        if ($process.ExitCode -ne 0 -or (Get-Content $report -Tail 1) -ne 'PASS') { throw "$name published self-test failed" }
    }
    Copy-Item "$root/README.md" "$folder/README.md"
    Copy-Item "$root/NOTICE.md" "$folder/PROJECT-NOTICE.md"
    Copy-Item "$root/docs" "$folder/docs" -Recurse
    if (Test-Path "$folder/UserData") { throw 'Unexpected user data in fresh release staging' }
    $zip = "$root/artifacts/packages/$name.Windows-win-x64.zip"
    Compress-Archive -LiteralPath $folder -DestinationPath $zip -CompressionLevel Optimal -Force
    $paths[$name] = "$folder/$name.Windows.exe"
    Write-Output "PASS $name -> $zip"
}
$paths | ConvertTo-Json | Set-Content "$root/artifacts/current.json" -Encoding utf8
Get-ChildItem "$root/artifacts/packages/*.zip" | Sort-Object Name | ForEach-Object { (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name } | Set-Content "$root/artifacts/packages/SHA256SUMS.txt" -Encoding ascii
Write-Output 'PASS'
