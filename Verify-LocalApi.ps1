$ErrorActionPreference = 'Stop'
$serverRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
Add-Type -Path (Join-Path $serverRoot 'MelonLoader\net35\Mono.Cecil.dll')
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $serverRoot 'Silica_Data\Managed\SilicaCore.dll'))
$mod = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $PSScriptRoot 'bin\Release\netstandard2.1\Si_Formation.dll'))
function Require-Method($typeName, $methodName, $parameterTypes) {
    $type = $game.MainModule.Types | Where-Object FullName -eq $typeName
    $methods = @($type.Methods | Where-Object { $_.Name -eq $methodName -and ($_.Parameters.ParameterType.FullName -join ',') -eq $parameterTypes })
    if ($methods.Count -ne 1) { throw "API mismatch: $typeName::$methodName($parameterTypes)" }
    return $methods[0]
}
function Require-Call($method, $typeName, $methodName, $count) {
    $calls = @($method.Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq $typeName -and $_.Operand.Name -eq $methodName
    })
    if ($calls.Count -ne $count) { throw "Call-site mismatch: $($method.FullName) -> $typeName::$methodName ($($calls.Count))" }
}
try {
    $follow = Require-Method 'Silica.AI.AIGroup' 'EvaluateLeaderFollow' ''
    Require-Call $follow 'Silica.AI.AIOrderProcessor' 'IssueResolvedOrder' 2
    $null = Require-Method 'Silica.AI.AIOrderProcessor' 'IssueResolvedOrder' 'UnityEngine.Vector3,Target,Silica.AI.OrderIssueParams&'
    $null = Require-Method 'Silica.AI.AIOrderProcessor' 'IssueOrder' 'Silica.AI.OrderDefinition,Silica.AI.OrderTarget&,Silica.AI.OrderIssueParams&'
    $null = Require-Method 'FPSCommanding' 'MoveServer' 'Player,UnityEngine.Vector3'
    $null = Require-Method 'FPSCommanding' 'AttackServer' 'Player,Target'
    foreach ($name in @('StopAllServer','FollowAllServer')) { $null = Require-Method 'FPSCommanding' $name 'Player' }
    foreach ($name in @('StopUnitServer','UnitFollowServer','ProtectServer','KickFromGroupServer')) { $null = Require-Method 'FPSCommanding' $name 'Player,Target' }
    $null = Require-Method 'Silica.AI.AIGroup' 'IgnoreTask' 'Unit,Silica.AI.AIGroup/EIgnoreTaskReason'
    $null = Require-Method 'Silica.AI.AIGroup' 'set_Task' 'Silica.AI.AITaskBase'
    $null = Require-Method 'StrategyMode' 'PerformMoveAttack' 'System.Collections.Generic.List`1<BaseGameObject>,UnityEngine.Vector3,Target,Silica.AI.AgentMoveSpeed,System.Boolean,System.Boolean,System.Boolean'
    $network = Require-Method 'NetworkLayer' 'ProcessMessage' 'GameByteStreamReader,System.UInt32,NetworkID,System.Boolean'
    Require-Call $network 'NetworkComponent' 'ProcessNetRPC' 1
    $sync = Require-Method 'Silica.StrategyNetworkManager' 'SyncMoveAttackOrder' 'GameByteStreamReader'
    Require-Call $sync 'StrategyMode' 'PerformMoveAttack' 1
    foreach ($typeName in @('VehicleWheeled','VehicleHovered')) {
        $type = $game.MainModule.Types | Where-Object FullName -eq $typeName
        $field = $type.Fields | Where-Object Name -eq 'MoveSpeed'
        if (!$field -or !$field.IsPublic -or $field.IsStatic -or $field.FieldType.FullName -ne 'System.Single') { throw "Per-instance speed API changed: $typeName" }
        $normal = $type.Methods | Where-Object Name -eq 'get_NormalSpeed'
        if (!($normal.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.FieldReference] -and $_.Operand.Name -eq 'MoveSpeed' })) { throw "NormalSpeed no longer uses instance MoveSpeed: $typeName" }
    }
    $unit = $game.MainModule.Types | Where-Object FullName -eq 'Unit'
    $null = Require-Method 'Unit' 'get_MoveSpeedLimit' ''
    $null = Require-Method 'Unit' 'get_CurrentVelocity' ''
    if (!($unit.Fields | Where-Object { $_.Name -eq 'MoveSpeedScale' -and $_.IsPublic -and !$_.IsStatic })) { throw 'MoveSpeedScale API mismatch' }
    $null = Require-Method 'Silica.AI.UnitCohesionGroup' 'Detach' 'Unit'
    $null = Require-Method 'GameAI' 'GetPointValidForAI' 'UnityEngine.Vector3,Pathfinding.GraphMask,UnityEngine.Vector3&,System.Single,System.Single,System.Single'
    $events = $game.MainModule.Types | Where-Object FullName -eq 'GameEvents'
    if (!($events.Fields | Where-Object Name -eq 'OnGameEnded')) { throw 'Round cleanup API missing' }
    foreach ($spec in @(
        @('Si_KingOfTheHill.dll','Si_KingOfTheHill.KingOfTheHill','_buyStates'),
        @('Si_UnitBalance.dll','Si_UnitBalance.UnitBalance','_menuStates'),
        @('Si_UnitBalance.dll','Si_UnitBalance.UnitBalance','_statsStates'))) {
        $foreign = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $serverRoot ('Mods\' + $spec[0])))
        try {
            $owner = $foreign.MainModule.Types | Where-Object FullName -eq $spec[1]
            $state = $owner.Fields | Where-Object Name -eq $spec[2]
            if (!$state -or !$state.IsStatic -or $state.FieldType.FullName -notmatch 'Dictionary`2<System.Int64,') { throw "Menu detection API changed: $($spec[0])" }
        } finally { $foreign.Dispose() }
    }
    $refs = @($mod.MainModule.AssemblyReferences.Name)
    if ($refs -contains 'Si_FollowFormation' -or $refs -contains 'Si_RTS_AI' -or $refs -contains 'Si_Logistics') { throw 'Unexpected mod dependency' }
    if ($mod.Name.Name -ne 'Si_Formation' -or $refs -notcontains 'Si_AdminMod' -or $refs -notcontains 'Newtonsoft.Json') { throw 'Assembly metadata/dependency mismatch' }
    Write-Host 'PASS: installed FPS/group-order/network/follow API signatures and call sites; renamed independent assembly.'
    Write-Host 'Metadata validation only; live Harmony interception and game navigation still need in-game tests.'
    Write-Host 'PASS: instance speed fields/getters, cohesion detach, navigation projection, round cleanup and foreign menu detection metadata.'
} finally { $game.Dispose(); $mod.Dispose() }
