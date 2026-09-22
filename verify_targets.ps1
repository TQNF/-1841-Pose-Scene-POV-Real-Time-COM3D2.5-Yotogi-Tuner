$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $here
Add-Type -Path (Join-Path $root "BepInEx\core\Mono.Cecil.dll")

function Read-Asm([string]$path) {
  $bytes = [byte[]][System.IO.File]::ReadAllBytes($path)
  return [Mono.Cecil.AssemblyDefinition]::ReadAssembly((New-Object System.IO.MemoryStream(,$bytes)))
}

function Find-Type([System.Collections.Generic.IEnumerable`1[Mono.Cecil.TypeDefinition]]$types, [string]$fullName) {
  foreach ($t in $types) {
    if ($t.FullName -eq $fullName) { return $t }
    foreach ($n in $t.NestedTypes) {
      if ($n.FullName -eq $fullName) { return $n }
      foreach ($n2 in $n.NestedTypes) {
        if ($n2.FullName -eq $fullName) { return $n2 }
        foreach ($n3 in $n2.NestedTypes) {
          if ($n3.FullName -eq $fullName) { return $n3 }
        }
      }
    }
  }
  return $null
}

$asmMain = Read-Asm (Join-Path $root "COM3D2x64_Data\Managed\Assembly-CSharp.dll")
$asmFirst = Read-Asm (Join-Path $root "COM3D2x64_Data\Managed\Assembly-CSharp-firstpass.dll")
$asmUnity = Read-Asm (Join-Path $root "COM3D2x64_Data\Managed\UnityEngine.dll")

# v1.6.0 target set (v1.3.2 core + v1.5.2 removals + v1.6.0 POV/LookAt members).
# v1.5.2: OnClickCommand/valid_command_dic_/player_state_ removed (orgasm-together retraction).
# v1.6.0 adds: TBody head/eye bones & look-at flags (POV 2.0/3.0 adaptation via
# official caches), Maid.EyeToReset, Slot multi-child indexer.
# v1.6.6 adds: Suspend/OnClickCommand/calledAdvFiles (P26 old-mode suspend guard;
# OnClickCommand returns as a target - it is re-invoked by the P26 prefix).
# v1.7.0 adds: select-cap trio (container slots / change-event / play array),
# skill browser (AddPlaySkill, skill table), char adjust (Maid.SetPos/SetRot),
# stage switch (SelectStage/ChangeBG/GetAllDatas), UI hide (uiVisible is not a
# method target - property setter), tower extension (UIGrid).
$checks = @(
  @{ asm="main";  kind="method+sig"; type="Yotogis.Skill/Data"; name="IsExecStage"; param="YotogiStage/Data" },
  @{ asm="main";  kind="method+sig"; type="Yotogis.Skill/Old/Data"; name="IsExecStage"; param="YotogiOld/Stage" },
  @{ asm="main";  kind="method+sig"; type="YotogiStage/Data"; name="isYotogiPlayable"; param="System.Int32" },
  @{ asm="main";  kind="method+sig"; type="YotogiStage/Data"; name="isYotogiPlayable"; param="Maid" },
  @{ asm="main";  kind="method";     type="YotogiPlayManager"; name="UpdateSkillTower" },
  @{ asm="main";  kind="method";     type="YotogiPlayManager"; name="OnSkillIconClick" },
  @{ asm="main";  kind="method";     type="YotogiPlayManager"; name="OnNextSkillMove" },
  @{ asm="main";  kind="method";     type="YotogiPlayManager"; name="NextSkill" },
  @{ asm="main";  kind="field";      type="YotogiPlayManager"; name="skill_group_parent_" },
  @{ asm="main";  kind="field";      type="YotogiPlayManager"; name="playing_skill_no_" },
  @{ asm="main";  kind="field";      type="YotogiPlayManager"; name="skill_icon_default_color_" },
  @{ asm="main";  kind="method+sig"; type="YotogiSkillListManager"; name="CreateDatas"; param="MaidStatus.Status" },
  @{ asm="main";  kind="method+sig"; type="YotogiSkillSelectManager"; name="CreateSkillButtons"; param="Yotogis.Skill/Data/SpecialConditionType" },
  @{ asm="main";  kind="field";      type="YotogiSkillSelectManager"; name="skill_container_mgr_" },
  @{ asm="main";  kind="method";     type="YotogiManager"; name="GetPlayPossibleMaidCount" },
  @{ asm="main";  kind="method";     type="YotogiManager"; name="GetSubMaidList" },
  @{ asm="main";  kind="method";     type="PlayerStatus.Status"; name="get_lockNTRPlay" },
  @{ asm="main";  kind="method+sig"; type="Yotogis.Skill/Data"; name="IsExecSeikeiken"; param="MaidStatus.Seikeiken" },
  @{ asm="main";  kind="method+sig"; type="Yotogis.Skill/Data"; name="IsExecRelation"; param="MaidStatus.Relation" },
  @{ asm="main";  kind="method+sig"; type="Yotogis.Skill/Data"; name="IsExecContract"; param="MaidStatus.Contract" },
  @{ asm="main";  kind="method+sig"; type="Yotogis.Skill/Data"; name="IsExecPersonal"; param="MaidStatus.Personal/Data" },
  @{ asm="main";  kind="method+sig"; type="MaidStatus.CsvData.AbstractClassData/LearnConditions"; name="isFutureLearnPossible"; param="MaidStatus.Status" },
  @{ asm="main";  kind="method+sig"; type="MaidStatus.PersonalEventBlocker"; name="IsEnabledYotodiSkill"; param="MaidStatus.Personal/Data" },
  @{ asm="main";  kind="method";     type="YotogiSkillUnit"; name="get_skill_param_data" },
  @{ asm="main";  kind="field";      type="YotogiSkillUnit"; name="param_data_" },
  @{ asm="main";  kind="method";     type="YotogiManager"; name="get_skill_select_max_hp" },
  @{ asm="main";  kind="method+sig"; type="Yotogi/SkillDataPair"; name="Create"; param="Maid" },
  @{ asm="main";  kind="field";      type="YotogiPlayManager"; name="param_basic_bar_" },
  @{ asm="main";  kind="method";     type="YotogiPlayManager"; name="UpdateCommand" },
  @{ asm="main";  kind="method+sig"; type="YotogiPlayManager"; name="ApplyExecCommandStatus"; param="Maid" },
  # (v1.5.2: OnClickCommand/valid_command_dic_/player_state_ were removed with the
  #  orgasm-together retraction; OnClickCommand returns in v1.6.6 as a P26 target)
  @{ asm="main";  kind="method+sig"; type="ScriptManager"; name="ReplacePersonal"; param="Maid[]" },
  @{ asm="main";  kind="method+sig"; type="GameUty"; name="IsExistFile"; param="System.String" },
  @{ asm="main";  kind="method";     type="MaidStatus.Personal"; name="GetAllDatas" },
  @{ asm="main";  kind="method";     type="BaseKagManager"; name="ReplaceFileNameCallBack" },
  @{ asm="main";  kind="method";     type="YotogiKagManager"; name="GetKagClassName" },
  # ---- v1.6.0 (POV rewrite + LookAtCamera) ----
  @{ asm="main";  kind="field";      type="TBody"; name="trsHead" },
  @{ asm="main";  kind="field";      type="TBody"; name="trsNeck" },
  @{ asm="main";  kind="field";      type="TBody"; name="trsEyeL" },
  @{ asm="main";  kind="field";      type="TBody"; name="trsLookTarget" },
  @{ asm="main";  kind="field";      type="TBody"; name="boHeadToCam" },
  @{ asm="main";  kind="field";      type="TBody"; name="boEyeToCam" },
  @{ asm="main";  kind="field";      type="TBody"; name="boEyeSorashi" },
  @{ asm="main";  kind="field";      type="TBody"; name="goSlot" },
  @{ asm="main";  kind="method";     type="TBody"; name="GetBone" },
  @{ asm="main";  kind="field";      type="TBody"; name="m_Animation" },
  @{ asm="main";  kind="method";     type="TBody/Slot"; name="CountChildren" },
  @{ asm="main";  kind="method+sig"; type="TBody/Slot"; name="get_Item"; param="System.Int32" },
  @{ asm="main";  kind="method+sig"; type="Maid"; name="EyeToReset"; param="System.Single" },
  # ---- v1.6.4 (P25 MotionScript null-safety) ----
  @{ asm="main";  kind="method";     type="MotionKagManager"; name="LoadScriptFile" },
  @{ asm="main";  kind="field";      type="MotionKagManager"; name="main_maid_" },
  @{ asm="main";  kind="field";      type="MotionKagManager"; name="main_man_" },
  # ---- v1.6.6 (P26 old-mode suspend guard) ----
  @{ asm="main";  kind="method";     type="YotogiPlayManager"; name="Suspend" },
  @{ asm="main";  kind="method";     type="YotogiPlayManager"; name="OnClickCommand" },
  @{ asm="main";  kind="field";      type="YotogiPlayManager"; name="calledAdvFiles" },
  # ---- v1.7.0 (browser/stage/ui; select-cap trio + char-adjust removed in v1.7.1) ----
  @{ asm="main";  kind="method";     type="YotogiManager"; name="AddPlaySkill" },
  @{ asm="main";  kind="field";      type="YotogiManager"; name="play_skill_array_" },
  @{ asm="main";  kind="method";     type="YotogiManager"; name="get_play_skill_array" },
  @{ asm="main";  kind="method";     type="YotogiManager"; name="get_playing_skill" },
  @{ asm="main";  kind="method";     type="YotogiManager"; name="get_is_free_mode" },
  @{ asm="main";  kind="method";     type="YotogiManager"; name="get_is_vr_mode" },
  @{ asm="main";  kind="method";     type="YotogiManager"; name="get_maid" },
  @{ asm="main";  kind="method";     type="YotogiManager"; name="IsAllCharaBusy" },
  @{ asm="main";  kind="method+sig"; type="YotogiManager/PlayingSkillData"; name=".ctor" },
  @{ asm="main";  kind="field";      type="YotogiManager/PlayingSkillData"; name="skill_pair" },
  @{ asm="main";  kind="field";      type="YotogiManager/PlayingSkillData"; name="is_play" },
  @{ asm="main";  kind="field";      type="YotogiManager/PlayingSkillData"; name="exp" },
  @{ asm="main";  kind="field";      type="YotogiManager/PlayingSkillData"; name="backup_total_exp" },
  @{ asm="main";  kind="method";     type="Yotogi/SkillDataPair"; name="CreateBaseDataOnly" },
  @{ asm="main";  kind="method";     type="YotogiSkillIcon"; name="SetOnClickEvent" },
  @{ asm="main";  kind="method";     type="YotogiStageSelectManager"; name="SelectStage" },
  @{ asm="main";  kind="method";     type="YotogiStageSelectManager/StageExpansionPack"; name="ChangeBG" },
  @{ asm="main";  kind="method";     type="YotogiStage"; name="GetAllDatas" },
  @{ asm="main";  kind="method";     type="YotogiStage/Data"; name="ChangeBg" },
  @{ asm="main";  kind="field";      type="YotogiStage/Data"; name="skillSelectcharacterData" },
  @{ asm="main";  kind="field";      type="YotogiStage/Data"; name="skillSelectLightData" },
  @{ asm="main";  kind="field";      type="YotogiStage/Data"; name="drawName" },
  @{ asm="main";  kind="field";      type="YotogiStage/Data"; name="uniqueName" },
  @{ asm="main";  kind="field";      type="YotogiStage/Data"; name="bgmFileName" },
  @{ asm="main";  kind="method";     type="YotogiStage/Data/Camera"; name="Apply" },
  @{ asm="main";  kind="method";     type="YotogiStage/Data/Light"; name="Apply" },
  @{ asm="main";  kind="method";     type="YotogiStage/Data/Character"; name="Apply" },
  @{ asm="main";  kind="method";     type="SoundMgr"; name="PlayBGM" },
  @{ asm="main";  kind="method";     type="EventDelegate"; name="Add" },
  # ---- v1.7.1 (P30 pose resource auto-mount) ----
  @{ asm="main";  kind="method";     type="BgMgr"; name="AddPrefabToBg" },
  @{ asm="main";  kind="method";     type="BgMgr"; name="GetPrefabFromBg" },
  @{ asm="main";  kind="method";     type="BgMgr"; name="DelPrefabFromBg" },
  @{ asm="main";  kind="method";     type="BgMgr"; name="CreateAssetBundle" },
  @{ asm="main";  kind="method";     type="CharacterMgr"; name="GetCharaAllPos" },
  @{ asm="main";  kind="field";      type="Yotogis.Skill/Data"; name="start_call_file" },
  @{ asm="main";  kind="field";      type="Yotogis.Skill/Data"; name="id" },
  # ---- v1.7.2 (P31 GetMaidStatus guard + P32 CallNormalFile trigger/finalizer + POV neck) ----
  @{ asm="main";  kind="method";     type="ScriptManager"; name="TJSFuncGetMaidStatus" },
  @{ asm="main";  kind="method";     type="YotogiPlayManager"; name="CallNormalFile" },
  @{ asm="main";  kind="method+sig"; type="CharacterMgr"; name="GetMaid"; param="System.Int32" },
  @{ asm="first"; kind="method";     type="TJSVariantRef"; name="SetString" },
  # ---- v1.7.3 (P33 voice-data guards + P34 talk-tag guards + P35/P36 sub-select + cross-stage position) ----
  @{ asm="main";  kind="method";     type="YotogiPlayManager"; name="SetAdditionalFtVoice" },
  @{ asm="main";  kind="method";     type="YotogiPlayManager"; name="SetRepeatVoiceFile" },
  @{ asm="main";  kind="method";     type="YotogiPlayManager"; name="AddRepeatVoiceFile" },
  @{ asm="main";  kind="method";     type="YotogiKagManager"; name="TagTalk" },
  @{ asm="main";  kind="method";     type="YotogiKagManager"; name="TagTalkAddFt" },
  @{ asm="main";  kind="method";     type="YotogiKagManager"; name="TagTalkRepeat" },
  @{ asm="main";  kind="method";     type="YotogiKagManager"; name="TagTalkRepeatAdd" },
  @{ asm="main";  kind="method";     type="YotogiKagManager"; name="TagVoiceWait" },
  @{ asm="main";  kind="method";     type="BaseKagManager"; name="GetVoiceTargetMaid" },
  @{ asm="first"; kind="method";     type="KagTagSupport"; name="IsValid" },
  @{ asm="first"; kind="method";     type="KagTagSupport"; name="GetTagProperty" },
  @{ asm="main";  kind="method";     type="YotogiSubCharacterSelectManager"; name="OnFadeEnd" },
  @{ asm="main";  kind="method";     type="YotogiSubCharacterSelectManager"; name="GetPlayerSelectNum" },
  @{ asm="main";  kind="field";      type="YotogiManager"; name="sub_chara_select_mgr_" },
  @{ asm="main";  kind="method";     type="WfScreenManager"; name="CallScreen" },
  @{ asm="main";  kind="method";     type="YotogiManager"; name="get_play_mgr" },
  @{ asm="main";  kind="field";      type="Yotogis.Skill/Data"; name="playable_stageid_list" },
  @{ asm="main";  kind="field";      type="Yotogis.Skill/Data"; name="player_num" },
  @{ asm="main";  kind="field";      type="YotogiStageSelectManager/StageExpansionPack"; name="stageData" },
  @{ asm="main";  kind="field";      type="YotogiStage/Data"; name="id" },
  @{ asm="main";  kind="method";     type="CharacterMgr"; name="SetCharaAllPos" },
  @{ asm="main";  kind="method";     type="CharacterMgr"; name="SetCharaAllRot" },
  @{ asm="main";  kind="method";     type="CharacterMgr"; name="GetCharaAllRot" },
  @{ asm="main";  kind="method";     type="TBody"; name="SetBoneHitHeightY" },
  @{ asm="main";  kind="method";     type="CameraMain"; name="SetTargetPos" },
  @{ asm="main";  kind="method";     type="CameraMain"; name="SetDistance" },
  @{ asm="main";  kind="method";     type="CameraMain"; name="SetAroundAngle" },
  # ---- v1.7.5 (P37 join reset + P38/P39 meta chain + look-aim v2 + cross-stage 2nd root) ----
  @{ asm="main";  kind="method";     type="CharacterMgr"; name="SetActiveMaid" },
  @{ asm="main";  kind="method";     type="YotogiManager"; name="TagYotogiCall" },
  @{ asm="main";  kind="method";     type="YotogiManager"; name="TagReStart" },
  @{ asm="main";  kind="field";      type="YotogiPlayManager"; name="suspendStoreData" },
  @{ asm="main";  kind="method";     type="YotogiManager"; name="AddPlaySkill" },
  @{ asm="main";  kind="method";     type="YotogiManager"; name="get_null_mgr" },
  @{ asm="main";  kind="field";      type="YotogiManager/PlayingSkillData"; name="skill_pair" },
  @{ asm="main";  kind="field";      type="Yotogi/SkillDataPair"; name="lock_skill_exp" },
  @{ asm="main";  kind="field";      type="Yotogi/SkillDataPair"; name="skill_data" },
  @{ asm="main";  kind="field";      type="TBody"; name="offsetLookTarget" },
  @{ asm="main";  kind="field";      type="TBody"; name="trsEyeR" },
  @{ asm="main";  kind="field";      type="TBody"; name="quaDefHead" },
  @{ asm="main";  kind="field";      type="TBody"; name="quaDefEyeL" },
  @{ asm="main";  kind="field";      type="TBody"; name="quaDefEyeR" },
  @{ asm="main";  kind="field";      type="TBody"; name="m_editYorime" },
  @{ asm="main";  kind="field";      type="TBody"; name="boLockHeadAndEye" },
  @{ asm="main";  kind="method";     type="CharacterMgr"; name="GetCharaAllOfsetPos" },
  @{ asm="main";  kind="method";     type="CharacterMgr"; name="CharaAllOfsetPos" },
  @{ asm="main";  kind="method";     type="CameraMain"; name="get_camera" },
  @{ asm="main";  kind="method";     type="CameraMain"; name="IsFadeProc" },
  @{ asm="main";  kind="method";     type="YotogiNullManager"; name="SetNextLabel" },
  @{ asm="main";  kind="method";     type="Yotogis.Skill"; name="Get" },
  @{ asm="main";  kind="field";      type="Maid"; name="motionOffsetGP03" },
  @{ asm="main";  kind="field";      type="Maid"; name="prevMotionOffsetGP03" },
  @{ asm="main";  kind="field";      type="Maid"; name="baseOffset" },
  @{ asm="main";  kind="field";      type="Maid"; name="baseEulerAngles" },
  @{ asm="main";  kind="field";      type="Maid"; name="rotateLinkMaid" },
  @{ asm="main";  kind="method";     type="Maid"; name="SetPosOffset" },
  # ---- v1.7.6 (P40/P41 3P motion-chain diagnostics) ----
  @{ asm="main";  kind="method";     type="ScriptManager"; name="LoadMotionScript" },
  @{ asm="main";  kind="field";      type="MotionKagManager"; name="ActiveMaidList" },
  @{ asm="main";  kind="field";      type="MotionKagManager"; name="ActiveManList" },
  # ---- v1.7.7 (P42 FT stage-name override + POV slot tracking) ----
  @{ asm="main";  kind="method";     type="ScriptManager"; name="TJSFuncGetYotogiSelectStageName" },
  @{ asm="main";  kind="method";     type="YotogiStage"; name="IdToUniqueName" },
  @{ asm="main";  kind="method+sig"; type="CharacterMgr"; name="GetMan"; param="System.Int32" },
  @{ asm="main";  kind="field";      type="TBody"; name="m_strDefSlotName" },
  # ---- v1.7.8 (fusion rebase + official position changer UI + param init) ----
  @{ asm="main";  kind="field";      type="YotogiPlayManager"; name="positionChanger" },
  @{ asm="main";  kind="method";     type="YotogiPositionChanger"; name="OnClickAppearButton" },
  @{ asm="main";  kind="field";      type="YotogiPositionChanger"; name="m_YotogiPositionChangerObj" },
  @{ asm="main";  kind="method";     type="BgMgr"; name="get_m_DicAttachObj" },
  @{ asm="main";  kind="method";     type="TBody"; name="get_BoneHitHeightY" },
  @{ asm="main";  kind="method+sig"; type="YotogiParamBasicBar"; name="SetCurrentExcite"; param="System.Int32" },
  @{ asm="main";  kind="method+sig"; type="YotogiParamBasicBar"; name="SetCurrentSensual"; param="System.Int32" },
  @{ asm="main";  kind="method";     type="MaidStatus.Status"; name="set_currentExcite" },
  @{ asm="main";  kind="method";     type="MaidStatus.Status"; name="set_currentSensual" },
  # ---- v1.8.0 (P43 OnFinish pre-zero pose hold) ----
  @{ asm="main";  kind="method";     type="YotogiPlayManager"; name="OnFinish" },
  # ---- v1.8.1 (pos-changer gizmo + propensity unlock) ----
  @{ asm="main";  kind="field";      type="YotogiPositionChanger"; name="m_GizmoControlObject" },
  @{ asm="main";  kind="method+sig"; type="MaidStatus.Propensity"; name="GetAllDatas"; param="System.Boolean" },
  @{ asm="main";  kind="method";     type="MaidStatus.Status"; name="AddPropensity" },
  # ---- v1.8.2 (P44 orbit input shield + bg list) ----
  @{ asm="main";  kind="field";      type="UltimateOrbitCamera"; name="m_bFallThrough" },
  @{ asm="main";  kind="method";     type="UltimateOrbitCamera"; name="Update" },
  @{ asm="main";  kind="method";     type="BgMgr"; name="HasAssetBundle" },
  @{ asm="main";  kind="method";     type="BgMgr"; name="ChangeBg" },
  @{ asm="main";  kind="method";     type="GameUty"; name="get_BgFiles" },
  @{ asm="main";  kind="method+sig"; type="ScriptManager"; name="ReplacePersonal"; param="Maid" },
  # ---- v1.8.3 (P45 bg override + P46 select-stage fix + photo bg table) ----
  @{ asm="main";  kind="method";     type="PhotoBGData"; name="get_data" },
  @{ asm="main";  kind="method";     type="PhotoBGData"; name="Create" },
  @{ asm="main";  kind="field";      type="PhotoBGData"; name="create_prefab_name" },
  @{ asm="main";  kind="field";      type="PhotoBGData"; name="name" },
  @{ asm="main";  kind="method";     type="YotogiStageSelectManager"; name="get_SelectedStage" },
  @{ asm="main";  kind="method+sig"; type="YotogiStageSelectManager/StageExpansionPack"; name=".ctor"; param="YotogiStage/Data" },
  @{ asm="main";  kind="method";     type="YotogiStage"; name="IsEnabled" },
  @{ asm="main";  kind="method";     type="YotogiAdaptMyRoomStage"; name="ChangerBG" }
  # (YotogiManager::CallScreen is inherited from WfScreenManager - declared there, verified above)
  # ---- firstpass (TJSScript / DllBase) ----
  @{ asm="first"; kind="method";     type="TJSScript"; name="OnNativeConsolLog" },
  @{ asm="first"; kind="method";     type="DllBase"; name="NativeUtf8ToString" },
  # ---- UnityEngine (v1.4.0 P24) ----
  @{ asm="unity"; kind="method";     type="UnityEngine.Debug"; name="LogError" }
)

$fail = 0
foreach ($c in $checks) {
  if ($c.asm -eq "main") { $types = $asmMain.MainModule.Types }
  elseif ($c.asm -eq "unity") { $types = $asmUnity.MainModule.Types }
  else { $types = $asmFirst.MainModule.Types }
  $t = Find-Type $types $c.type
  if ($null -eq $t) { Write-Output ("MISS TYPE : " + $c.asm + " " + $c.type); $fail++; continue }
  if ($c.kind -eq "field") {
    $hit = $false
    foreach ($f in $t.Fields) { if ($f.Name -eq $c.name) { $hit = $true } }
    if ($hit) { Write-Output ("OK field  : " + $c.asm + " " + $c.type + "::" + $c.name) }
    else { Write-Output ("MISS field: " + $c.asm + " " + $c.type + "::" + $c.name); $fail++ }
  } else {
    $hits = @()
    foreach ($m in $t.Methods) {
      if ($m.Name -ne $c.name) { continue }
      if ($null -ne $c.param) {
        $sigOk = ($m.Parameters.Count -ge 1) -and ($m.Parameters[0].ParameterType.FullName -eq $c.param)
        if (-not $sigOk) { continue }
      }
      $hits += $m
    }
    if ($hits.Count -gt 0) {
      $sig = ($hits[0].Parameters | ForEach-Object { $_.ParameterType.Name }) -join ", "
      Write-Output ("OK method : " + $c.asm + " " + $c.type + "::" + $c.name + "(" + $sig + ") x" + $hits.Count)
    } else {
      Write-Output ("MISS methd: " + $c.asm + " " + $c.type + "::" + $c.name + " param=" + $c.param); $fail++
    }
  }
}
if ($fail -gt 0) { throw ("VERIFY FAILED: " + $fail + " missing target(s)") }
Write-Output "ALL PATCH TARGETS VERIFIED (MAIN + FIRSTPASS)."
