$ErrorActionPreference = 'Stop'
# This prepares reviewable copies/diffs ONLY under Stage2Pending. It never applies
# patches to Assets and refuses an unexpected anchor before writing any output.
$project = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$changes = @(
    @{ Path='Assets/Elemental/Runtime/Physics/EarthSurfController.cs'; Edits=@(
        ,@('public sealed class EarthSurfController','public sealed partial class EarthSurfController',1)
    ) },
    @{ Path='Assets/Elemental/Runtime/Physics/EarthPlanetRockScatter.cs'; Edits=@(
        ,@('public bool IsComplete => completed;', "public bool IsComplete => completed;`n        public Transform GeneratedRoot => generatedRoot;",1)
    ) },
    @{ Path='Assets/Elemental/Input/Actions/PlanetInputReader.cs'; Edits=@(
        ,@('public bool UsesEarthPillarMobility => earthPillarMobility != null;', "public bool UsesEarthPillarMobility => earthPillarMobility != null;`n        public float TapJumpThresholdSeconds => tapJumpThresholdSeconds;",1)
    ) },
    @{ Path='Assets/Elemental/Runtime/Physics/EarthDualMouseAbilityController.cs'; Edits=@(
        @('public void BindDuel(EarthMvpDuelController duel) => duelController = duel;', "private EarthDuelFighterId _boundDuelFighter;`n        public EarthMvpDuelController BoundDuel => duelController;`n        public EarthDuelFighterId BoundDuelFighter => _boundDuelFighter;`n        public void BindDuel(EarthMvpDuelController duel, EarthDuelFighterId fighter = EarthDuelFighterId.Player)`n        { duelController = duel; _boundDuelFighter = fighter; }",1),
        @('duelController.PlayerPhase != EarthDuelFighterPhase.Active','(_boundDuelFighter == EarthDuelFighterId.Bot ? duelController.BotPhase : duelController.PlayerPhase) != EarthDuelFighterPhase.Active',2)
    ) },
    @{ Path='Assets/Elemental/Input/Gestures/MagicInputController.cs'; Edits=@(
        @('public void BindDuel(EarthMvpDuelController duel) => duelController = duel;', "private Elemental.Simulation.Combat.EarthDuelFighterId _boundDuelFighter;`n        public EarthMvpDuelController BoundDuel => duelController;`n        public Elemental.Simulation.Combat.EarthDuelFighterId BoundDuelFighter => _boundDuelFighter;`n        public void BindDuel(EarthMvpDuelController duel, Elemental.Simulation.Combat.EarthDuelFighterId fighter = Elemental.Simulation.Combat.EarthDuelFighterId.Player)`n        { duelController = duel; _boundDuelFighter = fighter; }",1),
        @('duelController.PlayerPhase == Elemental.Simulation.Combat.EarthDuelFighterPhase.Active','(_boundDuelFighter == Elemental.Simulation.Combat.EarthDuelFighterId.Bot ? duelController.BotPhase : duelController.PlayerPhase) == Elemental.Simulation.Combat.EarthDuelFighterPhase.Active',1),
        @("return new MagicCommand(`n                tick,`n                1u,", "return new MagicCommand(`n                tick,`n                _boundDuelFighter == Elemental.Simulation.Combat.EarthDuelFighterId.Bot ? 2u : 1u,",1)
    ) },
    @{ Path='Assets/Elemental/Input/Actions/EarthActionRouterBehaviour.cs'; Edits=@(
        @('public void BindDuel(EarthMvpDuelController match) => duel = match;', "private EarthDuelFighterId _boundDuelFighter;`n        public EarthMvpDuelController BoundDuel => duel;`n        public EarthDuelFighterId BoundDuelFighter => _boundDuelFighter;`n        public void BindDuel(EarthMvpDuelController match, EarthDuelFighterId fighter = EarthDuelFighterId.Player)`n        { duel = match; _boundDuelFighter = fighter; }",1),
        @('duel.PlayerPhase != EarthDuelFighterPhase.Active','(_boundDuelFighter == EarthDuelFighterId.Bot ? duel.BotPhase : duel.PlayerPhase) != EarthDuelFighterPhase.Active',1)
    ) },
    @{ Path='Assets/Elemental/Runtime/World/VoxelPlanetBehaviour.cs'; Edits=@(
        @('public sealed class VoxelPlanetBehaviour','public sealed partial class VoxelPlanetBehaviour',1),
        @("public void ApplyEditBatch(EditBatch batch)`n        {", "public void ApplyEditBatch(EditBatch batch)`n        {`n            if (!HasOnlineSimulationAuthority && !_applyingOnlineCanonical) return;",1),
        @("public VoxelEditReceipt ApplyEditBatchTransactional(EditBatch batch)`n        {", "public VoxelEditReceipt ApplyEditBatchTransactional(EditBatch batch)`n        {`n            if (!HasOnlineSimulationAuthority && !_applyingOnlineCanonical) return default;",1),
        @("            _state.Apply(batch);`n            QueueDirtyChunks();", "            _state.Apply(batch);`n            QueueDirtyChunks();`n            PublishOnlineBatch(batch, false);",1),
        @("            PrioritizeQueuedCoordinates(_renderQueue, _renderQueued, pending.Coords);`n            return receipt;", "            PrioritizeQueuedCoordinates(_renderQueue, _renderQueued, pending.Coords);`n            PublishOnlineBatch(batch, true);`n            return receipt;",1)
    ) },
    @{ Path='Assets/Elemental/Runtime/Characters/ActiveRagdollPuppet.cs'; Edits=@(
        ,@('public sealed class ActiveRagdollPuppet','public sealed partial class ActiveRagdollPuppet',1)
    ) },
    @{ Path='Assets/Elemental/Simulation/Combat/EarthDuelMatchState.cs'; Edits=@(
        ,@('public sealed class EarthDuelMatchState','public sealed partial class EarthDuelMatchState',1)
    ) },
    @{ Path='Assets/Elemental/Runtime/Characters/EarthMvpDuelController.cs'; Edits=@(
        @('public sealed class EarthMvpDuelController','public sealed partial class EarthMvpDuelController',1),
        @('public bool CanReceiveDamage(EarthDuelFighterId fighter) => CombatAllowed &&','public bool CanReceiveDamage(EarthDuelFighterId fighter) => HasSimulationAuthority && CombatAllowed &&',1),
        @("public void RestartRound()`n        {", "public void RestartRound()`n        {`n            if (!HasSimulationAuthority) return;",1),
        @("private void FixedUpdate()`n        {", "private void FixedUpdate()`n        {`n            if (!HasSimulationAuthority) return;",1)
    ) },
    @{ Path='Assets/Elemental/Runtime/Characters/EarthCharacterImpactTarget.cs'; Edits=@(
        @('public sealed class EarthCharacterImpactTarget','public sealed partial class EarthCharacterImpactTarget',1),
        @('if (duelController != null && !duelController.CanReceiveDamage(fighterId))', 'if (!HasSimulationAuthority || duelController != null && !duelController.CanReceiveDamage(fighterId))',2),
        @('                ImpactResolved?.Invoke(response);', "                PublishAuthorityImpact(in worldResponse, resolution.ReactionVelocityChange, velocityChange);`n                ImpactResolved?.Invoke(response);",1)
    ) }
)
$inputEdits = [Collections.Generic.List[object]]::new()
$inputEdits.Add(@('public sealed class EarthInputAdapter','public sealed partial class EarthInputAdapter',1))
$inputEdits.Add(@('[DisallowMultipleComponent]', '[DefaultExecutionOrder(-1200), DisallowMultipleComponent]',1))
$inputEdits.Add(@('using Elemental.Simulation.Bending;', "using Elemental.Simulation.Bending;`nusing Elemental.Simulation.Networking;",1))
$inputEdits.Add(@("private void Update()`n        {", "private void Update()`n        {`n            if (RemoteInputEnabled) { AdvanceRemoteInput(); return; }",1))
$inputEdits.Add(@("private void Bind()`n        {", "private void Bind()`n        {`n            if (RemoteInputEnabled) return;",1))
$inputEdits.Add(@("private void EnsureGameplayInputActive()`n        {", "private void EnsureGameplayInputActive()`n        {`n            if (RemoteInputEnabled) return;",1))
$inputEdits.Add(@('public Vector2 Move => _move?.ReadValue<Vector2>() ?? Vector2.zero;', 'public Vector2 Move => RemoteInputEnabled ? new Vector2(_remoteFrame.Move.x, _remoteFrame.Move.y) : _move?.ReadValue<Vector2>() ?? Vector2.zero;',1))
$inputEdits.Add(@('public Vector2 PointerPixels => _pointer?.ReadValue<Vector2>() ?? Vector2.zero;', 'public Vector2 PointerPixels => RemoteInputEnabled ? RemotePointerPixels : _pointer?.ReadValue<Vector2>() ?? Vector2.zero;',1))
$inputEdits.Add(@('public float BendParameter => _bendParameter?.ReadValue<float>() ?? 0f;', 'public float BendParameter => RemoteInputEnabled ? RemoteScroll : _bendParameter?.ReadValue<float>() ?? 0f;',1))
foreach ($control in @(@('BendPrimary','_bendPrimary','Primary'),@('BendForce','_bendForce','Force'),@('BendField','_bendField','Field'),@('Jump','_jumpOrStomp','Jump'))) {
    foreach ($edge in @(@('Pressed','WasPressedThisFrame','RemotePressed'),@('Released','WasReleasedThisFrame','RemoteReleased'),@('Held','IsPressed','RemoteHeld'))) {
        $before = 'public bool ' + $control[0] + $edge[0] + ' => ' + $control[1] + '?.' + $edge[1] + '() == true;'
        $after = 'public bool ' + $control[0] + $edge[0] + ' => RemoteInputEnabled ? ' + $edge[2] + '(EarthInputBits.' + $control[2] + ') : ' + $control[1] + '?.' + $edge[1] + '() == true;'
        $inputEdits.Add(@($before,$after,1))
    }
}
foreach ($control in @(@('BendModifierHeld','_bendModifier','IsPressed','RemoteHeld','Modifier'),@('CancelPressed','_cancel','WasPressedThisFrame','RemotePressed','Cancel'),@('ShoulderSwapPressed','_shoulderSwap','WasPressedThisFrame','RemotePressed','Shoulder'))) {
    $before = 'public bool ' + $control[0] + ' => ' + $control[1] + '?.' + $control[2] + '() == true;'
    $after = 'public bool ' + $control[0] + ' => RemoteInputEnabled ? ' + $control[3] + '(EarthInputBits.' + $control[4] + ') : ' + $control[1] + '?.' + $control[2] + '() == true;'
    $inputEdits.Add(@($before,$after,1))
}
foreach ($name in @('ElementFirePressed','ElementWaterPressed','DebugLookdevChargeHeld','DebugLookdevDayPressed','DebugLookdevSunsetPressed','DebugLookdevNightPressed','DebugLookdevSeamPressed','DebugLookdevHeavyImpactPressed')) {
    $inputEdits.Add(@(('public bool ' + $name + ' => '),('public bool ' + $name + ' => !RemoteInputEnabled && '),1))
}
$inputEdits.Add(@('                _jumpOrStomp?.WasPressedThisFrame() == true,','                JumpPressed,',1))
$inputEdits.Add(@("public bool DebugAbilityPressed(int oneBasedSlot)`n        {", "public bool DebugAbilityPressed(int oneBasedSlot)`n        {`n            if (RemoteInputEnabled) return oneBasedSlot >= 1 && oneBasedSlot <= 4 && RemotePressed((EarthInputBits)(64u << oneBasedSlot));",1))
$changes += @{ Path='Assets/Elemental/Input/Actions/EarthInputAdapter.cs'; Edits=$inputEdits.ToArray() }
$magicInputChange = $changes | Where-Object { $_.Path -eq 'Assets/Elemental/Input/Gestures/MagicInputController.cs' }
$magicInputChange.Edits += ,@('public sealed class MagicInputController','public sealed partial class MagicInputController',1)
$magicInputChange.Edits += ,@('duelController.CombatAllowed &&','duelController.HasSimulationAuthority && duelController.CombatAllowed &&',1)
foreach ($property in @(
    @('AbilityId SelectedAbility','new AbilityId((ushort)_onlineView.Ability)'),
    @('BendPhase CurrentBendPhase','_onlineView.Phase'), @('BendOriginMode BendOriginMode','_onlineView.Origin'),
    @('float BendAmount01','_onlineView.Amount'), @('float BendCharge01','_onlineView.Charge'),
    @('float BendFocus01','_onlineView.Focus'), @('Vector3 BendTargetPosition','(Vector3)_onlineView.Target'),
    @('bool IsArmorActive','_onlineView.ArmorActive'), @('float ArmorPhase01','_onlineView.ArmorPhase'),
    @('bool IsQuickStonePrimed','_onlineView.QuickPrimed'), @('float QuickStonePrime01','_onlineView.QuickPrime'),
    @('EarthActionOwner ActiveActionOwner','_onlineView.Owner'), @('float ResonanceCharge01','_onlineView.ResonanceCharge'),
    @('float SurfSpeed','_onlineView.SurfSpeed'))) {
    $magicInputChange.Edits += ,@(('public ' + $property[0] + ' => '),('public ' + $property[0] + ' => _onlineReplicaPresentation ? ' + $property[1] + ' : '),1)
}
foreach ($duelGate in @(
    @('Assets/Elemental/Input/Actions/EarthActionRouterBehaviour.cs','!duel.CombatAllowed','!duel.HasSimulationAuthority || !duel.CombatAllowed',1),
    @('Assets/Elemental/Runtime/Physics/EarthDualMouseAbilityController.cs','!duelController.CombatAllowed','!duelController.HasSimulationAuthority || !duelController.CombatAllowed',2))) {
    $duelChange = $changes | Where-Object { $_.Path -eq $duelGate[0] }
    $duelChange.Edits += ,@($duelGate[1],$duelGate[2],$duelGate[3])
}
$executorEdits = [Collections.Generic.List[object]]::new()
$executorEdits.Add(@('public sealed class MagicExecutor','public sealed partial class MagicExecutor',1))
$executorEdits.Add(@("public bool Execute(in MagicCommand command)`n        {", "public bool Execute(in MagicCommand command)`n        {`n            if (_onlineReplicaPresentation) return false;",1))
$executorEdits.Add(@("private void FixedUpdate()`n        {", "private void FixedUpdate()`n        {`n            if (_onlineReplicaPresentation) return;",1))
foreach ($property in @(
    @('Rigidbody HeldBody','_onlineHeldBody'), @('bool IsVectorFieldActive','_onlineView.VectorActive'),
    @('Vector3 VectorFieldDirection','(Vector3)_onlineView.VectorDirection'), @('Vector3 VectorFieldPoint','(Vector3)_onlineView.VectorPoint'),
    @('float VectorFieldCharge','_onlineView.VectorCharge'), @('bool IsGravityWellActive','_onlineView.GravityActive'),
    @('bool IsRepairActive','_onlineView.RepairActive'), @('Vector3 GravityWellFocus','(Vector3)_onlineView.GravityFocus'),
    @('float GravityWellStrength','_onlineView.GravityStrength'))) {
    $executorEdits.Add(@(('public ' + $property[0] + ' => '),('public ' + $property[0] + ' => _onlineReplicaPresentation ? ' + $property[1] + ' : '),1))
}
$changes += @{ Path='Assets/Elemental/Runtime/World/MagicExecutor.cs'; Edits=$executorEdits.ToArray() }
$prepared = @()
foreach ($change in $changes) {
    $original = [IO.File]::ReadAllText((Join-Path $project $change.Path)).Replace("`r`n","`n")
    $modified = $original
    foreach ($edit in $change.Edits) {
        $count = [regex]::Matches($modified,[regex]::Escape($edit[0])).Count
        if ($count -ne $edit[2]) { throw "Unexpected anchor count $count in $($change.Path): $($edit[0])" }
        $modified = $modified.Replace($edit[0],$edit[1])
    }
    $prepared += @{ Path=$change.Path; Original=$original; Modified=$modified }
}
$utf8 = [Text.UTF8Encoding]::new($false)
foreach ($item in $prepared) {
    foreach ($kind in @('Original','Modified')) {
        $outPath = Join-Path $PSScriptRoot ('Patches/' + $kind + '/' + $item.Path)
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($outPath)) | Out-Null
        [IO.File]::WriteAllText($outPath,$item[$kind],$utf8)
    }
}
'Prepared runtime patch copies: ' + $prepared.Count + '. Assets untouched.'
$patchLines = @()
Push-Location $project
try {
    foreach ($item in $prepared) {
        $before = 'BuildReports/AlphaImplementation/Stage2Pending/Patches/Original/' + $item.Path
        $after = 'BuildReports/AlphaImplementation/Stage2Pending/Patches/Modified/' + $item.Path
        $diff = & git -c core.quotePath=false diff --no-index --no-ext-diff --src-prefix=a/ --dst-prefix=b/ -- $before $after
        if ($LASTEXITCODE -gt 1) { throw 'Could not create reviewable runtime diff.' }
        $patchLines += $diff | ForEach-Object {
            $_.Replace('a/BuildReports/AlphaImplementation/Stage2Pending/Patches/Original/','a/').
                Replace('b/BuildReports/AlphaImplementation/Stage2Pending/Patches/Modified/','b/')
        }
    }
} finally { Pop-Location }
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'RuntimeBindings.patch'),(($patchLines -join "`n") + "`n"),$utf8)
