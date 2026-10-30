param([string]$ProjectPath = 'E:/Unity/Test', [switch]$Individual)
$ErrorActionPreference = 'Stop'
$checks = @(
    'ValidateLongitudinalClimb',
    'ValidateSurfaceVisualLifecycle',
    'ValidateShoulderClimb',
    'ValidateShoulderArticulation',
    'ValidateRutWallContacts',
    'ValidateVehicleRutDynamics',
    'ValidateSplitGripDifferential',
    'ValidateOffroadOverhaul',
    'ValidateOffroadDrive',
    'ValidateNaturalMud',
    'ValidateActualMudDrive',
    'ValidateMudMomentum',
    'ValidateVehicleEquipment',
    'ValidateStreamedMudPersistence'
)
$results = @()
if (!$Individual) {
    # Compile the existing scenarios into one ephemeral assembly. Their Main calls
    # execute within one Editor callback, so another CLI request cannot toggle
    # Play mode between scenarios. Nothing is imported into Assets.
    $sources = foreach ($check in $checks) { Get-Content -LiteralPath (Join-Path $ProjectPath "AgentScripts/$check.cs") -Raw }
    $imports = @($sources | ForEach-Object { [regex]::Matches($_, '(?m)^using [^\r\n]+;') | ForEach-Object { $_.Value } } | Sort-Object -Unique)
    $bodies = $sources | ForEach-Object { [regex]::Replace($_, '(?m)^using [^\r\n]+;\r?\n', '') }
    $calls = $checks | ForEach-Object { 'results.Add(new { name = "' + $_ + '", passed = true, result = ' + $_ + '.Main() });' }
    $runner = @'
public static class OffroadChecksBatch {
    public static string Main() {
        var results = new System.Collections.Generic.List<object>();
        try {
CALLS
        } catch (System.Exception error) {
            System.IO.File.WriteAllText("docs/reports/offroad-checks.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { utc = System.DateTime.UtcNow.ToString("O"), passed = false, checks = results, error = error.ToString() }, Newtonsoft.Json.Formatting.Indented));
            throw;
        }
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(new { utc = System.DateTime.UtcNow.ToString("O"), passed = true, checks = results }, Newtonsoft.Json.Formatting.Indented);
        System.IO.File.WriteAllText("docs/reports/offroad-checks.json", json);
        return json;
    }
}
'@
    $runner = $runner.Replace('CALLS', ($calls -join [Environment]::NewLine))
    $batchPath = Join-Path $ProjectPath 'work/offroad-check-batch.cs'
    New-Item -ItemType Directory -Path (Split-Path $batchPath) -Force | Out-Null
    [IO.File]::WriteAllText($batchPath, (($imports + $bodies + $runner) -join [Environment]::NewLine))
    $response = unity command script run --file $batchPath --entry OffroadChecksBatch.Main --timeout 120 --project-path $ProjectPath --json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or !$response.success -or !$response.data.result.success) {
        $response | ConvertTo-Json -Depth 12 | Write-Output
        throw 'Offroad batch failed; inspect docs/reports/offroad-checks.json and compiler diagnostics'
    }
    $report = $response.data.result.result | ConvertFrom-Json
    foreach ($check in $report.checks) { Write-Output "PASS $($check.name)" }
    return
}
foreach ($check in $checks) {
    $response = unity command script run --file "AgentScripts/$check.cs" --entry "$check.Main" --project-path $ProjectPath --json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or !$response.success -or !$response.data.result.success) {
        $response | ConvertTo-Json -Depth 12 | Write-Output
        throw "Offroad check failed: $check"
    }
    $results += [pscustomobject]@{ name = $check; passed = $true; result = $response.data.result.result }
    Write-Output "PASS $check"
}
$report = [pscustomobject]@{ utc = [DateTime]::UtcNow.ToString('O'); passed = $true; checks = $results }
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $ProjectPath 'docs/reports/offroad-checks.json') -Encoding utf8
