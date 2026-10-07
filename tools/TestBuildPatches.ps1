param([string]$Output,[string]$EditorData,[string]$ValheimManaged,[string]$BepInExPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$paths=@{}
$config=Join-Path $root '.local\BuildPaths.psd1'
if(Test-Path -LiteralPath $config){$paths=Import-PowerShellDataFile -LiteralPath $config}
if(!$EditorData){$EditorData=$paths['EditorData']}
if(!$ValheimManaged){$ValheimManaged=$paths['ValheimManaged']}
if(!$BepInExPath){$BepInExPath=$paths['BepInExPath']}
if(!$EditorData -or !$ValheimManaged -or !$BepInExPath){throw 'Supply the same paths as tools/Compile.ps1.'}
if(!$Output){$Output=Join-Path $root 'artifacts\build-patch-tests'}
New-Item -ItemType Directory -Force $Output | Out-Null
$Output=(Resolve-Path -LiteralPath $Output).Path
$core=Join-Path $BepInExPath 'core'
$refs=@(Get-ChildItem "$EditorData\MonoBleedingEdge\lib\mono\4.7.2-api" -Filter '*.dll' | ForEach-Object FullName)
$refs+=@(Get-ChildItem "$EditorData\MonoBleedingEdge\lib\mono\4.7.2-api\Facades" -Filter '*.dll' | ForEach-Object FullName)
$refs+=(Join-Path $core '0Harmony.dll')
$exe=Join-Path $Output 'BuildPatchTests.exe'
$rsp=Join-Path $Output 'BuildPatchTests.rsp'
$lines=@('/nologo','/nostdlib+','/langversion:9','/optimize+','/target:exe','/define:BUILD_PATCH_TESTS',('/out:"'+$exe+'"'))
$lines+=@($refs | Select-Object -Unique | ForEach-Object {'/reference:"'+$_+'"'})
$lines+=@('src\BuildPatches.cs','tests\BuildPatchTests.cs' | ForEach-Object {'"'+(Join-Path $root $_)+'"'})
[IO.File]::WriteAllLines($rsp,$lines)
& "$EditorData\NetCoreRuntime\dotnet.exe" "$EditorData\DotNetSdkRoslyn\csc.dll" "@$rsp"
if($LASTEXITCODE -ne 0){throw 'Build patch test compilation failed'}
# Execute managed patch fixtures on desktop CLR, where the installed Harmony's
# native detour teardown is supported outside the game host.
& $exe $core $ValheimManaged
if($LASTEXITCODE -ne 0){throw 'Build patch tests failed'}
# Inspect metadata without loading/executing game types. Current Valheim uses
# default interface methods which desktop .NET Framework cannot load.
Add-Type -Path (Join-Path $EditorData 'Managed\Unity.Cecil.dll')
$game=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $ValheimManaged 'assembly_valheim.dll'))
try {
 $player=$game.MainModule.GetType('Player')
 $attempt=@($player.Methods | Where-Object {$_.Name -eq 'TryPlacePiece' -and $_.Parameters.Count -eq 1})
 $place=@($player.Methods | Where-Object {$_.Name -eq 'PlacePiece' -and $_.Parameters.Count -eq 5})
 if($attempt.Count -ne 1 -or $place.Count -ne 1){throw 'Building method signatures changed'}
 $code=$attempt[0].Body.Instructions
 $marker=@($code | Where-Object {$_.OpCode.Code.ToString() -eq 'Ldstr' -and $_.Operand -ceq 'Placed '})
 $raycast=@($code | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'UpdatePlacementGhost'})
 $create=@($code | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'PlacePiece'})
 if($marker.Count -ne 1 -or $raycast.Count -ne 1 -or $create.Count -ne 1 -or $raycast[0].Offset -ge $marker[0].Offset -or $create[0].Offset -le $marker[0].Offset){throw 'Valid-placement branch changed'}
 if($attempt[0].Body.ExceptionHandlers.Count -ne 0){throw 'Placement gate now needs exception-region review'}
 $earlyStats=@($code | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -like 'Increment*' -and $_.Offset -lt $marker[0].Offset})
 if($earlyStats.Count -ne 0){throw 'Build statistics moved ahead of the payment gate'}
 $anchor=@($place[0].Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.GenericInstanceMethod] -and $_.Operand.DeclaringType.FullName -eq 'UnityEngine.GameObject' -and $_.Operand.Name -eq 'GetComponentInChildren' -and $_.Operand.Parameters.Count -eq 0 -and $_.Operand.GenericArguments.Count -eq 1 -and $_.Operand.GenericArguments[0].FullName -eq 'CraftingStation'})
 if($anchor.Count -ne 1){throw 'Created-object observation anchor changed'}
 $spawn=@($place[0].Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'Instantiate'})
 if($spawn.Count -ne 1 -or $spawn[0].Offset -ge $anchor[0].Offset){throw 'Object creation moved after the observation point'}
 Write-Output 'PASS installed game IL: payment gate follows placement checks and precedes statistics; created-object observation follows Instantiate.'
}finally{$game.Dispose()}
