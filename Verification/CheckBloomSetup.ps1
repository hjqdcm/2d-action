$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$checkCount = 0
function Read-ProjectFile([string]$relative) { Get-Content -LiteralPath (Join-Path $projectPath $relative) -Raw }
function Check([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
    $script:checkCount++
}
$urp = Read-ProjectFile 'Assets/Settings/UniversalRP.asset'
$renderer = Read-ProjectFile 'Assets/Settings/Renderer2D.asset'
$profile = Read-ProjectFile 'Assets/Settings/DefaultVolumeProfile.asset'
$scene = Read-ProjectFile 'Assets/SlashDemo/Scenes/SlashArena.unity'
$shader = Read-ProjectFile 'Assets/SlashDemo/Resources/SlashDemo/BladeTrailAdditive.shader'
Check ($urp -match 'm_SupportsHDR: 1') 'URP HDR is disabled.'
Check ($renderer -match 'm_PostProcessData: \{fileID: 11400000, guid: 41439944d30ece34e96484bdb6645b55') '2D Renderer has no valid post-processing resource.'
$postMeta = Read-ProjectFile 'Library/PackageCache/com.unity.render-pipelines.universal@ffe395a3a1d1/Runtime/Data/PostProcessData.asset.meta'
Check ($postMeta -match 'guid: 41439944d30ece34e96484bdb6645b55') 'Renderer post-process reference does not match installed URP.'
$bloom = [regex]::Match($profile, '(?ms)^--- !u!114 &-3978695733392391637\r?\n.*?(?=^---|\z)').Value
Check ($bloom -match 'active: 1') 'Bloom override is disabled.'
Check ($bloom -match 'threshold:\s+m_OverrideState: 1\s+m_Value: 0.9') 'Bloom threshold is not configured.'
Check ($bloom -match 'intensity:\s+m_OverrideState: 1\s+m_Value: 0.85') 'Bloom intensity is not enabled.'
Check ($bloom -match 'scatter:\s+m_OverrideState: 1\s+m_Value: 0.6') 'Bloom scatter is not configured.'
Check ($bloom -match 'm_Value: \{r: 0.9, g: 0.95, b: 1, a: 1\}') 'Bloom has no cool tint.'
Check ($scene -match 'bloomProfile: \{fileID: 11400000, guid: 3f9215ea0144899419cfbc0957140d3f') 'Arena does not reference its Bloom profile.'
Check ($scene -match 'm_RenderPostProcessing: 1') 'Scene camera does not enable post-processing.'
Check ($scene -match 'guid: a79441f348de89743a2939f4d699eac1') 'Scene camera has no URP additional data.'
$ids = [regex]::Matches($scene, '(?m)^--- !u!\d+ &(-?\d+)') | ForEach-Object { $_.Groups[1].Value }
$duplicates = @($ids | Group-Object | Where-Object Count -gt 1)
Check ($duplicates.Count -eq 0) 'Scene contains duplicate object IDs.'
Check ($shader -match 'Blend SrcAlpha One') 'Blade trail is not additive.'
Check ($shader -match '_Intensity.+2.2') 'Blade core has no HDR intensity default.'
Check ($shader -match 'input.color.rgb \* lerp\(_GlowIntensity, _Intensity, input.emission\), input.color.a') 'HDR boost must preserve alpha and separate core from glow.'
$player = Read-ProjectFile 'Assets/SlashDemo/Scripts/SlashPlayer.cs'
Check ($player -match 'trailMesh.SetUVs\(0, trailEmission\)') 'Trail mesh does not supply emission weights to the shader.'
$cameraSetup = Read-ProjectFile 'Assets/SlashDemo/Scripts/SlashArena.cs'
Check ($cameraSetup -match 'volume.isGlobal = true' -and $cameraSetup -match 'volume.sharedProfile = bloomProfile') 'Global volume is not bound to the authored profile.'
Write-Output ('BLOOM_SETUP_PASS: ' + $checkCount + ' configuration checks (not a render test).')
