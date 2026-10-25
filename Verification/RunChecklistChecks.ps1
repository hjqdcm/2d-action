param([ValidateSet('ChecklistChecks', 'HeavyAttackChecks', 'KineticChecks', 'BladeFeedbackChecks', 'EquipChecks', 'HeavyMotionChecks', 'SingleHandChecks', 'Hero360Checks', 'GaitChecks')][string]$Suite = 'ChecklistChecks')
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$unityData = 'D:\unity\6000.6.7f1\Editor\Data'
$monoLib = Join-Path $unityData 'MonoBleedingEdge\lib\mono\4.5'
$unityManaged = Join-Path $unityData 'Managed\UnityEngine'
$compilerPath = 'C:\Program Files\dotnet\sdk\10.0.402\Roslyn\bincore\csc.dll'
$outPath = Join-Path $projectPath ('Temp\' + $Suite)
New-Item -ItemType Directory -Path $outPath -Force | Out-Null
$arguments = @('-nologo','-nostdlib+','-target:exe','-langversion:9',('-out:"' + $outPath + '\' + $Suite + '.exe"'))
foreach ($assembly in @('mscorlib.dll','System.dll','System.Core.dll')) { $arguments += '-r:"' + (Join-Path $monoLib $assembly) + '"' }
$arguments += '-r:"' + (Join-Path $monoLib 'Facades\netstandard.dll') + '"'
foreach ($assembly in Get-ChildItem -LiteralPath $unityManaged -Filter '*.dll') { $arguments += '-r:"' + $assembly.FullName + '"' }
$arguments += '-r:"' + (Join-Path $projectPath 'Temp\bin\Debug\Assembly-CSharp.dll') + '"'
$arguments += '"' + (Join-Path $PSScriptRoot ($Suite + '.cs')) + '"'
$responsePath = Join-Path $outPath 'checklist.rsp'
Set-Content -LiteralPath $responsePath -Value $arguments -Encoding utf8
& dotnet exec $compilerPath ('@' + $responsePath)
if ($LASTEXITCODE -ne 0) { throw 'Check compilation failed.' }
$env:MONO_PATH = $unityManaged + ';' + (Join-Path $unityData 'Managed') + ';' + (Join-Path $projectPath 'Temp\bin\Debug')
& (Join-Path $unityData 'MonoBleedingEdge\bin\mono.exe') (Join-Path $outPath ($Suite + '.exe'))
if ($LASTEXITCODE -ne 0) { throw ($Suite + ' failed.') }
