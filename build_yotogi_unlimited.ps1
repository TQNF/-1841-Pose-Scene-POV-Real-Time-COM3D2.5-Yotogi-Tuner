$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $here
Set-Location $root

# COM3D2.5 Ver.3.38.0 ONLY (this plugin binds 3.38-specific internals:
# YotogiPlayManager tower fields/methods). Compile target = the actual
# runtime assembly pair (Assembly-CSharp + UnityEngine from COM3D2x64_Data).
$mainManaged = Join-Path $root "COM3D2x64_Data\Managed"

$src = [System.IO.File]::ReadAllText((Join-Path $here "COM3D2.YotogiUnlimited.cs"))
$refs = @(
  (Join-Path $root "BepInEx\core\BepInEx.dll"),
  (Join-Path $root "BepInEx\core\0Harmony.dll"),
  (Join-Path $mainManaged "Assembly-CSharp.dll"),
  (Join-Path $mainManaged "Assembly-CSharp-firstpass.dll"),
  (Join-Path $mainManaged "UnityEngine.dll"),
  "System.dll",
  "System.Core.dll"
)
$outDll = Join-Path $here "COM3D2.YotogiUnlimited.dll"
if (Test-Path $outDll) { Remove-Item $outDll -Force }
Add-Type -TypeDefinition $src -ReferencedAssemblies $refs -OutputAssembly $outDll -OutputType Library
Write-Output ("COMPILED: " + $outDll + " (" + (Get-Item $outDll).Length + " bytes)")

# ---- HARD GATE: BCL member-reference audit (same gate as v1.2.0 build) ----
# Game runs Unity 5.6 old Mono / CLR 2.0 (.NET 3.5-profile BCL). Any memberref
# into mscorlib/System/System.Core that only exists on .NET 4.0+ kills the
# method at JIT time IN GAME. Known trap: == / != on Type/MethodInfo/
# FieldInfo compiles to op_Equality/op_Inequality (.NET 4.0-only; String's
# are .NET 1.0 and safe). On failure the DLL is DELETED so a bad build
# cannot be deployed.
Add-Type -Path (Join-Path $root "BepInEx\core\Mono.Cecil.dll")
$dllBytes = [byte[]][System.IO.File]::ReadAllBytes($outDll)
$dllStream = New-Object System.IO.MemoryStream(,$dllBytes)
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($dllStream)
$brefs = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($t in $asm.MainModule.Types) {
  foreach ($m in $t.Methods) {
    if (-not $m.HasBody) { continue }
    foreach ($i in $m.Body.Instructions) {
      if ($null -eq $i.Operand) { continue }
      $mr = $i.Operand -as [Mono.Cecil.MemberReference]
      if ($null -eq $mr) { continue }
      $dt = $mr.DeclaringType
      if ($null -eq $dt) { continue }
      if ($dt.Scope -is [Mono.Cecil.AssemblyNameReference] -and
          $dt.Scope.Name -in @("mscorlib", "System", "System.Core")) {
        [void]$brefs.Add($dt.FullName + "::" + $mr.Name)
      }
    }
  }
}
$bad = @($brefs | Where-Object { ($_ -match "op_(In)?Equality") -and ($_ -notmatch "System.String::") })
if ($bad.Count -gt 0) {
  Write-Output "BCL AUDIT FAILED - .NET4-only operator memberrefs found:"
  $bad | ForEach-Object { Write-Output ("  " + $_) }
  Remove-Item $outDll -Force
  throw "BUILD REJECTED: fix the op_Equality/op_Inequality comparisons (use ReferenceEquals)"
}
Write-Output ("BCL AUDIT PASSED: " + $brefs.Count + " BCL memberrefs, only System.String operators.")

Write-Output "BUILD OK."

# ---- deploy ----
$dest = Join-Path $root "BepInEx\plugins\COM3D2.YotogiUnlimited.dll"
Copy-Item $outDll $dest -Force
Write-Output ("DEPLOYED: " + $dest + " (" + (Get-Item $dest).Length + " bytes)")

# ---- clear BepInEx cache (convention: any plugin change requires cache clear) ----
$cache = Join-Path $root "BepInEx\cache"
if (Test-Path $cache) {
  Remove-Item (Join-Path $cache "*") -Recurse -Force -ErrorAction SilentlyContinue
  Write-Output "BepInEx cache cleared."
}
