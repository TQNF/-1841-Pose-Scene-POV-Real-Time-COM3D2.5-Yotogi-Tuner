using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Yotogis;

// =====================================================================
// COM3D2.YotogiUnlimited v1.8.5  (COM3D2.5 Ver.3.38.0, BepInEx 5.4.x)
// ---------------------------------------------------------------------
// v1.0.0: 夜伽スキルのステージ制限解除 + 舞台全解放 + スキル再使用無限化
// v1.1.0: 特殊条件5種のミックス + 3P等マルチプレイ全解放 + NTR解除
// v1.2.0: 全条件無視(未習得含む全スキル) + 無限精神 + 興奮/官能スライダー
// v1.3.0: 自動絶頂禁止 + 一緒に絶頂 + ホイール速度 + お願い + 自動オープン
// v1.3.1: 欠損スクリプト安全化(C#入口) + 速度上限2倍
// v1.3.2: KAGエンジン層の欠損スクリプト安全化（性格変体回退）
//
// v1.4.0 追加要件（2026-09-19）:
//   内容17: 【お願い】/速度を v1.3.0 の X5 モードへ回帰 ——
//           v1.3.1 の2倍上限は突撃/加速が窮屈で擬真感を損なった。
//           SpeedMax 5.00x、突撃 1.9..2.4、律動上限クランプ3.0復元。
//   内容18: マウスホイール速度調整を廃止、スライダー専用へ。
//           （cfg MouseWheelSpeedControl 削除。旧cfgファイルに
//             孤立キーが残っても無害）
//   内容19: F7メニューUI全面リニューアル —— セクションBOX化・
//           1行化・コンパクト化・画面内クランプ（他プラグイン
//           ウィンドウとの視覚衝突の緩和）。
//   内容20: 性能最適化 ——
//           a) P7マージを「原版1回呼出(specialConditionCheck=false
//              =タイプフィルタなし) + クライアント側タイプ抽出」へ
//              変更：10回の全表スキャン+条件文再構築が1回になる
//              （旧方式は OnCall 5呼出 × 10スキャン=50回/開く度）。
//           b) 同一フレーム内マージ結果メモ（OnCall の5連呼出が
//              1スキャン+4再利用に。フレームを跨いだら必ず再構築
//              なので v1.2.1 の長命キャッシュ障害は構造的に不可能）。
//           c) 姿势デュープ除去（同名スキル变体を1項目に統合:
//              習得済み > Null型 > 先着 の優先度。リスト項目数が
//              1/5〜1/10に減り NGUI 生成コストも激減）。
//           d) 速度適用を「倍率≠1 or お願い中」のみに条件化
//              （停止中の毎フレーム全AnimationState走査を撤廃）。
//   内容21: 欠損OBJ/未検出系の自動検出抑止 —— UnityEngine.Debug.
//           LogError prefix（両オーバーロード）: 夜伽中の
//           「not found file」「〜が見つかりません」「〜を読み込め
//           ませんでした」系を Info 1回表示に格下げ。OBJ/.menu等
//           の欠損は存在しないデータなので静かにスキップされる
//           （動作は原版と同じ、ログの件数だけ整理）。
//
// v1.4.1 追加要件（2026-09-19）:
//   内容22: 【お願い】自動リズムの上限を 1.30x へ圧縮（ユーザー指定）。
//           加速 1.35..1.75→1.05..1.25、突撃 1.9..2.4→1.18..1.28
//           （ピストン波 0.25→0.06）、律動クランプ 3.0→1.30。
//   内容23: 【一緒に絶頂】後の状態復帰 —— 3P/ハーレムの副メイドは
//           興奮300のまま落ちない（絶頂実行は主メイドのみのため）。
//           主メイド絶頂後の currentExcite を読み、他の在场メイドへ
//           同期（一緒に落ちる）。また興奮クランプ(内容9)を
//           ym.maid だけでなく在场全メイドへ拡張（副メイドの
//           RRC9自動反応チェーンも防止）。
//   内容24: NPE無スタック問題の診断 —— "Object reference not set to
//           an instance of an object" がスタック無しで出るのは
//           EventDelegate.Execute 等の catch{LogError(ex.Message)}
//           チャネル。P24 を拡張し、夜伽中そのメッセージを検出したら
//           System.Environment.StackTrace 付きの Warning 1回に
//           変換（吞んだcatch位置が丸見えになり次回で確定修正できる）。
//
// v1.5.0 追加要件（2026-09-20）:
//   内容25: 音画同期 —— 速度倍率をアニメだけではなく在场キャラの
//           音声 AudioSource の pitch へも適用（AudioMan.Pitch =
//           SoundMgr.ConvertToAudioSourcePitch(m.VoicePitch) * 倍率）。
//           ベースは毎フレーム音声ピッチ設定値から再計算（換装で
//           AudioMan が作り直されても追従・スナップショット不要）。
//           BGM/UI音は SoundMgr 側なので無関係。範囲 [0.25..3.0]。
//   内容26: 【V】キーPOV —— 夜伽Play画面で在场全員（メイド+男）を
//           循環切替する本物の主観視点。F7メニュー外の独立機能。
//           機構: Camera.onPreCull で視点を毎フレーム最終書き込み
//           （KAG演出カメラより後＝必ず勝つ、ゲームのカメラ状態は
//           触らないので退出時に確実に戻る）。目位置は Eyepos_L/R
//           中点（無ければ頭骨+前方オフセット）。頭部は head/eye/
//           髪/頭部装備slotのレンダラーを enabled=false で隠す
//           （KK_PerspectiveX の教訓: シリアライズされるフラグは
//           絶対使わない・renderer.enabled は純ランタイム）。
//           0.5秒毎に再スキャン（演出中のロード追従）。FOV/前方/
//           上方/平滑度/感度は F7 のPOV区で調整可能。退出は V で
//           一周後、またはPlay画面を離れると自動（全状態復帰）。
//
// v1.5.1 修正要件（2026-09-20）:
//   内容25撤回: 「音画同期」はユーザー要件の誤解 —— 本当の要件は
//           速度同期ではなく【残留音声】の修復だった。pitch 書き込み
//           を全面撤回（音声は原速・原音調へ完全復元）。
//   内容27: 音声ハイジーン（残留音声の修復）——
//           a) Play画面を離れる瞬間の強制 VoiceStopAll（通常モードの
//              OnFinish は VoiceStopAll を呼ばない＝Result遷移までの
//              窓、途中退出・スキップ経路で loop 喘息が残留し得る。
//              安全網として確実に全停止）。
//           b) 残留観測: 停止時に「在场キャラでまだ音声が鳴っていた
//              数」を Info ログに記録（次回の根本特定用データ）。
//           c) F7メニューに「清除残留语音」ボタン（任意瞬間の
//              手動急救: 全 voice + SE 停止）。
//           repeat_voice (LoadPlay loop=true) は AudioSource 自身が
//           無限ループする設計で、@stopvoice/ClearVoice 以外に停止
//           手段が無い——演出スキップ・桩差し替え・画面遷移の隙間が
//           全て残留の温床になるため、出口で一括掃除が最も安全。
//
// v1.5.2 修正要件（2026-09-20）:
//   内容10撤回: 【一緒に絶頂】を削除 —— ユーザー特定: この機能が
//           残留音声の根源（OnClickCommand をプログラム的に Invoke
//           する経路が音声の正常停止フローを乱す）。関連する
//           DoOrgasmTogether / F7ボタン / OnClickCommand 反射一式を
//           完全撤去。v1.4.1 の「絶頂後副メイド同期」もこの機能の
//           一部だったため同時撤去（ExciteCap クランプ自体は
//           「禁止自動絶頂」機能として存続）。
//   内容27簡素化: 自動掃除（Play画面離脱時の VoiceStopAll）と
//           観測ログを撤回 —— 手動の「清除残留语音」ボタンのみ
//           残す（根源が消えたので自動安全網は不要、シンプル保持）。
//
// v1.6.0 修正要件（2026-09-20）: POV全面改稿 + 看镜头功能
//   内容26改稿: POVを「主人公（男）の主観」に固定 —— 循環切替を
//           廃止し【V】で入/切トグル。視点操作は【右ドラッグ】
//           （旧: 左ドラッグ）、FOVはF7スライダのみ（20..110、
//           ホイール調整は廃止）。KK_PerspectiveX 非参照の自研実装。
//           2.0/3.0両対応: 頭部骨は公式キャッシュ TBody.trsHead を
//           使用（非CRC男=ManBip01 Head / CRC身体=男女ともBip01 Head
//           をゲーム側が版本判定済み）。眼位置は Eyepos_L/R 中点→
//           trsEyeL/R（顔内眼球骨）→頭骨オフセットの順でフォール
//           バック。頭部非表示は公式スロット表の Head 添付スロット
//           （24種）+ マルチスロット子（レイヤー装備）も網羅。
//   内容28: 女仆头朝向镜头（トグル）—— TBody.boHeadToCam + trsLookTarget
//           =MainCamera.transform を毎フレーム強制（公式 EyeToCamera
//           と同一経路。KAG側の視線指定を上書きするのが仕様）。
//   内容29: 女仆眼朝向镜头（トグル）—— TBody.boEyeToCam 同上。
//           両方OFF時/画面離脱時は EyeToReset(0.5f) で原版へ滑らか復帰。
//
// v1.6.1 修正要件（2026-09-20）:
//   角標削除: POV状態の常時左下表示を撤去（F7 §POV の [POV中] のみ残す）。
//   镜头漂移修复: 旧锚点=眼球骨（头骨の子→转头时眼点扫动=相机漂移）。
//           新锚点=頭骨原点(回転支点、頭の向きに弱い) + 頸部坐标系
//           オフセット(trsNeck.rotation は頭単独の回転に追従しない)
//           ——側頭/うなずき等の頭部アニメでカメラはもう動かない。
//           cfgPovUp/cfgPovForward は「頭支点からの頸部系オフセット」
//           に意味変更（默认 up=0.07 / fwd=0.05）。
//   全員適配: POV対象を主人公固定→全員循環へ拡張（最初は必ず主人公、
//           以降 V で在场女仆/他男へ巡回、一周で退出）。
//   看镜头: POV対象自身は強制から除外（自分の眼位置を見ようとして
//           ジッターするのを防止）。
//   F7排版: 四区精簡（高潮区を功能区へ統合）+ 冗長ヒント削除。
//           FOV默认60。
//
// v1.6.2 修正要件（2026-09-20）:
//   轴向修复: 頸骨ローカル軸の実証値——X=骨方向(上)/Y=前後/Z=左右。
//           旧 (0,up,fwd) は up→Y(前後)・fwd→Z(左右) に化けていた。
//           新: upDir=頭位-頸位(解剖学上方向) + fwdDir=Cross(頸Z,upDir)
//           を頸Y軸と整合補正——軸名も符号も決定論的に導出。
//   视角跟随: カメラ回転=頭骨回転×ユーザーオフセット(Euler)。
//           頭が向けば視界も向く（側頭/うなずき/翻身すべて追従）、
//           右ドラッグは「頭の向きへの相対オフセット」に変更。
//   POV默认还原按钮: F7 §POV に「POV设置还原默认」——FOV70/前0.05/
//           上0.07/平滑0.65/灵敏度1/近裁剪0.02/看镜头双开。
//   默认FOV: 60→70。
//   在场检测: POV対象が非表示(姿势切替で退場等)が1秒続いたら自動
//           退出（主人公=透明男は例外維持）。頭部再スキャン0.5→0.25秒。
//           看镜头强制リストから外れた女仆は即 EyeToReset（退場後も
//           カメラを見続ける残留を防止）。
//   GetAnimation报错: TBody.GetAnimation() は骨未ロードで
//           「未だキャラがロードさていません」を吐く——速度缓存は
//           public フィールド m_Animation を直読みに変更（副作用ゼロ）。
//           P24 も同メッセージを夜伽中 Info 1回へ降格（KAG側発火分）。
//   全局看镜头: F1(Config)に「女仆头部朝向镜头-全局」「女仆眼睛朝向
//           镜头-全局」追加——夜伽以外の全場面でも強制。F7側の文本は
//           「女仆头部朝向镜头」「女仆眼睛朝向镜头」に変更。
//
// v1.6.3 修正要件（2026-09-20）:
//   右键旋转轴分解: 旧は camRot = 頭回転×Euler(yaw,pitch) ——頭が
//           傾いているとYaw軸も傾き、左右ドラッグが斜め滑りに見えた。
//           新: yaw は解剖学上軸(頭-頸)周り、pitch はその後の視線右軸
//           周りに分解適用——水平ドラッグ=純水平回転（傾き状態でも）。
//   退出视角复位: POV 进入時のカメラ position/rotation を保存し、
//           退出時に復元——「POVで向けた方向のまま通常視に戻る
//           （视角偏移が残る）」を根治。
//   动作抖动治理: 抖いの主因は頭部アニメの高周波ノイズが回転に
//           直結していたこと。v1.6.3は頭部回転のみをSlerp指数平滑
//           （ユーザーオフセットは平滑後へ即時適用=マウス操作は
//           一切遅延しない）。平滑曲線の最大追従も 4/s→1/s へ
//           約5倍強化（スライダ1で最強）。位置も同パラメタ平滑。
//
// v1.6.4 修正要件（2026-09-20）:
//   ADV MotionScript NPE 根治 (P25): ADV场景 @MotionScript 报
//           NullReferenceException (MotionKagManager.LoadScriptFile)。
//           根因（官方边界缺陷）: guid 未指定时 LoadScriptFile 内的
//           回退搜索循环（L259-266 maid×18 / L274-282 man×7）对
//           GetMaid/GetMan 的 null 返回值（越界/空槽）直接 .Visible
//           ——无 null 检查。修复两层：
//           prefix = main_maid_/main_man_ 为 null 时 null 安全地预解析
//           （可见且身体已加载的角色，与原回退意图一致）；
//           finalizer = 残余 NPE 吞掉并 Warning 单条（该动作跳过、
//           ADV 脚本继续，不再中断）。仅 NPE 被吞，其他异常照常。
//
// v1.6.5 修正要件（2026-09-20）: POV相机模型回退 v1.6.0 方案。
//   用户裁定: v1.6.1-1.6.3 的「头支点+颈系锚点」「头骨旋转跟随」
//   「轴分解」交互不合意——左右拖动应=纯镜头旋转。回退为:
//   - 眼位锚点(零硬编码): trsHead(官方版本缓存) + 眼位三级回退
//     Eyepos_L/R 中点 → trsEyeL/R(脸内眼球骨) → 头骨偏移
//   - 绝对自由视角: camRot = Euler(pitch,yaw)，进入时从头向初始化，
//     左右拖=纯水平旋转、上下拖=纯俯仰（世界轴，不随头动）
//   - 位置平滑保留（眼位跟随的抖动按滑条吸收）
//   保留的后续修复: 退出位姿复原(v1.6.3)/在场检测自动退出(v1.6.2)/
//   全员循环(v1.6.1)/头隐藏多槽(v1.6.0)/FOV70/还原按钮/P25。
//   偏移默认值随语义回退: 前0.03/上0。
//
// v1.6.6 修正要件（2026-09-20）: POV中切姿势黑屏根治（P26）。
//   官方边界缺陷: OnClickCommand 的 adv_hook 分支硬编码
//   Suspend(cmd, "*サスペンド") —— FadeOut 后 JumpLabel 落在
//   adv_kag「当前调度器文件」；*サスペンド 标签只存在于新模式
//   内容包调度器（av001_main.ks 等，标签与 hook 设置者同文件），
//   旧模式调度器（YotogiMain.ks 等）无此标签 → KAG 调度脚本死亡
//   → 淡出后永不淡入 = 永久黑屏。原版普通夜伽选不到带 adv_hook
//   的命令（AV/GP 包技能），本插件的全解锁特性让其流入普通夜伽
//   才踩中。游戏自己的 OnClickNext 已按 is_new_yotogi_mode 分流，
//   adv_hook 路径漏了同一门槛。
//   P26 = Suspend prefix: 非新夜伽模式时跳过原版挂起（状态掩码/
//   淡出/致命跳转全部不发生）+ 代行重入 OnClickCommand（同一命令）
//   ——首次点击时 calledAdvFiles 已记录该 ADV 演出 → 重入后
//   flag=false → 命令正常执行。用户体验=第一次点击直接生效，仅
//   跳过那场本就无法播出的 ADV 插入演出。s_suspendReentry 护栏
//   防病理递归。新模式（AV/GP 会话）原版放行——其调度器带标签，
//   挂起正常工作。
//
// v1.7.0 追加要件（2026-09-21）: 六项一次落地。
//   内容31: POV往下限制减小 —— 俯仰向下 85°→89°（接近垂直下方，
//           避开±90°欧拉奇异点），向上限不变（-85°）。
//   内容32: 夜伽技能选择上限破除（原生硬编码 7）+ 原生级滑条 ——
//           三层全破: P27=技能容器槽位运行时扩展（克隆
//           SkillIconUnit + UIGrid 重排，反射构造私有嵌套槽类并
//           重绑右键/拖拽回调）；P28=OnSkillChangeEvent 后置
//           （flag 用滑条上限重算，逐单元重放官方启用条件）；
//           P29=SetPlaySkillArray 后置（选择数>7 时全量重建
//           PlayingSkillData 数组——AddPlaySkill 同款构造）。
//           滑条 cfg SkillSelectCap 7..14 默认10（F7 与 F1 均可调）。
//   内容33: F7 姿势一览 —— 全技能滚动列表（skill_data_list 全表+
//           同名去重），点击即局内实时切换: 已在数组→塔点击协议
//           （playing_skill_no_=k-1 + OnNextSkillMove；k=0 走既有
//           P5/P6 塔位0协议）；不在数组→官方 AddPlaySkill 追加后
//           切至末位（P20 兜底未习得）。塔图标随数组增长自动扩展
//           （P4 内新增塔子节点克隆+点击重绑，顺带根治原版
//           OnSkillIconClick 在数组>塔位数时的越界崩溃）。
//   内容34: F7 角色位置 —— 在场每个人（主女仆/副女仆/男）的
//           位置 X/Y/Z + 旋转 Y 滑条；基线=本姿势的原始变换
//           （首次交互时懒捕获，切姿势自动失效），支持单角色
//           「复原」与「全部复原」（Maid.SetPos/SetRot 官方本地
//           通道=根坐标系，与官方位置调整器同机制）。
//   内容35: Tab 隐藏UI（默认开，cfg 可改键/可关）—— ym.uiVisible
//           官方整体面板开关（官方挂起同款通道，含指令菜单/塔/
//           参数条）+ 消息窗口节流关闭；仅 Play 画面生效，离开
//           自动恢复。
//   内容36: F7 场景切换 —— YotogiStage.GetAllDatas 官方舞台列表
//           （drawName 显示），点击走官方舞台选择屏同源序列:
//           SelectStage → skillSelectcharacterData.Apply（角色根
//           锚定新舞台坐标系）→ skillSelectLightData.Apply →
//           SelectedStage.ChangeBG（加载预制体+维护舞台静态）→
//           PlayBGM。局内实时生效。
//
// v1.7.1 修正要件（2026-09-21）: 两删一加。
//   删除: 内容32「技能选择上限破除」—— P27(容器槽位克隆)/P28(flag重算)/
//           P29(会话数组重建)/SkillSelectCap 滑条 全部撤除（用户裁定；
//           补丁基线恢复为原生7上限行为）。
//   删除: 内容34「F7角色位置」—— 每人独立位置/旋转调整、基线懒捕获、
//           复原按钮、P4 的基线失效钩子 全部撤除（用户裁定）。
//   追加: 内容37「姿势资源/道具自动加载」（场景除外）——
//           进入姿势时自动挂载该姿势所需的道具（垫子/猥亵椅/
//           拘束机器/贴刑架/道具箱等），不切换场景。
//           机制（逆向实证）: 游戏的姿势道具 = FT 文件(start_call_file,
//           YotogiPlayManager 协程 L631 官方执行)里的 @AddPrefabBg 挂载,
//           坐标相对该舞台分支的角色锚点（@AllPos）。官方只覆盖
//           FT 分支里列出的舞台——本插件全解锁让 VIP 姿势流入
//           普通夜伽 + F7 自由换舞台后, 分支外舞台全部悬空。
//           P30 = 每帧维持式补挂: 内置 135 条「FT→道具+相对锚点偏移」
//           逆向表（从 script_cbl.arc 的 ft_*.ks 官方挂载提取, 每
//           ft×src 取偏移模长最小条目; name 以 \uXXXX 转义规避
//           Add-Type 编码坑）。挂载走官方 BgMgr.AddPrefabToBg 通道
//           （当前舞台根下 local 坐标）; 位置 = 角色锚点
//           (GetCharaAllPos) + 表偏移; 官方 FT 已挂（同名在
//           m_DicAttachObj）则跳过（天然幂等）; NextSkill/StageSwitch
//           的 ChangeBG 重放会清挂载物 → 维持式自动补回; 姿势加载
//           中(IsAllCharaBusy)不挂; 离开 Play 不清（场景切换时
//           官方 DelPrefabFromBgAll 兜底）。cfg PoseResourceEnabled
//           默认开, F7 §功能 可关（关闭时主动清一次自己挂的）。
//
// v1.7.2 修正要件（2026-09-21）: NRE根治 + P30事件驱动化 + POV解除限制/隐藏脖子。
//   修复1 (P31): FT脚本执行 NRE 根治 —— ScriptManager.TJSFuncGetMaidStatus
//           对 GetMaid(n) 的 null（3P/NTR变体FT查询不在场副女仆,
//           全脚本弧内 3600 处 GetMaidStatus(1,...) 调用点）直接
//           .status deref → NullReferenceException 从 KAG 同步执行段
//           一路穿透 CallFtFiles 协程 → 协程死亡 → doneFtFileCall
//           永不置位 → Play屏 fade 卡死 FadeInWait（Finish呼び出し
//           刷屏）+ 官方姿势道具永不挂载。
//           prefix = 查询的女仆为 null 时 result="" 跳过原版
//           （脚本 @if 分支自然走 else, 官方挂载继续）。
//           另加 CallNormalFile finalizer: FT/KA 同步段残余 NRE
//           吞掉（Warning 1条, KAG 后续帧继续执行未完成的标签）——
//           任何未知 NRE 不再能杀死 FT 协程/卡死画面。
//   修复2 (P30v2): 姿势道具自动加载全面重构 —— 撤除 0.3s Update
//           轮询维持（卡顿根源之一）与错误查表键。根因: ①运行时
//           start_call_file = "?_FT_XXXX.ks"（性格占位符, 3.5系
//           技能）或 "ft_XXXX.ks"（CBL通用）, v1.7.1 的表只按
//           "ft_XXXX.ks" 精确文件名索引 → 性格占位技能全部查空
//           （一个道具都没挂过）。②轮询式每 0.3s 扫描。
//           新机制（纯事件驱动, 零轮询）:
//           - 表重构: 全 script 弧扫描（通用 ft_*.ks + 性格变体
//             ?1_ft_*.ks 全部 12025 个 FT 文件, SJIS 解码）,
//             「技能部位键」= 剥离性格前缀后的编号（04130/0610/
//             vr_0055rotenburo）, 每 (部位×src) 取偏移模长最小
//             条目 —— 105 部位 / 134 条（性格变体合并去重）。
//           - 触发: CallNormalFile postfix —— FT 的舞台分支+@AllPos
//             +@AddPrefabBg 同步段在 CallNormalFile 内部就执行完
//             毕（NRE 栈实证）, postfix 时点官方道具已挂、锚点已
//             稳 → 即时查表补挂缺失道具（官方同名已挂→跳过=天然
//             幂等; "YU_"+src 已挂→跳过）。FT2/KA 调用（label非空）
//             与气绝系统脚本不触发。
//           - 场景切换(F7)/开关开启: 直接触发同一挂载通道。
//           - ChangeBG 清场后由下次 FT/切换自动重评估, 无需维持。
//           - 关闭时主动 DelPrefabFromBg 清自己挂的。
//   修复3 (内容38): POV 上下限制解除 —— 俯仰 ±90°（真·垂直上下,
//           不越过奇异点所以无翻转）。
//   修复4 (内容39): POV 隐藏脖子 —— trsNeck 骨骼 localScale 缩至
//           0.01（颈部以上全部塌缩到锁骨一点; 头部本就隐藏,
//           男女身体都适配——男体脸部在身体网格内, 以前从眼看
//           下去脖子戳镜头）, 退出/切换目标时精确复原。
//
// v1.7.3 修正要件（2026-09-21）: 用户验收六项一次落地。
//   修复A (问题1/1.1): 姿势一览独立大窗 —— 从 F7 主菜单 150px 内嵌
//           滚动区改为独立窗口（附着在主菜单左侧, 平行一体, 随主窗
//           移动）, 尺寸 380x560; 内置搜索框（实时过滤）+ 虚拟化
//           渲染（固定行高, 只绘制可视行≈20行）——原实现每帧全量
//           绘制 ~700 个 GUILayout 按钮（OnGUI 每帧 Layout+Repaint
//           多事件 × 700 控件 = IMGUI 布局/GC 开销巨大）= 打开姿势
//           一览即掉帧的根源。搜索+虚拟化后每帧 ~20 行, 卡顿消除。
//   修复B (问题2): POV 俯仰限制完全解除 —— ±90° → ±180°（可越过
//           垂直方向一直翻到正后方, Euler 构造无奇异问题）。
//   修复C (问题3): 3P系姿势无副女仆 —— 双层根治:
//           ① P33/P34 = 语音数据层 NRE 防护（YotogiPlayManager.
//           SetAdditionalFtVoice/SetRepeatVoiceFile/AddRepeatVoiceFile
//           拒绝缺席女仆的槽位 + YotogiKagManager 五个 talk/voice
//           标签的目标女仆 null 守卫）—— 原版 Update() 每帧
//           GetMaid(1).AudioMan NRE 刷屏 + TagTalk 同步段 NRE 卡死
//           KAG（Finish呼び出し 复发）的真正根源。
//           ② P35/P36 = 点击 3P 姿势且在场女仆不足时, 介入官方
//           「夜伽追加女仆选择」屏（YotogiSubCharacterSelectManager）:
//           预瞄技能索引 + GetPlayerSelectNum 精确化 → 选完 OK 后
//           OnFadeEnd 重定向回 Play 屏, OnCall→NextSkill 落在预瞄
//           姿势上 → 副女仆已入槽, 3P 姿势完整成立。
//   修复D (问题4): POV 中切姿势 —— ①切姿势后颈锚重捕获: P6
//           NextSkill 前置置脏标, 0.25s 巡检在新姿势稳定（非busy
//           持续0.4s）后 RestoreNeck→HideNeck 重锚（新姿势眼高/
//           骨架姿态正确重捕获, 相机平滑位置重置吸附新眼位）。
//           ② PovHideNeck 自愈: 幂等检查改为「书签目标一致 且 颈骨
//           实际仍处于塌缩态」, 换装/骨骼重建（新骨 scale=1）后
//           自动重捕获+重塌缩（旧实现早退 → 重建后脖子复现戳镜头）。
//           ③ 自动退出（离场/换屏）时相机改为官方标准取景
//           （SetTargetPos+SetDistance(3)+SetAroundAngle, 与
//           CallFtFiles 尾部 cameraSet 同源）——旧实现还原到进入
//           POV 时的远古位置, 多姿势后退出视角异常。V 键手动退出
//           仍精确复原进入时位姿。
//   修复E (问题5): 非对应地图位置保留 —— Skill.Data.
//           playable_stageid_list（技能原始可玩舞台表, P1/P2 解锁
//           只旁路判定不破坏数据）判定当前舞台是否为该姿势的
//           「对应地图」: 非对应时 CallNormalFile prefix 捕获
//           chara 根位姿, postfix 还原（@AllPos 的舞台无关坐标/
//           BoneHitHeightY 全部回退）—— 姿势在当前位置播放而不是
//           传送到「姿势的原坐标」。对应地图保持官方锚定（床等
//           舞台内置家具对齐不受影响）。
//   修复F (问题6): 道具按舞台分支精确偏移 —— FT 表重构为舞台键控
//           （扫描器解析 @if/@elsif GetYotogiSelectStageName 分支栈,
//           每行携带分支舞台集+锚点旋转; 872 行 / 453 部位舞台集）。
//           挂载时: 当前舞台命中该部位分支 → 用该舞台的精确条目
//           （官方几何）; 未命中（跨舞台） → 回退条目 + 旋转感知
//           放置（把分支坐标系下的 (道具-锚点) 偏移重基底到当前
//           角色根旋转系: pos = 锚点 + R_now·(R_branch⁻¹·off),
//           rot 同链合成）——修复「随机偏移」（不同舞台分支锚点
//           朝向不同, 世界系偏移直接套用必然错位）。
//
// v1.7.4 修正要件（2026-09-21）: 用户验收两小项（行为微调, 补丁数不变）。
//   问题1: POV 俯仰翻越 ±90° 后水平拖拽「逆向」—— ±180° 解除限制
//           的副作用: 相机上下颠倒（翻越垂直看向正后方）后, 世界轴
//           水平旋转（Ry 外层, Unity Euler Z→X→Y 序）在翻转屏幕上的
//           表现方向反向（拖右看左）。修复: |pitch|>90° 区间内反转
//           水平响应（yawSign）——拖拽方向始终与屏幕一致; 上下俯仰
//           不受影响（pitch 内层旋转轴恒为相机自身右轴=屏幕水平轴,
//           任何角度都无翻转）。±90° 正中为几何退化点（水平拖=绕
//           视轴滚转, 与现实低头原地转身一致）, 无需处理。
//   更改2: POV 前方偏移默认 0.03 → 0.000（cfg 默认值 + 「POV设置
//           还原默认」按钮 + onPreCull 回退值三处同步; 旧 cfg 已存
//           0.03 的用户按一次还原按钮或手动归零即可生效）。
//
// v1.7.5 修正要件（2026-09-21）: 翻越垂直区「各种未提到的问题」
//           的结构性根治（用户验收: v1.7.4 的符号翻转只治其一,
//           颠倒视野/极点打转/回绕怪异仍在）。
//   方案: POV 相机核心重构 = 四元数自由相机 + 自动回正（Auto-Level）。
//   - 存储: s_povRot（Quaternion）替代 Euler yaw/pitch——无奇点、
//     无 ±180 回绕、无角度钳制需求。
//   - 拖拽: 世界Y轴水平旋转（保持 v1.6.5 批准的「纯水平」手感）
//     + 相机自身右轴俯仰（恒为屏幕水平轴, 任何朝向上下都不反向）;
//     瞬态颠倒期（回正进行中）水平响应反号, 屏幕始终一致。
//   - PovAutoLevel 每帧: 以当前视线 fwd 为锚, Slerp 向「同一 fwd
//     的无滚转直立朝向」。正常范围目标==当前（零干扰, 手感不变）;
//     翻越垂直后把颠倒朝向平滑转换为「直立向后方看」（Over-the-Top,
//     视线连续、地平线永远水平）; 极点退化区（|fwd.y|>0.9999）跳过。
//   - 效果: 垂直 360° 全向可达（越过头顶/脚底继续向后看）, 地平线
//     永不翻转, 水平反向只存在于 <0.3s 回正瞬态, 无打转/无回绕/无
//     边界抖动。
//
// v1.7.5 追加要件（2026-09-21 第二批, 用户验收四项）:
//   追加1: PovNearClip 默认 0.02 → 0.1（贴脸的男体/道具不再被近裁剪
//           裁掉; 用户 cfg 已同步）。
//   追加2: 看镜头（头/眼）默认改为【关闭】+ POV 模式根治:
//           旧目标 = 相机 rig 根（cm.transform, 环绕枢轴）而非真实
//           渲染相机 → POV 下目标离实际视点甚远 = 「不起作用」;
//           官方 HeadToCam 层只有 40%/20% 增益 + ±60° 入锥门控
//           = 「范围太小」。修复: 目标改为 cm.camera（真实相机,
//           POV 写的正是它）+ offsetLookTarget 清零 + preCull 直连
//           瞄准通道（头: 全增益, 钳 ±80°/−55..65°; 眼: 0.6/0.4
//           增益, 钳 ±45°/±40°; 官方锁定/そらし目语义保留）。
//   追加3: 选择女仆后角色扭曲 / 吃醋·スワッピング等姿势位置不正确
//           —— 双根治:
//           P37 = SetActiveMaid prefix: 夜伽中途入会女仆本地位姿清零
//           （官方 ResetCharaPosAll 只在场景/会话开始跑, 中途
//           @CharaActivate maid=1 npc=… 入会带着残留位姿 → 错位扭曲）;
//           P38/P39 = @YotogiCall/@ReStart 元技能链确定性落位（官方
//           插入子技能「找第一个空槽, 满则静默放弃」——满 7 技能时
//           链断裂: NPC 停原点重叠 + AddAllOffset_Ignore 残留 → 后续
//           姿势全部沉入床台; 修复 = 保证插入（满则追加）+ 预瞄
//           playing_skill_no_=idx-1 → NextSkill 的 ++ 精确落位）。
//   追加4: 跨舞台位置保持补第二层根（m_goAllOffset/@AddAllOffset,
//           床台抬升层）——旧版只还原第一层（m_goActive/@AllPos）,
//           抬升残留 → 跨舞台姿势悬浮。
//
// v1.7.6 修正要件（2026-09-21 第三批, 用户实测 v1.7.5 后验收七项）:
//   1. 看镜头「范围扩大」回退 —— LookAimPass 直连通道的增益/锥角
//      回归官方值: 头 0.4×增益 + ±60°/-40..50° 锥（官方 HeadToCam
//      同值）; 眼 0.2/0.1×增益（官方 EyeToCam 同值, 撤销 ±45/±40 钳）。
//      保留: 真实相机目标（cm.camera）/ offset 清零 / preCull 时序 /
//      官方锁定·そらし目语义。扩大的 ±80°/全增益让女仆转头过度不自然。
//   2. POV「特定角度镜头自动旋转」根治（用户头晕）—— 撤销 v1.7.5 的
//      PovAutoLevel 自动回正（翻越垂直时把颠倒朝向滚转回正 = 突发
//      横滚是头晕根源）+ 俯仰钳制 ±89.9°（四元数重建, 永不越过垂直,
//      结构上杜绝横滚/颠倒/反向区; 水平拖拽恒为世界Y轴纯水平）。
//   3+4. 选择女仆后「没有进入动作/站着」「人物全部混合在一起」——
//      本批新增 3P 动作链诊断插桩（P40/P41 + CallNormalFile 带 label
//      日志）, 下次实测日志将精确定位断点（动作加载层 vs 定位层）。
//      已实证排除: 「Maid0同帧重复加载」警告 = 官方同帧去重（正常）;
//      isMotionScriptSet 每帧重置（TBody.Update）→ 非跨技能残留。
//   5. F7【POV设置还原默认】同步 v1.7.6 新默认（旧按钮仍是 v1.6.2 值:
//      near 0.02/look 开 → 现 near 0.01/look 关/cull 开）。
//   6. 「前方偏移是左右偏移」修复 —— 旧实现把偏移挂在头骨局部系
//      （headRot*(0,up,forward)）: 头一转向/姿势中头骨轴与世界错开,
//      「前方」跟着头骨跑 = 观感上是左右偏。改为: 前方 = 当前视线
//      方向（s_povRot*forward, 看哪推哪）, 上下 = 世界竖直。
//   7. PovNearClip 默认 0.1 → 0.01 + POV 自身整体剔除（PovCullSelf,
//      默认开）: 进入 POV 时把当前 POV 角色的全部渲染器 enabled=false
//      （0.25s 追扫换装重建, 退出/切换精确复原）——脖子残骸（塌缩点）
//      连同整个自身一起消失, 近裁剪面推到 0.01 也不再看到自身几何,
//      贴脸的男体/道具以全距离可见。
//
// v1.2.1 は重大バグのため破棄(ユーザー指示)。v1.3.x 以降は v1.2.0 が基礎。
//
// v1.7.7 修正要件（2026-09-22, 用户实测 v1.7.6 后验收四项）:
//   1+3. 「姿势位置错乱/人物全部重叠」（美希達とスワッピング立ち愛撫等
//      多对姿势, 副女仆/NPC 不在自己的位置而是叠在主角色上; 选女仆界面
//      回来后姿势扭曲, 换其他姿势又正常）—— 根因三层, 全部源于
//      「FT 同步段的舞台名门控」在非对应地图上整体关闭:
//      ① ft_0116/ft_0075 等多对姿势的定位块（@AllResetPos + @AllPos
//        锚点 + @Offset maid=1/man=1 双对分离 + @RotateLink + 道具
//        @AddPrefabBg + 相机）全部写在 @if GetYotogiSelectStageName()
//        == '官方舞台' 之内 —— 非对应舞台时整块跳过 → 第二对角色
//        无分离定位 → 与第一对叠在同一锚点上 = 「全部重叠」。
//      ② 门控内还有 SetTmpFlag('AddAllOffset_Ignore',1) —— 该旗标
//        同时就是 ScriptManager.isGP001Mode: 旗标未立时 OM 系动作脚本
//        的 @autooffset 走 motionOffsetGP03（旧语义）而非 IK 碰撞高
//        度, @inverseoffset 直接 no-op = 「姿势扭曲」的另一来源。
//      ③ v1.7.3 的「跨舞台位置保持」(问题5修复E) 会把 FT 的 @AllPos
//        传送还原回旧位置 —— 与①的修复方向直接冲突, 本批移除
//        （用户现在的要求 = 「姿势要去对应的地方」, 与 v1.7.3 当时
//        的「保持当前位置」要求相反, 以最新要求为准）。
//      修复 = P42: TJSFuncGetYotogiSelectStageName prefix —— FT 同步段
//      执行窗口内（CallNormalFile prefix→postfix/finalizer, 无 label 的
//      FT 整文件调用; 官方实证舞台分支在此窗口内同步执行完毕）,
//      把返回值覆盖为「当前技能 playable_stageid_list 第一个舞台的
//      uniqueName」→ 门控在任何地图上都按官方舞台分支执行:
//      定位/双对分离/道具/相机全部落地, 旗标立起, OM 动作脚本走
//      GP001 语义。对应地图/无舞台绑定技能不受影响（原值返回）。
//      旗标生命周期与官方一致（NextSkill(free mode)/OnFadeEnd 清零）。
//   2. 「剔除问题: 你把整个剔除了我看什么?」—— PovCullSelf 把 POV
//      角色的全部渲染器关掉, 男主角 POV（V 键默认第一个目标）时
//      男器(chinko 槽位)也被剔除 = 视野里动作部位消失。修复:
//      PovCullSelf 跳过 chinko 槽位（含其子层级）的渲染器 ——
//      剔除范围只影响自身身体, 男器保留可见。
//   4. 「POV 中切换姿势或选女仆界面后我的角色没有恢复正常状态,
//      POV 还有问题」—— 根因 = 男体 swap 导致 POV 目标引用悬空:
//      MotionKagManager.LoadScriptFile 的 CRC 分支会 SwapNewManBody
//      （活动槽位的 Maid 对象被 pairMan 整个替换, 旧对象移回仓库
//      并隐藏）。POV 目标是替换前的旧对象 → 相机锚到仓库里的
//      旧身体（POV 视角飞到原点/异常）, 剔除也打在旧对象上,
//      旧身体带着剔除状态回仓库 = 「角色没有恢复正常」。
//      修复三件:
//      a) POV 槽位跟踪重解析 —— PovEnter 记录目标槽位(man),
//         0.25s 追扫时发现槽位现住者≠当前目标 → 完整切换
//         (复原旧目标头/颈/剔除 → 新目标重新隐藏/剔除/塌缩,
//          相机平滑重置)。仅对 man 目标生效（maid 不做对象级
//          换体, SwapNewMaidBody 只是属性交换; npcmaid 的
//          SwapActiveSlot 是换角色, 走既有的 targetGone 自动退出）。
//      b) V 键手动退出也改官方标准取景（与自动退出同源）—— 旧实现
//         还原「进入 POV 时的位姿」, 多次切姿势后必然视角异常
//         （v1.7.3 已在自动退出修过同样的问题, 本批统一）。
//      c) 姿势切换重锚块（s_povPoseDirty 稳定后）追加 PovCullSelf
//         —— 重建的身体立即补剔除, 不等下一个 0.25s 追扫。
//
// v1.7.8 修正要件（2026-09-22 第二批, 用户实测 v1.7.7 后验收）:
//   1+1.1+1.2【融合】「舞台门控定位」与「跨舞台位置保持」—— v1.7.7 的
//      门控强开有效（双对分离/道具/旗标全部落地）, 但 FT 的 @AllPos
//      是官方舞台的绝对坐标 → 非对应地图上角色浮空/飞出地图外,
//      姿势中切场景后旧地面高度残留 = 物理崩坏、道具错位。
//      融合方案 = 「官方相对布局 + 保留锚点」:
//      - 前置捕获: 双层根位姿（m_goActive/m_goAllOffset）+ 挂载道具
//        名字快照（BgMgr.m_DicAttachObj）;
//      - 门控块照常执行（P42 不变: @AllPos/@Offset 双对分离/@RotateLink/
//        @PhisicsHit/道具/旗标全落地——全部是相对布局数据）;
//      - 后置重基底: ① 窗口内新挂的道具从「FT 锚点系」重基底到
//        「保留锚点系」（pos = pre + R_pre·(R_ft⁻¹·(p-ft)),
//        rot 链式合成）; ② 每角色物理地面保持相对差
//        （floor = preY + (floor_ft - ftY)）; ③ 双层根还原到
//        FT 之前的位姿; ④ 相机回到官方标准取景（对准还原后的
//        全角色中心）。整体在淡出遮罩内完成（doneFtFileCall 之前）。
//      → 双对分离保留、角色不再浮空/出图、道具贴着角色、物理正常;
//        官方每(舞台×技能)自定义位置系统（CallFtFiles 尾部）在其后
//        追加应用, 层级关系正确。
//   1.2 场景切换物理重置: StageSwitch 后 BoneHitHeightY 全员重置为
//      当前根高度（TagAllPos 同式默认关系）+ 第二层根（抬升）清零
//      —— 姿势中切场景后旧舞台的地面高度/床台抬升不再残留。
//   2. POV 剔除功能整体移除（用户指示: 「删除：POV中隐藏自身,
//      保留PovNearClip」）—— PovCullSelf/PovRestoreSelf/PovChinkoRoots/
//      cfgPovCullSelf/相关调用与 F7 开关全部删除。脖子塌缩+头部隐藏
//      （内容39）保留, 男体换体重解析（v1.7.7 a项）保留。
//      POV 相关的状态残留面因此缩小到 头部渲染器+颈骨scale 两条,
//      退出/切换/换体重解析三条复原路径全部覆盖。
//   追加1: 兴奋/感性会话初始值 —— 夜伽会话首次进入 Play 画面时
//      主女仆 currentExcite=0, currentSensual=10（同步参数条+
//      UpdateCommand）。每会话仅一次。
//   追加2: F7 接入官方「夜伽位置调整」UI（YotogiPositionChanger,
//      官方功能, 入口按钮被 PluginData X001 门控隐藏）——
//      F7 §场景 新增按钮直接调用 OnClickAppearButton() 打开
//      官方移动/旋转 gizmo 调整界面（含道具）, 官方返回按钮负责
//      关闭+按(舞台×技能)保存自定义位置。
//   优化: 帧数 —— ① POV 剔除移除 = 撤掉 0.25s 一次的全层级渲染器
//      深扫（GetComponentsInChildren(true) 含未激活, POV 中每 0.25s
//      一次的卡顿源）; ② 融合修复后角色不再浮空 → 裙子/头发物理
//      不再对空射线（每帧求解器空转 = 帧数下降主源）。
//
// v1.8.0 修正要件（2026-09-22 第三批, 用户实测 v1.7.8 后验收）:
//   修复1【问题1复发】拘束台/三角木馬/ソファ系姿势位置仍不正确 ——
//      v1.7.8 重基底把布局还原到「FT 之前的位姿」, 但官方 OnFinish
//      (YotogiPlayManager L2611) 每次切姿势前都 SetCharaAllPos(0)
//      归零根 → prefix 捕获到的 pre 永远是世界原点, 而原点在用户
//      所选地图上未必是有效位置（浮空/穿墙/位置突跳）。
//      修复 = P43: OnFinish prefix 在归零前捕获双层根位姿
//      (playing_skill_no_ != -1 的「继续下一技能」分支), FT prefix
//      优先使用该 hold 值 → 重基底还原到上一姿势的实际位置（视角
//      连续, 官方对应地图/非对应地图行为都正确）。
//   修复2【物品未实时加载】PoseResMountPass 非对应地图只挂
//      stage==null 的兜底条目 —— 姿势道具只存在于 stage 分支条目
//      时（官方舞台变体专属道具）全部漏挂。修复 = cross-stage 回退:
//      null 条目优先, 仍缺的 src 回退到第一个 stage 条目（位置数学
//      基于已还原的根, 重基底语义一致）。
//   修复3【POV 视角反转】旋转/特定角度直接反转 —— 拖拽增量一帧内
//      可越过 ±90°（快速甩鼠标/高灵敏度）, 越过垂直点后 forward
//      水平分量反向, PovClampPitch 重建的 Atan2(f.x,f.z) 给出
//      yaw+180° = 视角直接反转。修复 = 增量预钳制: 先算当前俯仰
//      pyCur, 本帧俯仰增量截断使 pyCur+Δ 永不越过 ±89.9°（Atan2
//      恒在稳定区）, PovClampPitch 保留兜底。
//   更改1: 每次换姿势（NextSkill）兴奋=-100 预设、感性=10 ——
//      NextSkill_Prefix 统一重置主女仆 status, 官方 NextSkill
//      L987-989 读 status 自动刷新参数条（SetCurrentExcite/
//      SetCurrentSensual is_anime:false）。v1.7.8 的会话初始值
//      Update 钩子移除（NextSkill 覆盖所有进 Play 场景, 含首次
//      与选人回来）。KA 段分支随之恒走 *KA2（currentExcite<0）。
//   更改2: 前方偏移默认 0.000 → 0.040（cfg Bind 默认值 + F7
//      「POV设置还原默认」同步）。
//   追加1: F7【重新载入当前姿势】按钮 —— playing_skill_no_=cur-1
//      + OnNextSkillMove（NextSkill 的 ++ 恰落回当前姿势, 塔位0
//      协议同 P35）→ 完整重载链（归零→FT→门控→重基底→动作）。
//   追加2: 选女仆后自动热加载 —— P35 joined 分支置位
//      s_replayAfterJoin, Update 检测全员空闲稳定 0.6s 后自动触发
//      ReloadCurrentPose()（用户方案: 选人后首次加载仍扭曲时,
//      自动重播一遍即修复, 等价于手动「换姿势再切回来」）。
//   美化: F7 菜单排版 —— 滑条标签+滑条同行（BeginHorizontal,
//      每行高度减半）、区块顺序整理（参数→速度→POV→功能→姿势与
//      场景）、标题版本号同步 v1.8.0（v1.7.6 起一直漏更）。
//
// v1.8.1 修正要件（2026-09-22 第五批, 用户实测 v1.8.0 后验收）:
//   修复1【重载终止夜伽】点击【重新载入当前姿势】后夜伽直接结束 ——
//      当前姿势 idx=0（塔位0）时 ReloadCurrentPose 设 playing_skill_no_
//      = cur-1 = -1, OnNextSkillMove/OnFinish 都把 -1 判定为「结束分支」
//      → 夜伽终止。修复 = 塔位0协议（与 IconClick 同式）: cur==0 时设 0
//      + s_forceSkillZero, NextSkill_Prefix 消费时改设 -1 → 官方 ++ 后
//      恰落回 0。cur>0 路径不变。
//   修复2【旋转gizmo随距离缩放】官方夜伽位置调整的【旋转调整】圆环
//      大小 ∝ 相机距离（GizmoRender.generalLens = -2·tan(fov/2)·dist/50
//      ·offsetScale, 屏幕恒定设计; 且 fov 度/弧度混用 bug 在部分 fov
//      下系数为负 = 圆环不可点）。修复 = Update 每帧驱动 offsetScale
//      补偿距离, 使 generalLens 恒为 0.3m 世界半径（固定大小, 与移动
//      轴一致的语义, 符号同时修正恒可点）。仅 changer 打开时驱动。
//   追加1: 场景切换全量列表 —— GetAllDatas(true) 只返回「已解锁」
//      部分（DLC/兼容舞台不全）→ 改 GetAllDatas(false) 全表。
//   追加2: 解锁所有性癖 —— NextSkill_Prefix 对在场全部女仆幂等添加
//      Propensity.GetAllDatas(true) 全部已启用的性癖（AddPropensity
//      自带 IsEnabled 门控与去重）。非 F7 功能, 恒生效。
//   更改1: 感性默认值 10 → 300 锁死 —— NextSkill 重置 300 + Update
//      每帧维持（防绝顶后/estrus 清零等中途下降）。
//   调整1: 姿势一览/场景切换列表打开时锁定游戏相机输入（拖动/滚轮
//      不再转动/缩放视角; IMGUI 菜单滚轮不受影响）。只恢复自己锁的
//      （POV 的锁定由 POV 退出路径负责）。
//   调整2: F7 排版回退两行式（标签行+滑条行, 用户认可 v1.7.x 设计;
//      v1.8.0 的行内布局撤销）, 保留合并区块/重载按钮/版本号。
//
// v1.8.2 修正要件（2026-09-22 第六批, 用户实测 v1.8.1 后验收）:
//   调整1: 感性回退 v1.8.0 设计 —— v1.8.1 的「300 锁死 + Update 维持」
//      撤销, 恢复「NextSkill 重置 10, 中途可自然变化」。
//   修复1【空场景】調教地下室2 以下的部分舞台条目 prefab 资源缺失
//      （表内有效但 BgMgr.ChangeBg 加载失败 = 空场景）。BuildStageList
//      按当前时段 prefabName 预检（与 ChangeBg 加载链同源: HasAssetBundle
//      / Resources.Load("BG/") / ("BG/2_0/"))过滤无效条目。
//   追加1【全场景】纯背景全量列表 —— GameUty.BgFiles 是全部已安装
//      .asset_bg 资源索引（含 Live/餐厅/日常等游戏内全部背景）。
//      F7 场景列表追加「其他背景（全部资源）」区（排除夜伽舞台已用
//      prefab 防重复）, 点击 = StageSwitchBg（ChangeBg + 物理重置 +
//      相机默认取景 + 道具补挂, 与 StageSwitch 后半段同构）。
//   调整2【输入粒度】v1.8.1 的 inputEnabled=false 把右键旋转视角也
//      锁了（用户不可接受）。撤销整体锁定, 改 P44: UltimateOrbitCamera
//      .Update prefix —— 官方机制 UICamera.fallThrough→OnHover→
//      m_bFallThrough（鼠标不在 NGUI UI 上时相机响应输入）; IMGUI 菜单
//      不在 NGUI 体系 → 穿透。修复: 鼠标位于我们 IMGUI 窗口 rect 内
//      （OnGUI 帧内更新 s_imGuiMouseOver）时反射置 m_bFallThrough=false
//      → 滚轮缩放/左右键拖动/中键平移全不穿透; 菜单外一切照常
//      （右键旋转视角保留）。
//
// v1.8.3 修正要件（2026-09-22 第七批, 用户实测 v1.8.2 后验收）:
//   修复1【帧数】v1.8.2 的「其他背景」列表两个掉帧源 ——
//      ① 数据源用 GameUty.BgFiles（全部 .asset_bg 资源索引）: 条目
//         数百上千, 且 OnGUI 每帧全量 GUILayout.Button 循环渲染 =
//         打开列表即掉帧（与 v1.7.3 前姿势一览相同的问题, 用户实测
//         「优化又犯了和之前一样的问题」）。修复 = 列表虚拟化渲染
//         （固定行高 BrowserRowH + 只绘制可视区, 同姿势一览模式）。
//      ② StagePrefabExists 预检内的 Resources.Load 会把 prefab 真实
//         加载进内存（逐条预检 = 资源堆积）。修复 = 只查
//         GameUty.BgFiles 索引（HasAssetBundle, 纯字典查询）。
//   修复2【OBJ混入】「你为什么把OBJ加进来了」—— BgFiles 全量索引把
//      道具/物件资源（Odogu 等）也列进了「其他背景」。修复 = 数据源
//      改为官方摄影背景表 PhotoBGData（phot_bg_list.nei + 启用列表）:
//      只有真正的官方场景背景（Live/餐厅/剧场等, 与摄影模式可选
//      背景一致）, 无 OBJ; name = 官方日文显示名（不再是资源名）。
//   修复3【重载回到其他地图】「在其他地图使用重载会回到其他的地图
//      或者原先进入的地图」—— 纯背景（Live 等）不经 SelectedStage,
//      官方 NextSkill 每次换姿势/重载都 ChangeBG(SelectedStage) 把
//      场景拉回夜伽舞台。修复 = P45: BgMgr.ChangeBg prefix 在 Play
//      画面内把传入名重定向到 s_bgOverride（纯背景保持）;
//      StageSwitch 切回夜伽舞台 / 会话结束时清空。
//   修复4【未解锁舞台】SelectStage 内 `IsEnabled ? data : null` ——
//      未解锁舞台 stageData 置空 → ChangeBG no-op（点了没反应 +
//      SelectedStage 悬空影响后续重载）。修复 = P46: SelectStage
//      postfix 检测到 stageData 为空时用反射强制覆盖 SelectedStage
//      （private setter）为完整 pack（插件本意=全舞台解禁）。
//
// v1.8.4 追加要件（2026-09-22 第八批, 用户指定）: 多言語対応。
//   追加1: 言語切替機能 —— F7メニュー先頭に「言語」行（简体中文 /
//           English / 日本語 の3ボタン, 現在言語に ● マーカー）+
//           F1(cfg) の UiLanguage 項目（AcceptableValueList で
//           ドロップダウン選択）。切替は即時反映（同一フレーム内で
//           以降の描画が新言語になる）, cfg へ即保存（再起動後も保持）。
//   追加1.1: 全 UI 文本の三語翻訳（简体中文 / English / 日本語）——
//           ・F7 メイン窓: タイトル/パラメータ/速度/POV/機能/姿勢と
//             場景の全ラベル・トグル・ボタン（約30キー）
//           ・姿勢一覧窓: タイトル/検索/件数/閉じる
//           ・場景リスト: 「その他背景」区切りラベル
//           ・【お願い】フェーズ名（温存/律動/加速/突撃/緩和）
//           ・F1 設定項目の説明文 30 件（cfg 説明は Awake で
//             Bind するため, 保存済み言語で生成 = 変更後は次回
//             起動で反映, F7 行にもその旨を明記）
//           実装 = 埋め込みテーブル（key → {zh,en,ja}）+ L()/LF()
//           参照。外部ファイル不要（DLL 単体で完結）。
//           既存の中国語 UI は「zh-CN」列へそのまま移設（既定
//           言語 = zh-CN なので従来の見た目・文字列は不変）。
//   注: 表示は IMGUI 動的フォント（OS フォールバック）なので
//       日本語かな/漢字・簡体字のいずれも追加フォント不要。
//
// v1.8.5 追加要件（2026-09-22 第九批, ユーザー指定）: 多言語対応の完成。
//   追加2: F1(cfg) の説明文を「言語切替で即時反映」へ ——
//          v1.8.4 は Bind 時の言語で固定（次回起動で反映）だったが,
//          全 cfg 項目の ConfigDescription を反射で貼り替え
//          （<Description>k__BackingField, AcceptableValues/Tags は
//          維持）→ ConfigurationManager の公開 API
//          BuildSettingList() を呼び直して F1 の一覧を再構築
//          （SettingEntryBase.Description は生成時コピーなので
//          再構築が必須）→ cfg も Save()（コメントも選択言語へ）。
//          ConfigurationManager は GUID 直参照せずプラグイン一覧から
//          「BuildSettingList を持つ型」を探す（未導入環境でも無害）。
//   追加2b: F1 内で UiLanguage を変更した場合の反映漏れ修正 ——
//          切替は v1.8.4 では F7 ボタン経由のみ対応だったため,
//          ConfigurationManager のドロップダウンで変更すると F1 の
//          説明文も F7 の UI も旧言語のままだった。
//          修正 = cfgUiLanguage.SettingChanged を購読し, 変更を検出
//          したらフラグを立てて次フレームの Update で反映（変更は
//          ConfigurationManager の OnGUI 中に起きるため, その場で
//          一覧を作り直すと描画中のリストを壊す）。
//   追加3: F7 言語欄を「クリックで開く折り畳み」へ（v1.8.4 は常時
//          3ボタン展開）。ヘッダ行 = ▶/▼ + 現在言語のネイティブ名,
//          展開時のみ 简体中文 / English / 日本語 を表示。
//   追加4: 名称の翻訳（姿勢一覧・場景切替）——
//          a) 名称テーブル（原文 → {zh, en}）を埋め込み,
//             言語が ja 以外のとき表示名を差し替え（ja=原文）。
//          b) 姿勢一覧の検索は「原文 + zh + en」の全表記に一致
//             （訳が分からなくても日本語・中文・英語で引ける）。
//          c) 対象 = 姿勢名（Skill.Data.name）/ 夜伽舞台名
//             （YotogiStage.Data.drawName）/ 摄影背景名
//             （PhotoBGData.name）。
//          d) 名称リストの自動エクスポート: 起動後, ファイルが無ければ
//             BepInEx\config\YotogiUnlimited.name_list.txt へ
//             全名称（stage/background/skill）を UTF-8 で書き出す
//             （翻訳表の更新用。F7 §機能 のボタンで再出力も可）。
//             ゲーム側 .nei は独自バイナリで外部抽出不可のため,
//             名称一覧は実行時列挙が唯一の入手経路（ユーザー指定）。
//
// パッチ構成: P1..P21 + P22/P23(v1.3.2) + P24(Debug.LogError)
//             + P25(MotionScript null安全化)
//             + P26(Suspend旧モード守卫)
//             + P31(GetMaidStatus null安全化)
//             + P32(CallNormalFile prefix捕获+postfix挂载/还原+finalizer)
//             + P33(语音数据三入口null防护)
//             + P34(talk/voice标签五入口null防护)
//             + P35(SubCharaSelect OnFadeEnd回Play重定向)
//             + P36(GetPlayerSelectNum精确化)
//             + P37(中途入会女仆本地位姿清零)
//             + P38(@YotogiCall元技能链确定性落位)
//             + P39(@ReStart元技能链确定性落位)
//             + P40(LoadMotionScript 动作加载诊断日志)
//             + P41(MotionKagManager.LoadScriptFile 应用对象诊断日志)
//             + P42(TJSFuncGetYotogiSelectStageName FT窗口官方舞台覆盖)
//             + P43(OnFinish 归零前位姿保持)
//             + P44(UltimateOrbitCamera.Update IMGUI菜单输入穿透屏蔽, v1.8.2)
//             + P45(BgMgr.ChangeBg 纯背景保持重定向, v1.8.3)
//             + P46(YotogiStageSelectManager.SelectStage 未解锁舞台修正, v1.8.3)
//             = 50 patches（v1.8.3; P30v2 的 postfix 计入 P32 非独立补丁）
//             （P3 は isYotogiPlayable 両オーバーロード、P24 は LogError
//              両オーバーロード、P33 は 3入口、P34 は 5入口へそれぞれ
//              適用されるため実計 50）
//
// 版互換の注意（旧 Mono / CLR 2.0。project_memory 参照）:
//   - Type/MethodInfo 等の null 判定は object.ReferenceEquals を使う
//     (== は .NET4 専用 op_Equality にコンパイルされゲーム内JIT即死)
//   - UnityEngine.Object の == は Unity 演算子なので安全
//   - 純粋 C# 5 構文のみ（Add-Type/CodeDom コンパイル）
//   - private/static メソッドは GetMethod(name, Type[]) では取れない
//     → BindingFlags 付きオーバーロードで解決してから PatchDirect
//   - TJSScript/DllBase は Assembly-CSharp-firstpass にあるため
//     ビルド参照に firstpass が必須（v1.3.1から）
// =====================================================================
namespace COM3D2YotogiUnlimited
{
    [BepInPlugin("org.com3d2.yotogiunlimited", "COM3D2 YotogiUnlimited", PluginVersion)]
    public class YotogiUnlimitedPlugin : BaseUnityPlugin
    {
        // v1.8.4: バージョン表記の一元化（BepInPlugin/F7タイトル共用）
        private const string PluginVersion = "1.8.5";

        // ---------------- config (v1.2.0) ----------------
        private static ConfigEntry<bool> cfgAnyStage;       // 内容1a
        private static ConfigEntry<bool> cfgUnlockStages;   // 内容1b
        private static ConfigEntry<bool> cfgUnlimitedReuse;  // 内容2
        private static ConfigEntry<bool> cfgMixConditions;  // 内容3
        private static ConfigEntry<bool> cfgMultiPlayer;    // 内容4a
        private static ConfigEntry<bool> cfgUnlockNTR;      // 内容4b
        private static ConfigEntry<bool> cfgIgnoreAll;      // 内容5+6
        private static ConfigEntry<bool> cfgInfiniteMind;   // 内容7
        private static ConfigEntry<bool> cfgSliders;        // 内容8
        private static ConfigEntry<KeyCode> cfgSliderKey;   // 内容8
        // ---------------- config (v1.3.0) ----------------
        private static ConfigEntry<bool> cfgForbidAutoClimax; // 内容9
        private static ConfigEntry<bool> cfgOnegai;           // 内容12
        private static ConfigEntry<bool> cfgMenuAutoOpen;     // 内容13
        // ---------------- config (v1.3.1/v1.4.0) ----------------
        private static ConfigEntry<bool> cfgSafeScript;       // 内容14/16/21
        private static ConfigEntry<bool> cfgDedup;            // 内容20c
        // ---------------- config (v1.5.0/v1.6.0) ----------------
        private static ConfigEntry<KeyCode> cfgPovKey;        // 内容26: POV入/切トグル键(主人公視点)
        private static ConfigEntry<float> cfgPovFov;          // 内容26: 視野角(20..110)
        private static ConfigEntry<float> cfgPovForward;     // 内容26: 目から前方オフセット
        private static ConfigEntry<float> cfgPovUp;          // 内容26: 目線の上下オフセット
        private static ConfigEntry<float> cfgPovSmooth;      // 内容26: 位置平滑度
        private static ConfigEntry<float> cfgPovSens;        // 内容26: 右ドラッグ感度
        private static ConfigEntry<float> cfgPovNear;        // 内容26: nearClip
        private static ConfigEntry<bool> cfgLookHead;        // 内容28(v1.6.0): 女仆头部朝向镜头(夜伽Play)
        private static ConfigEntry<bool> cfgLookEye;         // 内容29(v1.6.0): 女仆眼睛朝向镜头(夜伽Play)
        private static ConfigEntry<bool> cfgLookHeadGlobal;  // 内容28b(v1.6.2): 头朝镜头-全局(全场景)
        private static ConfigEntry<bool> cfgLookEyeGlobal;   // 内容29b(v1.6.2): 眼朝镜头-全局(全场景)
        // ---------------- config (v1.7.0) ----------------
        private static ConfigEntry<bool> cfgUiHide;          // 内容35: 热键隐藏UI 开关(默认开)
        private static ConfigEntry<KeyCode> cfgUiHideKey;    // 内容35: 隐藏UI热键(默认Tab)
        // ---------------- config (v1.7.1) ----------------
        private static ConfigEntry<bool> cfgPoseResource;     // 内容37: 姿势资源/道具自动加载(默认开)
        // ---------------- v1.8.4: 多言語対応 ----------------
        private static ConfigEntry<string> cfgUiLanguage;      // UI言語 (zh-CN / en / ja)
        private static int s_lang;                             // 0=zh-CN, 1=en, 2=ja
        private const int LangZh = 0;
        private const int LangEn = 1;
        private const int LangJa = 2;
        private static Dictionary<string, string[]> s_langIndex; // key → {zh,en,ja}（初回遅延構築）
        // ---- v1.8.5: F1 説明文のライブ更新 / 言語欄の折り畳み ----
        private static bool s_langOpen;                        // F7 言語欄の展開状態
        private static bool s_langRefreshPending;              // F1 側で言語変更 → Update で遅延反映
        private static List<ConfigEntryBase> s_langEntries;    // 説明文を貼り替える cfg 項目
        private static List<string> s_langEntryKeys;           // 各項目の翻訳キー
        private static ConfigFile s_cfgFile;                   // cfg 保存用（言語切替時に Save）
        // ---- v1.8.5: 名称翻訳（姿勢/舞台/背景）+ 名称リスト出力 ----
        private static Dictionary<string, string[]> s_nameIndex; // 原文 → {zh,en}（埋め込み・遅延構築）
        private static Dictionary<string, string[]> s_nameExt;   // 原文 → {zh,en}（外部ファイル・優先）
        private static bool s_nameExtLoaded;                     // 外部ファイル読込済み
        private static long s_nameExtStampTicks = -1;            // 自動再読込用（更新時刻）
        private static float s_nameExtCheckTime = -10f;          // 自動再読込のスロットル
        private static Dictionary<string, string> s_nameCache;   // 原文 → 表示名（言語別キャッシュ）
        private static int s_nameCacheLang = -1;                 // キャッシュ生成時の言語
        private static bool s_nameExportTried;                   // 起動時1回の自動出力を試行済み
        private const string NameListFile = "YotogiUnlimited.name_i18n.txt";
        private static ManualLogSource log;

        // ---------------- cached reflection (v1.2.0) ----------------
        private static FieldInfo fiSkillGroupParent;      // YotogiPlayManager.skill_group_parent_
        private static FieldInfo fiPlayingSkillNo;        // YotogiPlayManager.playing_skill_no_
        private static FieldInfo fiSkillIconDefaultColor; // YotogiPlayManager.skill_icon_default_color_
        private static MethodInfo miOnNextSkillMove;      // YotogiPlayManager.OnNextSkillMove()
        private static FieldInfo fiContainerMgr;         // YotogiSkillSelectManager.skill_container_mgr_
        private static FieldInfo fiParamBasicBar;        // YotogiPlayManager.param_basic_bar_
        private static FieldInfo fiParamData;            // YotogiSkillUnit.param_data_
        // P25(v1.6.4): MotionKagManager 主角字段（null安全预解析用）
        private static FieldInfo fiMotionMainMaid;       // MotionKagManager.main_maid_
        private static FieldInfo fiMotionMainMan;        // MotionKagManager.main_man_
        private static FieldInfo fiMotionActiveMaids;    // MotionKagManager.ActiveMaidList (P41 诊断)
        private static FieldInfo fiMotionActiveMen;      // MotionKagManager.ActiveManList (P41 诊断)
        // P26(v1.6.6): Suspend 旧模式守卫（黑屏根治）
        private static PropertyInfo piIsNewYotogiMode;   // YotogiPlayManager.is_new_yotogi_mode
        private static MethodInfo miOnClickCommand;      // YotogiPlayManager.OnClickCommand(Data)
        private static bool s_suspendReentry;            // 代行重入护栏（防病理递归）
        // P35/P36(v1.7.3): 官方追加女仆选择介入
        private static FieldInfo fiSubCharaSelectMgr;    // YotogiManager.sub_chara_select_mgr_
        // P39(v1.7.5): @ReStart 守卫复刻（中断恢复态检查）
        private static FieldInfo fiSuspendStore;         // YotogiPlayManager.suspendStoreData
        // v1.7.8 追加2: 官方夜伽位置调整 UI（入口按钮被 X001 门控隐藏）
        private static FieldInfo fiPositionChanger;      // YotogiPlayManager.positionChanger
        // v1.8.1: 旋转 gizmo 固定大小 —— changer 引用 + GizmoObject 反射
        private static YotogiPositionChanger s_posChanger;
        private static FieldInfo fiPosChangerGizmo;      // YotogiPositionChanger.m_GizmoControlObject
        private const float PosChangerGizmoLens = 0.3f;  // 旋转圆恒定世界半径(米)
        // v1.8.2 (P44): IMGUI 菜单鼠标悬停标志（OnGUI 帧内更新）+
        // UltimateOrbitCamera.m_bFallThrough 反射
        private static bool s_imGuiMouseOver;
        private static FieldInfo fiOrbitFallThrough;  // UltimateOrbitCamera.m_bFallThrough

        // P5/P6 one-shot flag
        private static bool s_forceSkillZero;
        // P7(v1.4.0) 同一フレーム内マージ結果メモ（OnCall の5連呼出を1スキャン化。
        // フレームを跨いだら必ず再構築＝v1.2.1の長命キャッシュ障害は構造的に不可能）
        private static int s_mergeFrame = -1;
        private static MaidStatus.Status s_mergeStatus;
        private static bool s_mergeIgnoreAll;
        private static Dictionary<int, YotogiSkillListManager.Data> s_mergeCache;
        // P18 shared dummy
        private static MaidStatus.YotogiSkillData s_dummyParam;
        // P22/P23(v1.3.2) 欠損報告済みリスト（同一ファイル/メッセージの重複ログ防止）
        private static HashSet<string> s_reportedMissing = new HashSet<string>();
        // P22(v1.3.2) 性格プレフィックス一覧（初回欠損時に遅延構築）
        private static string[] s_personalPrefixes;

        // ---- 内容8/13: メニュー状態 ----
        private bool slidersVisible = true;
        private Rect sliderWinRect = new Rect(30f, 130f, 336f, 400f);
        private const int sliderWinId = 0x1237A55;

        // ---- 内容11/12: 速度制御状態 ----
        private static float s_speedMul = 1f;             // 速度倍率 (0.10..5.00)
        private static List<Animation> s_speedAnims = new List<Animation>();
        private static float s_speedCacheTime = -10f;
        private static bool s_speedWasApplied;
        private const float SpeedMin = 0.1f;
        private const float SpeedMax = 5f;                // 内容17: v1.3.0のX5モードへ回帰
        private const float OnegaiAutoMax = 1.30f;       // 内容22(v1.4.1): お願い自動リズム専用の上限

        // ---- 内容26(v1.6.1): POV状態（onPreCullがstaticのため全static）----
        //       第一人=主人公(男)固定。以降 V で在场全員を巡回、一周で退出。
        private static bool s_povActive;
        private static Maid s_povTarget;                   // 現在のPOV対象
        private static List<Maid> s_povList = new List<Maid>(); // 巡回列表(主人公先頭)
        // v1.7.6: POV 相机旋转 = 四元数自由相机（替代 Euler yaw/pitch 存储）。
        // 拖拽 = 世界Y轴水平旋转（保持 v1.6.5 批准的「纯水平」手感）
        // + 相机自身右轴俯仰（恒为屏幕水平轴, 任何状态上下都不反向）;
        // 俯仰钳 ±89.9°（PovClampPitch 四元数重建）——永不越过垂直,
        // 结构上无横滚/无颠倒/无反向区, 也没有任何自动旋转
        // （v1.7.5 的 PovAutoLevel 滚转回正已被撤销: 用户头晕）。
        private static Quaternion s_povRot = Quaternion.identity;
        private static Vector3 s_povSmoothPos;
        private static bool s_povSmoothInit;
        private static float s_povOrigFov = 30f;
        private static float s_povOrigNear = 0.3f;
        private static bool s_povOrigControl;
        private static bool s_povCamSaved;
        // v1.6.3: 退出时相机位姿复原用
        private static Vector3 s_povOrigPos;
        private static Quaternion s_povOrigRot = Quaternion.identity;
        // 頭部非表示レンダラー（元のenabled値を保持・纯ランタイム状態のみ）
        private static Dictionary<Renderer, bool> s_povHidden = new Dictionary<Renderer, bool>();
        private static float s_povNextRescan;
        // v1.7.8: PovCullSelf（自身整体剔除）已按用户指示整体移除——
        // 近裁剪面配置（PovNearClip）保留。
        // 内容39(v1.7.2): POV 隐藏脖子——颈骨 localScale 塌缩（0.01）,
        // 颈部以上（含未隐藏也会戳镜头的男体脸部）全部收进锁骨一点。
        // 原始 scale 精确保存, 退出/切换目标时复原。
        // 塌缩会把头（眼的父级骨骼）一并拉到锁骨位置 → 相机锚点
        // 改挂颈父骨（上胸）: 进入时捕获眼位在上胸系里的局部偏移,
        // 每帧 TransformPoint 复原原眼高（呼吸/前倾仍正确跟随, 头部
        // 转动完全不再带动锚点——比旧眼骨锚更稳）。
        private static Maid s_povNeckTarget;                     // 已缩颈的目标（null=未缩）
        private static Vector3 s_povNeckOrigScale = Vector3.one;
        private static Transform s_povNeckParentBone;            // 颈父骨（上胸, 塌缩不影响）
        private static Vector3 s_povEyeFromParent;              // 眼位在颈父骨系里的偏移
        private static readonly Vector3 PovNeckShrink = new Vector3(0.01f, 0.01f, 0.01f);
        // v1.7.7 (问题4): POV 目标槽位跟踪 —— 男体 CRC 换体（SwapNewManBody）
        // 会把活动槽位的 Maid 对象整个替换（旧对象移回仓库）。0.25s 追扫时
        // 发现槽位现住者≠当前目标 → 完整切换到新对象（相机/剔除/塌缩全部
        // 重锚）, 防止 POV 锚到仓库里的旧身体。仅 man 目标启用（maid 不做
        // 对象级换体; npcmaid 的槽位交换是换角色, 走 targetGone 自动退出）。
        private static int s_povTargetSlot = -1;                 // -1 = 不跟踪（maid 目标）

        // ---- 内容28/29(v1.6.0): 看镜头（LookAtCamera）状態 ----
        private static bool s_lookApplied;                  // 适用中フラグ（OFF遷移時に EyeToReset で復帰）
        private static List<Maid> s_lookEnforced = new List<Maid>(); // v1.6.2: 已强制名单（离场即复位）
        private static bool s_lookHeadOn;                   // v1.7.5: 本帧头/眼开关（含 inPlay 门控, preCull 直连通道复用）
        private static bool s_lookEyeOn;
        // 内容38(v1.7.6): POV俯仰钳制 ±89.9°——四元数自由相机+PovClampPitch
        // （无欧拉奇点/无回绕; 无自动旋转——v1.7.5 自动回正已按用户反馈撤销）

        // ---- v1.7.3 (issue 4): POV 切姿势重锚状态 ----
        private static bool s_povPoseDirty;                // NextSkill 发生（姿势切换中）
        private static float s_povSettleSince = -1f;        // 非busy持续起点（-1=未开始计时）

        // ---- 内容26(v1.6.2): POV 在场检测 ----
        private static float s_povGoneSince = -1f;          // 対象非表示の継続開始時刻(-1=在场)

        // ---- 内容35(v1.7.0): 热键隐藏UI状态 ----
        private static bool s_uiHidden;
        private static float s_uiMsgCloseNext;              // 消息窗口节流关闭的下次时刻

        // ---- 内容33(v1.7.0): 姿势一览状态 ----
        private static bool s_skillBrowserOpen;
        private static Vector2 s_skillBrowserScroll;
        private static List<Skill.Data> s_skillList = new List<Skill.Data>();
        // v1.7.3 (issue 1): 搜索过滤 + 虚拟化（独立大窗）状态
        private static string s_skillFilter = "";
        private static List<Skill.Data> s_skillFiltered = new List<Skill.Data>();
        private static bool s_skillFilteredDirty = true;
        private const int browserWinId = 0x1237A56;
        private const float BrowserRowH = 26f;
        private const float StageListH = 156f; // v1.8.3: 场景列表可视高度（虚拟化）
        private static Rect s_browserRectLast;              // 可视高度估算用（OnGUI 帧内更新）

        // ---- v1.7.3 (issue 3): 官方追加女仆选择介入状态 ----
        private static bool s_subSelectActive;             // 介入中（P35/P36 前置生效）
        private static int s_subSelectNeeded;              // 需选择的副女仆数
        private static int s_subPrevNo;                    // 介入时的当前技能索引（未选人时回落）
        // ---- v1.8.0 (追加2): 选女仆后自动热加载状态 ----
        private static bool s_replayAfterJoin;             // 选人回来后待自动重播
        private static float s_replaySettleSince = -1f;    // 全员空闲持续起点

        // ---- 内容36(v1.7.0): 场景切换状态 ----
        private static bool s_stageBrowserOpen;
        private static Vector2 s_stageBrowserScroll;
        private static List<YotogiStage.Data> s_stageList = new List<YotogiStage.Data>();
        // v1.8.2: 纯背景附加列表 + 有效性预检（prefab 缺失的空舞台被过滤）
        // v1.8.3: 数据源改为官方摄影背景表（PhotoBGData: phot_bg_list.nei
        // + 启用列表）——官方场景清单, 不含 OBJ/道具资源（Odogu 等）,
        // 覆盖 Live/餐厅/剧场等全部官方场景; name = 官方日文显示名。
        private static List<PhotoBGData> s_bgList = new List<PhotoBGData>();
        // v1.8.3 (P45): 纯背景保持 —— 用户经 F7 切到「其他背景」后,
        // 官方 NextSkill 的 ChangeBG(SelectedStage) 会把场景拉回夜伽舞台
        // （重载/换姿势时表现 = 「回到其他地图/原先地图」）。override
        // 记录当前纯背景 prefab 名, P45 把 Play 画面内的 ChangeBg 重定向
        // 到它; 切回夜伽舞台/离开夜伽会话时清空。
        private static string s_bgOverride;
        // v1.8.3 (P46): SelectedStage 反射（private setter 强制覆盖）
        private static PropertyInfo piSelectedStage;
        private static MethodInfo miSelectedStageSet;

        // ---- 内容12: 【お願い】フェーズマシン ----
        private int onegaiPhase = -1;        // -1=idle, 0=温存 1=律動 2=加速 3=突撃 4=緩和
        private float onegaiTimer;
        private float onegaiPhaseDur = 1f;
        private float onegaiBase;
        private float onegaiSeed;
        // v1.8.4: 表示名は言語切替に追従させるためキー保持（描画時に L() 参照）
        private static readonly string[] OnegaiPhaseKeys = new string[] { "ph.0", "ph.1", "ph.2", "ph.3", "ph.4" };

        // ---- 画面遷移トラッキング (内容13) ----
        private static string s_lastScreen = "";

        // ---- 内容9: 自動絶頂禁止の興奮上限 ----
        // RRC9(絶頂寸前)は興奮が275/300を「横断」または300で「維持」すると
        // 発火するため、274にクランプすれば原理的に発火不能になる。
        private const int ExciteCap = 274;

        // ---- 内容14: 欠損シナリオ差し替え用スタブ（script.arc 収録）----
        // 中身: *top ラベル → @return のみ（施設解放用の空シナリオ）。
        // yotogi_skill_call_main.ks から @call されると即座に呼び出し元へ
        // 復帰するため、*finish 到達は原版エラー経路と完全に同一。
        private const string MissingScriptStub = "fac_main_0001.ks";

        private void Awake()
        {
            log = Logger;
            s_cfgFile = Config;   // v1.8.5: 言語切替時の cfg 保存用
            // v1.8.4: 言語を最初に確定 —— 以降の cfg 説明文が
            // 保存済み言語で生成される（Bind は Awake 時の言語で固定）。
            cfgUiLanguage = Config.Bind("Yotogi", "UiLanguage", "zh-CN",
                new ConfigDescription(L("cfg.lang"), new AcceptableValueList<string>("zh-CN", "en", "ja")));
            ApplyLanguageValue(cfgUiLanguage.Value, false);
            // v1.8.5 (追加2b): F1(ConfigurationManager) 側で UiLanguage を
            // 変更した場合も反映する —— 変更は OnGUI 中に起きるため,
            // その場で設定一覧を作り直すと描画中のリストを壊す。
            // フラグだけ立てて Update（次フレーム）で反映する。
            cfgUiLanguage.SettingChanged += OnUiLanguageSettingChanged;
            cfgAnyStage = Config.Bind("Yotogi", "AnyStageForSkills", true, L("cfg.anyStage"));
            cfgUnlockStages = Config.Bind("Yotogi", "UnlockAllStages", true, L("cfg.unlockStages"));
            cfgUnlimitedReuse = Config.Bind("Yotogi", "UnlimitedSkillReuse", true, L("cfg.reuse"));
            cfgMixConditions = Config.Bind("Yotogi", "MixSpecialConditions", true, L("cfg.mix"));
            cfgMultiPlayer = Config.Bind("Yotogi", "UnlockMultiPlayer", true, L("cfg.multi"));
            cfgUnlockNTR = Config.Bind("Yotogi", "UnlockNTR", true, L("cfg.ntr"));
            cfgIgnoreAll = Config.Bind("Yotogi", "IgnoreAllConditions", true, L("cfg.ignoreAll"));
            cfgInfiniteMind = Config.Bind("Yotogi", "InfiniteMind", true, L("cfg.mind"));
            cfgSliders = Config.Bind("Yotogi", "ExciteSensualSliders", true, L("cfg.sliders"));
            cfgSliderKey = Config.Bind("Yotogi", "SliderToggleKey", KeyCode.F7, L("cfg.sliderKey"));
            cfgForbidAutoClimax = Config.Bind("Yotogi", "ForbidAutoClimax", true, L("cfg.forbidClimax"));
            cfgOnegai = Config.Bind("Yotogi", "OnegaiAutoRhythm", false, L("cfg.onegai"));
            cfgMenuAutoOpen = Config.Bind("Yotogi", "MenuAutoOpen", true, L("cfg.autoOpen"));
            cfgSafeScript = Config.Bind("Yotogi", "SafeSkipMissingScript", true, L("cfg.safeScript"));
            cfgDedup = Config.Bind("Yotogi", "DedupSkills", true, L("cfg.dedup"));
            // ---- v1.6.0（内容26改稿: 主人公POV + 内容28/29: 看镜头） ----
            cfgPovKey = Config.Bind("Yotogi", "PovToggleKey", KeyCode.V, L("cfg.povKey"));
            cfgPovFov = Config.Bind("Yotogi", "PovFieldOfView", 70f,
                new ConfigDescription(L("cfg.povFov"), new AcceptableValueRange<float>(20f, 110f)));
            cfgPovForward = Config.Bind("Yotogi", "PovForwardOffset", 0.04f,
                new ConfigDescription(L("cfg.povForward"), new AcceptableValueRange<float>(0f, 0.2f)));
            cfgPovUp = Config.Bind("Yotogi", "PovUpOffset", 0f,
                new ConfigDescription(L("cfg.povUp"), new AcceptableValueRange<float>(-0.1f, 0.1f)));
            cfgPovSmooth = Config.Bind("Yotogi", "PovSmoothing", 0.65f,
                new ConfigDescription(L("cfg.povSmooth"), new AcceptableValueRange<float>(0f, 1f)));
            cfgPovSens = Config.Bind("Yotogi", "PovMouseSensitivity", 1f,
                new ConfigDescription(L("cfg.povSens"), new AcceptableValueRange<float>(0.3f, 3f)));
            cfgPovNear = Config.Bind("Yotogi", "PovNearClip", 0.01f,
                new ConfigDescription(L("cfg.povNear"), new AcceptableValueRange<float>(0.01f, 0.1f)));
            cfgLookHead = Config.Bind("Yotogi", "LookHeadToCamera", false, L("cfg.lookHead"));
            cfgLookEye = Config.Bind("Yotogi", "LookEyeToCamera", false, L("cfg.lookEye"));
            // ---- v1.6.2: 全局看镜头（F1/ConfigurationManager 用，全场景生效） ----
            cfgLookHeadGlobal = Config.Bind("Yotogi", "LookHeadToCameraGlobal", false, L("cfg.lookHeadGlobal"));
            cfgLookEyeGlobal = Config.Bind("Yotogi", "LookEyeToCameraGlobal", false, L("cfg.lookEyeGlobal"));
            // ---- v1.7.0: 热键隐藏UI / 技能选择上限 ----
            cfgUiHide = Config.Bind("Yotogi", "UiHideEnabled", true, L("cfg.uiHide"));
            cfgUiHideKey = Config.Bind("Yotogi", "UiHideKey", KeyCode.Tab, L("cfg.uiHideKey"));
            // ---- v1.7.1: pose resource auto-mount (v1.7.2 event-driven) ----
            cfgPoseResource = Config.Bind("Yotogi", "PoseResourceEnabled", true, L("cfg.poseRes"));

            // ---- v1.8.5: F1 説明文のライブ更新用に全項目を登録 ----
            // （SetUiLanguage 時に ConfigDescription を貼り替え,
            //   ConfigurationManager の一覧を再構築する）
            RegisterLangEntry(cfgUiLanguage, "cfg.lang");
            RegisterLangEntry(cfgAnyStage, "cfg.anyStage");
            RegisterLangEntry(cfgUnlockStages, "cfg.unlockStages");
            RegisterLangEntry(cfgUnlimitedReuse, "cfg.reuse");
            RegisterLangEntry(cfgMixConditions, "cfg.mix");
            RegisterLangEntry(cfgMultiPlayer, "cfg.multi");
            RegisterLangEntry(cfgUnlockNTR, "cfg.ntr");
            RegisterLangEntry(cfgIgnoreAll, "cfg.ignoreAll");
            RegisterLangEntry(cfgInfiniteMind, "cfg.mind");
            RegisterLangEntry(cfgSliders, "cfg.sliders");
            RegisterLangEntry(cfgSliderKey, "cfg.sliderKey");
            RegisterLangEntry(cfgForbidAutoClimax, "cfg.forbidClimax");
            RegisterLangEntry(cfgOnegai, "cfg.onegai");
            RegisterLangEntry(cfgMenuAutoOpen, "cfg.autoOpen");
            RegisterLangEntry(cfgSafeScript, "cfg.safeScript");
            RegisterLangEntry(cfgDedup, "cfg.dedup");
            RegisterLangEntry(cfgPovKey, "cfg.povKey");
            RegisterLangEntry(cfgPovFov, "cfg.povFov");
            RegisterLangEntry(cfgPovForward, "cfg.povForward");
            RegisterLangEntry(cfgPovUp, "cfg.povUp");
            RegisterLangEntry(cfgPovSmooth, "cfg.povSmooth");
            RegisterLangEntry(cfgPovSens, "cfg.povSens");
            RegisterLangEntry(cfgPovNear, "cfg.povNear");
            RegisterLangEntry(cfgLookHead, "cfg.lookHead");
            RegisterLangEntry(cfgLookEye, "cfg.lookEye");
            RegisterLangEntry(cfgLookHeadGlobal, "cfg.lookHeadGlobal");
            RegisterLangEntry(cfgLookEyeGlobal, "cfg.lookEyeGlobal");
            RegisterLangEntry(cfgUiHide, "cfg.uiHide");
            RegisterLangEntry(cfgUiHideKey, "cfg.uiHideKey");
            RegisterLangEntry(cfgPoseResource, "cfg.poseRes");
            // 保存済み言語で全説明文を確定（言語項目だけはバインド時に
            // 既定言語で生成されるため, ここで貼り直す）
            RefreshLangEntryDescriptions();

            Type tPlay = typeof(YotogiPlayManager);
            fiSkillGroupParent = tPlay.GetField("skill_group_parent_", BindingFlags.NonPublic | BindingFlags.Instance);
            fiPlayingSkillNo = tPlay.GetField("playing_skill_no_", BindingFlags.NonPublic | BindingFlags.Instance);
            fiSkillIconDefaultColor = tPlay.GetField("skill_icon_default_color_", BindingFlags.NonPublic | BindingFlags.Instance);
            miOnNextSkillMove = tPlay.GetMethod("OnNextSkillMove", BindingFlags.NonPublic | BindingFlags.Instance);
            fiParamBasicBar = tPlay.GetField("param_basic_bar_", BindingFlags.NonPublic | BindingFlags.Instance);
            fiContainerMgr = typeof(YotogiSkillSelectManager).GetField("skill_container_mgr_", BindingFlags.NonPublic | BindingFlags.Instance);
            fiParamData = typeof(YotogiSkillUnit).GetField("param_data_", BindingFlags.NonPublic | BindingFlags.Instance);
            // P25(v1.6.4): MotionKagManager 主角字段
            fiMotionMainMaid = typeof(MotionKagManager).GetField("main_maid_", BindingFlags.NonPublic | BindingFlags.Instance);
            fiMotionMainMan = typeof(MotionKagManager).GetField("main_man_", BindingFlags.NonPublic | BindingFlags.Instance);
            // P41(v1.7.6): MotionKagManager 应用名单字段（诊断日志用）
            fiMotionActiveMaids = typeof(MotionKagManager).GetField("ActiveMaidList", BindingFlags.NonPublic | BindingFlags.Instance);
            fiMotionActiveMen = typeof(MotionKagManager).GetField("ActiveManList", BindingFlags.NonPublic | BindingFlags.Instance);
            // P26(v1.6.6): Suspend 旧模式守卫（is_new_yotogi_mode 判定 + 命令代行重入）
            piIsNewYotogiMode = tPlay.GetProperty("is_new_yotogi_mode", BindingFlags.NonPublic | BindingFlags.Instance);
            miOnClickCommand = tPlay.GetMethod("OnClickCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            // P35/P36(v1.7.3): 官方追加女仆选择介入
            fiSubCharaSelectMgr = typeof(YotogiManager).GetField("sub_chara_select_mgr_",
                BindingFlags.NonPublic | BindingFlags.Instance);
            // P39(v1.7.5): @ReStart 守卫复刻
            fiSuspendStore = tPlay.GetField("suspendStoreData",
                BindingFlags.NonPublic | BindingFlags.Instance);
            // v1.7.8 追加2: 官方夜伽位置调整 UI（YotogiPositionChanger,
            // 入口按钮被 PluginData X001 门控隐藏——F7 直接调用打开）
            fiPositionChanger = tPlay.GetField("positionChanger",
                BindingFlags.NonPublic | BindingFlags.Instance);
            // v1.8.1: 旋转 gizmo 固定大小（m_GizmoControlObject 反射）
            fiPosChangerGizmo = typeof(YotogiPositionChanger).GetField("m_GizmoControlObject",
                BindingFlags.NonPublic | BindingFlags.Instance);
            // v1.8.2 (P44): UltimateOrbitCamera 穿透标志（IMGUI 菜单屏蔽）
            fiOrbitFallThrough = typeof(UltimateOrbitCamera).GetField("m_bFallThrough",
                BindingFlags.NonPublic | BindingFlags.Instance);
            // v1.8.3 (P46): SelectedStage 反射（private setter, 未解锁舞台修正）
            piSelectedStage = typeof(YotogiStageSelectManager).GetProperty("SelectedStage",
                BindingFlags.Public | BindingFlags.Static);
            if (!object.ReferenceEquals(piSelectedStage, null))
            {
                miSelectedStageSet = piSelectedStage.GetSetMethod(true);
            }

            Harmony h = new Harmony("org.com3d2.yotogiunlimited");
            Type self = typeof(YotogiUnlimitedPlugin);
            int ok = 0;
            int miss = 0;

            // ---- P1/P2: IsExecStage -> true ----
            ok += PatchExact(h, self, "SkillStage_Prefix", null, typeof(Skill.Data), "IsExecStage",
                new Type[] { typeof(YotogiStage.Data) }, ref miss);
            ok += PatchExact(h, self, "SkillStage_Prefix", null, typeof(Skill.Old.Data), "IsExecStage",
                new Type[] { typeof(YotogiOld.Stage) }, ref miss);

            // ---- P3: isYotogiPlayable 两个重载全部 -> true ----
            MethodInfo[] stageMethods = typeof(YotogiStage.Data).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            for (int i = 0; i < stageMethods.Length; i++)
            {
                if (stageMethods[i].Name != "isYotogiPlayable")
                {
                    continue;
                }
                ok += PatchDirect(h, self, "StagePlayable_Prefix", null, stageMethods[i], ref miss);
            }

            // ---- P4: UpdateSkillTower postfix ----
            ok += PatchExact(h, self, null, "Tower_Postfix", typeof(YotogiPlayManager), "UpdateSkillTower",
                null, ref miss);

            // ---- P5: OnSkillIconClick prefix ----
            ok += PatchExact(h, self, "IconClick_Prefix", null, typeof(YotogiPlayManager), "OnSkillIconClick",
                null, ref miss);

            // ---- P6: NextSkill prefix ----
            ok += PatchExact(h, self, "NextSkill_Prefix", null, typeof(YotogiPlayManager), "NextSkill",
                null, ref miss);

            // ---- P43 (v1.8.0): OnFinish prefix —— 归零前位姿保持 ----
            ok += PatchExact(h, self, "OnFinish_HoldPrefix", null, typeof(YotogiPlayManager), "OnFinish",
                null, ref miss);

            // ---- P44 (v1.8.2): UltimateOrbitCamera.Update prefix —— IMGUI 菜单输入穿透屏蔽 ----
            ok += PatchExact(h, self, "OrbitUpdate_Prefix", null, typeof(UltimateOrbitCamera), "Update",
                null, ref miss);

            // ---- P45 (v1.8.3): BgMgr.ChangeBg prefix —— 纯背景保持重定向 ----
            ok += PatchExact(h, self, "ChangeBg_Override_Prefix", null, typeof(BgMgr), "ChangeBg",
                new Type[] { typeof(string) }, ref miss);

            // ---- P46 (v1.8.3): SelectStage postfix —— 未解锁舞台 stageData 修正 ----
            ok += PatchExact(h, self, null, "SelectStage_Fix_Postfix", typeof(YotogiStageSelectManager), "SelectStage",
                new Type[] { typeof(YotogiStage.Data), typeof(YotogiAdaptMyRoomStage.Data), typeof(bool) }, ref miss);

            // ---- P7: CreateDatas prefix ----
            ok += PatchExact(h, self, "CreateDatas_Prefix", null, typeof(YotogiSkillListManager), "CreateDatas",
                new Type[] { typeof(MaidStatus.Status), typeof(bool), typeof(Skill.Data.SpecialConditionType) }, ref miss);

            // ---- P8: CreateSkillButtons pre+post ----
            ok += PatchExact(h, self, "SaveSel_Prefix", "RestoreSel_Postfix", typeof(YotogiSkillSelectManager), "CreateSkillButtons",
                new Type[] { typeof(Skill.Data.SpecialConditionType) }, ref miss);

            // ---- P9: GetPlayPossibleMaidCount prefix ----
            ok += PatchExact(h, self, "PossibleMaidCount_Prefix", null, typeof(YotogiManager), "GetPlayPossibleMaidCount",
                null, ref miss);

            // ---- P10: GetSubMaidList prefix ----
            ok += PatchExact(h, self, "SubMaidList_Prefix", null, typeof(YotogiManager), "GetSubMaidList",
                null, ref miss);

            // ---- P11: get_lockNTRPlay prefix -> false ----
            ok += PatchExact(h, self, "LockNTR_Prefix", null, typeof(PlayerStatus.Status), "get_lockNTRPlay",
                null, ref miss);

            // ---- P12~P15: 四个 Exec 硬过滤 -> true ----
            ok += PatchExact(h, self, "ExecAlways_Prefix", null, typeof(Skill.Data), "IsExecSeikeiken",
                new Type[] { typeof(MaidStatus.Seikeiken) }, ref miss);
            ok += PatchExact(h, self, "ExecAlways_Prefix", null, typeof(Skill.Data), "IsExecRelation",
                new Type[] { typeof(MaidStatus.Relation) }, ref miss);
            ok += PatchExact(h, self, "ExecAlways_Prefix", null, typeof(Skill.Data), "IsExecContract",
                new Type[] { typeof(MaidStatus.Contract) }, ref miss);
            ok += PatchExact(h, self, "ExecAlways_Prefix", null, typeof(Skill.Data), "IsExecPersonal",
                new Type[] { typeof(MaidStatus.Personal.Data) }, ref miss);

            // ---- P16: isFutureLearnPossible -> true ----
            ok += PatchExact(h, self, "ExecAlways_Prefix", null, typeof(MaidStatus.CsvData.AbstractClassData.LearnConditions),
                "isFutureLearnPossible", new Type[] { typeof(MaidStatus.Status) }, ref miss);

            // ---- P17: PersonalEventBlocker.IsEnabledYotodiSkill -> true ----
            ok += PatchExact(h, self, "ExecAlways_Prefix", null, typeof(MaidStatus.PersonalEventBlocker),
                "IsEnabledYotodiSkill", new Type[] { typeof(MaidStatus.Personal.Data), typeof(int) }, ref miss);

            // ---- P18: 未習得技能 getter dummy ----
            ok += PatchExact(h, self, "SkillParam_Prefix", null, typeof(YotogiSkillUnit), "get_skill_param_data",
                null, ref miss);

            // ---- P19: 选择预算 max(orig,9999) ----
            ok += PatchExact(h, self, "SelectHp_Prefix", null, typeof(YotogiManager), "get_skill_select_max_hp",
                null, ref miss);

            // ---- P20: 未習得 dummy Lv3 ----
            ok += PatchExact(h, self, "PairCreate_Prefix", null, typeof(Yotogi.SkillDataPair), "Create",
                new Type[] { typeof(Maid), typeof(Skill.Data) }, ref miss);

            // ---- P21 (v1.3.0): ApplyExecCommandStatus postfix（内容9）----
            ok += PatchExact(h, self, null, "ExecStatus_Postfix", typeof(YotogiPlayManager), "ApplyExecCommandStatus",
                new Type[] { typeof(Maid), typeof(Skill.Data.Command.Data) }, ref miss);

            // ---- P22 (v1.3.2): ReplaceFileNameCallBack postfix（内容16）----
            // private string ReplaceFileNameCallBack(string file_name)
            // KAGエンジンの全シナリオ読込に必ず通る managed コールバック
            // （SetReplaceEvent で登録される ? / ?0..?4 性格置換処理）。
            // private instance のため BindingFlags で解決。
            MethodInfo miReplaceFileName = typeof(BaseKagManager).GetMethod("ReplaceFileNameCallBack",
                BindingFlags.NonPublic | BindingFlags.Instance);
            ok += PatchDirect(h, self, null, "FileNameReplace_Postfix", miReplaceFileName, ref miss);

            // ---- P23 (v1.3.2): OnNativeConsolLog prefix（内容16）----
            // private static void OnNativeConsolLog(IntPtr utf8_native_string, bool exception)
            // 原生KAG/TJSエラーの唯一の managed 出力チャネル
            // （→ Debug.LogError）。firstpass の TJSScript にあるため
            // typeof(TJSScript) で直接参照（ビルド参照に firstpass 必須）。
            MethodInfo miConsolLog = typeof(TJSScript).GetMethod("OnNativeConsolLog",
                BindingFlags.NonPublic | BindingFlags.Static);
            ok += PatchDirect(h, self, "ConsolLog_Filter", null, miConsolLog, ref miss);

            // ---- P24 (v1.4.0): Debug.LogError prefix（内容21）----
            // UnityEngine.Debug.LogError の両オーバーロード (object) /
            // (object, UnityEngine.Object)。prefix は名前注入で message
            // のみ受け取る（両方に共通適用可）。夜伽中の欠損OBJ/未検出
            // 系メッセージを Info 1回表示へ格下げする。
            MethodInfo[] logErrorMethods = typeof(Debug).GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < logErrorMethods.Length; i++)
            {
                if (logErrorMethods[i].Name != "LogError")
                {
                    continue;
                }
                if (logErrorMethods[i].GetParameters().Length < 1)
                {
                    continue;
                }
                ok += PatchDirect(h, self, "LogError_Filter", null, logErrorMethods[i], ref miss);
            }

            // ---- P25 (v1.6.4): MotionKagManager.LoadScriptFile null安全化 ----
            // 官方边界缺陷: guid 未指定时的回退搜索循环对 GetMaid/GetMan 的
            // null（越界/空槽）直接 .Visible → ADV 動作脚本 NPE 中断。
            // prefix = 主角字段为 null 时 null 安全预解析；
            // finalizer = 残余 NPE 吞掉（Warning 单条，ADV 继续）。
            MethodInfo miMotionLoad = typeof(MotionKagManager).GetMethod("LoadScriptFile");
            MethodInfo miMotionPrefix = self.GetMethod("MotionLoad_Prefix", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo miMotionFinal = self.GetMethod("MotionLoad_Finalizer", BindingFlags.NonPublic | BindingFlags.Static);
            if (!object.ReferenceEquals(miMotionLoad, null)
                && !object.ReferenceEquals(miMotionPrefix, null)
                && !object.ReferenceEquals(miMotionFinal, null))
            {
                h.Patch(miMotionLoad, prefix: new HarmonyMethod(miMotionPrefix), finalizer: new HarmonyMethod(miMotionFinal));
                ok++;
            }
            else
            {
                miss++;
                log.LogWarning("patch target not found: MotionLoad (P25)");
            }

            // ---- P26 (v1.6.6): YotogiPlayManager.Suspend 旧模式守卫 ----
            // 官方边界缺陷: OnClickCommand 的 adv_hook 分支硬编码
            // Suspend(cmd, "*サスペンド")，淡出后 JumpLabel 落在 adv_kag
            // 当前文件——*サスペンド 标签只存在于新模式内容包调度器
            // （av001_main.ks 等，标签与 hook 设置者同文件），旧模式
            // 调度器（YotogiMain.ks 等）没有 → KAG 调度脚本死亡 →
            // 永久黑屏。原版普通夜伽选不到带 adv_hook 的命令（AV/GP
            // 包技能），全解锁特性让其流入普通夜伽才踩中。
            // prefix = 非新夜伽模式时跳过原版挂起（状态掩码/淡出/
            // 致命跳转全部不发生）并代行重入 OnClickCommand（同一
            // 命令；calledAdvFiles 已记录 → 重入后 flag=false → 命令
            // 正常执行=用户第一次点击直接生效）；s_suspendReentry
            // 护栏防病理递归。新模式（AV/GP 会话）原版放行。
            MethodInfo miSuspend = tPlay.GetMethod("Suspend");
            MethodInfo miSuspendPrefix = self.GetMethod("Suspend_Prefix", BindingFlags.NonPublic | BindingFlags.Static);
            if (!object.ReferenceEquals(miSuspend, null)
                && !object.ReferenceEquals(miSuspendPrefix, null))
            {
                h.Patch(miSuspend, prefix: new HarmonyMethod(miSuspendPrefix));
                ok++;
            }
            else
            {
                miss++;
                log.LogWarning("patch target not found: Suspend (P26)");
            }

            // ---- P31 (v1.7.2): TJSFuncGetMaidStatus null安全化（FT NRE根治）----
            // 官方边界缺陷: FT/KA 脚本大量调用 GetMaidStatus(1,...)（3P/NTR
            // 变体分支, 全弧 3600 处）——单人会话 GetMaid(1)=null 直接
            // .status deref → NRE 从 KAG 同步段穿透 CallFtFiles 协程 →
            // doneFtFileCall 永不置位 → Play 屏 fade 卡死 FadeInWait
            // （Finish呼び出し刷屏）+ 后续 @AddPrefabBg 全部不执行。
            // prefix = 查询女仆为 null 时 result="" 并跳过原版（脚本
            // @if 走 else 分支, 挂载/演出继续）。
            MethodInfo miGms = typeof(ScriptManager).GetMethod("TJSFuncGetMaidStatus",
                BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo miGmsPrefix = self.GetMethod("GetMaidStatusSafe_Prefix", BindingFlags.Public | BindingFlags.Static);
            if (!object.ReferenceEquals(miGms, null)
                && !object.ReferenceEquals(miGmsPrefix, null))
            {
                h.Patch(miGms, prefix: new HarmonyMethod(miGmsPrefix));
                ok++;
            }
            else
            {
                miss++;
                log.LogWarning("patch target not found: TJSFuncGetMaidStatus (P31)");
            }

            // ---- P32 (v1.7.2→v1.7.3): CallNormalFile prefix（跨舞台位置捕获）
            // + postfix（位置还原 + P30v2 事件触发挂载）+ finalizer（未知 NRE
            // 不再能杀死 FT 协程/卡死画面）。FT 的舞台分支+@AllPos+
            // @AddPrefabBg 同步段在 CallNormalFile 内部执行完毕（NRE 栈实证）
            // → postfix 时点官方道具已挂、锚点已稳, 即时补挂缺失道具
            // （FT2/KA 的 label 非空调用不触发）。
            MethodInfo miCallNormal = tPlay.GetMethod("CallNormalFile");
            MethodInfo miCallNormalPre = self.GetMethod("CallNormalFile_Prefix", BindingFlags.Public | BindingFlags.Static);
            MethodInfo miCallNormalPost = self.GetMethod("CallNormalFile_Postfix", BindingFlags.Public | BindingFlags.Static);
            MethodInfo miCallNormalFinal = self.GetMethod("CallNormalFile_Finalizer", BindingFlags.Public | BindingFlags.Static);
            if (!object.ReferenceEquals(miCallNormal, null)
                && !object.ReferenceEquals(miCallNormalPre, null)
                && !object.ReferenceEquals(miCallNormalPost, null)
                && !object.ReferenceEquals(miCallNormalFinal, null))
            {
                h.Patch(miCallNormal,
                    prefix: new HarmonyMethod(miCallNormalPre),
                    postfix: new HarmonyMethod(miCallNormalPost),
                    finalizer: new HarmonyMethod(miCallNormalFinal));
                ok++;
            }
            else
            {
                miss++;
                log.LogWarning("patch target not found: CallNormalFile (P32)");
            }

            // ---- P42 (v1.7.7): TJSFuncGetYotogiSelectStageName prefix ----
            // FT 同步段窗口内返回官方舞台名 → 舞台门控定位块在任何地图
            // 上都执行（多对姿势的双对分离/道具/相机 + AddAllOffset_Ignore
            // 旗标 = GP001 动作语义）。窗口由 P32 prefix/postfix/finalizer
            // 管理（s_ftStageOverride）。
            MethodInfo miStageName = typeof(ScriptManager).GetMethod("TJSFuncGetYotogiSelectStageName",
                BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo miStageNamePrefix = self.GetMethod("YotogiStageNameOverride_Prefix", BindingFlags.Public | BindingFlags.Static);
            if (!object.ReferenceEquals(miStageName, null)
                && !object.ReferenceEquals(miStageNamePrefix, null))
            {
                h.Patch(miStageName, prefix: new HarmonyMethod(miStageNamePrefix));
                ok++;
            }
            else
            {
                miss++;
                log.LogWarning("patch target not found: TJSFuncGetYotogiSelectStageName (P42)");
            }

            // ---- P33 (v1.7.3): 语音数据入口 null 防护 ----
            // SetAdditionalFtVoice / SetRepeatVoiceFile / AddRepeatVoiceFile
            // 对缺席女仆槽位的写入 = Update() 每帧 NRE 刷屏的根源。
            ok += PatchDirect(h, self, "VoiceDataAddFt_Prefix", null,
                tPlay.GetMethod("SetAdditionalFtVoice"), ref miss);
            ok += PatchDirect(h, self, "VoiceDataMaid_Prefix", null,
                tPlay.GetMethod("SetRepeatVoiceFile"), ref miss);
            ok += PatchDirect(h, self, "VoiceDataMaid_Prefix", null,
                tPlay.GetMethod("AddRepeatVoiceFile"), ref miss);

            // ---- P34 (v1.7.3): talk/voice 标签目标女仆 null 防护 ----
            // TagTalk/TagTalkAddFt（GetVoiceTargetMaid null → .AudioMan/
            // .ActiveSlotNo NRE 卡死 KAG）+ TagTalkRepeat/TagTalkRepeatAdd/
            // TagVoiceWait（maid 属性槽位缺席）。
            Type tYKag = typeof(YotogiKagManager);
            ok += PatchDirect(h, self, "TagTalkSafe_Prefix", null,
                tYKag.GetMethod("TagTalk"), ref miss);
            ok += PatchDirect(h, self, "TagTalkSafe_Prefix", null,
                tYKag.GetMethod("TagTalkAddFt"), ref miss);
            ok += PatchDirect(h, self, "TagTalkMaidSafe_Prefix", null,
                tYKag.GetMethod("TagTalkRepeat"), ref miss);
            ok += PatchDirect(h, self, "TagTalkMaidSafe_Prefix", null,
                tYKag.GetMethod("TagTalkRepeatAdd"), ref miss);
            ok += PatchDirect(h, self, "TagTalkMaidSafe_Prefix", null,
                tYKag.GetMethod("TagVoiceWait"), ref miss);

            // ---- P35/P36 (v1.7.3): 官方追加女仆选择介入 ----
            // OnFadeEnd 重定向回 Play + GetPlayerSelectNum 精确化。
            Type tSubSel = typeof(YotogiSubCharacterSelectManager);
            ok += PatchDirect(h, self, "SubCharaFadeEnd_Prefix", null,
                tSubSel.GetMethod("OnFadeEnd", BindingFlags.NonPublic | BindingFlags.Instance), ref miss);
            ok += PatchDirect(h, self, "SubCharaSelectNum_Prefix", null,
                tSubSel.GetMethod("GetPlayerSelectNum"), ref miss);

            // ---- P37 (v1.7.5): 中途入会女仆本地位姿清零 ----
            // 嫉妒/吃醋/スワッピング系 FT 的 @CharaActivate maid=1 npc=...
            // 与 P35 选人屏 OnFinish 的 SetActiveMaid——官方只在场景/会话
            // 开始时 ResetCharaPosAll, 中途入会带着上个场景的残留本地位姿
            // → 该角色错位/扭曲。prefix 清零（不动共享根/其他角色）。
            ok += PatchDirect(h, self, "SetActiveMaid_JoinReset_Prefix", null,
                typeof(CharacterMgr).GetMethod("SetActiveMaid"), ref miss);

            // ---- P38/P39 (v1.7.5): スワッピング系元技能链确定性落位 ----
            // @YotogiCall name=Play skillid / @ReStart skillid 的子技能
            // 插入在满 7 技能数组下静默失败 → 链断裂/NPC 停在原点重叠/
            // AddAllOffset_Ignore 残留。prefix = 保证插入 + 预瞄落位。
            ok += PatchDirect(h, self, "YotogiCallChain_Prefix", null,
                typeof(YotogiManager).GetMethod("TagYotogiCall"), ref miss);
            ok += PatchDirect(h, self, "ReStartChain_Prefix", null,
                typeof(YotogiManager).GetMethod("TagReStart"), ref miss);

            // ---- P40/P41 (v1.7.6): 3P 动作链诊断日志 ----
            // 问题「选择女仆后没有进入动作/站着、人物混在一起」的定位:
            // 3P 动作经 personality FT *KA 段的 @MotionScript 加载
            // （无 maid 参数 = 按 meta.maidCount 分发多女仆; maid=1 =
            // 指定副女仆; MotionKagManager.GetMaid(0)=main_maid_ 局部索引）。
            // 两个观察点（只读不改, 下次实测日志即可定位断点）:
            //   P40 入口 = ScriptManager.LoadMotionScript: 谁(guid)请求了什么
            //   P41 出口 = MotionKagManager.LoadScriptFile: 实际应用到哪些角色
            ok += PatchDirect(h, self, "LoadMotionScriptDiag_Prefix", null,
                typeof(ScriptManager).GetMethod("LoadMotionScript"), ref miss);
            ok += PatchDirect(h, self, "MotionLoadDiag_Postfix", null,
                typeof(MotionKagManager).GetMethod("LoadScriptFile"), ref miss);

            string problems = "";
            if (object.ReferenceEquals(fiSkillGroupParent, null)) { problems += " skill_group_parent_"; }
            if (object.ReferenceEquals(fiPlayingSkillNo, null)) { problems += " playing_skill_no_"; }
            if (object.ReferenceEquals(fiSkillIconDefaultColor, null)) { problems += " skill_icon_default_color_"; }
            if (object.ReferenceEquals(miOnNextSkillMove, null)) { problems += " OnNextSkillMove"; }
            if (object.ReferenceEquals(fiContainerMgr, null)) { problems += " skill_container_mgr_"; }
            if (object.ReferenceEquals(fiParamBasicBar, null)) { problems += " param_basic_bar_"; }
            if (object.ReferenceEquals(fiParamData, null)) { problems += " param_data_"; }
            if (object.ReferenceEquals(fiMotionMainMaid, null)) { problems += " motion_main_maid_"; }
            if (object.ReferenceEquals(fiMotionMainMan, null)) { problems += " motion_main_man_"; }
            if (object.ReferenceEquals(piIsNewYotogiMode, null)) { problems += " is_new_yotogi_mode"; }
            if (object.ReferenceEquals(miOnClickCommand, null)) { problems += " OnClickCommand"; }
            if (object.ReferenceEquals(fiSubCharaSelectMgr, null)) { problems += " sub_chara_select_mgr_"; }
            if (object.ReferenceEquals(fiSuspendStore, null)) { problems += " suspendStoreData"; }
            if (object.ReferenceEquals(fiMotionActiveMaids, null)) { problems += " ActiveMaidList"; }
            if (object.ReferenceEquals(fiMotionActiveMen, null)) { problems += " ActiveManList"; }
            if (object.ReferenceEquals(fiPositionChanger, null)) { problems += " positionChanger"; }
            if (object.ReferenceEquals(fiPosChangerGizmo, null)) { problems += " m_GizmoControlObject"; }

            log.LogInfo("YotogiUnlimited v" + PluginVersion + " loaded: " + ok + " patch(es) installed."
                + ((miss > 0) ? (" (" + miss + " target(s) not found - see above.)") : "")
                + ((problems.Length > 0) ? (" MISSING REFLECTION:" + problems) : "")
                + (" [lang=" + LanguageCodeOf(s_lang) + "]"));

            // 内容26(v1.5.0): POV —— レンダリング直前フック（KAG演出カメラより
            // 後に必ず走るのでPOVが最終的に勝つ。ゲームのカメラ状態は触らない）
            Camera.onPreCull += OnCameraPreCull;
        }

        private void OnDestroy()
        {
            // 事故予防: プラグイン破棄時にPOV状態を完全復帰させる
            try
            {
                PovExit();
            }
            catch (Exception)
            {
            }
            // v1.6.0: 看镜头状態も復帰（破棄後も女仆がカメラを見続ける事故防止）
            try
            {
                ResetLookAtCamera();
            }
            catch (Exception)
            {
            }
            Camera.onPreCull -= OnCameraPreCull;
        }

        // =================================================================
        // patch helpers
        // =================================================================
        private static int PatchExact(Harmony h, Type self, string prefixName, string postfixName,
            Type targetType, string targetMethod, Type[] paramTypes, ref int miss)
        {
            MethodInfo target = (paramTypes != null)
                ? targetType.GetMethod(targetMethod, paramTypes)
                : targetType.GetMethod(targetMethod, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return PatchDirect(h, self, prefixName, postfixName, target, ref miss);
        }

        private static int PatchDirect(Harmony h, Type self, string prefixName, string postfixName,
            MethodInfo target, ref int miss)
        {
            if (object.ReferenceEquals(target, null))
            {
                miss++;
                log.LogWarning("patch target not found: " + ((postfixName != null) ? postfixName : prefixName));
                return 0;
            }
            HarmonyMethod pre = null;
            HarmonyMethod post = null;
            if (prefixName != null)
            {
                MethodInfo mi = self.GetMethod(prefixName, BindingFlags.Public | BindingFlags.Static);
                if (object.ReferenceEquals(mi, null))
                {
                    miss++;
                    log.LogWarning("prefix method missing: " + prefixName);
                    return 0;
                }
                pre = new HarmonyMethod(mi);
            }
            if (postfixName != null)
            {
                MethodInfo mi = self.GetMethod(postfixName, BindingFlags.Public | BindingFlags.Static);
                if (object.ReferenceEquals(mi, null))
                {
                    miss++;
                    log.LogWarning("postfix method missing: " + postfixName);
                    return 0;
                }
                post = new HarmonyMethod(mi);
            }
            try
            {
                h.Patch(target, prefix: pre, postfix: post);
                return 1;
            }
            catch (Exception e)
            {
                miss++;
                log.LogWarning("patch failed [" + target.DeclaringType.FullName + "::" + target.Name + "]: " + e.Message);
                return 0;
            }
        }

        // =================================================================
        // P1/P2: 技能-舞台绑定 -> 恒可用
        // =================================================================
        public static bool SkillStage_Prefix(ref bool __result)
        {
            if (object.ReferenceEquals(cfgAnyStage, null) || !cfgAnyStage.Value)
            {
                return true; // 配置关闭：原版行为
            }
            __result = true;
            return false;
        }

        // =================================================================
        // P3: 舞台可玩条件 -> 恒可用
        // =================================================================
        public static bool StagePlayable_Prefix(ref bool __result)
        {
            if (object.ReferenceEquals(cfgUnlockStages, null) || !cfgUnlockStages.Value)
            {
                return true;
            }
            __result = true;
            return false;
        }

        // =================================================================
        // P4 postfix: 技能塔 —— 已播放姿势恢复可点击（重播入口）
        //   v1.7.0 追加: 塔子节点自动扩展（数组>塔位数时克隆+重绑点击，
        //   根治原版 OnSkillIconClick 的越界崩溃）。
        //   （v1.7.1: 角色位置基线失效钩子随内容34一并撤除）
        // =================================================================
        public static void Tower_Postfix(YotogiPlayManager __instance)
        {
            // ---- v1.7.0 内容33/32: 塔位扩展（不受 cfgUnlimitedReuse 门控）----
            try
            {
                GameObject towerRoot0 = (GameObject)fiSkillGroupParent.GetValue(__instance);
                if (towerRoot0 != null)
                {
                    YotogiManager ym0 = YotogiManager.instans;
                    if (ym0 != null)
                    {
                        YotogiManager.PlayingSkillData[] arr0 = ym0.play_skill_array;
                        if (arr0 != null && arr0.Length > towerRoot0.transform.childCount
                            && towerRoot0.transform.childCount > 0)
                        {
                            TowerEnsureChildren(__instance, towerRoot0, arr0);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                log.LogWarning("Tower_Postfix(extend): " + e.Message);
            }
            if (object.ReferenceEquals(cfgUnlimitedReuse, null) || !cfgUnlimitedReuse.Value)
            {
                return;
            }
            try
            {
                GameObject towerRoot = (GameObject)fiSkillGroupParent.GetValue(__instance);
                if (towerRoot == null)
                {
                    return;
                }
                YotogiManager ym = YotogiManager.instans;
                if (ym == null)
                {
                    return;
                }
                YotogiManager.PlayingSkillData[] arr = ym.play_skill_array;
                if (arr == null || arr.Length == 0)
                {
                    return;
                }
                int cur = (int)fiPlayingSkillNo.GetValue(__instance);
                Color def = (Color)fiSkillIconDefaultColor.GetValue(__instance);
                Transform parent = towerRoot.transform;
                int count = (arr.Length < parent.childCount) ? arr.Length : parent.childCount;
                for (int i = 0; i < count; i++)
                {
                    YotogiManager.PlayingSkillData psd = arr[i];
                    if (psd == null || psd.skill_pair == null || psd.skill_pair.base_data == null)
                    {
                        continue; // 未选槽位（原版已隐藏）
                    }
                    if (!psd.is_play || i == cur)
                    {
                        continue; // 未播放 / 当前播放
                    }
                    GameObject icon = UTY.GetChildObjectNoError(parent.GetChild(i).gameObject, "Icon");
                    if (icon == null)
                    {
                        continue;
                    }
                    UIButton btn = icon.GetComponent<UIButton>();
                    if (btn == null)
                    {
                        continue;
                    }
                    btn.enabled = true;
                    // 原版把颜色设在图标的 UI2DSprite 上（不是 UIButton）
                    if (def.a > 0f)
                    {
                        UI2DSprite spr = icon.GetComponent<UI2DSprite>();
                        if (spr != null)
                        {
                            spr.color = def;
                        }
                    }
                    UIPlayAnimation anim = icon.GetComponent<UIPlayAnimation>();
                    if (anim != null)
                    {
                        anim.enabled = true;
                    }
                }
            }
            catch (Exception e)
            {
                log.LogWarning("Tower_Postfix: " + e.Message);
            }
        }

        // =================================================================
        // v1.7.0 内容33/32: 塔子节点扩展 —— 克隆末位 SkillGroup、按均匀
        // 间距排位、重绑点击（字符串名→经补丁的 OnSkillIconClick）、
        // 为新槽位补图标数据与当前位视觉。原版 UpdateSkillTower 的循环
        // 以 childCount 为界，克隆后后续调用自动覆盖全部槽位。
        // =================================================================
        private static void TowerEnsureChildren(YotogiPlayManager pm, GameObject towerRoot,
            YotogiManager.PlayingSkillData[] arr)
        {
            Transform parent = towerRoot.transform;
            int baseCount = parent.childCount;
            if (baseCount == 0)
            {
                return;
            }
            int want = arr.Length;
            if (want <= baseCount)
            {
                return;
            }
            Vector3 spacing = Vector3.zero;
            if (baseCount >= 2)
            {
                spacing = parent.GetChild(1).localPosition - parent.GetChild(0).localPosition;
            }
            Transform tpl = parent.GetChild(baseCount - 1);
            for (int k = baseCount; k < want; k++)
            {
                GameObject nu = (GameObject)UnityEngine.Object.Instantiate(tpl.gameObject);
                nu.name = tpl.gameObject.name + "_" + k;
                nu.transform.SetParent(parent, false);
                nu.transform.localPosition = tpl.localPosition + spacing * (float)(k - baseCount + 1);
                nu.transform.localRotation = tpl.localRotation;
                nu.transform.localScale = tpl.localScale;
                GameObject icon = UTY.GetChildObjectNoError(nu, "Icon");
                    if (icon != null)
                    {
                        UIButton btn = icon.GetComponent<UIButton>();
                        if (btn != null)
                        {
                            btn.onClick.Clear();
                            btn.onClick.Add(new EventDelegate(pm, "OnSkillIconClick"));
                        }
                    }
            }
            // 新槽位图标数据（本次调用原版只刷了旧 childCount 个）
            int cur = (int)fiPlayingSkillNo.GetValue(pm);
            Color def = (Color)fiSkillIconDefaultColor.GetValue(pm);
            for (int i = baseCount; i < want; i++)
            {
                GameObject go = parent.GetChild(i).gameObject;
                GameObject icon2 = UTY.GetChildObjectNoError(go, "Icon");
                if (icon2 == null)
                {
                    continue;
                }
                YotogiManager.PlayingSkillData psd = arr[i];
                if (psd == null || psd.skill_pair == null || psd.skill_pair.base_data == null)
                {
                    icon2.SetActive(false);
                    continue;
                }
                icon2.SetActive(true);
                YotogiSkillIcon ic = icon2.GetComponent<YotogiSkillIcon>();
                if (ic != null)
                {
                    ic.SetSkillData(psd.skill_pair.base_data);
                }
                UI2DSprite spr = icon2.GetComponent<UI2DSprite>();
                UIButton b = icon2.GetComponent<UIButton>();
                GameObject arrow = UTY.GetChildObjectNoError(go, "Arrow");
                if (i == cur)
                {
                    if (spr != null)
                    {
                        spr.color = Color.white;
                    }
                    if (b != null)
                    {
                        b.enabled = false;
                    }
                    if (arrow != null)
                    {
                        arrow.SetActive(true);
                    }
                }
                else
                {
                    if (def.a > 0f && spr != null)
                    {
                        spr.color = def;
                    }
                    if (arrow != null)
                    {
                        arrow.SetActive(false);
                    }
                }
            }
        }

        // =================================================================
        // P5 prefix: 技能塔图标点击 —— v1.7.0 起完全接管。
        //   原版在 play_skill_array.Length > 塔子节点数时 OnSkillIconClick
        //   必越界崩溃（GetChild(i) 无界循环）；塔位0 又是原版死区。
        //   接管条件: 复用重播开启 或 数组超塔（安全需要）。其余放行原版。
        // =================================================================
        public static bool IconClick_Prefix(YotogiPlayManager __instance)
        {
            bool reuseOn = !object.ReferenceEquals(cfgUnlimitedReuse, null) && cfgUnlimitedReuse.Value;
            try
            {
                YotogiManager ym = YotogiManager.instans;
                if (ym == null)
                {
                    return true;
                }
                if (ym.IsAllCharaBusy())
                {
                    return false; // 原版在 busy 时本来就是 no-op
                }
                GameObject towerRoot = (GameObject)fiSkillGroupParent.GetValue(__instance);
                if (towerRoot == null)
                {
                    return true;
                }
                UIButton currentBtn = UIButton.current;
                if (currentBtn == null)
                {
                    return true;
                }
                YotogiManager.PlayingSkillData[] arr = ym.play_skill_array;
                if (arr == null || arr.Length == 0)
                {
                    return true;
                }
                Transform parent = towerRoot.transform;
                bool oversized = arr.Length > parent.childCount;
                if (!reuseOn && !oversized)
                {
                    return true; // 全原生状态: 原版逻辑照跑
                }
                int limit = (arr.Length < parent.childCount) ? arr.Length : parent.childCount;
                int found = -1;
                for (int i = 0; i < limit; i++)
                {
                    GameObject icon = UTY.GetChildObjectNoError(parent.GetChild(i).gameObject, "Icon");
                    if (icon == null)
                    {
                        continue;
                    }
                    if (icon.GetComponent<UIButton>() == currentBtn)
                    {
                        found = i;
                        break;
                    }
                }
                if (found == -1)
                {
                    // 未命中: 原版是 no-op；oversized 时原版会越界 → 自行吞掉
                    return !oversized;
                }
                // ---- v1.7.3 (issue 3): 3P系姿势且副女仆不在场 → 先走官方
                // 追加女仆选择（选完 Play 重入落到本姿势）, 否则维持原行为 ----
                Skill.Data sdT = null;
                if (arr[found] != null && arr[found].skill_pair != null
                    && arr[found].skill_pair.base_data != null)
                {
                    sdT = arr[found].skill_pair.base_data;
                }
                if (sdT != null && sdT.player_num > 1 && CountMissingSubMaids(sdT) > 0)
                {
                    int curNoT = (int)fiPlayingSkillNo.GetValue(__instance);
                    if (TryOpenSubCharaSelect(ym, sdT, curNoT))
                    {
                        if (found == 0)
                        {
                            s_forceSkillZero = true;
                            fiPlayingSkillNo.SetValue(__instance, 0);
                        }
                        else
                        {
                            fiPlayingSkillNo.SetValue(__instance, found - 1);
                        }
                        return false;
                    }
                    // 俱乐部可选女仆不足: 放行（P33/P34 防护下单人播放不崩溃）
                }
                // ---- 完全接管: 塔位0死区修复 + 越界根治 + 复用重播 ----
                if (found == 0)
                {
                    s_forceSkillZero = true;
                    fiPlayingSkillNo.SetValue(__instance, 0);
                }
                else
                {
                    fiPlayingSkillNo.SetValue(__instance, found - 1);
                }
                miOnNextSkillMove.Invoke(__instance, null);
                return false;
            }
            catch (Exception e)
            {
                log.LogWarning("IconClick_Prefix: " + e.Message + " - falling back to vanilla.");
                return true;
            }
        }

        // =================================================================
        // P6 prefix: NextSkill —— 消费塔位0重播标志 + POV切姿势置脏标
        // =================================================================
        public static void NextSkill_Prefix(YotogiPlayManager __instance)
        {
            // v1.8.0 (更改1): 每次换姿势（NextSkill = 姿势切换统一入口:
            // 塔点击/下一姿势/姿势一览/Play屏重入/选人回来）重置主女仆
            // 兴奋=-100 预设、感性=10（v1.8.2: 感性回退 v1.8.0 设计,
            // v1.8.1 的 300 锁死 + Update 维持撤销）。官方 NextSkill
            // L987-989 随后读 status 刷新参数条（is_anime:false）。
            // KA 段分支随之恒走 *KA2（CallFtFiles L651: currentExcite<0）。
            // v1.8.1 (追加): 解锁所有性癖 —— 对在场全部女仆幂等添加
            // 全部已启用的性癖（Propensity.GetAllDatas(true)）, 性癖系
            // 技能/演出随之全开放（非 F7 功能, 恒生效）。
            try
            {
                YotogiManager ymNs = YotogiManager.instans;
                Maid mNs = (ymNs != null) ? ymNs.maid : null;
                if (mNs != null && mNs.status != null)
                {
                    mNs.status.currentExcite = -100;
                    mNs.status.currentSensual = 10;
                }
                CharacterMgr cmNs = (GameMain.Instance != null) ? GameMain.Instance.CharacterMgr : null;
                if (cmNs != null)
                {
                    List<MaidStatus.Propensity.Data> props =
                        MaidStatus.Propensity.GetAllDatas(true);
                    if (props != null && props.Count > 0)
                    {
                        for (int pi = 0; pi < cmNs.GetMaidCount(); pi++)
                        {
                            Maid pmNs = cmNs.GetMaid(pi);
                            if (pmNs == null || !pmNs.Visible || pmNs.status == null)
                            {
                                continue;
                            }
                            for (int pj = 0; pj < props.Count; pj++)
                            {
                                pmNs.status.AddPropensity(props[pj]);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            // v1.7.3 (issue 4): 任何姿势切换（官方下一姿势按钮/塔点击/
            // 姿势一览/Play屏重入）都标记 POV 重锚——新姿势的眼高与
            // 骨架姿态不同, 颈父骨锚的局部眼位偏移需要重捕获。
            if (s_povActive)
            {
                s_povPoseDirty = true;
            }
            if (!s_forceSkillZero)
            {
                return;
            }
            s_forceSkillZero = false;
            try
            {
                int cur = (int)fiPlayingSkillNo.GetValue(__instance);
                if (cur == 0)
                {
                    fiPlayingSkillNo.SetValue(__instance, -1); // ++ 后落在 0
                }
            }
            catch (Exception e)
            {
                log.LogWarning("NextSkill_Prefix: " + e.Message);
            }
        }

        // =================================================================
        // P43 prefix (v1.8.0): OnFinish —— 归零前位姿保持。
        // 官方 OnFinish（YotogiPlayManager L2611）在「继续下一技能」
        // 分支（playing_skill_no_ != -1）里 SetCharaAllPos(0)/
        // SetCharaAllRot(0) 归零双层根后才 CallScreen(Play) → CallFtFiles
        // → FT 的 @AllPos 以世界原点为起点重定位。v1.7.8 的重基底把
        // pre 捕获在 FT 调用时（= 归零后的原点）→ 还原到原点, 而原点
        // 在用户所选地图上未必有效（位置突跳/浮空）。这里在归零之前
        // 捕获双层根, FT prefix（CallNormalFile_Prefix）优先使用。
        // 结束分支（playing_skill_no_ == -1）不捕获（无下一姿势）。
        // =================================================================
        public static void OnFinish_HoldPrefix(YotogiPlayManager __instance)
        {
            try
            {
                GameMain gm = GameMain.Instance;
                if (gm == null || gm.CharacterMgr == null)
                {
                    return;
                }
                int curNo = -1;
                if (!object.ReferenceEquals(fiPlayingSkillNo, null))
                {
                    curNo = (int)fiPlayingSkillNo.GetValue(__instance);
                }
                if (curNo < 0)
                {
                    return; // 结束分支: 无下一姿势, 不保持
                }
                CharacterMgr cm = gm.CharacterMgr;
                s_ftHoldPos = cm.GetCharaAllPos();
                s_ftHoldRot = cm.GetCharaAllRot();
                s_ftHoldOffs = cm.GetCharaAllOfsetPos();
                s_ftHoldValid = true;
            }
            catch (Exception)
            {
                s_ftHoldValid = false;
            }
        }

        // =================================================================
        // P44 prefix (v1.8.2): UltimateOrbitCamera.Update —— IMGUI 菜单
        // 区域的输入穿透屏蔽。
        // 官方机制: UICamera.fallThrough 对象收到 OnHover(isOver) →
        // m_bFallThrough（鼠标不在 NGUI UI 上时 true）→ 相机响应滚轮
        // 缩放/左右键拖动旋转/中键平移。IMGUI（F7 菜单/姿势一览/场景
        // 列表）不在 NGUI 体系 → 鼠标悬停在菜单上时 m_bFallThrough 仍
        // true → 滚轮/点击穿透到游戏相机（用户实测痛点）。
        // 修复: Update prefix 在鼠标位于我们的 IMGUI 窗口 rect 内时
        // 反射置 m_bFallThrough=false（相机本帧完全不响应鼠标输入）;
        // 菜单区域外不干预（右键旋转视角/滚轮缩放照常）。
        // =================================================================
        public static bool OrbitUpdate_Prefix(UltimateOrbitCamera __instance)
        {
            try
            {
                if (!s_imGuiMouseOver)
                {
                    return true;
                }
                if (object.ReferenceEquals(fiOrbitFallThrough, null))
                {
                    return true;
                }
                fiOrbitFallThrough.SetValue(__instance, false);
            }
            catch (Exception)
            {
            }
            return true;
        }

        // =================================================================
        // P45 prefix (v1.8.3): BgMgr.ChangeBg —— 纯背景保持重定向。
        // 用户经 F7 切到「其他背景」（官方摄影背景表条目: Live/餐厅等）
        // 后, 官方 NextSkill 在每次换姿势/重载时都会
        // ChangeBG(SelectedStage) 把场景拉回夜伽舞台 —— 用户实测
        // 「重载回到其他地图/原先进入的地图」。s_bgOverride 记录当前
        // 纯背景 prefab 名, Play 画面内把传入名重定向到它。
        // 清空时机: StageSwitch 切回夜伽舞台 / 会话结束（Update）。
        // =================================================================
        public static void ChangeBg_Override_Prefix(ref string f_strPrefubName)
        {
            try
            {
                if (string.IsNullOrEmpty(s_bgOverride))
                {
                    return;
                }
                YotogiManager ymBg = YotogiManager.instans;
                if (ymBg == null || ymBg.cur_call_screen_name != "Play")
                {
                    return; // 仅 Play 画面干预（舞台选择屏等官方流程原样）
                }
                if (!string.Equals(f_strPrefubName, s_bgOverride, StringComparison.OrdinalIgnoreCase))
                {
                    f_strPrefubName = s_bgOverride;
                }
            }
            catch (Exception)
            {
            }
        }

        // =================================================================
        // P46 postfix (v1.8.3): YotogiStageSelectManager.SelectStage ——
        // 未解锁舞台的 stageData=null 修正。
        // 官方: SelectStage 内 `IsEnabled(data.id) ? data : null` ——
        // 未解锁舞台被置空 → StageExpansionPack.ChangeBG 直接 no-op
        // （点击无反应 / 之后重载场景不动 = 位置感错乱的一部分）。
        // 插件本意 = 全舞台解禁（P3 已让 isYotogiPlayable 恒 true）,
        // 这里在官方赋值后若 stageData 为空则用反射强制覆盖
        // SelectedStage（private setter）为完整 pack。
        // =================================================================
        public static void SelectStage_Fix_Postfix(YotogiStage.Data data,
            YotogiAdaptMyRoomStage.Data myRoomData, bool isDayTime)
        {
            try
            {
                if (data == null)
                {
                    return;
                }
                YotogiStageSelectManager.StageExpansionPack cur = YotogiStageSelectManager.SelectedStage;
                if (cur != null && cur.stageData != null)
                {
                    return; // 已解锁: 官方结果正确
                }
                if (object.ReferenceEquals(miSelectedStageSet, null))
                {
                    return;
                }
                miSelectedStageSet.Invoke(null, new object[]
                {
                    new YotogiStageSelectManager.StageExpansionPack(data, myRoomData, isDayTime)
                });
            }
            catch (Exception)
            {
            }
        }

        // =================================================================
        // P7 prefix: CreateDatas —— 特殊条件混合（v1.4.0 高速化版）
        // 旧方式は原版を10回呼んでタイプ毎の結果をunion（OnCall が
        // 5回呼ぶため開く度に最大50回の全表スキャン+未習得条件文の
        // 再構築が走り重かった）。CreateDatas は specialConditionCheck=
        // false でタイプフィルタをスキップする（4つのExecフィルタは
        // タイプ非依存で結果恒等）→ 原版1回呼出+クライアント側で
        // タイプ抽出で同一結果を1回のスキャンで得る。
        // さらに同一フレーム内の呼出はメモを返す（OnCall の5連呼出
        // → 1スキャン+4再利用。フレームを跨いだら必ず再構築なので
        // v1.2.1 のような長命キャッシュの鮮度問題は構造的に起きない）。
        // =================================================================
        public static bool CreateDatas_Prefix(ref Dictionary<int, YotogiSkillListManager.Data> __result,
            MaidStatus.Status status, bool specialConditionCheck, Skill.Data.SpecialConditionType type)
        {
            if (!specialConditionCheck)
            {
                return true; // 档案技能表等：原版
            }
            bool ignoreAll = !object.ReferenceEquals(cfgIgnoreAll, null) && cfgIgnoreAll.Value;
            bool mix = !object.ReferenceEquals(cfgMixConditions, null) && cfgMixConditions.Value;
            if (!ignoreAll && !mix)
            {
                return true; // 两开关全关：原版
            }
            if (!ignoreAll)
            {
                int t = (int)type;
                if (t == 4 || t < 0 || 5 < t)
                {
                    return true; // 仅混合模式：Faint/NewType/回想系维持原版
                }
            }
            // 同一フレームメモ（OnCall の5連呼出対策）
            if (Time.frameCount == s_mergeFrame
                && !object.ReferenceEquals(s_mergeStatus, null)
                && object.ReferenceEquals(status, s_mergeStatus)
                && ignoreAll == s_mergeIgnoreAll
                && !object.ReferenceEquals(s_mergeCache, null))
            {
                __result = s_mergeCache;
                return false;
            }
            Dictionary<int, YotogiSkillListManager.Data> merged =
                new Dictionary<int, YotogiSkillListManager.Data>();
            try
            {
                // 原版を1回だけ（specialConditionCheck=false = 全タイプ返却）
                Dictionary<int, YotogiSkillListManager.Data> all =
                    YotogiSkillListManager.CreateDatas(status, false, Skill.Data.SpecialConditionType.Null);
                if (all != null)
                {
                    foreach (KeyValuePair<int, YotogiSkillListManager.Data> kv in all)
                    {
                        YotogiSkillListManager.Data d = kv.Value;
                        if (d == null || d.skillData == null)
                        {
                            continue;
                        }
                        int st = (int)d.skillData.specialConditionType;
                        bool keep = ignoreAll
                            ? (0 <= st && st <= 9)
                            : (st == 0 || st == 1 || st == 2 || st == 3 || st == 5);
                        if (keep && !merged.ContainsKey(kv.Key))
                        {
                            merged.Add(kv.Key, d);
                        }
                    }
                }
                // 内容20c: 同名姿势デュープ除去（变体を1項目に統合）
                if (!object.ReferenceEquals(cfgDedup, null) && cfgDedup.Value)
                {
                    merged = DedupSkillsByName(merged);
                }
            }
            catch (Exception e)
            {
                log.LogWarning("CreateDatas_Prefix: " + e.Message + " - vanilla fallback.");
                return true; // 異常時は原版へ戻す（安全弁）
            }
            __result = merged;
            s_mergeFrame = Time.frameCount;
            s_mergeStatus = status;
            s_mergeIgnoreAll = ignoreAll;
            s_mergeCache = merged;
            return false;
        }

        // 内容20c: 同名スキルの重複統合。優先度: 習得済み > Null型 > 先着。
        // 特殊条件变体（普通/微醺/遮掩/魅药/告白/気絶…）は同名で別IDの
        // 別行 —— リスト上「同じ姿势が並ぶ」の正体。1項目に統合すると
        // リスト項目数が1/5〜1/10になり、NGUIボタン生成コストも激減。
        private static Dictionary<int, YotogiSkillListManager.Data> DedupSkillsByName(
            Dictionary<int, YotogiSkillListManager.Data> src)
        {
            Dictionary<string, YotogiSkillListManager.Data> best =
                new Dictionary<string, YotogiSkillListManager.Data>();
            Dictionary<int, YotogiSkillListManager.Data> output =
                new Dictionary<int, YotogiSkillListManager.Data>();
            foreach (KeyValuePair<int, YotogiSkillListManager.Data> kv in src)
            {
                YotogiSkillListManager.Data d = kv.Value;
                if (d == null || d.skillData == null || string.IsNullOrEmpty(d.skillData.name))
                {
                    output.Add(kv.Key, d);
                    continue;
                }
                YotogiSkillListManager.Data cur;
                if (!best.TryGetValue(d.skillData.name, out cur))
                {
                    best.Add(d.skillData.name, d);
                    output.Add(kv.Key, d);
                }
                else if (IsBetterRep(d, cur))
                {
                    best[d.skillData.name] = d;
                    output.Remove(cur.skillData.id);
                    output.Add(d.skillData.id, d);
                }
            }
            return output;
        }

        private static bool IsBetterRep(YotogiSkillListManager.Data cand, YotogiSkillListManager.Data cur)
        {
            bool cl = !object.ReferenceEquals(cand.maidStatusSkillData, null);
            bool rl = !object.ReferenceEquals(cur.maidStatusSkillData, null);
            if (cl != rl)
            {
                return cl; // 習得済みを優先
            }
            if (!cl)
            {
                bool cn = cand.skillData.specialConditionType == Skill.Data.SpecialConditionType.Null;
                bool rn = cur.skillData.specialConditionType == Skill.Data.SpecialConditionType.Null;
                if (cn != rn)
                {
                    return cn; // 未習得同士なら通常版を優先
                }
            }
            return false;
        }

        // =================================================================
        // P8: CreateSkillButtons pre+post —— 切换勾选保留已选容器
        // =================================================================
        public static void SaveSel_Prefix(YotogiSkillSelectManager __instance,
            ref List<KeyValuePair<Skill.Data, bool>> __state)
        {
            __state = null;
            if (object.ReferenceEquals(cfgMixConditions, null) || !cfgMixConditions.Value)
            {
                return;
            }
            if (object.ReferenceEquals(fiContainerMgr, null))
            {
                return;
            }
            try
            {
                YotogiSkillContainerViewer viewer =
                    (YotogiSkillContainerViewer)fiContainerMgr.GetValue(__instance);
                if (viewer == null)
                {
                    return;
                }
                Skill.Data[] arr = viewer.GetSettingSkillArray();
                if (arr == null || arr.Length == 0)
                {
                    return;
                }
                bool[] locks = viewer.GetSettingSkillLockArray();
                List<KeyValuePair<Skill.Data, bool>> saved = new List<KeyValuePair<Skill.Data, bool>>();
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i] != null)
                    {
                        bool lk = (locks != null && i < locks.Length) ? locks[i] : false;
                        saved.Add(new KeyValuePair<Skill.Data, bool>(arr[i], lk));
                    }
                }
                if (saved.Count > 0)
                {
                    __state = saved;
                }
            }
            catch (Exception e)
            {
                log.LogWarning("SaveSel_Prefix: " + e.Message);
            }
        }

        public static void RestoreSel_Postfix(YotogiSkillSelectManager __instance,
            List<KeyValuePair<Skill.Data, bool>> __state)
        {
            if (__state == null || __state.Count == 0)
            {
                return;
            }
            if (object.ReferenceEquals(fiContainerMgr, null))
            {
                return;
            }
            try
            {
                YotogiSkillContainerViewer viewer =
                    (YotogiSkillContainerViewer)fiContainerMgr.GetValue(__instance);
                if (viewer == null)
                {
                    return;
                }
                for (int i = 0; i < __state.Count; i++)
                {
                    viewer.AddSkill(__state[i].Key, __state[i].Value);
                }
            }
            catch (Exception e)
            {
                log.LogWarning("RestoreSel_Postfix: " + e.Message);
            }
        }

        // =================================================================
        // P9 prefix: GetPlayPossibleMaidCount —— 全俱乐部计数
        // =================================================================
        public static bool PossibleMaidCount_Prefix(YotogiManager __instance, ref int __result)
        {
            if (object.ReferenceEquals(cfgMultiPlayer, null) || !cfgMultiPlayer.Value)
            {
                return true;
            }
            try
            {
                CharacterMgr cm = GameMain.Instance.CharacterMgr;
                Maid main = __instance.maid;
                int n = 0;
                if (main != null)
                {
                    for (int i = 0; i < cm.GetStockMaidCount(); i++)
                    {
                        Maid m = cm.GetStockMaid(i);
                        if (m != null && main.status.heroineType != MaidStatus.HeroineType.Sub && main.IsCrcBody == m.IsCrcBody)
                        {
                            n++;
                        }
                    }
                }
                if (n == 0)
                {
                    n = 1;
                }
                __result = n;
                return false;
            }
            catch (Exception e)
            {
                log.LogWarning("PossibleMaidCount_Prefix: " + e.Message + " - vanilla fallback.");
                return true;
            }
        }

        // =================================================================
        // P10 prefix: GetSubMaidList —— 全俱乐部子角色列表
        // =================================================================
        public static bool SubMaidList_Prefix(YotogiManager __instance, List<Maid> draw_list)
        {
            if (object.ReferenceEquals(cfgMultiPlayer, null) || !cfgMultiPlayer.Value)
            {
                return true;
            }
            if (draw_list == null)
            {
                return true; // 原版对 null 直接 return
            }
            try
            {
                CharacterMgr cm = GameMain.Instance.CharacterMgr;
                Maid main = __instance.maid;
                List<Maid> list = new List<Maid>();
                if (main != null)
                {
                    for (int i = 0; i < cm.GetStockMaidCount(); i++)
                    {
                        Maid m = cm.GetStockMaid(i);
                        if (m != null && main != m && m.status.heroineType != MaidStatus.HeroineType.Sub && main.IsCrcBody == m.IsCrcBody)
                        {
                            list.Add(m);
                        }
                    }
                }
                draw_list.Clear();
                draw_list.AddRange(list);
                return false;
            }
            catch (Exception e)
            {
                log.LogWarning("SubMaidList_Prefix: " + e.Message + " - vanilla fallback.");
                return true;
            }
        }

        // =================================================================
        // P11 prefix: NTR 解锁
        // =================================================================
        public static bool LockNTR_Prefix(ref bool __result)
        {
            if (object.ReferenceEquals(cfgUnlockNTR, null) || !cfgUnlockNTR.Value)
            {
                return true;
            }
            __result = false;
            return false;
        }

        // =================================================================
        // P12~P17 通用 prefix：技能需求条件 -> 恒可用
        // =================================================================
        public static bool ExecAlways_Prefix(ref bool __result)
        {
            if (object.ReferenceEquals(cfgIgnoreAll, null) || !cfgIgnoreAll.Value)
            {
                return true;
            }
            __result = true;
            return false;
        }

        // =================================================================
        // P18 prefix: 未习得技能的 skill_param_data -> 共享 dummy
        // =================================================================
        public static bool SkillParam_Prefix(YotogiSkillUnit __instance, ref MaidStatus.YotogiSkillData __result)
        {
            if (object.ReferenceEquals(cfgIgnoreAll, null) || !cfgIgnoreAll.Value)
            {
                return true;
            }
            if (object.ReferenceEquals(fiParamData, null))
            {
                return true;
            }
            MaidStatus.YotogiSkillData cur = (MaidStatus.YotogiSkillData)fiParamData.GetValue(__instance);
            if (cur != null)
            {
                return true; // 已习得：原版
            }
            if (object.ReferenceEquals(s_dummyParam, null))
            {
                s_dummyParam = new MaidStatus.YotogiSkillData();
                s_dummyParam.lockSkillExp = false;
            }
            __result = s_dummyParam;
            return false;
        }

        // =================================================================
        // P19 prefix: 技能选择预算 -> max(原值, 9999)
        // =================================================================
        public static bool SelectHp_Prefix(YotogiManager __instance, ref int __result)
        {
            if (object.ReferenceEquals(cfgIgnoreAll, null) || !cfgIgnoreAll.Value)
            {
                return true;
            }
            try
            {
                Maid m = __instance.maid;
                if (m == null || m.status == null)
                {
                    return true;
                }
                int orig = m.status.maxHp;
                __result = (orig < 9999) ? 9999 : orig;
                return false;
            }
            catch (Exception e)
            {
                log.LogWarning("SelectHp_Prefix: " + e.Message + " - vanilla fallback.");
                return true;
            }
        }

        // =================================================================
        // P20 prefix: 未习得技能进播放数组 -> dummy YotogiSkillData(Lv3)
        // =================================================================
        public static bool PairCreate_Prefix(Maid maid, Skill.Data base_data, ref Yotogi.SkillDataPair __result)
        {
            if (object.ReferenceEquals(cfgIgnoreAll, null) || !cfgIgnoreAll.Value)
            {
                return true;
            }
            if (maid == null || base_data == null || maid.status == null || maid.status.yotogiSkill == null)
            {
                return true;
            }
            try
            {
                if (maid.status.yotogiSkill.Get(base_data.id) != null)
                {
                    return true; // 已习得：原版
                }
                MaidStatus.YotogiSkillData d = new MaidStatus.YotogiSkillData();
                d.data = base_data;
                if (base_data.skill_exp_table != null && base_data.skill_exp_table.Length > 0)
                {
                    List<int> table = new List<int>(base_data.skill_exp_table);
                    d.expSystem.SetExreienceList(table);
                    d.expSystem.SetTotalExp(100000000); // -> Lv3
                }
                Yotogi.SkillDataPair pair = new Yotogi.SkillDataPair();
                pair.maid = maid;
                pair.base_data = base_data;
                pair.skill_data = d;
                __result = pair;
                return false;
            }
            catch (Exception e)
            {
                log.LogWarning("PairCreate_Prefix: " + e.Message + " - vanilla fallback.");
                return true;
            }
        }

        // =================================================================
        // P21 postfix (v1.3.0 内容9): ApplyExecCommandStatus 后置拦截
        // =================================================================
        public static void ExecStatus_Postfix(YotogiPlayManager __instance, Maid maid)
        {
            if (object.ReferenceEquals(cfgForbidAutoClimax, null) || !cfgForbidAutoClimax.Value)
            {
                return;
            }
            try
            {
                if (maid == null || maid.status == null)
                {
                    return;
                }
                if (maid.status.currentExcite > ExciteCap)
                {
                    maid.status.currentExcite = ExciteCap; // setter が -100..300 に夹める
                    if (!object.ReferenceEquals(fiParamBasicBar, null))
                    {
                        object bar = fiParamBasicBar.GetValue(__instance);
                        if (bar is YotogiParamBasicBar)
                        {
                            ((YotogiParamBasicBar)bar).SetCurrentExcite(ExciteCap, false);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                log.LogWarning("ExecStatus_Postfix: " + e.Message);
            }
        }

        // =================================================================
        // 内容16 共通: シナリオファイル存在チェック（.ks 拡張子の有無両対応）
        // GameUty のファイルシステム（script系arc 含む）で解決する。
        // KasizukiMainMenu が同様の存在チェックに GameUty.FileSystem を
        // 使用している実績手法。
        // =================================================================
        private static bool ScriptFileExists(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return true; // 原版が空ガードを持つため判定不要
            }
            if (GameUty.IsExistFile(name))
            {
                return true;
            }
            if (GameUty.IsExistFile(name + ".ks"))
            {
                return true;
            }
            return false;
        }

        private static void ReportMissingScript(string name)
        {
            try
            {
                if (!s_reportedMissing.Contains(name))
                {
                    s_reportedMissing.Add(name);
                    if (!object.ReferenceEquals(log, null))
                    {
                        log.LogInfo("YotogiUnlimited: scenario script missing (fallback): " + name);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        // =================================================================
        // P22 postfix (v1.3.2 内容16): ReplaceFileNameCallBack —— KAGエンジン
        // 層の欠損スクリプト安全化（変体回退 + 桩）。
        // この managed コールバックは KAGエンジンがシナリオファイルを
        // 読み込む前に必ず通る（C#側 LoadScenario も .ks 内部の @call も
        // 全部ここでファイル名が確定する）。夜伽KAGのみに適用。
        //   1) 解決後ファイルが存在すれば何もしない。
        //   2) 欠損なら全性格 replaceText を試して「別性格_残り」が存在
        //      すれば差し替える（ラベル群は変体間で共通のため成立）。
        //      姿勢モーションは性格変体ファイル内の @MotionScript が
        //      担うため、変体回退＝「動く姿勢」の必須条件。
        //   3) 変体が全く無ければ空シナリオ桩へ（@return で即復帰。
        //      この場合ラベル不一致エラーが出得るが P23 が吸収）。
        // =================================================================
        public static void FileNameReplace_Postfix(BaseKagManager __instance, ref string __result)
        {
            if (object.ReferenceEquals(cfgSafeScript, null) || !cfgSafeScript.Value)
            {
                return;
            }
            if (!(__instance is YotogiKagManager))
            {
                return; // 夜伽KAG以外（ADV/モーション等）は原版動作
            }
            try
            {
                string resolved = __result;
                if (string.IsNullOrEmpty(resolved))
                {
                    return;
                }
                if (ScriptFileExists(resolved))
                {
                    return; // 存在する：原版のまま
                }
                string variant = FindVariantFallback(resolved);
                if (!string.IsNullOrEmpty(variant))
                {
                    ReportMissingScript(resolved + " -> variant " + variant);
                    __result = variant;
                    return;
                }
                ReportMissingScript(resolved + " -> stub");
                __result = MissingScriptStub;
            }
            catch (Exception e)
            {
                log.LogWarning("FileNameReplace_Postfix: " + e.Message);
            }
        }

        // 全性格の replaceText 一覧（初回欠損時に遅延構築・キャッシュ）
        private static string[] GetPersonalPrefixes()
        {
            if (!object.ReferenceEquals(s_personalPrefixes, null))
            {
                return s_personalPrefixes;
            }
            List<string> list = new List<string>();
            try
            {
                List<MaidStatus.Personal.Data> all = MaidStatus.Personal.GetAllDatas(true);
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        MaidStatus.Personal.Data d = all[i];
                        if (d != null && !string.IsNullOrEmpty(d.replaceText)
                            && !list.Contains(d.replaceText))
                        {
                            list.Add(d.replaceText);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                log.LogWarning("GetPersonalPrefixes: " + e.Message);
            }
            s_personalPrefixes = list.ToArray();
            return s_personalPrefixes;
        }

        // "B1_SOU_07550a" → "w1_SOU_07550a" 等の別性格変体を探す
        private static string FindVariantFallback(string resolved)
        {
            int us = resolved.IndexOf('_');
            if (us <= 0)
            {
                return null;
            }
            string rest = resolved.Substring(us + 1);
            if (string.IsNullOrEmpty(rest))
            {
                return null;
            }
            string[] prefixes = GetPersonalPrefixes();
            for (int i = 0; i < prefixes.Length; i++)
            {
                string cand = prefixes[i] + "_" + rest;
                if (ScriptFileExists(cand))
                {
                    return cand;
                }
            }
            return null;
        }

        // =================================================================
        // P23 prefix (v1.3.2 内容16): OnNativeConsolLog —— 原生KAG/TJS
        // エラーの managed 出力チャネル（NativeUtf8ToString →
        // Debug.LogError）。夜伽中の欠損シナリオ/ラベル系メッセージを
        // Info 1回表示へ格下げする（LogError 自体を抑止）。
        // 対象パターン（cm3d2.dll 内の UTF-16 文字列テーブルと実測一致）:
        //   ・シナリオファイル %1 が見つかりません
        //   ・シナリオファイル %1 内にラベル %2 が見つかりません
        //   ・シナリオが読み込まれていないため、指定ラベル[...]への移動に失敗
        //   ・指定ラベル[%1]は存在しないため...
        // =================================================================
        public static bool ConsolLog_Filter(IntPtr utf8_native_string, bool exception)
        {
            if (object.ReferenceEquals(cfgSafeScript, null) || !cfgSafeScript.Value || !exception)
            {
                return true; // 非エラーは原版挙動（何も出力されない）
            }
            try
            {
                if (YotogiManager.instans == null)
                {
                    return true; // 夜伽中以外は原版のまま
                }
                string text = DllBase.NativeUtf8ToString(utf8_native_string);
                if (string.IsNullOrEmpty(text) || !IsKagMissingScriptError(text))
                {
                    return true;
                }
                ReportMissingScript("[KAG] " + text);
                return false; // LogError を抑制（Info 1回のみ残る）
            }
            catch (Exception)
            {
                return true;
            }
        }

        private static bool IsKagMissingScriptError(string text)
        {
            if (text.IndexOf("シナリオファイル") >= 0 && text.IndexOf("見つかりません") >= 0)
            {
                return true;
            }
            if (text.IndexOf("内にラベル") >= 0 && text.IndexOf("見つかりません") >= 0)
            {
                return true;
            }
            if (text.IndexOf("読み込まれていないため") >= 0)
            {
                return true;
            }
            if (text.IndexOf("指定ラベル") >= 0 && text.IndexOf("存在しないため") >= 0)
            {
                return true;
            }
            return false;
        }

        // =================================================================
        // Update(): 熱鍵 / 画面遷移 / 速度 / 自動絶頂禁止 / 無限精神
        // =================================================================
        private void Update()
        {
            // v1.8.5: 名称リストの自動出力（起動後1回, ファイルが無い場合のみ）
            AutoExportNameListOnce();
            // v1.8.5: 外部名称翻訳ファイルの更新を自動検知（2秒スロットル）
            CheckNameTranslationAutoReload();
            // v1.8.5 (追加2b): F1 側で言語が変更された場合の遅延反映
            ProcessLangRefreshIfPending();

            // 内容8: 滑条窗口开关热键
            if (!object.ReferenceEquals(cfgSliders, null) && cfgSliders.Value
                && !object.ReferenceEquals(cfgSliderKey, null))
            {
                try
                {
                    if (Input.GetKeyDown(cfgSliderKey.Value))
                    {
                        slidersVisible = !slidersVisible;
                    }
                }
                catch (Exception)
                {
                }
            }

            YotogiManager ym = YotogiManager.instans;
            string screen = (ym == null) ? string.Empty : ym.cur_call_screen_name;
            if (ym == null)
            {
                // v1.8.3 (P45): 会话结束 → 撤销纯背景保持（下次夜伽从官方舞台开始）
                s_bgOverride = null;
            }
            if (screen != s_lastScreen)
            {
                s_lastScreen = screen;
                // 内容13: Play画面进入时自动打开菜单
                if (screen == "Play" && !object.ReferenceEquals(cfgMenuAutoOpen, null) && cfgMenuAutoOpen.Value)
                {
                    slidersVisible = true;
                }
                if (screen != "Play")
                {
                    // 画面離脱時：【お願い】の内部状態をリセット
                    onegaiPhase = -1;
                    onegaiTimer = 0f;
                    // （v1.5.2: 離脱時の自動音声掃除は撤回——残留の根源は
                    //  【一緒に絶頂】だったため機能ごと削除済み。音声の
                    //  掃除は F7 の手動ボタンのみ）
                }
            }
            bool inPlay = (ym != null && screen == "Play");

            // v1.8.2 (调整): 感性锁死已撤销（回退 v1.8.0 设计: 仅
            // NextSkill_Prefix 重置 10, 不做每帧维持）。
            // v1.8.2 (输入粒度): 列表打开时的输入屏蔽 = P44
            // （UltimateOrbitCamera.Update prefix: 鼠标在 IMGUI 菜单
            // 上时 m_bFallThrough=false → 滚轮缩放/左右键拖动不再穿透
            // 到游戏相机; 菜单外右键旋转视角保留）。此处无每帧逻辑。

            // v1.8.1 (追加问题): 官方夜伽位置调整的旋转 gizmo 固定大小 ——
            // GizmoRender.generalLens ∝ 相机距离（屏幕恒定设计, 且 fov
            // 度/弧度混用 bug 可致符号翻转 = 不可点）。这里每帧驱动
            // offsetScale 补偿距离, 使 gizmo 世界尺寸恒定（与移动轴一致
            // 的「固定大小」语义, 远近不再缩放）。
            try
            {
                if (inPlay && s_posChanger != null
                    && s_posChanger.m_YotogiPositionChangerObj != null
                    && s_posChanger.m_YotogiPositionChangerObj.activeInHierarchy)
                {
                    YotogiPositionChanger.GizmoObject gizmoObj = null;
                    if (!object.ReferenceEquals(fiPosChangerGizmo, null))
                    {
                        gizmoObj = fiPosChangerGizmo.GetValue(s_posChanger)
                            as YotogiPositionChanger.GizmoObject;
                    }
                    GizmoRender gizmoRot = (gizmoObj != null) ? gizmoObj.gizmoRot : null;
                    Camera camGizmo = Camera.main;
                    if (gizmoRot != null && camGizmo != null)
                    {
                        float distG = (camGizmo.transform.position - gizmoRot.transform.position).magnitude;
                        if (distG > 0.1f)
                        {
                            // 官方: generalLens = -2*tan(0.5*fov)*dist/50*offsetScale
                            // 令 generalLens == PosChangerGizmoLens（常数）→ 解出 offsetScale。
                            float coefG = -2f * Mathf.Tan(0.5f * camGizmo.fieldOfView) * distG / 50f;
                            if (Mathf.Abs(coefG) > 0.000001f)
                            {
                                gizmoRot.offsetScale = PosChangerGizmoLens / coefG;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            // v1.8.0 (追加2): 选女仆后自动热加载 —— P35 joined 置位后,
            // 等 Play 画面全员空闲稳定 0.6s（姿势加载完成）再重播一遍
            // （重播本身会经历 busy, 天然不会被本块二次触发）。
            if (s_replayAfterJoin)
            {
                if (!inPlay)
                {
                    s_replayAfterJoin = false;      // 离开 Play（异常/取消）: 放弃
                    s_replaySettleSince = -1f;
                }
                else if (ym.IsAllCharaBusy())
                {
                    s_replaySettleSince = -1f;      // 还在加载: 继续等
                }
                else
                {
                    if (s_replaySettleSince < 0f)
                    {
                        s_replaySettleSince = Time.time;
                    }
                    if (Time.time - s_replaySettleSince >= 0.6f)
                    {
                        s_replayAfterJoin = false;
                        s_replaySettleSince = -1f;
                        ReloadCurrentPose();
                    }
                }
            }

            // 内容26(v1.6.0): POV —— Vキー入/切・右ドラッグ視点・自動退出
            UpdatePovInput(inPlay);

            // 内容28/29(v1.6.0): 女仆头/眼朝向镜头（每帧强制，覆盖脚本指定）
            UpdateLookAtCamera(inPlay);

            // 内容35(v1.7.0): Tab热键隐藏UI + 每帧维持
            UpdateUiHide(inPlay, ym);
            // P30 (v1.7.2): 姿势道具挂载已事件驱动化（CallNormalFile
            // postfix / 场景切换 / 开关开启三入口）, Update 零轮询。

            // 内容12: 【お願い】自動擬人リズム
            if (inPlay && !object.ReferenceEquals(cfgOnegai, null) && cfgOnegai.Value)
            {
                UpdateOnegai();
            }

            // 内容11/12(v1.4.0最適化): 速度倍率の毎フレーム適用。
            // ホイール廃止（内容18）→ 適用条件は「お願い中 or 倍率≠1」。
            // 倍率1.0のままなら全AnimationState走査を完全スキップ（内容20d）。
            // 新アニメ読込でspeedが1.0へ戻るため適用中は連続適用、
            // 適用停止時に一度だけ1.0へ戻す。
            bool onegaiOn = !object.ReferenceEquals(cfgOnegai, null) && cfgOnegai.Value;
            bool applySpeed = inPlay
                && (onegaiOn || Mathf.Abs(s_speedMul - 1f) > 0.0001f);
            if (applySpeed)
            {
                ApplySpeed(s_speedMul);
                s_speedWasApplied = true;
            }
            else if (s_speedWasApplied)
            {
                ApplySpeed(1f);
                s_speedWasApplied = false;
            }

            // 内容9(v1.4.1拡張): 興奮クランプ（スライダー等の外部書き込み経路）
            // v1.4.0 までは主メイド(ym.maid)のみ → 3P/ハーレムの副メイドが
            // 300 のまま残り、RRC9 自動反応チェーンの温床になっていた。
            // 在场メイド全員へクランプ（バー表示は主メイドのみ更新すればよい）。
            if (inPlay && !object.ReferenceEquals(cfgForbidAutoClimax, null) && cfgForbidAutoClimax.Value)
            {
                try
                {
                    CharacterMgr cmAll = GameMain.Instance.CharacterMgr;
                    if (cmAll != null)
                    {
                        for (int i = 0; i < cmAll.GetMaidCount(); i++)
                        {
                            Maid m = cmAll.GetMaid(i);
                            if (m == null || !m.Visible || m.status == null || m.status.currentExcite <= ExciteCap)
                            {
                                continue;
                            }
                            m.status.currentExcite = ExciteCap;
                            if (m == ym.maid)
                            {
                                YotogiPlayManager pm = ym.play_mgr;
                                if (pm != null && !object.ReferenceEquals(fiParamBasicBar, null))
                                {
                                    object bar = fiParamBasicBar.GetValue(pm);
                                    if (bar is YotogiParamBasicBar)
                                    {
                                        ((YotogiParamBasicBar)bar).SetCurrentExcite(ExciteCap, false);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    LogOnce("ForbidAutoClimax: " + e.Message);
                }
            }

            // 内容7: 无限精神（v1.2.0 から同じ）
            if (object.ReferenceEquals(cfgInfiniteMind, null) || !cfgInfiniteMind.Value)
            {
                return;
            }
            if (!inPlay)
            {
                return;
            }
            try
            {
                Maid m = ym.maid;
                YotogiPlayManager pm = ym.play_mgr;
                if (m == null || pm == null || m.status == null)
                {
                    return;
                }
                if (m.status.currentMind < m.status.maxMind)
                {
                    m.status.currentMind = m.status.maxMind;
                    object bar = fiParamBasicBar.GetValue(pm);
                    if (bar is YotogiParamBasicBar)
                    {
                        ((YotogiParamBasicBar)bar).SetCurrentMind(m.status.maxMind, false);
                    }
                }
            }
            catch (Exception e)
            {
                LogOnce("InfiniteMind: " + e.Message);
            }
        }

        // =================================================================
        // 内容12: 【お願い】フェーズマシン（人間らしい速度変化）
        //   温存(0.4→0.9緩上昇) → 律動(0.85..1.1 呼吸ブレ) → 加速(1.05..1.25)
        //   → 突撃(1.18..1.28 ピストン波) → 緩和(→0.5) → 律動へ戻る循環
        //   すべて継続時間ランダム + Lerp追従 + 微ジッターで生体感を出す。
        //   v1.4.1 内容22: 自動リズム全体の上限を 1.30x へ圧縮（ユーザー
        //   指定）。スライダー手動上限 5.00x は不変（お願い自動時のみ）。
        // =================================================================
        private void UpdateOnegai()
        {
            onegaiTimer -= Time.deltaTime;
            if (onegaiTimer <= 0f)
            {
                // フェーズ遷移: 初回は温存(0)、以降 1→2→3→4→1 の循環
                if (onegaiPhase < 0)
                {
                    onegaiPhase = 0;
                }
                else if (onegaiPhase >= 4)
                {
                    onegaiPhase = 1;
                }
                else
                {
                    onegaiPhase++;
                }
                switch (onegaiPhase)
                {
                    case 0: // 温存: ゆっくり始める
                        onegaiPhaseDur = UnityEngine.Random.Range(6f, 10f);
                        onegaiBase = 0.55f;
                        break;
                    case 1: // 律動: 落ち着いたピストン
                        onegaiPhaseDur = UnityEngine.Random.Range(8f, 14f);
                        onegaiBase = UnityEngine.Random.Range(0.85f, 1.1f);
                        break;
                    case 2: // 加速（v1.4.1 内容22: 1.30上限に合わせ圧縮）
                        onegaiPhaseDur = UnityEngine.Random.Range(4f, 7f);
                        onegaiBase = UnityEngine.Random.Range(1.05f, 1.25f);
                        break;
                    case 3: // 突撃: 最速区間（v1.4.1 内容22: 上限1.30へ圧縮）
                        onegaiPhaseDur = UnityEngine.Random.Range(2f, 4f);
                        onegaiBase = UnityEngine.Random.Range(1.18f, 1.28f);
                        break;
                    default: // 4 緩和
                        onegaiPhaseDur = UnityEngine.Random.Range(5f, 8f);
                        onegaiBase = 0.6f;
                        break;
                }
                onegaiTimer = onegaiPhaseDur;
                onegaiSeed = UnityEngine.Random.Range(0f, 100f);
            }

            float progress = 1f - (onegaiTimer / onegaiPhaseDur);
            if (progress < 0f) { progress = 0f; }
            if (progress > 1f) { progress = 1f; }
            float target = onegaiBase;
            if (onegaiPhase == 0)
            {
                target = Mathf.Lerp(0.4f, 0.9f, progress); // ゆっくり加速していく
            }
            else if (onegaiPhase == 1)
            {
                target = onegaiBase + 0.12f * Mathf.Sin(Time.time * 1.3f + onegaiSeed); // 呼吸ブレ
            }
            else if (onegaiPhase == 3)
            {
                target = onegaiBase + 0.06f * Mathf.Sin(Time.time * 4.5f + onegaiSeed); // 強いピストン波（v1.4.1: 0.25→0.06 波幅圧縮）
            }
            else if (onegaiPhase == 4)
            {
                target = Mathf.Lerp(onegaiBase, 0.5f, progress); // なめらかに減速
            }
            // 微ジッター: 完全な周期にならないように
            target += 0.05f * Mathf.Sin(Time.time * 0.7f + onegaiSeed * 2f);
            if (target < 0.15f) { target = 0.15f; }
            if (target > OnegaiAutoMax) { target = OnegaiAutoMax; } // v1.4.1 内容22: お願い自動の上限1.30（手動スライダー5.00は不変）
            // なめらかな追従（人間の加減速感）。v1.3.0と同じく
            // Lerp後の即時クランプは行わない（高倍率から自然に減衰する）
            s_speedMul = Mathf.Lerp(s_speedMul, target, Time.deltaTime * 1.6f);
        }

        // =================================================================
        // 内容11/12: 速度倍率を全キャラの再生中AnimationStateへ適用
        // =================================================================
        private static void ApplySpeed(float mul)
        {
            try
            {
                if (s_speedAnims.Count == 0 || Time.time - s_speedCacheTime > 2f)
                {
                    RefreshSpeedCache();
                }
                for (int i = 0; i < s_speedAnims.Count; i++)
                {
                    Animation a = s_speedAnims[i];
                    if (a == null)
                    {
                        continue;
                    }
                    foreach (AnimationState st in a)
                    {
                        if (st.enabled)
                        {
                            st.speed = mul;
                        }
                    }
                }
                // （v1.5.1: 内容25の音声pitch同期は撤回——ユーザー要件は
                //  速度同期ではなく残留音声の修復だった。音声は無変更）
            }
            catch (Exception e)
            {
                LogOnce("ApplySpeed: " + e.Message);
            }
        }

        // =================================================================
        // 内容27(v1.5.2 簡素化版): 残留音声の手動クリア —— F7 ボタン専用。
        // 全 voice (AudioSourceMgr チャネル) + SE を一括停止する。
        // repeat_voice は LoadPlay(loop=true) で AudioSource 自身が無限
        // ループする設計のため、残留した際の急救手段として機能する。
        // =================================================================
        private static void VoiceHygieneSweep()
        {
            try
            {
                GameMain gm = GameMain.Instance;
                if (gm == null || gm.SoundMgr == null)
                {
                    return;
                }
                gm.SoundMgr.VoiceStopAll();
                gm.SoundMgr.StopSe();
            }
            catch (Exception e)
            {
                LogOnce("voice clear: " + e.Message);
            }
        }

        private static void RefreshSpeedCache()
        {
            s_speedCacheTime = Time.time;
            List<Animation> list = new List<Animation>();
            try
            {
                CharacterMgr cm = GameMain.Instance.CharacterMgr;
                if (cm == null)
                {
                    s_speedAnims = list;
                    return;
                }
                // 在场メイド全員（3P対応）
                // v1.6.2: GetAnimation() は骨未ロードで「未だキャラがロー
                // ドさていません」を LogError する副作用付きgetter——
                // public フィールド m_Animation を直読みする（未ロード
                // なら Unity null で安全にスキップ）。
                for (int i = 0; i < cm.GetMaidCount(); i++)
                {
                    Maid m = cm.GetMaid(i);
                    if (m != null && m.Visible && m.body0 != null)
                    {
                        Animation a = m.body0.m_Animation;
                        if (a != null)
                        {
                            list.Add(a);
                        }
                    }
                }
                // 男（旧/新ボディ両対応: body0 + _BO_mbody）
                for (int i = 0; i < cm.GetManCount(); i++)
                {
                    Maid man = cm.GetMan(i);
                    if (man == null || !man.Visible)
                    {
                        continue;
                    }
                    if (man.body0 != null)
                    {
                        Animation a = man.body0.m_Animation;
                        if (a != null)
                        {
                            list.Add(a);
                        }
                    }
                    GameObject bo = GameObject.Find("Man[" + i + "]/Offset/_BO_mbody");
                    if (bo != null)
                    {
                        Animation a2 = bo.GetComponent<Animation>();
                        if (a2 != null && !list.Contains(a2))
                        {
                            list.Add(a2);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogOnce("RefreshSpeedCache: " + e.Message);
            }
            s_speedAnims = list;
        }

        // =================================================================
        // 内容26(v1.6.0): 【V】キーPOV —— 主人公（男）の主観視点トグル。
        // 自研実装（KK_PerspectiveX 非参照）:
        //   - 対象は CharacterMgr.GetMan(0)（主人公）固定。循環なし。
        //   - Camera.onPreCull で最終書き込み（KAG演出カメラに必ず勝つ）
        //   - ゲームのカメラ状態（位置・FOV・操作権）は保存/復帰のみで
        //     破壊しない → 退出時にゲームが自力で元に戻せる
        //   - 頭部非表示は renderer.enabled のみ（シリアライズされる
        //     フラグは絶対触らない = データ破損事故ゼロ）
        //   - カーソルロックはしない。視点は【右ドラッグ】で操作
        //   - Play画面を離れたら自動退出・主人公消滅でも自動退出
        //   - 2.0/3.0両対応: 頭部骨は公式キャッシュ TBody.trsHead を
        //     使用（非CRC男=ManBip01 Head / CRC身体=男女とも Bip01 Head
        //     をゲーム側が版本判定済み——骨骼名ハードコード禁止）
        // =================================================================
        private static void UpdatePovInput(bool inPlay)
        {
            // 自動退出: 画面切替・主人公消滅・対象离场
            if (s_povActive)
            {
                if (!inPlay || s_povTarget == null)
                {
                    // v1.7.3 (issue 4): 自动退出走官方标准取景——还原到进入
                    // POV 时的远古位姿在多次切姿势后必然视角异常。
                    PovExit();
                }
                else
                {
                    // v1.6.2: 在场检测——対象が非表示(姿势切替の退場等)が
                    // 1秒継続したら自動退出。主人公(透明男)は例外で維持。
                    // 一瞬の切替演出では落ちないようヒステリシス付き。
                    bool targetGone = false;
                    try
                    {
                        if (!s_povTarget.Visible && s_povTarget != GetProtagonistMan())
                        {
                            targetGone = true;
                        }
                    }
                    catch (Exception)
                    {
                    }
                    if (targetGone)
                    {
                        if (s_povGoneSince < 0f)
                        {
                            s_povGoneSince = Time.time;
                        }
                        if (Time.time - s_povGoneSince > 1f)
                        {
                            PovExit();
                            return;
                        }
                    }
                    else
                    {
                        s_povGoneSince = -1f;
                    }
                    // 演出やロードで頭部が作り直された場合の追従（0.25秒毎）
                    if (Time.time >= s_povNextRescan)
                    {
                        s_povNextRescan = Time.time + 0.25f;
                        // v1.7.7 (问题4): man 目标槽位重解析 —— 男体 CRC 换体
                        // （SwapNewManBody）把活动槽位的 Maid 对象整个替换
                        // （旧对象移回仓库并隐藏）。发现槽位现住者≠当前目标
                        // → 完整切换到新对象: 复原旧目标（头/颈）,
                        // 新目标重新隐藏/塌缩, 相机平滑重置吸附新眼位。
                        // 防止 POV 锚到仓库里的旧身体（视角飞到原点/隐藏
                        // 状态跟着旧身体回仓库 = 「角色没有恢复正常」）。
                        if (s_povTargetSlot >= 0 && s_povTarget != null && s_povTarget.boMAN)
                        {
                            try
                            {
                                CharacterMgr cmPov = GameMain.Instance.CharacterMgr;
                                Maid occupant = (cmPov != null) ? cmPov.GetMan(s_povTargetSlot) : null;
                                if (occupant != null && !ReferenceEquals(occupant, s_povTarget))
                                {
                                    try
                                    {
                                        PovRestoreHead();
                                        PovRestoreNeck();
                                    }
                                    catch (Exception)
                                    {
                                    }
                                    s_povTarget = occupant;
                                    s_povGoneSince = -1f;
                                    s_povSmoothInit = false;
                                    PovHideHead();
                                    PovHideNeck();
                                    if (!object.ReferenceEquals(log, null))
                                    {
                                        log.LogInfo("YotogiUnlimited: POV target re-resolved after man body swap (slot "
                                            + s_povTargetSlot + ")");
                                    }
                                }
                            }
                            catch (Exception)
                            {
                            }
                        }
                        PovHideHead();
                        PovHideNeck(); // 内容39(v1.7.2): 颈骨塌缩追従（换装/重建后幂等）
                    }
                    // v1.7.3 (issue 4): 姿势切换后的重锚——新姿势稳定
                    // （全员非busy持续0.4s）后重捕获颈父骨锚（RestoreNeck→
                    // HideNeck: 新姿势下的真实眼位重新入胸骨系）, 头部
                    // 渲染器重扫（骨骼重建后新渲染器需隐藏）, 相机平滑
                    // 位置重置（吸附新眼位, 不做跨场景长距离插值）。
                    if (s_povPoseDirty)
                    {
                        YotogiManager ymR = YotogiManager.instans;
                        bool busyR = (ymR != null) && ymR.IsAllCharaBusy();
                        if (busyR)
                        {
                            s_povSettleSince = -1f;
                        }
                        else
                        {
                            if (s_povSettleSince < 0f)
                            {
                                s_povSettleSince = Time.time;
                            }
                            if (Time.time - s_povSettleSince >= 0.4f)
                            {
                                s_povPoseDirty = false;
                                s_povSettleSince = -1f;
                                try
                                {
                                    PovRestoreNeck();
                                    PovHideNeck();
                                    PovHideHead();
                                }
                                catch (Exception)
                                {
                                }
                                s_povSmoothInit = false;
                                if (!object.ReferenceEquals(log, null))
                                {
                                    log.LogInfo("YotogiUnlimited: POV re-anchored after pose switch");
                                }
                            }
                        }
                    }
                    else
                    {
                        s_povSettleSince = -1f;
                    }
                    // 視点操作: 右ドラッグ（IMGUI操作中は除外）
                    // v1.7.6: 四元数增量相机——世界Y轴水平旋转（纯水平,
                    // v1.6.5 批准语义, 倾斜时也保持世界水平）+ 相机自身
                    // 右轴俯仰（恒为屏幕水平轴, 任何朝向上下都不反向）。
                    // v1.7.6: 撤销 PovAutoLevel 自动回正（翻越垂直时的
                    // 滚转回正 = 用户头晕根源）, 改为俯仰钳制 ±89.9°
                    // （PovClampPitch）——永不越过垂直, 结构上无横滚/
                    // 无颠倒/无反向区, 也不再有任何「自动旋转镜头」。
                    if (Input.GetMouseButton(1) && GUIUtility.hotControl == 0)
                    {
                        float sens = (!object.ReferenceEquals(cfgPovSens, null)) ? cfgPovSens.Value : 1f;
                        float mx = Input.GetAxis("Mouse X") * 3f * sens;
                        float my = Input.GetAxis("Mouse Y") * 3f * sens;
                        // v1.8.0 (修复3): 俯仰增量预钳制 —— 快速甩鼠标/高灵敏
                        // 度时一帧增量可越过 ±90°, 越过垂直点后 forward 水平分量
                        // 反向, PovClampPitch 重建的 Atan2(f.x,f.z) 给出 yaw+180°
                        // = 「视角直接反转」。先把本帧俯仰变化截断在 ±89.9° 内
                        // （Atan2 恒在稳定区, 从结构上杜绝反转）。
                        // AngleAxis(θ, rightAx) 的俯仰变化 = +θ（水平右轴下精确）。
                        Vector3 fwdCur = s_povRot * new Vector3(0f, 0f, 1f);
                        float pyCur = -Mathf.Asin(Mathf.Clamp(fwdCur.y, -1f, 1f)) * Mathf.Rad2Deg;
                        float pyNew = Mathf.Clamp(pyCur - my, -89.9f, 89.9f);
                        float pitchDelta = pyNew - pyCur;
                        Vector3 rightAx = s_povRot * new Vector3(1f, 0f, 0f);
                        Quaternion yawRot = Quaternion.AngleAxis(mx, Vector3.up);
                        Quaternion pitchRot = Quaternion.AngleAxis(pitchDelta, rightAx);
                        s_povRot = yawRot * pitchRot * s_povRot;
                        PovClampPitch();
                    }
                }
            }

            // Vキー: 巡環切替（主人公→在场女仆→他男→…→退出）。
            // IMGUIテキスト入力中は無視＝他プラグインの検索ボックス等で
            // タイピングしているVを拾わない。
            if (!inPlay || object.ReferenceEquals(cfgPovKey, null) || GUIUtility.keyboardControl != 0)
            {
                return;
            }
            bool povKeyHit = false;
            try
            {
                povKeyHit = Input.GetKeyDown(cfgPovKey.Value);
            }
            catch (Exception)
            {
            }
            if (!povKeyHit)
            {
                return;
            }
            // 每回重新构建列表（3P等で途中参加/退出があるため）し、
            // 現在対象の次へ。不在なら先頭(主人公)へ。
            PovBuildList();
            if (s_povList.Count == 0)
            {
                PovExit();
                return;
            }
            int next = 0;
            if (s_povActive && s_povTarget != null)
            {
                next = s_povList.IndexOf(s_povTarget) + 1;
                if (next >= s_povList.Count)
                {
                    PovExit();
                    return;
                }
            }
            PovEnter(next);
        }

        // 主人公（男）の取得。GetMan(0) がゲーム全体の主人公枠。
        private static Maid GetProtagonistMan()
        {
            try
            {
                CharacterMgr cm = GameMain.Instance.CharacterMgr;
                if (cm != null && 0 < cm.GetManCount())
                {
                    Maid man = cm.GetMan(0);
                    if (man != null && man.body0 != null)
                    {
                        return man;
                    }
                }
            }
            catch (Exception e)
            {
                LogOnce("POV man: " + e.Message);
            }
            return null;
        }

        // 巡回列表: 主人公 → 在场女仆 → 他の在场男（全員適配 v1.6.1）。
        // 主人公は非表示でも採用（透明男でも主観は成立）；他は Visible 必須。
        private static void PovBuildList()
        {
            s_povList.Clear();
            Maid proto = GetProtagonistMan();
            if (proto != null)
            {
                s_povList.Add(proto);
            }
            try
            {
                CharacterMgr cm = GameMain.Instance.CharacterMgr;
                if (cm == null)
                {
                    return;
                }
                for (int i = 0; i < cm.GetMaidCount(); i++)
                {
                    Maid m = cm.GetMaid(i);
                    if (m != null && m.Visible && m.body0 != null)
                    {
                        s_povList.Add(m);
                    }
                }
                for (int i = 1; i < cm.GetManCount(); i++)
                {
                    Maid man = cm.GetMan(i);
                    if (man != null && man.Visible && man.body0 != null)
                    {
                        s_povList.Add(man);
                    }
                }
            }
            catch (Exception e)
            {
                LogOnce("POV list: " + e.Message);
            }
        }

        private static void PovEnter(int index)
        {
            Maid target = s_povList[index];
            if (target == null)
            {
                PovExit();
                return;
            }
            if (s_povActive)
            {
                // 切替: 前の対象の頭部を先に復帰
                PovRestoreHead();
                PovRestoreNeck(); // 内容39(v1.7.2): 颈骨scale也一并复原
            }
            else
            {
                // 初回进入時のみカメラ状態を保存（v1.6.3: 位姿も）
                try
                {
                    CameraMain cm = GameMain.Instance.MainCamera;
                    Camera cam = cm.camera;
                    s_povOrigFov = cam.fieldOfView;
                    s_povOrigNear = cam.nearClipPlane;
                    s_povOrigControl = cm.inputEnabled;
                    s_povOrigPos = cam.transform.position;
                    s_povOrigRot = cam.transform.rotation;
                    s_povCamSaved = true;
                    cm.inputEnabled = false; // プレイヤーのカメラ操作を無効化
                }
                catch (Exception e)
                {
                    LogOnce("POV enter: " + e.Message);
                }
            }
            s_povTarget = target;
            // v1.7.6(承接 v1.7.5): 四元数自由相机——进入时从头向初始化
            // 朝向（无滚转的 yaw/pitch 等价四元数）, 其后左右拖=纯水平
            // 旋转、上下拖=纯俯仰（世界轴）, 俯仰钳 ±89.9°（PovClampPitch,
            // 永不越过垂直, 无任何自动旋转）。
            Vector3 eyePos0;
            Quaternion headRot0;
            if (PovGetEye(out eyePos0, out headRot0))
            {
                Vector3 f = headRot0 * Vector3.forward;
                float yaw0 = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                float pitch0 = -Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
                s_povRot = Quaternion.Euler(pitch0, yaw0, 0f);
            }
            else
            {
                s_povRot = Quaternion.identity;
            }
            PovClampPitch();
            s_povGoneSince = -1f;
            s_povSmoothInit = false;
            s_povActive = true;
            // v1.7.7 (问题4): man 目标记录槽位 —— 男体 CRC 换体
            // （SwapNewManBody）会把活动槽位的 Maid 对象整个替换,
            // 0.25s 追扫时据此发现目标悬空并切换到新对象。maid 目标
            // 不跟踪（无对象级换体; npcmaid 槽位交换=换角色, 走
            // targetGone 自动退出）。
            s_povTargetSlot = (target.boMAN && target.ActiveSlotNo >= 0) ? target.ActiveSlotNo : -1;
            PovHideHead();
            PovHideNeck(); // 内容39(v1.7.2): 颈骨塌缩（头部上方全部收进锁骨）
            s_povNextRescan = Time.time + 0.25f;
        }

        private static void PovExit()
        {
            if (!s_povActive)
            {
                return;
            }
            try
            {
                PovRestoreHead();
                PovRestoreNeck(); // 内容39(v1.7.2): 颈骨scale复原
            }
            catch (Exception)
            {
            }
            try
            {
                if (s_povCamSaved)
                {
                    CameraMain cm = GameMain.Instance.MainCamera;
                    Camera cam = cm.camera;
                    cam.fieldOfView = s_povOrigFov;
                    cam.nearClipPlane = s_povOrigNear;
                    // v1.7.7 (问题4): 两条退出路径统一为官方标准取景——与
                    // CallFtFiles 尾部 cameraSet 同源（目标=当前全角色中心
                    // +0.8, 距离3, 俯仰角(180,11)）。还原到进入POV时的远古
                    // 位姿在多次切姿势后必然视角异常（v1.7.3 已在自动退出
                    // 修过, 本批把 V 键手动退出也统一）。
                    try
                    {
                        Vector3 allPos = GameMain.Instance.CharacterMgr.GetCharaAllPos();
                        cm.SetTargetPos(new Vector3(allPos.x, allPos.y + 0.8f, allPos.z));
                        cm.SetDistance(3f);
                        cm.SetAroundAngle(new Vector2(180f, 11f));
                    }
                    catch (Exception)
                    {
                        cam.transform.SetPositionAndRotation(s_povOrigPos, s_povOrigRot);
                    }
                    cm.inputEnabled = s_povOrigControl;
                }
            }
            catch (Exception e)
            {
                LogOnce("POV exit: " + e.Message);
            }
            s_povActive = false;
            s_povTarget = null;
            s_povTargetSlot = -1;
            s_povGoneSince = -1f;
            s_povSmoothInit = false;
            s_povCamSaved = false;
            s_povPoseDirty = false;
            s_povSettleSince = -1f;
        }

        // v1.7.6: POV 俯仰钳制（替代 v1.7.5 的 PovAutoLevel 自动回正）。
        // 用户实测反馈: 「到了特定的角度会旋转我的镜头（头晕）」——
        // 即翻越垂直时 AutoLevel 把颠倒朝向「滚转回正」的突发横滚。
        // 新方案: 俯仰永远钳在 ±89.9°（四元数重建）:
        //   - 永不越过垂直 → 颠倒区/水平反向区/极点退化区在结构上
        //     不存在, 拖拽恒为「世界Y纯水平 + 相机右轴纯俯仰」;
        //   - 不再有任何自动旋转——相机只在用户拖拽时变化;
        //   - 水平线恒水平（无滚转分量, 重建 Euler(x,y,0) 本身无滚转）。
        // 重建即规范化: Euler 顺序 Z→X→Y 下 x=俯仰、y=偏航、z=0。
        private static void PovClampPitch()
        {
            try
            {
                Vector3 f = s_povRot * new Vector3(0f, 0f, 1f);
                float py = -Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
                if (py > 89.9f || py < -89.9f)
                {
                    float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                    s_povRot = Quaternion.Euler((py > 0f) ? 89.9f : -89.9f, yaw, 0f);
                }
            }
            catch (Exception)
            {
                // 钳制绝不抛出（最坏=该帧不钳）
            }
        }

        // 頭部slot（attach骨が Bip01 Head / ManBip01 Head の物）の
        // レンダラーを enabled=false。元値は辞書に保持（純ランタイム）。
        // v1.6.0: マルチスロット子（goSlot[i,j] j>=1, レイヤー装備）も
        // 網羅——3.0身体の多重着せ替えで頭部アイテムが子側に載る事故対策。
        // スロット表は公式静的表 m_strDefSlotName を使用（ManBip 置換は
        // "Head" 部分文字列を保存、CRC表も頭部行は同一——2.0/3.0両対応）。
        private static void PovHideHead()
        {
            Maid m = s_povTarget;
            if (m == null || m.body0 == null)
            {
                return;
            }
            try
            {
                string[] defSlots = TBody.m_strDefSlotName;
                TBody.Slot slots = m.body0.goSlot;
                for (int i = 0; i * 3 + 2 < defSlots.Length; i++)
                {
                    if (defSlots[i * 3] == "end")
                    {
                        break;
                    }
                    if (defSlots[i * 3 + 1].IndexOf("Head") < 0)
                    {
                        continue;
                    }
                    int children = 1;
                    try
                    {
                        children = slots.CountChildren(i);
                    }
                    catch (Exception)
                    {
                        children = 1;
                    }
                    if (children < 1)
                    {
                        children = 1;
                    }
                    for (int j = 0; j < children; j++)
                    {
                        TBodySkin skin = null;
                        try
                        {
                            skin = slots[i, j];
                        }
                        catch (Exception)
                        {
                        }
                        if (skin == null || skin.obj == null)
                        {
                            continue;
                        }
                        Renderer[] rs = skin.obj.GetComponentsInChildren<Renderer>(true);
                        for (int r = 0; r < rs.Length; r++)
                        {
                            Renderer rend = rs[r];
                            if (rend == null)
                            {
                                continue;
                            }
                            if (!s_povHidden.ContainsKey(rend))
                            {
                                s_povHidden[rend] = rend.enabled;
                            }
                            rend.enabled = false;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogOnce("POV hide head: " + e.Message);
            }
        }

        private static void PovRestoreHead()
        {
            foreach (KeyValuePair<Renderer, bool> kv in s_povHidden)
            {
                Renderer r = kv.Key;
                if (r == null)
                {
                    continue; // 破棄済み（換装/退出済み）
                }
                try
                {
                    r.enabled = kv.Value;
                }
                catch (Exception)
                {
                }
            }
            s_povHidden.Clear();
        }

        // 内容39(v1.7.2): POV 隐藏脖子 —— 颈骨 localScale 塌缩到 0.01。
        // 颈部以上（头/头发/脸部——含男体身体网格内的脸）全部收进锁骨
        // 一点, 从眼睛位置往下看不再有脖子戳镜头。骨骼用官方缓存
        // TBody.trsNeck（2.0/3.0·男女全自动适配）。塌缩前先把眼位
        // 记录到颈父骨（上胸）系里（PovGetEye 用它复原相机原眼高）。
        // 幂等: 目标没变时不重复缩; 目标切换/退出时 PovRestoreNeck 复原。
        private static void PovHideNeck()
        {
            try
            {
                Maid m = s_povTarget;
                if (m == null || m.body0 == null)
                {
                    return;
                }
                Transform neck = m.body0.trsNeck;
                if (neck == null)
                {
                    return;
                }
                // v1.7.3 (issue 4): 幂等检查升级——不仅要求书签目标一致,
                // 还要求颈骨「实际」仍处于塌缩态。换装/姿势加载会重建
                // 骨骼（新骨 scale=1）, 旧实现早退导致脖子复现戳镜头;
                // 现在检测到未塌缩即自动重捕获+重塌缩（自愈）。
                bool shrunk = Mathf.Abs(neck.localScale.x - PovNeckShrink.x) < 0.001f
                    && Mathf.Abs(neck.localScale.y - PovNeckShrink.y) < 0.001f
                    && Mathf.Abs(neck.localScale.z - PovNeckShrink.z) < 0.001f;
                if (shrunk && s_povNeckTarget == m)
                {
                    return; // already shrunk for this target (fast path)
                }
                Transform parent = neck.parent;
                if (parent != null)
                {
                    // capture the real eye position in chest-bone space BEFORE
                    // the shrink collapses the eye/head bones. 重锚路径先经
                    // PovRestoreNeck（书签已清）→ 此处 PovGetEye 走真实
                    // 眼骨中点; 骨骼重建后同样取到新骨架的真实眼位。
                    Vector3 ep;
                    Quaternion hr;
                    if (PovGetEye(out ep, out hr))
                    {
                        s_povNeckParentBone = parent;
                        s_povEyeFromParent = parent.InverseTransformPoint(ep);
                    }
                }
                if (!shrunk)
                {
                    s_povNeckOrigScale = neck.localScale;
                }
                else if (s_povNeckTarget != m)
                {
                    // shrunk bone but bookkeeping lost (edge): unit default
                    s_povNeckOrigScale = Vector3.one;
                }
                s_povNeckTarget = m;
                neck.localScale = PovNeckShrink;
            }
            catch (Exception e)
            {
                LogOnce("POV hide neck: " + e.Message);
            }
        }

        private static void PovRestoreNeck()
        {
            try
            {
                if (object.ReferenceEquals(s_povNeckTarget, null))
                {
                    return;
                }
                Maid m = s_povNeckTarget;
                s_povNeckTarget = null;
                s_povNeckParentBone = null;
                if (m == null || m.body0 == null)
                {
                    return; // body disposed; nothing to restore
                }
                Transform neck = m.body0.trsNeck;
                if (neck != null)
                {
                    neck.localScale = s_povNeckOrigScale;
                }
            }
            catch (Exception)
            {
                s_povNeckTarget = null;
                s_povNeckParentBone = null;
            }
        }

        // =================================================================
        // v1.7.8: PovCullSelf（POV 自身整体剔除, v1.7.6）/ PovRestoreSelf /
        // PovChinkoRoots（v1.7.7 男器豁免）已按用户指示整体移除
        // （「删除：POV中隐藏自身（男器除外），保留PovNearClip」）。
        // POV 的视觉处理保留两条: PovHideHead（头部槽位渲染器隐藏）+
        // PovHideNeck（颈骨塌缩, 内容39）。剔除相关的 0.25s 全层级
        // 渲染器深扫（GetComponentsInChildren(true), POV 中的周期性
        // 卡顿源之一）随之消失。
        // =================================================================

        // 目位置と頭部回転（v1.6.5 = v1.6.0 方案回退）。
        // 頭部骨は公式キャッシュ TBody.trsHead（2.0/3.0自動適応——
        // 非CRC男=ManBip01 Head / CRC身体=男女とも Bip01 Head を
        // ゲーム側が版本判定済み。骨骼名ハードコード禁止）。
        // 眼位置は三级回退: Eyepos_L/R 中点 → trsEyeL/R（顔内眼球骨）
        // → 頭骨オフセット——男女·2.0/3.0 全組み合わせ対応。
        private static bool PovGetEye(out Vector3 pos, out Quaternion headRot)
        {
            pos = Vector3.zero;
            headRot = Quaternion.identity;
            Maid m = s_povTarget;
            if (m == null || m.body0 == null)
            {
                return false;
            }
            Transform head = m.body0.trsHead;
            if (head == null)
            {
                return false;
            }
            headRot = head.rotation;
            // 内容39(v1.7.2): 颈骨塌缩后眼/头骨已收进锁骨 —— 相机锚点
            // 改用颈父骨（上胸）+ 进入时捕获的局部眼位偏移（呼吸/前倾
            // 跟随正确, 头部转动不再带动锚点）。头部旋转不受缩放影响
            // 仍然取 trsHead.rotation。
            if (!object.ReferenceEquals(s_povNeckTarget, null) && s_povNeckTarget == m
                && s_povNeckParentBone != null)
            {
                pos = s_povNeckParentBone.TransformPoint(s_povEyeFromParent);
                return true;
            }
            Transform eyeL = m.body0.GetBone("Eyepos_L");
            Transform eyeR = m.body0.GetBone("Eyepos_R");
            if (eyeL != null && eyeR != null)
            {
                pos = Vector3.Lerp(eyeL.position, eyeR.position, 0.5f);
                return true;
            }
            if (m.body0.trsEyeL != null && m.body0.trsEyeR != null)
            {
                pos = Vector3.Lerp(m.body0.trsEyeL.position, m.body0.trsEyeR.position, 0.5f);
                return true;
            }
            pos = head.position + headRot * new Vector3(0f, 0.03f, 0.06f);
            return true;
        }

        // =================================================================
        // 内容28/29(v1.6.0): 女仆头/眼朝向镜头。
        // 公式 EyeToCamera(EyeMoveType) と同一経路（TBody の
        // trsLookTarget + boHeadToCam/boEyeToCam）。KAG側の視線指定
        // （@lookat 等のスクリプト指定）を毎フレーム上書きするのが仕様。
        // 両方OFF時・画面離脱時は EyeToReset(0.5f) で原版へ滑らか復帰
        // （フェード速度 1/0.5=2/s、頭は0.5秒かけて戻る）。
        // 男には眼球追従が存在しないため（TBody L5583: boMAN は
        // trsEyeL/R 未キャッシュ）、対象は女仆のみ。
        // =================================================================
        private static void UpdateLookAtCamera(bool inPlay)
        {
            // v1.6.2: 夜伽Play版(F7)と全局版(F1)は独立スイッチでOR合成
            bool headOn = (!object.ReferenceEquals(cfgLookHead, null) && cfgLookHead.Value && inPlay)
                || (!object.ReferenceEquals(cfgLookHeadGlobal, null) && cfgLookHeadGlobal.Value);
            bool eyeOn = (!object.ReferenceEquals(cfgLookEye, null) && cfgLookEye.Value && inPlay)
                || (!object.ReferenceEquals(cfgLookEyeGlobal, null) && cfgLookEyeGlobal.Value);
            bool want = headOn || eyeOn;
            s_lookHeadOn = headOn;   // v1.7.5: preCull 直连通道复用（含门控）
            s_lookEyeOn = eyeOn;
            if (want)
            {
                try
                {
                    CameraMain cm = GameMain.Instance.MainCamera;
                    CharacterMgr mgr = GameMain.Instance.CharacterMgr;
                    if (cm == null || mgr == null)
                    {
                        return;
                    }
                    List<Maid> current = new List<Maid>();
                    // v1.7.5: 看镜头目标 = 真实渲染相机（cm.camera）而非相机
                    // rig 根（cm.transform = 环绕枢轴, 在 POV 时与实际视点相距
                    // 甚远 → 旧实现「POV下不起作用/方向怪」的直接原因）。
                    // POV 写的正是 cm.camera.transform, 因此普通视角与 POV
                    // 都看向玩家实际所在位置。
                    Transform camTf = cm.camera.transform;
                    for (int i = 0; i < mgr.GetMaidCount(); i++)
                    {
                        Maid m = mgr.GetMaid(i);
                        if (m == null || m.boMAN || !m.Visible || m.body0 == null)
                        {
                            continue;
                        }
                        // POV対象自身は除外（自分の眼位置を見ようとして
                        // ゼロベクトル追従でジッターするのを防止）
                        if (s_povActive && m == s_povTarget)
                        {
                            continue;
                        }
                        m.body0.trsLookTarget = camTf;
                        // v1.7.5: 官方层的 +1m 上偏移是为「枢轴在地面」的
                        // 相机根设计的; 目标改为真实相机后不再需要 → 清零
                        m.body0.offsetLookTarget = Vector3.zero;
                        m.body0.boHeadToCam = headOn;
                        m.body0.boEyeToCam = eyeOn;
                        m.body0.boEyeSorashi = false;
                        current.Add(m);
                    }
                    // v1.6.2: 前回強制→今回対象外（退場/非表示/POV対象化）
                    // の女仆は即 EyeToReset——退場後もカメラを見続ける
                    // 残留（「遗留在场」）を防止
                    for (int j = s_lookEnforced.Count - 1; j >= 0; j--)
                    {
                        Maid prev = s_lookEnforced[j];
                        if (prev == null)
                        {
                            continue;
                        }
                        if (!current.Contains(prev))
                        {
                            try
                            {
                                prev.EyeToReset(0.5f);
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }
                    s_lookEnforced = current;
                    s_lookApplied = true;
                }
                catch (Exception e)
                {
                    LogOnce("lookcam: " + e.Message);
                }
            }
            else if (s_lookApplied)
            {
                ResetLookAtCamera();
            }
        }

        private static void ResetLookAtCamera()
        {
            s_lookApplied = false;
            s_lookEnforced.Clear();
            s_lookHeadOn = false;
            s_lookEyeOn = false;
            // v1.7.5: 清理已销毁女仆的瞄准缓存
            try
            {
                List<Maid> dead = null;
                foreach (KeyValuePair<Maid, LookAimCache> kv in s_lookAimCache)
                {
                    if (kv.Key == null)
                    {
                        if (dead == null) { dead = new List<Maid>(); }
                        dead.Add(kv.Key);
                    }
                }
                if (dead != null)
                {
                    for (int i = 0; i < dead.Count; i++) { s_lookAimCache.Remove(dead[i]); }
                }
            }
            catch (Exception)
            {
            }
            try
            {
                CharacterMgr mgr = GameMain.Instance.CharacterMgr;
                if (mgr == null)
                {
                    return;
                }
                for (int i = 0; i < mgr.GetMaidCount(); i++)
                {
                    Maid m = mgr.GetMaid(i);
                    if (m == null || m.boMAN)
                    {
                        continue;
                    }
                    try
                    {
                        // v1.7.5: 复位官方 +1m 上偏移（清零是本插件强制期专用的）
                        if (m.body0 != null)
                        {
                            m.body0.offsetLookTarget = new Vector3(0f, 1f, 0f);
                        }
                        m.EyeToReset(0.5f);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        // =================================================================
        // v1.7.5 (看镜头升级): 直连瞄准通道 —— 官方 HeadToCam/EyeToCam 的
        // 输出是「40%/20% 增益 + ±60° 入锥门控」的弱朝向, 且 POV 下旧目标
        // （相机 rig 枹点）离实际视点甚远 → 「POV下不起作用/范围太小」。
        // 本通道在 onPreCull（所有 LateUpdate 之后、渲染之前）对已强制名单
        // 追加直写。
        // v1.7.6 回退「范围扩大」: 增益/锥角全部对齐官方值（用户实测
        // v1.7.5 的 ±80°/全增益让女仆转头过度不自然）:
        //   头: 颈空间 SetFromToRotation(up, 方向) 的欧拉角, 钳制
        //       x∈(-60,60)/z∈(-40,50)（官方入锥同值）,
        //       0.4×增益（官方 HeadToCam 同值）, 指数趋近 dt*10。
        //   眼: 头空间方向 → (x,z) 角, 0.2/0.1 增益（官方 EyeToCam
        //       同值, 官方本无钳制——保留宽松安全钳 ±60 防欧拉回绕）,
        //       含 m_editYorime 寄与。
        // 动画采样每帧重写骨骼基础旋转 → 官方 LateUpdate 弱 slerp → 本通道
        // 直写 → 渲染: 本通道最后写入即最终画面。POV 自身/上锁/そらし目
        // 按官方语义跳过。POV 时目标即玩家视点 —— 「她看向你的眼睛」。
        // =================================================================
        private sealed class LookAimCache
        {
            public Transform trsHead;
            public Quaternion defHead;
            public Quaternion defEyeL;
            public Quaternion defEyeR;
        }
        private static readonly Dictionary<Maid, LookAimCache> s_lookAimCache =
            new Dictionary<Maid, LookAimCache>();

        private static LookAimCache GetLookAimCache(Maid m)
        {
            LookAimCache c;
            if (!s_lookAimCache.TryGetValue(m, out c) || object.ReferenceEquals(c, null)
                || !object.ReferenceEquals(c.trsHead, m.body0.trsHead))
            {
                // 骨骼重建（换装/身体重建）会生成新的头骨 → 以 trsHead
                // 实例变化为信号重新捕获默认姿态
                c = new LookAimCache();
                c.trsHead = m.body0.trsHead;
                c.defHead = m.body0.quaDefHead;
                c.defEyeL = m.body0.quaDefEyeL;
                c.defEyeR = m.body0.quaDefEyeR;
                s_lookAimCache[m] = c;
            }
            return c;
        }

        private static void LookAimPass(Camera cam, bool headOn, bool eyeOn)
        {
            try
            {
                if (s_lookEnforced.Count == 0)
                {
                    return;
                }
                Vector3 target = cam.transform.position;
                float dt = Time.deltaTime;
                if (dt <= 0f)
                {
                    dt = 0.016f;
                }
                float k = 1f - Mathf.Exp(-10f * dt);
                for (int i = 0; i < s_lookEnforced.Count; i++)
                {
                    Maid m = s_lookEnforced[i];
                    if (m == null || m.body0 == null || m.body0.trsNeck == null
                        || m.body0.trsHead == null)
                    {
                        continue;
                    }
                    if (m.body0.boLockHeadAndEye)
                    {
                        continue; // 官方锁定: 头眼均不介入
                    }
                    LookAimCache c = GetLookAimCache(m);
                    if (headOn)
                    {
                        // 头: 颈空间方向（与官方 HeadToCam 同一数学;
                        // v1.7.6 增益/锥角回退官方值 0.4×/±60/-40..50）
                        Vector3 dir = target - m.body0.trsNeck.position;
                        Vector3 local = Quaternion.Inverse(m.body0.trsNeck.rotation) * dir;
                        Quaternion q = Quaternion.identity;
                        q.SetFromToRotation(Vector3.up, local);
                        Vector3 e = q.eulerAngles;
                        if (e.x >= 180f) { e.x -= 360f; }
                        if (e.z >= 180f) { e.z -= 360f; }
                        e.x = Mathf.Clamp(e.x, -60f, 60f);
                        e.z = Mathf.Clamp(e.z, -40f, 50f);
                        Quaternion aim = c.defHead * Quaternion.Euler(e.x * 0.4f, 0f, e.z * 0.4f);
                        m.body0.trsHead.localRotation = Quaternion.Slerp(
                            m.body0.trsHead.localRotation, aim, k);
                    }
                    if (eyeOn && !m.body0.boEyeSorashi
                        && m.body0.trsEyeL != null && m.body0.trsEyeR != null)
                    {
                        // 眼: 头空间方向 → 眼球偏航/俯仰（v1.7.6 增益回退
                        // 官方值 0.2/0.1; 官方无钳制, 留 ±60 安全钳防回绕）
                        Vector3 dirH = target - m.body0.trsHead.position;
                        Vector3 localH = Quaternion.Inverse(m.body0.trsHead.rotation) * dirH;
                        Quaternion q2 = Quaternion.identity;
                        q2.SetFromToRotation(Vector3.up, localH);
                        Vector3 e2 = q2.eulerAngles;
                        if (e2.x >= 180f) { e2.x -= 360f; }
                        if (e2.z >= 180f) { e2.z -= 360f; }
                        e2.x = Mathf.Clamp(e2.x, -60f, 60f);
                        e2.z = Mathf.Clamp(e2.z, -60f, 60f);
                        float yorime = m.body0.m_editYorime;
                        Quaternion aimL = c.defEyeL * Quaternion.Euler(0f, -e2.x * 0.2f + yorime, -e2.z * 0.1f);
                        Quaternion aimR = c.defEyeR * Quaternion.Euler(0f, e2.x * 0.2f + yorime, e2.z * 0.1f);
                        m.body0.trsEyeL.localRotation = Quaternion.Slerp(
                            m.body0.trsEyeL.localRotation, aimL, k);
                        m.body0.trsEyeR.localRotation = Quaternion.Slerp(
                            m.body0.trsEyeR.localRotation, aimR, k);
                    }
                }
            }
            catch (Exception e)
            {
                LogOnce("lookaim: " + e.Message);
            }
        }

        // レンダリング直前の最終カメラ書き込み（static・全フレーム）。
        // KAG演出カメラより後に走るのでPOVが常に勝つ。ゲームのカメラ状態
        // （transform以外の内部値）には触らない。
        private static void OnCameraPreCull(Camera cam)
        {
            try
            {
                GameMain gm = GameMain.Instance;
                if (gm == null)
                {
                    return;
                }
                CameraMain cm = gm.MainCamera;
                if (cm == null || cam != cm.camera)
                {
                    return;
                }
                // v1.7.5: 看镜头直连瞄准通道（所有 LateUpdate 之后、渲染之前
                // ——官方 HeadToCam 弱层之上再叠加全增益直写）。POV 与普通
                // 视角共用（目标=本相机=玩家实际视点）。开关状态沿用
                // UpdateLookAtCamera 本帧计算（含夜伽 inPlay 门控）。
                if (s_lookApplied && s_lookEnforced.Count > 0 && (s_lookHeadOn || s_lookEyeOn))
                {
                    LookAimPass(cam, s_lookHeadOn, s_lookEyeOn);
                }
                if (!s_povActive || s_povTarget == null)
                {
                    return;
                }
                Vector3 eyePos;
                Quaternion headRot;
                if (!PovGetEye(out eyePos, out headRot))
                {
                    return;
                }
                float forward = (!object.ReferenceEquals(cfgPovForward, null)) ? cfgPovForward.Value : 0f;
                float up = (!object.ReferenceEquals(cfgPovUp, null)) ? cfgPovUp.Value : 0f;
                // v1.7.6: 前方偏移沿【当前视线方向】（看哪推哪）, 上下偏移用
                // 【世界竖直】。旧实现挂在头骨局部系（headRot*(0,up,forward)）
                // ——头一转向/姿势里头骨轴与世界错开, 「前方」跟着头骨跑,
                // 观感上变成左右偏移（用户实测报告）。
                Vector3 targetPos = eyePos + s_povRot * new Vector3(0f, 0f, forward)
                    + new Vector3(0f, up, 0f);
                // 位置平滑（指数補間。眼位跟随の抖动を吸収したい場合は上げる）
                float dt = Time.deltaTime;
                if (dt <= 0f)
                {
                    dt = 0.016f;
                }
                float smooth = (!object.ReferenceEquals(cfgPovSmooth, null)) ? cfgPovSmooth.Value : 0.65f;
                float followSpeed = Mathf.Lerp(70f, 1f, smooth);
                if (!s_povSmoothInit)
                {
                    s_povSmoothPos = targetPos;
                    s_povSmoothInit = true;
                }
                else
                {
                    s_povSmoothPos = Vector3.Lerp(s_povSmoothPos, targetPos, 1f - Mathf.Exp(-followSpeed * dt));
                }
                // v1.7.5: 相机朝向 = 四元数自由相机（拖拽在 UpdatePovInput,
                // 回正 PovClampPitch 在拖拽后维护; 这里只写入最终朝向）
                cam.transform.SetPositionAndRotation(s_povSmoothPos, s_povRot);
                if (!object.ReferenceEquals(cfgPovFov, null))
                {
                    cam.fieldOfView = cfgPovFov.Value;
                }
                if (!object.ReferenceEquals(cfgPovNear, null))
                {
                    cam.nearClipPlane = cfgPovNear.Value;
                }
            }
            catch (Exception)
            {
                // onPreCull内では例外を絶対に出さない
            }
        }

        // =================================================================
        // P24 prefix (v1.4.0 内容21 / v1.4.1 内容24): UnityEngine.Debug.
        // LogError —— 夜伽中の欠損OBJ/未検出系エラーの自動検出抑止 +
        // スタック無しNPEの診断（呼出スタック付きWarningへ変換）。
        // OBJ/.menu 等が無い場合は「存在しないデータ」なので原版動作
        // （スキップして続行）はそのまま、ログだけ整理する。
        // 対象: "not found file"（ScriptManager系）/ "〜が見つかりません" /
        // "〜を読み込めませんでした" / NPE標準メッセージ（無スタック）。
        // 非夜伽・非該当は原版のまま。
        // 両オーバーロード (object)/(object,Object) に名前注入で共通適用。
        // =================================================================
        public static bool LogError_Filter(object message)
        {
            if (object.ReferenceEquals(cfgSafeScript, null) || !cfgSafeScript.Value)
            {
                return true;
            }
            try
            {
                if (YotogiManager.instans == null)
                {
                    return true; // 夜伽中以外は原版のまま
                }
                string text = message as string;
                if (string.IsNullOrEmpty(text))
                {
                    return true;
                }
                // 内容24(v1.4.1): スタック無しのNPEメッセージ（EventDelegate.
                // Execute 等の catch{LogError(ex.Message)} チャネル由来）を
                // 検出したら、現在の呼出スタック（＝吞んだcatch位置）を
                // 付けた Warning 1回に変換して確定修正の材料を出す。
                if (text == "Object reference not set to an instance of an object")
                {
                    ReportNPE();
                    return false;
                }
                if (!IsMissingAssetError(text))
                {
                    return true;
                }
                ReportMissingScript("[Asset] " + text);
                return false; // LogError を抑制（Info 1回のみ残る）
            }
            catch (Exception)
            {
                return true;
            }
        }

        // 内容24(v1.4.1): NPE診断 —— 最大5回まで、呼出スタック付きで
        // Warning 出力（吞んだcatch位置とNPE発生源への手がかりが現れる。
        // 複数の発生源がある場合も拾えるよう回数制限で刷屏を防ぐ）。
        private static int s_npeDiagCount;

        private static void ReportNPE()
        {
            try
            {
                if (s_npeDiagCount < 5)
                {
                    s_npeDiagCount++;
                    if (!object.ReferenceEquals(log, null))
                    {
                        log.LogWarning("YotogiUnlimited [NPE-diag #" + s_npeDiagCount + "]: swallowed NPE detected. Call stack:\n"
                            + System.Environment.StackTrace);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private static bool IsMissingAssetError(string text)
        {
            if (text.IndexOf("not found file") >= 0)
            {
                return true;
            }
            if (text.IndexOf("が見つかりません") >= 0)
            {
                return true;
            }
            if (text.IndexOf("が見つからなかった") >= 0)
            {
                return true;
            }
            if (text.IndexOf("を読み込めません") >= 0)
            {
                return true;
            }
            // v1.6.2: 骨未ロードの男スロットへKAG側がアニメ操作した際の
            // 無害ノイズ（GetAnimation/StopAnime系）。本体は継続動作する。
            if (text.IndexOf("未だキャラがロード") >= 0)
            {
                return true;
            }
            return false;
        }

        // =================================================================
        // P25 (v1.6.4 内容30): MotionKagManager.LoadScriptFile null安全化。
        // 官方边界缺陷: @MotionScript 未指定 guid 时，方法内的回退搜索
        // 循环（maid×18 / man×7 硬编码槽位）对 GetMaid/GetMan 的 null
        // 返回值（越界/空槽）直接 .Visible —— ADV 動作脚本 NPE 中断。
        //   prefix    = 主角字段为 null 时 null 安全预解析（可见且身体
        //               已加载；与官方回退意图一致但防爆）
        //   finalizer = 残余 NPE 吞掉 + Warning 单条（该动作跳过、
        //               ADV 脚本继续）。非 NPE 异常原样传播。
        // =================================================================
        private static void MotionLoad_Prefix(MotionKagManager __instance)
        {
            try
            {
                GameMain gm = GameMain.Instance;
                if (gm == null)
                {
                    return;
                }
                CharacterMgr cm = gm.CharacterMgr;
                if (cm == null)
                {
                    return;
                }
                // main_maid_ 未解析 → null 安全地找「可见且身体已加载」的女仆
                if (!object.ReferenceEquals(fiMotionMainMaid, null))
                {
                    Maid cur = fiMotionMainMaid.GetValue(__instance) as Maid;
                    if (cur == null)
                    {
                        for (int i = 0; i < cm.GetMaidCount(); i++)
                        {
                            Maid m = cm.GetMaid(i);
                            if (m != null && m.Visible && m.body0 != null && m.body0.isLoadedBody)
                            {
                                fiMotionMainMaid.SetValue(__instance, m);
                                break;
                            }
                        }
                    }
                }
                // main_man_ 未解析 → 可见优先，其次身体已加载（官方回退同序）
                if (!object.ReferenceEquals(fiMotionMainMan, null))
                {
                    Maid cur2 = fiMotionMainMan.GetValue(__instance) as Maid;
                    if (cur2 == null)
                    {
                        Maid best = null;
                        for (int i = 0; i < cm.GetManCount(); i++)
                        {
                            Maid man = cm.GetMan(i);
                            if (man == null || man.body0 == null || !man.body0.isLoadedBody)
                            {
                                continue;
                            }
                            if (man.Visible)
                            {
                                best = man;
                                break;
                            }
                            if (best == null)
                            {
                                best = man;
                            }
                        }
                        if (best != null)
                        {
                            fiMotionMainMan.SetValue(__instance, best);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private static void MotionLoad_Finalizer(ref Exception __exception, string fileName)
        {
            if (__exception == null)
            {
                return;
            }
            if (!(__exception is NullReferenceException))
            {
                return;
            }
            try
            {
                if (s_reportedMissing.Add("[MotionNPE] " + fileName))
                {
                    if (!object.ReferenceEquals(log, null))
                    {
                        log.LogWarning("YotogiUnlimited: motion script skipped (null character, ADV continues): " + fileName);
                    }
                }
            }
            catch (Exception)
            {
            }
            __exception = null;
        }

        // =================================================================
        // P26 (v1.6.6): YotogiPlayManager.Suspend 旧模式守卫（黑屏根治）。
        // 官方边界缺陷: OnClickCommand 的 adv_hook 分支硬编码
        // Suspend(cmd, "*サスペンド")，淡出后 JumpLabel 落在 adv_kag
        // 当前调度器文件——*サスペンド 标签只存在于新模式内容包调度器
        // （av001_main.ks 等，标签与 hook 设置者同文件），旧模式调度器
        // （YotogiMain.ks / yotogimaincbl.ks / yotogionlymain.ks）没有
        // → KAG 调度脚本死亡 → 永久黑屏。原版普通夜伽选不到带
        // adv_hook 的命令（AV/GP 包技能），本插件的全解锁特性让其
        // 流入普通夜伽才踩中。游戏自己的 OnClickNext 已按
        // is_new_yotogi_mode 分流，adv_hook 路径漏了同一门槛。
        //   prefix = 非新夜伽模式时跳过原版挂起（状态掩码/淡出/致命
        //            跳转全部不发生）并代行重入 OnClickCommand（同一
        //            命令）——首次点击时 calledAdvFiles 已记录该 ADV
        //            演出 → 重入后 flag=false → 命令正常执行。用户体验
        //            =第一次点击直接生效，仅跳过那场本就无法播出的
        //            ADV 插入演出（重入路径=用户第二次点击的原版路径）。
        //   护栏   = s_suspendReentry: 异常事態下重入的 OnClickCommand
        //            再次走到 Suspend 时只中止挂起、不再代行（防无限
        //            递归）。
        // 新模式（is_new_yotogi_mode=true，含 AV/GP 会话）原版放行——
        // 其调度器带标签，挂起正常工作。判定手段缺失/异常时保守放行
        // （旧模式黑屏只是本就异常的 adv_hook 路径，新模式挂起是
        // 核心功能，不可误杀）。
        // =================================================================
        private static bool Suspend_Prefix(YotogiPlayManager __instance,
            Skill.Data.Command.Data suspendCommandData)
        {
            try
            {
                // 判定/代行手段缺失（加载时已报 MISSING REFLECTION）→ 原版放行
                if (object.ReferenceEquals(piIsNewYotogiMode, null)
                    || object.ReferenceEquals(miOnClickCommand, null))
                {
                    return true;
                }
                object v = piIsNewYotogiMode.GetValue(__instance, null);
                if (!(v is bool) || (bool)v)
                {
                    return true; // 新夜伽/AV/GP 会话: 原版放行（调度器带标签）
                }
            }
            catch (Exception e)
            {
                LogOnce("P26 mode check: " + e.Message);
                return true; // 判定异常 → 保守放行原版
            }
            // ---- 旧模式: 挂起=必黑屏，跳过并代行 ----
            // 病理重入护栏: 代行的 OnClickCommand 若再次走到 Suspend
            // （hook 每次设置不同 file/label 等），只中止挂起、不再代行。
            if (s_suspendReentry)
            {
                return false;
            }
            s_suspendReentry = true;
            try
            {
                if (!object.ReferenceEquals(log, null))
                {
                    log.LogInfo("YotogiUnlimited: old-mode suspend skipped (adv hook not playable in old scheduler; command re-dispatched)");
                }
                if (!object.ReferenceEquals(suspendCommandData, null))
                {
                    miOnClickCommand.Invoke(__instance, new object[] { suspendCommandData });
                }
            }
            catch (Exception e)
            {
                // 代行异常: 挂起仍跳过（旧模式挂起=必黑屏）；
                // 用户再点一次即走原版直行（calledAdvFiles 已记录）。
                LogOnce("P26 re-dispatch: " + e.Message);
            }
            finally
            {
                s_suspendReentry = false;
            }
            return false;
        }

        // =================================================================
        // 内容35(v1.7.0): Tab 热键隐藏UI —— ym.uiVisible 官方整体面板
        // 开关（官方挂起同款通道，含指令菜单/塔/参数条）每帧维持；
        // 消息窗口（独立 UI 根）节流关闭。恢复为一-shot（不与官方
        // 挂起流互抢 true 状态）。仅 Play 画面。
        // =================================================================
        private static void UpdateUiHide(bool inPlay, YotogiManager ym)
        {
            try
            {
                if (object.ReferenceEquals(cfgUiHide, null) || object.ReferenceEquals(cfgUiHideKey, null))
                {
                    return;
                }
                if (inPlay && cfgUiHide.Value && GUIUtility.keyboardControl == 0)
                {
                    try
                    {
                        if (Input.GetKeyDown(cfgUiHideKey.Value))
                        {
                            s_uiHidden = !s_uiHidden;
                            if (!s_uiHidden && ym != null)
                            {
                                ym.uiVisible = true; // 一-shot 恢复
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
                if (!inPlay || !cfgUiHide.Value)
                {
                    if (s_uiHidden)
                    {
                        s_uiHidden = false;
                        if (ym != null)
                        {
                            try { ym.uiVisible = true; } catch (Exception) { }
                        }
                    }
                    return;
                }
                if (s_uiHidden && ym != null)
                {
                    try { ym.uiVisible = false; } catch (Exception) { }
                    if (Time.time >= s_uiMsgCloseNext)
                    {
                        s_uiMsgCloseNext = Time.time + 0.5f;
                        try { ym.messageWindowVisible = false; } catch (Exception) { }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        // =================================================================
        // 内容33(v1.7.0): 姿势一览 —— 列表构建（全表+同名去重，
        // 习得变体优先）+ 局内实时切换。
        // =================================================================
        private static void BuildSkillBrowserList()
        {
            s_skillList.Clear();
            YotogiManager ym = YotogiManager.instans;
            if (ym == null || ym.maid == null || ym.maid.status == null)
            {
                return;
            }
            Maid maid = ym.maid;
            Dictionary<string, Skill.Data> byName = new Dictionary<string, Skill.Data>();
            Dictionary<string, bool> learned = new Dictionary<string, bool>();
            SortedDictionary<int, Skill.Data>[] cats = Skill.skill_data_list;
            if (cats == null)
            {
                return;
            }
            for (int c = 0; c < cats.Length; c++)
            {
                if (cats[c] == null)
                {
                    continue;
                }
                foreach (Skill.Data sd in cats[c].Values)
                {
                    if (sd == null || string.IsNullOrEmpty(sd.name))
                    {
                        continue;
                    }
                    bool isLearned = false;
                    try
                    {
                        isLearned = (maid.status.yotogiSkill.Get(sd.id) != null);
                    }
                    catch (Exception)
                    {
                    }
                    bool prevLearned;
                    if (!learned.TryGetValue(sd.name, out prevLearned))
                    {
                        byName[sd.name] = sd;
                        learned[sd.name] = isLearned;
                    }
                    else if (!prevLearned && isLearned)
                    {
                        byName[sd.name] = sd; // 习得变体优先（与选择屏去重同则）
                        learned[sd.name] = true;
                    }
                }
            }
            s_skillList.AddRange(byName.Values);
            s_skillList.Sort(delegate(Skill.Data a, Skill.Data b)
            {
                return string.Compare(a.name, b.name);
            });
        }

        private static void BrowserSwitchSkill(Skill.Data sd)
        {
            try
            {
                YotogiManager ym = YotogiManager.instans;
                if (ym == null || sd == null)
                {
                    return;
                }
                if (ym.IsAllCharaBusy())
                {
                    return;
                }
                YotogiManager.PlayingSkillData[] arr = ym.play_skill_array;
                if (arr == null || arr.Length == 0)
                {
                    return;
                }
                YotogiPlayManager pm = ym.play_mgr;
                if (pm == null)
                {
                    return;
                }
                int k = -1;
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i] != null && arr[i].skill_pair != null && arr[i].skill_pair.base_data != null
                        && arr[i].skill_pair.base_data.id == sd.id)
                    {
                        k = i;
                        break;
                    }
                }
                int curNo = (int)fiPlayingSkillNo.GetValue(pm);
                if (k == -1)
                {
                    // 不在数组: 官方追加（P20 兜底未习得）→ 切至末位
                    ym.AddPlaySkill(sd);
                    arr = ym.play_skill_array;
                    k = arr.Length - 1;
                    if (arr[k] == null || arr[k].skill_pair == null || arr[k].skill_pair.base_data == null)
                    {
                        return; // 追加失败
                    }
                }
                if (k == curNo)
                {
                    return; // 当前播放: 跳过（与塔行为一致）
                }
                // v1.7.3 (问题3): 3P系姿势且副女仆不在场 → 介入官方追加女仆
                // 选择（预瞄索引; 选完 OK 后 P35 重定向回 Play 屏, NextSkill
                // 的 ++ 恰好落在本姿势上）。俱乐部可选不足则放行单人播放
                // （P33/P34 防护下无 NRE）。
                if (sd.player_num > 1 && CountMissingSubMaids(sd) > 0
                    && TryOpenSubCharaSelect(ym, sd, curNo))
                {
                    if (k == 0)
                    {
                        s_forceSkillZero = true;
                        fiPlayingSkillNo.SetValue(pm, 0);
                    }
                    else
                    {
                        fiPlayingSkillNo.SetValue(pm, k - 1);
                    }
                    return; // 等待选人后 Play 重入完成切换
                }
                if (k == 0)
                {
                    s_forceSkillZero = true;
                    fiPlayingSkillNo.SetValue(pm, 0);
                }
                else
                {
                    fiPlayingSkillNo.SetValue(pm, k - 1);
                }
                miOnNextSkillMove.Invoke(pm, null);
            }
            catch (Exception e)
            {
                LogOnce("BrowserSwitchSkill: " + e.Message);
            }
        }

        // =================================================================
        // P30 v2 (v1.7.2 #37): pose resource/prop auto-mount, EVENT-DRIVEN.
        // Native pose props = @AddPrefabBg mounts inside FT files
        // (start_call_file), positioned relative to the stage-branch
        // anchor (@AllPos). The whole setup segment (stage @if branch,
        // @AllPos anchor, @AddPrefabBg mounts) executes SYNCHRONOUSLY
        // inside YotogiPlayManager.CallNormalFile (proven by the NRE
        // stack: LoadYotogiScript -> Exec -> tag callbacks), so by the
        // time our postfix runs the official props for covered stages
        // are already mounted and the anchor is final.
        // Table: full script-arc scan (generic ft_*.ks + personality
        // variants *_ft_*.ks, 12025 FT files, SJIS-decoded) keyed by
        // SKILL PART (personality prefix stripped: ?_FT_04130.ks /
        // b1_ft_0610.ks / ft_0610.ks all -> "04130"/"0610"), per
        // (part x src) min-magnitude offset entry = the anchor-hugging
        // instance. 105 parts / 134 entries, variants merged.
        // Mount channels (zero polling):
        //   1. CallNormalFile postfix (label empty = start_call_file)
        //   2. F7 stage switch (StageSwitch) - ChangeBG wiped everything
        //   3. F7 toggle re-enable
        // Idempotency: official name already in m_dicAttachObj -> skip;
        // "YU_"+src present -> skip. cfg-off actively deletes own mounts.
        // =================================================================
        private struct PoseProp
        {
            public string part;
            public string src;
            public string name;
            public string stage;      // null = cross-stage fallback entry; else exact branch stage (SJIS-escaped)
            public Vector3 off;       // prop - anchor (branch stage-local)
            public Vector3 rot;       // prop euler (branch stage-local)
            public Vector3 anchorRot; // @AllPos rotation of the branch (rebase frame)

            public PoseProp(string part_, string src_, string name_, string stage_,
                Vector3 off_, Vector3 rot_, Vector3 anchorRot_)
            {
                part = part_;
                src = src_;
                name = name_;
                stage = stage_;
                off = off_;
                rot = rot_;
                anchorRot = anchorRot_;
            }
        }

        private static readonly PoseProp[] PoseProps = new PoseProp[]
        {
            // auto-generated: FT pose-prop table v1.7.3 (stage-keyed, rotation-aware)
            new PoseProp("0002", "Odogu_Apartment_Futon", "\u5E03\u56E3", null, new Vector3(-0.008f, -0.033f, -0.218f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0002", "Odogu_Apartment_Futon", "\u5E03\u56E3", "\u5E1D\u56FD\u8358\uFF12", new Vector3(-0.008f, -0.033f, -0.218f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0002", "Odogu_Apartment_Futon", "\u5E03\u56E3", null, new Vector3(-0.008f, -0.033f, -0.218f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(0f, 0.03f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u5287\u5834", new Vector3(0f, -0.002f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", new Vector3(0f, 0.3106f, 0.207f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", new Vector3(0f, 0.006f, 0f), new Vector3(-90f, 270f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(0.002f, -0.005f, -0.002f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(0.002f, -0.005f, -0.002f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u30C8\u30A4\u30EC", new Vector3(0.006f, -0.02f, 0.002f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(0.023f, 0f, 0.004f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u30E1\u30A4\u30C9\u90E8\u5C4B", new Vector3(0f, 0f, 0f), new Vector3(-90f, 273.079f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(0f, 0.006f, 0f), new Vector3(-90f, 270f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(0f, 0.03f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0f, 0.006f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(0f, -0.017f, 0f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u5C45\u9152\u5C4B", new Vector3(-0.008f, -0.01f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(-0.004f, 0.022f, 0.027f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u5730\u4E0B\u5BA4", new Vector3(0f, -0.014f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(0f, 0.03f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(0f, 0.006f, 0f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "SM\u30AF\u30E9\u30D6", new Vector3(0f, 0.006f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(0f, 0f, -0.008f), new Vector3(-90f, -135f, 0f), new Vector3(0f, 225f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(0.002f, -0.005f, -0.002f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", new Vector3(0f, 0.3106f, 0.207f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(0f, 0.03f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u5287\u5834", new Vector3(0f, -0.002f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u30E1\u30A4\u30C9\u90E8\u5C4B", new Vector3(0f, 0f, 0f), new Vector3(-90f, 273.079f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(0f, 0f, -0.008f), new Vector3(-90f, -135f, 0f), new Vector3(0f, 225f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(0f, 0.03f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u5730\u4E0B\u5BA4", new Vector3(0f, -0.014f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(0f, 0.006f, 0f), new Vector3(-90f, 270f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0f, 0.006f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u30C8\u30A4\u30EC", new Vector3(0.006f, -0.02f, 0.002f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(0.023f, 0f, 0.004f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(0.002f, -0.005f, -0.002f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(0f, -0.017f, 0f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(0f, 0.006f, 0f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u5C45\u9152\u5C4B", new Vector3(-0.008f, -0.01f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "SM\u30AF\u30E9\u30D6", new Vector3(0f, 0.006f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(0f, 0.03f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", new Vector3(0f, 0.006f, 0f), new Vector3(-90f, 270f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0013a", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(-0.004f, 0.022f, 0.027f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0018", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0018", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0018", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", new Vector3(0f, 0f, 0.007f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0018", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", new Vector3(0f, 0f, 0.341f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0018", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0f, 0f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0019", "Odogu_Mat", "\u30DE\u30C3\u30C8", null, new Vector3(0.128f, -0.095f, -0.039f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0019", "Odogu_Mat", "\u30DE\u30C3\u30C8", null, new Vector3(0.128f, -0.095f, -0.039f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0019", "Odogu_Mat", "\u30DE\u30C3\u30C8", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0f, -0.1f, 0.14f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0020", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", new Vector3(0f, 0f, 0.341f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0020", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0020", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0020", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0f, 0f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0020", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442", new Vector3(0f, 0f, 0f), new Vector3(0f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0020", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", new Vector3(0f, 0f, 0.007f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0020a", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", new Vector3(0f, 0f, 0.341f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0020a", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0020a", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0020a", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", new Vector3(0f, 0f, 0.007f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0020a", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0f, 0f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0021", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442", new Vector3(-0.002f, 0f, 0.001f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0021", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0021", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0f, 0f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0021", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0030", "Odogu_Sankakumokuba", "\u4E09\u89D2\u6728\u99AC", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(-0.09f, -0.02f, -1.06f), new Vector3(-90f, 3.733867f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0030", "Odogu_Sankakumokuba", "\u4E09\u89D2\u6728\u99AC", null, new Vector3(0f, -0.027f, -0.006f), new Vector3(-90f, 3.733867f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0030", "Odogu_Sankakumokuba", "\u4E09\u89D2\u6728\u99AC", null, new Vector3(0f, -0.027f, -0.006f), new Vector3(-90f, 3.733867f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0030", "Odogu_Sankakumokuba", "\u4E09\u89D2\u6728\u99AC", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(-0.09f, -0.02f, -1.06f), new Vector3(-90f, 3.733867f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0030", "Odogu_Sankakumokuba", "\u4E09\u89D2\u6728\u99AC", "SM\u30AF\u30E9\u30D6", new Vector3(-2.831f, -0.01f, 0.06f), new Vector3(-90f, 3.733867f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0031", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", "SM\u30AF\u30E9\u30D6", new Vector3(-0.016018f, 0.03f, 1.56f), new Vector3(-90f, -90.1658f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0031", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(0.008f, -0.01f, 0.552f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0031", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(0.008f, -0.01f, 0.552f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0031", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", null, new Vector3(-0.516368f, 0f, 0.025564f), new Vector3(-90f, 2.834246f, 0f), new Vector3(0f, 272.8343f, 0f)),
            new PoseProp("0031", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", null, new Vector3(-0.516368f, 0f, 0.025564f), new Vector3(-90f, -177.1658f, 0f), new Vector3(0f, 272.8343f, 0f)),
            new PoseProp("0031", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", null, new Vector3(-0.516368f, 0f, 0.025564f), new Vector3(-90f, 2.834246f, 0f), new Vector3(0f, 272.8343f, 0f)),
            new PoseProp("0031", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(2.724982f, -0.01f, 0.44f), new Vector3(-90f, -90.1658f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0031", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", "SM\u30AF\u30E9\u30D6", new Vector3(-0.016018f, 0.01f, 0.56f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0031", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", null, new Vector3(-0.516368f, 0f, 0.025564f), new Vector3(-90f, -177.1658f, 0f), new Vector3(0f, 272.8343f, 0f)),
            new PoseProp("0031", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(2.724982f, -0.01f, 0.44f), new Vector3(-90f, -90.1658f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0031a", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(0.008f, -0.01f, 0.552f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0031a", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", null, new Vector3(-0.516368f, 0f, 0.025564f), new Vector3(-90f, 2.834246f, 0f), new Vector3(0f, 272.8343f, 0f)),
            new PoseProp("0031a", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", null, new Vector3(-0.516368f, 0f, 0.025564f), new Vector3(-90f, -177.1658f, 0f), new Vector3(0f, 272.8343f, 0f)),
            new PoseProp("0031a", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", null, new Vector3(-0.516368f, 0f, 0.025564f), new Vector3(-90f, -177.1658f, 0f), new Vector3(0f, 272.8343f, 0f)),
            new PoseProp("0031a", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", null, new Vector3(-0.516368f, 0f, 0.025564f), new Vector3(-90f, 2.834246f, 0f), new Vector3(0f, 272.8343f, 0f)),
            new PoseProp("0031a", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", "SM\u30AF\u30E9\u30D6", new Vector3(-0.016018f, 0.01f, 0.56f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0031a", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(2.724982f, -0.01f, 0.44f), new Vector3(-90f, -90.1658f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0031a", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(0.008f, -0.01f, 0.552f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0031a", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(2.724982f, -0.01f, 0.44f), new Vector3(-90f, -90.1658f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0031a", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", "SM\u30AF\u30E9\u30D6", new Vector3(-0.016018f, 0.03f, 1.56f), new Vector3(-90f, -90.1658f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0047a", "Odogu_ShinShitsumu_Monitor", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(0.86f, 0.7625f, 1.27f), new Vector3(0f, 40f, 0f), new Vector3(0f, 120f, 0f)),
            new PoseProp("0047a", "Odogu_ShinShitsumu_Monitor_Night", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(0.86f, 0.7625f, 1.27f), new Vector3(0f, 40f, 0f), new Vector3(0f, 120f, 0f)),
            new PoseProp("0047a", "Odogu_ShinShitsumu_Monitor_Night", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(0.86f, 0.7625f, 1.27f), new Vector3(0f, 40f, 0f), new Vector3(0f, 120f, 0f)),
            new PoseProp("0047a", "Odogu_ShinShitsumu_Monitor", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(0.86f, 0.7625f, 1.27f), new Vector3(0f, 40f, 0f), new Vector3(0f, 120f, 0f)),
            new PoseProp("0050a", "Odogu_ShinShitsumu_Monitor", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(1.52f, -0.02f, 0.06f), new Vector3(0f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0050a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(1.52f, -0.7865f, 0.31f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0050a", "Odogu_ShinShitsumu_Monitor_Night", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(1.52f, -0.02f, 0.06f), new Vector3(0f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0050a", "Odogu_ShinShitsumu_Monitor", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(1.52f, -0.02f, 0.06f), new Vector3(0f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0050a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(1.52f, -0.7865f, 0.31f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0050a", "Odogu_ShinShitsumu_Monitor_Night", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(1.52f, -0.02f, 0.06f), new Vector3(0f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0057", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", null, new Vector3(0.427f, -0.05f, -2.4286f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0057", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", null, new Vector3(0.427f, -0.05f, -2.4286f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(0.005f, 0.08f, 0.72f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(-0.372f, 0.1f, 0.274f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.01f, 0.08f, -0.21f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 359.0316f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u30C8\u30A4\u30EC", new Vector3(-0.256f, 0.078f, -0.383f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(-0.76f, 0.08f, -0.07f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 91.94118f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(-0.27f, 0.08f, -0.39f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(0f, 0.1f, 0.75f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", new Vector3(0.23f, 0.08f, -0.383f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", new Vector3(-0.23f, 0.081f, 0.38f), new Vector3(-90f, -90f, 0f), new Vector3(0.019095f, 180f, 359.7805f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.01f, 0.08f, -0.21f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 359.0316f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(-0.398f, 0.103f, 0.428f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", new Vector3(-0.361f, 0.1f, 0.361f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(0.005f, 0.08f, 0.72f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u30E1\u30A4\u30C9\u90E8\u5C4B", new Vector3(0.004f, 0.097f, -0.222f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u5287\u5834", new Vector3(0f, 0.098f, -0.214f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u5730\u4E0B\u5BA4", new Vector3(-0.23f, 0.08f, -0.393f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", new Vector3(-0.747f, 0.1f, 0.002f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(-0.257f, 0.075f, -0.391f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", new Vector3(0.42f, -0.237f, -0.07f), new Vector3(0f, 0f, 0f), new Vector3(0f, 274.9839f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(-0.251f, 0.078f, -0.4f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u5C45\u9152\u5C4B", new Vector3(-0.26f, 0.078f, 0.384f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(-0.257f, 0.075f, -0.391f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", new Vector3(0.001f, 0.08f, 0.704f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(-0.74f, 0.1f, 0f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "SM\u30AF\u30E9\u30D6", new Vector3(0.379f, 0.092f, 0.27f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0061", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(0.005f, 0.08f, 0.72f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0063", "Odogu_Mat", "\u30DE\u30C3\u30C8", null, new Vector3(0f, -0.1f, 0.14f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0063", "Odogu_Mat", "\u30DE\u30C3\u30C8", null, new Vector3(0f, -0.1f, 0.14f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0075", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0f, -0.771f, 0.69f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0075", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0f, -0.771f, 0.69f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0075a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0f, -0.771f, 0.69f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0075a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0f, -0.771f, 0.69f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0077", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0f, -0.771f, 0.69f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0077", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0f, -0.771f, 0.69f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0077a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0f, -0.771f, 0.69f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0077a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0f, -0.771f, 0.69f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0080", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0f, -0.77f, 0.76f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0080", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0f, -0.77f, 0.76f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0080a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0f, -0.77f, 0.76f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0080a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0f, -0.77f, 0.76f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0f, -0.002f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", new Vector3(0.01f, 0f, 0.02f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 359.0316f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(-0.51f, 0f, 0.01f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 91.94118f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(0.011f, 0.003f, 0.533f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(0f, -0.01f, -0.04f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(0f, -0.01f, -0.04f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", new Vector3(0.001f, -0.02f, 0.49f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", new Vector3(-0.477f, 0f, 0.032f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u30C8\u30A4\u30EC", new Vector3(0.007f, -0.01f, -0.146f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0f, -0.002f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0.025f, 0f, 0.02f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(0f, -0.01f, -0.04f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u5C45\u9152\u5C4B", new Vector3(0f, 0f, 0.5f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u5730\u4E0B\u5BA4", new Vector3(-0.024f, -0.01f, 0.017f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", new Vector3(0.474f, -0.023f, -0.16f), new Vector3(0f, 0f, 90f), new Vector3(0f, 274.9839f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(0f, 0f, 0.46f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(-0.49f, 0f, 0.03f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(-0.01f, -0.01f, -0.04f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(0f, -0.01f, -0.04f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", new Vector3(-0.382f, -0.01f, -0.398f), new Vector3(-90f, -135f, 0f), new Vector3(0f, 40.3861f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u30E1\u30A4\u30C9\u90E8\u5C4B", new Vector3(0.004f, -0.003f, -0.012f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", new Vector3(0.011f, -0.02f, -0.025f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(-0.01f, -0.01f, -0.04f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", new Vector3(-0.018f, 0f, 0.001f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(0f, 0f, -0.05f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088", "Odogu_SimpleTable", "\u53F0", "SM\u30AF\u30E9\u30D6", new Vector3(0.5f, 0f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u5C45\u9152\u5C4B", new Vector3(0f, 0f, 0.5f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", new Vector3(-0.018f, 0f, 0.001f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", new Vector3(0.001f, -0.02f, 0.49f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0f, -0.002f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(0f, -0.01f, -0.04f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", new Vector3(0.01f, 0f, 0.02f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 359.0316f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(-0.01f, -0.01f, -0.04f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(-0.51f, 0f, 0.01f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 91.94118f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(0.011f, 0.003f, 0.533f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0f, -0.002f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(0f, -0.01f, -0.04f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0.025f, 0f, 0.02f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", new Vector3(0.474f, -0.023f, -0.16f), new Vector3(0f, 0f, 90f), new Vector3(0f, 274.9839f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u5730\u4E0B\u5BA4", new Vector3(-0.024f, -0.01f, 0.017f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(0f, -0.01f, -0.04f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(0f, 0f, -0.05f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u30E1\u30A4\u30C9\u90E8\u5C4B", new Vector3(0.004f, -0.003f, -0.012f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(0f, -0.01f, -0.04f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "SM\u30AF\u30E9\u30D6", new Vector3(0.5f, 0f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(-0.49f, 0f, 0.03f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u30C8\u30A4\u30EC", new Vector3(0.007f, -0.01f, -0.146f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", new Vector3(0.01f, 0.005f, -0.02f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(-0.01f, -0.01f, -0.04f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(0f, 0f, 0.46f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", new Vector3(-0.382f, -0.01f, -0.398f), new Vector3(-90f, -135f, 0f), new Vector3(0f, 40.3861f, 0f)),
            new PoseProp("0088_x1a", "Odogu_SimpleTable", "\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", new Vector3(-0.477f, 0f, 0.032f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0089a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(1.546f, -0.7865f, 0.31f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0089a", "Odogu_ShinShitsumu_Monitor", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(1.546f, -0.02f, 0.06f), new Vector3(0f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0089a", "Odogu_ShinShitsumu_Monitor_Night", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(1.546f, -0.02f, 0.06f), new Vector3(0f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0089a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(1.546f, -0.7865f, 0.31f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0089a", "Odogu_ShinShitsumu_Monitor_Night", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(1.546f, -0.02f, 0.06f), new Vector3(0f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0089a", "Odogu_ShinShitsumu_Monitor", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(1.546f, -0.02f, 0.06f), new Vector3(0f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0091", "Odogu_KousokuKijyouiChair", "\u62D8\u675F\u9A0E\u4E57\u4F4D\u6905\u5B50", null, new Vector3(-0.251f, 0f, 0.183f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 358.9556f, 0f)),
            new PoseProp("0091", "Odogu_KousokuKijyouiChair", "\u62D8\u675F\u9A0E\u4E57\u4F4D\u6905\u5B50", null, new Vector3(-0.251f, 0f, 0.183f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 358.9556f, 0f)),
            new PoseProp("0091", "Odogu_KousokuKijyouiChair", "\u62D8\u675F\u9A0E\u4E57\u4F4D\u6905\u5B50", "SM\u30AF\u30E9\u30D6", new Vector3(-2.831f, 0.03f, 1.56f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0091", "Odogu_KousokuKijyouiChair", "\u62D8\u675F\u9A0E\u4E57\u4F4D\u6905\u5B50", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(-0.251f, 0f, 0.183f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 358.9556f, 0f)),
            new PoseProp("0091", "Odogu_KousokuKijyouiChair", "\u62D8\u675F\u9A0E\u4E57\u4F4D\u6905\u5B50", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(-0.251f, 0f, 0.183f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 358.9556f, 0f)),
            new PoseProp("0092a", "Odogu_ShinShitsumu_Monitor_Night", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(1.48f, -0.02f, 0.668f), new Vector3(0f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0092a", "Odogu_ShinShitsumu_Monitor", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(1.48f, -0.02f, 0.668f), new Vector3(0f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0092a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(1.48f, -0.7865f, 0.918f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0092a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(1.48f, -0.7865f, 0.918f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0092a", "Odogu_ShinShitsumu_Monitor_Night", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(1.48f, -0.02f, 0.668f), new Vector3(0f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0092a", "Odogu_ShinShitsumu_Monitor", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(1.48f, -0.02f, 0.668f), new Vector3(0f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0096", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(0.23f, -0.248f, 0.57f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0096", "Odogu_SimpleTable", "\u53F0", "\u30C8\u30A4\u30EC", new Vector3(0.323f, -0.24f, -0.574f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0096", "Odogu_SimpleTable", "\u53F0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", new Vector3(0.259f, -0.24f, -0.575f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0096", "Odogu_SimpleTable", "\u53F0", "\u5730\u4E0B\u5BA4", new Vector3(0.16f, -0.24f, 0.56f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0096", "Odogu_SimpleTable", "\u53F0", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", new Vector3(0.231f, -0.252f, 0.572f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0096", "Odogu_SimpleTable", "\u53F0", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(0.23f, -0.25f, 0.57f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0096", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.15f, -0.24f, 0.54f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 359.0316f, 0f)),
            new PoseProp("0096", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(-0.559f, -0.25f, 0.141f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0096", "Odogu_SimpleTable", "\u53F0", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", new Vector3(0.264f, -0.25f, -0.57f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0096", "Odogu_SimpleTable", "\u53F0", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(-0.3f, -0.24f, -0.55f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0096", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.15f, -0.24f, 0.54f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 359.0316f, 0f)),
            new PoseProp("0096", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(0.23f, -0.248f, 0.57f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0096", "Odogu_SimpleTable", "\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(0.55f, -0.24f, 0.29f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 91.94118f, 0f)),
            new PoseProp("0096", "Odogu_SimpleTable", "\u53F0", "\u96A0\u308C\u5165\u308A\u6C5F", new Vector3(0.268f, -0.25f, -0.565f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0096", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(0.23f, -0.248f, 0.57f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0096", "Odogu_SimpleTable", "\u53F0", "\u5C45\u9152\u5C4B", new Vector3(-0.26f, -0.25f, 0.62f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0096", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", new Vector3(0.18f, -0.24f, 0.561f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0096", "Odogu_SimpleTable", "\u53F0", "\u5287\u5834", new Vector3(0.19f, -0.24f, 0.54f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0096", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0.36f, -0.24f, -0.56f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u5730\u4E0B\u5BA4", new Vector3(-0.101f, -0.02f, -0.33f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", new Vector3(0.071f, -0.026f, 0.833f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(-0.15f, -0.02f, -0.45f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 315f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u30E1\u30A4\u30C9\u90E8\u5C4B", new Vector3(-0.058f, -0.02f, -0.32f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(-0.111f, -0.025f, -0.325f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(-0.854f, -0.018f, 0.052f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 91.94118f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", null, new Vector3(-0.318f, -0.02f, 0.02f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u5287\u5834", new Vector3(-0.148f, -0.025f, -0.311f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u5C45\u9152\u5C4B", new Vector3(0.1f, -0.03f, 0.864f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", null, new Vector3(-0.318f, -0.02f, 0.02f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", new Vector3(-0.825f, -0.022f, 0.109f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(-0.111f, -0.025f, -0.325f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(-0.08f, -0.03f, -0.34f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(0.057f, -0.021f, 0.82f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(-0.88f, -0.018f, 0.052f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 91.94118f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", new Vector3(-0.094f, -0.03f, -0.368f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(-0.84f, -0.02f, 0.079f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", new Vector3(-0.018f, -0.02f, -0.336f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "SM\u30AF\u30E9\u30D6", new Vector3(0.826f, -0.021f, -0.113f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", new Vector3(0.827f, -0.033f, -0.189f), new Vector3(0f, 0f, 90f), new Vector3(0f, 274.9839f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", new Vector3(0.004f, -0.02f, -0.324f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 359.0316f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(-0.88f, -0.018f, 0.052f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 91.94118f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(0.106f, -0.023f, 0.83f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", new Vector3(-0.082f, -0.025f, -0.33f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u30C8\u30A4\u30EC", new Vector3(-0.06f, -0.02f, -0.358f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0101", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(-0.111f, -0.025f, -0.325f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0103", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", new Vector3(0.251f, -0.267f, 0.1f), new Vector3(-90f, 45f, 0f), new Vector3(0f, 315f, 0f)),
            new PoseProp("0103", "Odogu_SimpleTable", "\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(0.25f, -0.271f, -0.16f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0103", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.25f, -0.26f, -0.09f), new Vector3(-90f, 88.82f, 0f), new Vector3(0f, 359.0316f, 0f)),
            new PoseProp("0103", "Odogu_SimpleTable", "\u53F0", "\u5730\u4E0B\u5BA4", new Vector3(0.251f, -0.27f, 0.15f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0103", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.25f, -0.26f, -0.09f), new Vector3(-90f, 88.82f, 0f), new Vector3(0f, 359.0316f, 0f)),
            new PoseProp("0103", "Odogu_SimpleTable", "\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(0.25f, -0.271f, -0.069f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0103", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(-0.11f, -0.26f, 0.257f), new Vector3(-90f, 0.3f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0103", "Odogu_SimpleTable", "\u53F0", "\u30C8\u30A4\u30EC", new Vector3(0.249f, -0.27f, -0.154f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0103", "Odogu_SimpleTable", "\u53F0", "\u5C45\u9152\u5C4B", new Vector3(-0.26f, -0.268f, 0.15f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0103", "Odogu_SimpleTable", "\u53F0", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(-0.25f, -0.29f, -0.35f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0103", "Odogu_SimpleTable", "\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(0.25f, -0.271f, -0.069f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0103", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(0.113f, -0.26f, 0.241f), new Vector3(-90f, 0.3f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0103", "Odogu_SimpleTable", "\u53F0", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", new Vector3(-0.248f, -0.28f, 0.246f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0103", "Odogu_SimpleTable", "\u53F0", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(0.25f, -0.26f, -0.34f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0103", "Odogu_SimpleTable", "\u53F0", "\u5287\u5834", new Vector3(0.252f, -0.269f, -0.13f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0.095f, 0.005f, -0.33f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(-0.08f, 0.007f, 0.82f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.088f, 0.005f, -0.322f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u5287\u5834", new Vector3(-0.129f, 0.004f, 0.833f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(0.36f, 0.008f, -0.502f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u30C8\u30A4\u30EC", new Vector3(0.129f, 0f, -0.341f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(0.088f, 0.005f, -0.322f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", new Vector3(0.121f, 0.008f, -0.331f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", new Vector3(0.155f, 0.005f, 0.48f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.088f, 0.005f, -0.322f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(-0.09f, 0.01f, 0.84f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u30E1\u30A4\u30C9\u90E8\u5C4B", new Vector3(0.116f, 0.005f, -0.343f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(-0.096f, -0.001f, 0.82f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(-0.15f, -0.02f, -0.4f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(-0.08f, 0.007f, 0.82f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", new Vector3(0.36f, 0f, -0.501f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", new Vector3(-0.355f, 0.06f, -0.23f), new Vector3(0f, 0f, 90f), new Vector3(0f, 274.9839f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(-0.096f, -0.001f, 0.82f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u5C45\u9152\u5C4B", new Vector3(-0.26f, 0f, 0.5f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u5730\u4E0B\u5BA4", new Vector3(-0.161f, 0.005f, 0.829f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", new Vector3(-0.102f, 0.01f, 0.866f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(-0.849f, 0.007f, -0.111f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(-0.08f, 0.007f, 0.82f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", new Vector3(-0.105f, 0.005f, 0.826f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "SM\u30AF\u30E9\u30D6", new Vector3(0.834f, 0.007f, 0.116f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0104", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(-0.12f, 0.005f, 0.83f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(0.001f, 0.008f, 0.637f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", new Vector3(0f, 0.011f, -0.68f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(0.65f, -0.02f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(0f, 0.01f, 0.65f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(-0.659f, 0.004f, 0.026f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 272.55f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", new Vector3(0.025f, 0.006f, 0.592f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(0f, 0.01f, 0.65f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(0.67f, 0.006f, 0.02f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", null, new Vector3(-0.01f, 0.015f, 0.576f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u30C8\u30A4\u30EC", new Vector3(-0.659f, 0.007f, 0.012f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", null, new Vector3(-0.01f, 0.015f, 0.576f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(-0.01f, 0.015f, 0.576f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "SM\u30AF\u30E9\u30D6", new Vector3(-0.038f, 0.01f, -0.669f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0.05f, 0.01f, 0.65f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", new Vector3(0.497f, 0.005f, -0.446f), new Vector3(-90f, 45f, 0f), new Vector3(0f, 135f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(0.03f, 0.011f, 0.605f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u5C45\u9152\u5C4B", new Vector3(0f, 0f, 0.7f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u5287\u5834", new Vector3(-0.65f, 0.01f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(0f, 0.01f, 0.65f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", new Vector3(0.753f, 0.258f, 0.118f), new Vector3(0f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u30E1\u30A4\u30C9\u90E8\u5C4B", new Vector3(-0.693f, 0.012f, -0.005f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(0.03f, 0.01f, -0.69f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u5730\u4E0B\u5BA4", new Vector3(0.066f, 0.007f, 0.62f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0106", "Odogu_ashikokidai", "\u8DB3\u30B3\u30AD\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", new Vector3(-0.659f, 0.004f, 0.026f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 272.55f, 0f)),
            new PoseProp("0114", "Odogu_SimpleTable", "\u30B7\u30F3\u30D7\u30EB\u30C6\u30FC\u30D6\u30EB", null, new Vector3(-0.04f, -0.017f, -0.573f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0114", "Odogu_SimpleTable", "\u30B7\u30F3\u30D7\u30EB\u30C6\u30FC\u30D6\u30EB", null, new Vector3(-0.04f, -0.017f, -0.573f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0114", "Odogu_SimpleTable", "\u30B7\u30F3\u30D7\u30EB\u30C6\u30FC\u30D6\u30EB", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", new Vector3(-0.4f, 0.025f, -0.44f), new Vector3(-90f, 45f, 0f), new Vector3(0f, 40.3861f, 0f)),
            new PoseProp("0114", "Odogu_SimpleTable", "\u30B7\u30F3\u30D7\u30EB\u30C6\u30FC\u30D6\u30EB", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", new Vector3(-0.005f, 0.033f, -0.578f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0122", "Odogu_SimpleTable", "\u30B7\u30F3\u30D7\u30EB\u30C6\u30FC\u30D6\u30EB", null, new Vector3(-2.33f, -0.5f, -0.8f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0122", "Odogu_SimpleTable", "\u30B7\u30F3\u30D7\u30EB\u30C6\u30FC\u30D6\u30EB", null, new Vector3(-2.33f, -0.5f, -0.8f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0122a", "Odogu_SimpleTable", "\u30B7\u30F3\u30D7\u30EB\u30C6\u30FC\u30D6\u30EB", null, new Vector3(-2.33f, -0.5f, -0.8f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0122a", "Odogu_SimpleTable", "\u30B7\u30F3\u30D7\u30EB\u30C6\u30FC\u30D6\u30EB", null, new Vector3(-2.33f, -0.5f, -0.8f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u30E1\u30A4\u30C9\u90E8\u5C4B", new Vector3(0.004f, 0f, -0.012f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(0f, 0.052f, 0.03f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u30C8\u30A4\u30EC", new Vector3(0.007f, 0.002f, 0.007f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(0f, 0f, 0.05f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(0.011f, 0.003f, -0.001f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", new Vector3(-0.005f, 0f, -0.006f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(-0.003f, 0.009f, -0.038f), new Vector3(-90f, -180f, 0f), new Vector3(0f, 182.5f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", new Vector3(0.001f, 0.001f, 0.004f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u5287\u5834", new Vector3(0f, 0.002f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", new Vector3(0.003f, 0.003f, 0.032f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(0.02f, 0.009f, -0.02f), new Vector3(-90f, -180f, 0f), new Vector3(0f, 182.5f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(0f, -0.02f, 0f), new Vector3(-90f, -45f, 0f), new Vector3(0f, 315f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", new Vector3(0f, -0.023f, -0.149f), new Vector3(0f, 0f, -90f), new Vector3(0f, 274.9839f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0.025f, 0.004f, 0.02f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(0f, 0f, 0.05f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(-0.002f, -0.01f, 0.003f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(0f, 0f, 0.05f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", new Vector3(-0.021f, 0f, 0.001f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(0f, 0.004f, -0.02f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u5730\u4E0B\u5BA4", new Vector3(-0.024f, 0.007f, 0.017f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", new Vector3(0.01f, 0f, 0.02f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 359.0316f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(0.02f, 0.009f, -0.02f), new Vector3(-90f, -180f, 0f), new Vector3(0f, 182.5f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "SM\u30AF\u30E9\u30D6", new Vector3(0f, 0.002f, 0f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0163", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", new Vector3(-0.002f, -0.01f, 0.004f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0190a", "Odogu_ShinShitsumu_Monitor", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(1.368f, 0.7625f, 1.396f), new Vector3(0f, 40f, 0f), new Vector3(0f, 40f, 0f)),
            new PoseProp("0190a", "Odogu_ShinShitsumu_Monitor_Night", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(1.368f, 0.7625f, 1.396f), new Vector3(0f, 40f, 0f), new Vector3(0f, 40f, 0f)),
            new PoseProp("0190a", "Odogu_ShinShitsumu_Monitor", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(1.368f, 0.7625f, 1.396f), new Vector3(0f, 40f, 0f), new Vector3(0f, 40f, 0f)),
            new PoseProp("0190a", "Odogu_ShinShitsumu_Monitor_Night", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(1.368f, 0.7625f, 1.396f), new Vector3(0f, 40f, 0f), new Vector3(0f, 40f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u5730\u4E0B\u5BA4", new Vector3(-0.024f, 0.78f, 2f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", new Vector3(-0.25f, -0.19f, -0.03f), new Vector3(0f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u5C45\u9152\u5C4B", new Vector3(0.25f, 0.03f, 0.25f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(-0.3f, 0.02f, 0.261f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u5287\u5834", new Vector3(0.25f, 0.028f, 0.3f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(-0.2f, 0.03f, 0.27f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", new Vector3(0f, 0.79f, 2.22f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 359.0316f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", null, new Vector3(0.034f, 0.782f, -1.296f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(0.25f, 0.03f, 0.23f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", new Vector3(0.034f, 0.037f, -0.492f), new Vector3(-88.76202f, 180f, -1.884318f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(-1.99f, 0.8f, 0.016f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(-0.27f, 0.03f, 0.23f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u5730\u4E0B\u5BA4", new Vector3(0.24f, 0.02f, 0.25f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u30C8\u30A4\u30EC", new Vector3(0.3f, 0.03f, 0.25f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30C8\u30A4\u30EC", new Vector3(2f, 0.79f, 0f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(-0.3f, 0.02f, 0.261f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", null, new Vector3(-0.022f, 0.052f, 0.114f), new Vector3(-88.76202f, 180f, -1.884318f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", null, new Vector3(0.034f, 0.782f, -1.296f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(-1.99f, 0.8f, 0.016f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", new Vector3(0.253f, 0.02f, 0.25f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(2f, 0.79f, -0.05f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(-0.25f, 0.02f, 0.2f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", new Vector3(-0.026f, 0.776f, 1.604f), new Vector3(-90f, -180f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", new Vector3(0.23f, 0.03f, 0.22f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 359.0316f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(0f, 0.77f, 2.01f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", new Vector3(0.241f, 0.02f, -0.296f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(-0.3f, 0.02f, 0.261f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(2f, 0.76f, 0f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30E1\u30A4\u30C9\u90E8\u5C4B", new Vector3(-0.02f, 0.79f, 1.74f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(-0.25f, 0.02f, 0.2f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u30E1\u30A4\u30C9\u90E8\u5C4B", new Vector3(0.23f, 0.02f, 0.24f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u5C45\u9152\u5C4B", new Vector3(0f, 0.79f, 2f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "SM\u30AF\u30E9\u30D6", new Vector3(-0.2f, 0.03f, 0.27f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", new Vector3(1.853f, 0.78f, 0f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "SM\u30AF\u30E9\u30D6", new Vector3(-1.7f, 0.79f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", null, new Vector3(-0.022f, 0.052f, 0.114f), new Vector3(-88.76202f, 180f, -1.884318f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(0.008f, 0.798f, 1.939f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(1.889f, 0.78f, -0.003f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(0.266f, 0.02f, 0.265f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(-2f, 0.79f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(-1.99f, 0.8f, 0.016f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(-0.25f, 0.025f, 0.302f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0.2f, 0.03f, 0.25f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(0f, 0.77f, 2.01f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u5287\u5834", new Vector3(0f, 0.788f, 2f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(0.27f, 0.02f, -0.2f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", new Vector3(-2f, 0f, 0.73f), new Vector3(0f, 0f, 90f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(0.002f, 0.77f, 2.019f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(-0.04f, 0.79f, -2f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0200", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", new Vector3(-0.009f, 0.78f, -1.996f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", new Vector3(0.265f, 0.02f, 0.242f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(-0.25f, 0.025f, 0.302f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u5C45\u9152\u5C4B", new Vector3(0.25f, 0.03f, 0.25f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(-0.3f, 0.02f, 0.261f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", new Vector3(0.23f, 0.03f, 0.22f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 359.0316f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "SM\u30AF\u30E9\u30D6", new Vector3(-0.2f, 0.03f, 0.27f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", new Vector3(0.241f, 0.02f, -0.296f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(-0.3f, 0.02f, 0.261f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(-0.25f, -0.19f, -0.03f), new Vector3(0f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(-0.3f, 0.02f, 0.261f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(-0.27f, 0.03f, 0.23f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(-0.25f, 0.02f, 0.2f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(0.266f, 0.02f, 0.265f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(0.25f, 0.03f, 0.23f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", new Vector3(0.253f, 0.02f, 0.25f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(-0.25f, 0.02f, 0.2f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(-0.25f, -0.19f, -0.03f), new Vector3(0f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", new Vector3(-0.24f, 0.022f, -0.25f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0.2f, 0.03f, 0.25f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u5287\u5834", new Vector3(0.25f, 0.028f, 0.3f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u30E1\u30A4\u30C9\u90E8\u5C4B", new Vector3(0.23f, 0.02f, 0.24f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u5730\u4E0B\u5BA4", new Vector3(0.24f, 0.02f, 0.25f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(-0.2f, 0.03f, 0.27f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(0.27f, 0.02f, -0.2f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0200a", "Odogu_SimpleTable", "\u53F0", "\u30C8\u30A4\u30EC", new Vector3(0.3f, 0.03f, 0.25f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u5C45\u9152\u5C4B", new Vector3(0f, 0.78f, 2f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(2.14f, 0.31f, -0.02f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(0f, 0.3f, 1.96f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(0f, 0.27f, 1.78f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", new Vector3(0f, 0.79f, 2f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 359.0316f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(-1.51f, 0.76f, -1.64f), new Vector3(-90f, 38f, 0f), new Vector3(0f, 232f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30E1\u30A4\u30C9\u90E8\u5C4B", new Vector3(0f, 0.14f, 2f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", new Vector3(0.086f, 0.529f, -1.603f), new Vector3(-76.11407f, -167.9255f, 158.4039f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", null, new Vector3(-0.32f, 0.07f, 1.371f), new Vector3(-88.76202f, -179.9999f, -28.10269f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30C8\u30A4\u30EC", new Vector3(2f, 0.79f, 0f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u5730\u4E0B\u5BA4", new Vector3(0f, 0.749f, 2f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "SM\u30AF\u30E9\u30D6", new Vector3(2f, 0.27f, 0f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(-0.003f, 0.149f, 2.033f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", null, new Vector3(-0.32f, 0.07f, 1.371f), new Vector3(-88.76202f, -179.9999f, -28.10269f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", new Vector3(-1.5f, -0.01f, 0.73f), new Vector3(0f, 0f, 90f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0f, 0.79f, -2f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(0f, 0.25f, -2f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(-0.005f, 0.29f, -1.97f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", new Vector3(2.2f, 0.26f, 0.056f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(-0.005f, 0.29f, -1.97f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(0f, 0.3f, 1.96f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(-0.005f, 0.29f, -1.97f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(-2f, 0.79f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", new Vector3(0f, 0.27f, 2f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0201", "Odogu_denkigai2019w_camera", "\u30AB\u30E1\u30E9", "\u5287\u5834", new Vector3(0f, 0.788f, 2f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0211", "Odogu_KousokudaiHgata", "\u62D8\u675F\u53F0\u5EA7H\u578B", null, new Vector3(0f, -0.009f, 0.014f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0211", "Odogu_KousokudaiHgata", "\u62D8\u675F\u53F0\u5EA7H\u578B", null, new Vector3(0f, -0.009f, 0.014f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0211", "Odogu_KousokudaiHgata", "\u62D8\u675F\u53F0\u5EA7H\u578B", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(0f, 0f, -0.05f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0211", "Odogu_KousokudaiHgata", "\u62D8\u675F\u53F0\u5EA7H\u578B", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(0f, 0f, -0.05f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0211", "Odogu_KousokudaiHgata", "\u62D8\u675F\u53F0\u5EA7H\u578B", "SM\u30AF\u30E9\u30D6", new Vector3(0.036f, -0.011f, 0.027f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0251", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.015f, -0.292f, -0.027f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0251", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.015f, -0.292f, -0.027f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0251", "Odogu_SimpleTable", "\u53F0", "\u5E1D\u56FD\u8358\uFF12", new Vector3(0.015f, -0.292f, -0.027f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0251a", "Odogu_SimpleTable", "\u53F0", "\u5E1D\u56FD\u8358\uFF12", new Vector3(0.015f, -0.79f, -0.027f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0251a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.015f, -0.79f, -0.027f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0251a", "Odogu_SimpleTable", "\u53F0", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442", new Vector3(0f, -0.79f, -0.04f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0251a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.015f, -0.79f, -0.027f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0260a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(-1.48f, -0.7865f, 0.63f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0260a", "Odogu_ShinShitsumu_Monitor", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(-1.48f, -0.02f, 0.38f), new Vector3(0f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0260a", "Odogu_ShinShitsumu_Monitor_Night", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(-1.48f, -0.02f, 0.38f), new Vector3(0f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0260a", "Odogu_ShinShitsumu_Monitor", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(-1.48f, -0.02f, 0.38f), new Vector3(0f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0260a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(-1.48f, -0.7865f, 0.63f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0260a", "Odogu_ShinShitsumu_Monitor_Night", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(-1.48f, -0.02f, 0.38f), new Vector3(0f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0270a", "Odogu_ShinShitsumu_Monitor_Night", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(1.476f, 0.7625f, 2.774f), new Vector3(0f, 40f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0270a", "Odogu_ShinShitsumu_Monitor", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(1.476f, 0.7625f, 2.774f), new Vector3(0f, 40f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0270a", "Odogu_ShinShitsumu_Monitor", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(1.476f, 0.7625f, 2.774f), new Vector3(0f, 40f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0270a", "Odogu_ShinShitsumu_Monitor_Night", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(1.476f, 0.7625f, 2.774f), new Vector3(0f, 40f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0.01f, -0.015f, 0.02f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(0f, -0.01f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", new Vector3(0.01f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(-0.029f, -0.01f, -0.001f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(0.001f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(-0.01f, 0f, 0.01f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(-0.01f, -0.01f, 0f), new Vector3(-90f, -130f, 0f), new Vector3(0f, 230f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30E1\u30A4\u30C9\u90E8\u5C4B", new Vector3(-0.001f, -0.012f, 0.014f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", new Vector3(0f, -0.003f, 0f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(0f, -0.01f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(0.01f, 0f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5730\u4E0B\u5BA4", new Vector3(0f, -0.01f, 0.02f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "SM\u30AF\u30E9\u30D6", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", new Vector3(0f, -0.01f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30C8\u30A4\u30EC", new Vector3(0.009f, -0.01f, -0.003f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", new Vector3(-0.008f, -0.013f, 0.011f), new Vector3(-90f, -35f, 0f), new Vector3(0f, 325f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5C45\u9152\u5C4B", new Vector3(0.01f, -0.017f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", new Vector3(-0.00514f, -0.004f, 0.031506f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", new Vector3(0f, 0.03f, -0.107f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0300", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(0f, -0.01f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", new Vector3(0f, -0.01f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(0.01f, 0f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", new Vector3(-0.008f, -0.013f, 0.011f), new Vector3(-90f, -35f, 0f), new Vector3(0f, 325f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(-0.029f, -0.01f, -0.001f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", new Vector3(0f, 0.01f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(-0.01f, 0f, 0.01f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(0f, -0.01f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "SM\u30AF\u30E9\u30D6", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(0f, 0.03f, -0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", new Vector3(0f, -0.003f, -0.007f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30C8\u30A4\u30EC", new Vector3(0.009f, -0.01f, -0.003f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", new Vector3(0.01f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", new Vector3(0f, 0.03f, -0.107f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5C45\u9152\u5C4B", new Vector3(0.01f, -0.017f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30E1\u30A4\u30C9\u90E8\u5C4B", new Vector3(-0.001f, -0.012f, 0.014f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", new Vector3(-0.00514f, -0.004f, 0.031506f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0.01f, -0.015f, 0.02f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5730\u4E0B\u5BA4", new Vector3(0f, -0.01f, 0.02f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(0.001f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5973\u6027\u30C8\u30A4\u30EC", new Vector3(0.009f, -0.01f, -0.003f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(0f, 0.03f, -0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0301", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(-0.01f, -0.01f, 0f), new Vector3(-90f, -130f, 0f), new Vector3(0f, 230f, 0f)),
            new PoseProp("0313", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.25f, -1.56f, 0.65f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0313", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.25f, -1.56f, 0.65f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0331", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", new Vector3(0.78f, -0.754f, -0.023f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0331", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(0.778f, -0.755f, -0.028f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0331", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(-0.035f, -0.755f, -0.282f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0331", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(-0.035f, -0.755f, -0.282f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0331", "Odogu_SimpleTable", "\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(0f, -0.76f, -0.28f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0331", "Odogu_SimpleTable", "\u53F0", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", new Vector3(0.79f, -0.655f, -0.25f), new Vector3(0f, 0f, 90f), new Vector3(0f, 274.9839f, 0f)),
            new PoseProp("0331", "Odogu_SimpleTable", "\u53F0", null, new Vector3(-0.03f, -0.755f, -0.275f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0331", "Odogu_SimpleTable", "\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(0f, -0.76f, -0.28f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0331", "Odogu_SimpleTable", "\u53F0", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(-0.782f, -0.755f, 0.03f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0331", "Odogu_SimpleTable", "\u53F0", "\u5C45\u9152\u5C4B", new Vector3(0.036f, -0.755f, 0.78f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0331", "Odogu_SimpleTable", "\u53F0", "\u5287\u5834", new Vector3(-0.027f, -0.755f, -0.276f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0331", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(0.03f, -0.756f, 0.785f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0331", "Odogu_SimpleTable", "\u53F0", "\u5730\u4E0B\u5BA4", new Vector3(-2f, -0.735f, -1.49f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0331", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(-0.28f, -0.755f, 0.03f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0331", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(-0.035f, -0.755f, -0.282f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0331", "Odogu_SimpleTable", "\u53F0", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(-0.03f, -0.755f, -0.279f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0331", "Odogu_SimpleTable", "\u53F0", null, new Vector3(-0.03f, -0.755f, -0.275f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0331", "Odogu_SimpleTable", "\u53F0", "\u30C8\u30A4\u30EC", new Vector3(-0.031f, -0.755f, -0.293f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0358", "Odogu_Mat", "\u30DE\u30C3\u30C8", null, new Vector3(0f, -0.09f, 0.036f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0358", "Odogu_Mat", "\u30DE\u30C3\u30C8", null, new Vector3(0f, -0.09f, 0.036f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0361", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.78f, -0.755f, -0.03f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0361", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.78f, -0.755f, -0.03f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0391a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(-1.2f, -0.2465f, 0.75f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0391a", "Odogu_ShinShitsumu_Monitor_Night", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(-1.2f, 0.52f, 0.5f), new Vector3(0f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0391a", "Odogu_ShinShitsumu_Monitor", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(-1.2f, 0.52f, 0.5f), new Vector3(0f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0391a", "Odogu_ShinShitsumu_Monitor_Night", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(-1.2f, 0.52f, 0.5f), new Vector3(0f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0391a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(-1.2f, -0.2465f, 0.75f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0391a", "Odogu_ShinShitsumu_Monitor", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(-1.2f, 0.52f, 0.5f), new Vector3(0f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u96A0\u308C\u5165\u308A\u6C5F", new Vector3(-0.171f, -0.056f, 0.107f), new Vector3(-90f, 120f, 0f), new Vector3(0f, 120f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(-0.25f, -0.049f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", new Vector3(-0.747f, -0.055f, 0.002f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(0f, -0.06f, -0.25f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(0f, -0.046f, -0.28f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(-0.01f, -0.052f, -0.2f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(0f, -0.055f, 0.75f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(0f, -0.06f, -0.25f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(0f, -0.06f, -0.25f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0f, -0.041f, -0.116f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u5730\u4E0B\u5BA4", new Vector3(0f, -0.049f, -0.218f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", new Vector3(0.71f, -0.025f, -0.2f), new Vector3(0f, 0f, 90f), new Vector3(0f, 274.9839f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u30E1\u30A4\u30C9\u90E8\u5C4B", new Vector3(0.004f, -0.052f, -0.222f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u5C45\u9152\u5C4B", new Vector3(0f, -0.05f, 0.71f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(0.211f, -0.049f, -0.007f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u5287\u5834", new Vector3(0f, -0.051f, -0.214f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(-0.745f, -0.055f, 0f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(-0.74f, -0.049f, 0f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", new Vector3(0.01f, -0.045f, -0.21f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 359.0316f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", new Vector3(0.01f, -0.055f, 0.2f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", new Vector3(0f, -0.055f, 0.69f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u30C8\u30A4\u30EC", new Vector3(0.001f, -0.05f, -0.209f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "SM\u30AF\u30E9\u30D6", new Vector3(0.72f, -0.055f, 0.02f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", new Vector3(0.001f, -0.05f, 0.704f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(-0.01f, -0.05f, -0.2f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0f, -0.041f, -0.116f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(0f, -0.046f, -0.28f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0400", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", new Vector3(-0.2f, -0.049f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0410", "Odogu_Mat", "\u30DE\u30C3\u30C8", null, new Vector3(0f, -0.09f, 0.14f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0410", "Odogu_Mat", "\u30DE\u30C3\u30C8", null, new Vector3(0f, -0.09f, 0.14f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0411", "Odogu_Mat", "\u30DE\u30C3\u30C8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(0f, -0.09f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0411", "Odogu_Mat", "\u30DE\u30C3\u30C8", null, new Vector3(0f, -0.09f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0411", "Odogu_Mat", "\u30DE\u30C3\u30C8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(0f, -0.09f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0411", "Odogu_Mat", "\u30DE\u30C3\u30C8", null, new Vector3(0f, -0.09f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0411", "Odogu_Mat", "\u30DE\u30C3\u30C8", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0f, -0.09f, 0.14f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0411a", "Odogu_Mat", "\u30DE\u30C3\u30C8", null, new Vector3(0f, -0.09f, 0.14f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0411a", "Odogu_Mat", "\u30DE\u30C3\u30C8", null, new Vector3(0f, -0.09f, 0.14f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0420a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(-1.48f, -0.7865f, 0.63f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0420a", "Odogu_ShinShitsumu_Monitor_Night", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(-1.48f, -0.02f, 0.38f), new Vector3(0f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0420a", "Odogu_ShinShitsumu_Monitor", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(-1.48f, -0.02f, 0.38f), new Vector3(0f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0420a", "Odogu_ShinShitsumu_Monitor_Night", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(-1.48f, -0.02f, 0.38f), new Vector3(0f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0420a", "Odogu_ShinShitsumu_Monitor", "\u30E2\u30CB\u30BF\u30FC", null, new Vector3(-1.48f, -0.02f, 0.38f), new Vector3(0f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0420a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(-1.48f, -0.7865f, 0.63f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0431", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u96A0\u308C\u5165\u308A\u6C5F", new Vector3(0f, -0.46f, 0.2f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0431", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", new Vector3(0f, -0.46f, -0.3f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0431", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", null, new Vector3(0f, -0.459f, 0.2f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0431", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "SM\u30AF\u30E9\u30D6", new Vector3(0.3f, -0.46f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0431", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", null, new Vector3(0f, -0.459f, 0.2f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0431", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", new Vector3(0f, -0.46f, -0.28337f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0431", "Odogu_Sexisu", "\u5EA7\u4F4D\u6905\u5B50", "\u5730\u4E0B\u5BA4", new Vector3(0f, -0.46f, 0.29f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", new Vector3(-0.00514f, -0.43f, -0.018494f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u5730\u4E0B\u5BA4", new Vector3(0f, -0.45f, -0.03f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u548C\u98A8\u65C5\u9928", new Vector3(0.007f, -0.44f, -0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(0f, -0.45f, -0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u30C8\u30A4\u30EC", new Vector3(-0.021f, -0.441f, -0.008f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", new Vector3(0f, -0.44f, 0.01f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(0.011f, -0.44f, -0.051f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", new Vector3(0f, -0.464f, -0.016f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(0f, -0.45f, -0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", null, new Vector3(0f, -0.389f, -0.087f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(0f, -0.445f, -0.026f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(0f, -0.45f, -0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(-0.01f, -0.44f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", new Vector3(0f, -0.44f, -0.02f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0.01f, -0.43f, -0.03f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u5287\u5834", new Vector3(0f, -0.44f, -0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", null, new Vector3(0f, -0.389f, -0.087f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(-0.01f, -0.44f, 0.01f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(0f, -0.445f, -0.026f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "SM\u30AF\u30E9\u30D6", new Vector3(0f, -0.44f, -0.02f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", new Vector3(0.017f, -0.44f, -0.024f), new Vector3(-90f, -35f, 0f), new Vector3(0f, 325f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", new Vector3(0.01f, -0.44f, -0.02f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(0.01f, -0.45f, 0.01f), new Vector3(-90f, -130f, 0f), new Vector3(0f, 230f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u5C45\u9152\u5C4B", new Vector3(0f, -0.45f, -0.02f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u30E1\u30A4\u30C9\u90E8\u5C4B", new Vector3(-0.001f, -0.45f, -0.006f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(0f, -0.45f, -0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0441", "Odogu_omytgc023_semotareisu", "\u80CC\u3082\u305F\u308C\u6905\u5B50", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(0.005f, -0.446f, -0.02f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0451", "Odogu_Apartment_Futon", "\u30AA\u30D5\u30C8\u30A5\u30F3", null, new Vector3(-0.03f, -0.027f, -0.2f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0451", "Odogu_Apartment_Futon", "\u30AA\u30D5\u30C8\u30A5\u30F3", "\u5E1D\u56FD\u8358", new Vector3(-0.03f, -0.027f, -0.2f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0451", "Odogu_Apartment_Futon", "\u30AA\u30D5\u30C8\u30A5\u30F3", null, new Vector3(-0.03f, -0.027f, -0.2f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0451a", "Odogu_Apartment_Futon", "\u30AA\u30D5\u30C8\u30A5\u30F3", null, new Vector3(-0.03f, -0.056f, -0.2f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0451a", "Odogu_Apartment_Futon", "\u30AA\u30D5\u30C8\u30A5\u30F3", null, new Vector3(-0.03f, -0.056f, -0.2f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0451a", "Odogu_Apartment_Futon", "\u30AA\u30D5\u30C8\u30A5\u30F3", "\u5E1D\u56FD\u8358", new Vector3(-0.03f, -0.056f, -0.2f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0460", "Odogu_HeroineChair_v1", "[HF]\u90E8\u5C4B\u6905\u5B50", null, new Vector3(-2.518f, -0.38f, -1.675f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0460", "Odogu_HeroineChair_v1", "[HF]\u90E8\u5C4B\u6905\u5B50", null, new Vector3(-2.518f, -0.38f, -1.675f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0460a", "Odogu_HeroineChair_v1", "[HF]\u90E8\u5C4B\u6905\u5B50", null, new Vector3(-2.518f, -0.38f, -1.675f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0460a", "Odogu_HeroineChair_v1", "[HF]\u90E8\u5C4B\u6905\u5B50", null, new Vector3(-2.518f, -0.38f, -1.675f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0461", "Odogu_HeroineChair_v1", "[HF]\u90E8\u5C4B\u6905\u5B50", null, new Vector3(-2.518f, -0.38f, -1.675f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0461", "Odogu_HeroineChair_v1", "[HF]\u90E8\u5C4B\u6905\u5B50", null, new Vector3(-2.518f, -0.38f, -1.675f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0462", "Odogu_HeroineChair_v1", "[HF]\u90E8\u5C4B\u6905\u5B50", null, new Vector3(-2.4f, -0.38f, -1.384f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0462", "Odogu_HeroineChair_v1", "[HF]\u90E8\u5C4B\u6905\u5B50", null, new Vector3(-2.4f, -0.38f, -1.384f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0463", "Odogu_HeroineChair_v1", "[HF]\u90E8\u5C4B\u6905\u5B50", null, new Vector3(-2.4f, -0.38f, -1.384f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0463", "Odogu_HeroineChair_v1", "[HF]\u90E8\u5C4B\u6905\u5B50", null, new Vector3(-2.4f, -0.38f, -1.384f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0541", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", null, new Vector3(0.01f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0541", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", null, new Vector3(0.01f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0584", "Odogu_SimpleTable", "\u53F0", "\u30C8\u30A4\u30EC", new Vector3(0.3f, 0.03f, 0.25f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0584", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(0.253f, 0.02f, -0.251f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0584", "Odogu_SimpleTable", "\u53F0", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(0.266f, 0.02f, 0.265f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0584", "Odogu_SimpleTable", "\u53F0", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", new Vector3(0.241f, 0.02f, -0.296f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0584", "Odogu_SimpleTable", "\u53F0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", new Vector3(-0.24f, 0.022f, -0.25f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0584", "Odogu_SimpleTable", "\u53F0", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", new Vector3(0.265f, 0.02f, 0.242f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0584", "Odogu_SimpleTable", "\u53F0", null, new Vector3(-0.25f, -0.19f, -0.03f), new Vector3(0f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0584", "Odogu_SimpleTable", "\u53F0", "\u30E1\u30A4\u30C9\u90E8\u5C4B", new Vector3(0.23f, 0.02f, 0.24f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0584", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0.2f, 0.03f, 0.25f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0584", "Odogu_SimpleTable", "\u53F0", "\u5C45\u9152\u5C4B", new Vector3(0.25f, 0.03f, 0.25f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0584", "Odogu_SimpleTable", "\u53F0", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(0.25f, 0.03f, 0.23f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0584", "Odogu_SimpleTable", "\u53F0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", new Vector3(0.23f, 0.03f, 0.22f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 359.0316f, 0f)),
            new PoseProp("0584", "Odogu_SimpleTable", "\u53F0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", new Vector3(0.253f, 0.02f, 0.25f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0584", "Odogu_SimpleTable", "\u53F0", "\u5730\u4E0B\u5BA4", new Vector3(0.24f, 0.02f, 0.25f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0584", "Odogu_SimpleTable", "\u53F0", null, new Vector3(-0.25f, -0.19f, -0.03f), new Vector3(0f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0584", "Odogu_SimpleTable", "\u53F0", "\u5287\u5834", new Vector3(0.25f, 0.028f, 0.3f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0584", "Odogu_SimpleTable", "\u53F0", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(-0.27f, 0.03f, 0.23f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0584", "Odogu_SimpleTable", "\u53F0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(-0.2f, 0.03f, 0.27f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0584", "Odogu_SimpleTable", "\u53F0", "SM\u30AF\u30E9\u30D6", new Vector3(-0.2f, 0.03f, 0.27f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0610", "odogu_kousokumachine_chair", "\u62D8\u675F\u53F0", null, new Vector3(-0.004f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0610", "odogu_kousokumachine_chair", "\u62D8\u675F\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(-0.004f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0610", "odogu_kousokumachine_chair", "\u62D8\u675F\u53F0", null, new Vector3(-0.004f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0610", "Odogu_KousokuMachine_Console", "\u64CD\u4F5C\u7AEF\u672B", null, new Vector3(1.15f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0610", "Odogu_KousokuMachine_Console", "\u64CD\u4F5C\u7AEF\u672B", "SM\u30AF\u30E9\u30D6", new Vector3(-0.004f, -0.001f, 1.25f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0610", "Odogu_KousokuMachine_Console", "\u64CD\u4F5C\u7AEF\u672B", null, new Vector3(1.15f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0610", "odogu_kousokumachine_chair", "\u62D8\u675F\u53F0", "SM\u30AF\u30E9\u30D6", new Vector3(-0.004f, -0.001f, -0.01f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0610", "Odogu_KousokuMachine_Console", "\u64CD\u4F5C\u7AEF\u672B", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(1.15f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0611", "Odogu_KousokuMachine_Console", "\u64CD\u4F5C\u7AEF\u672B", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(1.03f, 0f, 0.1f), new Vector3(-90f, -15f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0611", "Odogu_KousokuMachine_ArmDenma", "\u62D8\u675F\u30DE\u30B7\u30F3\u30A2\u30FC\u30E0\u96FB\u30DE", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(-0.004f, 0f, 0.01f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0611", "Odogu_KousokuMachine_ArmDenma", "\u62D8\u675F\u30DE\u30B7\u30F3\u30A2\u30FC\u30E0\u96FB\u30DE", "SM\u30AF\u30E9\u30D6", new Vector3(-0.004f, -0.001f, -0.01f), new Vector3(0f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0611", "Odogu_KousokuMachine_Console", "\u64CD\u4F5C\u7AEF\u672B", null, new Vector3(1.03f, 0f, 0.1f), new Vector3(-90f, -15f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0611", "odogu_kousokumachine_chair", "\u62D8\u675F\u53F0", null, new Vector3(-0.004f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0611", "Odogu_KousokuMachine_Armbase", "\u62D8\u675F\u30DE\u30B7\u30F3\u30A2\u30FC\u30E0\u30D9\u30FC\u30B9", null, new Vector3(-0.004f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0611", "Odogu_KousokuMachine_Console", "\u64CD\u4F5C\u7AEF\u672B", null, new Vector3(1.03f, 0f, 0.1f), new Vector3(-90f, -15f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0611", "Odogu_KousokuMachine_ArmDenma", "\u62D8\u675F\u30DE\u30B7\u30F3\u30A2\u30FC\u30E0\u96FB\u30DE", null, new Vector3(-0.004f, 0f, 0.01f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0611", "Odogu_KousokuMachine_Armbase", "\u62D8\u675F\u30DE\u30B7\u30F3\u30A2\u30FC\u30E0\u30D9\u30FC\u30B9", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(-0.004f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0611", "odogu_kousokumachine_chair", "\u62D8\u675F\u53F0", null, new Vector3(-0.004f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0611", "odogu_kousokumachine_chair", "\u62D8\u675F\u53F0", "SM\u30AF\u30E9\u30D6", new Vector3(-0.004f, -0.001f, -0.01f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0611", "odogu_kousokumachine_chair", "\u62D8\u675F\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(-0.004f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0611", "odogu_KousokuMachine_armbase", "\u62D8\u675F\u30DE\u30B7\u30F3\u30A2\u30FC\u30E0\u30D9\u30FC\u30B9", "SM\u30AF\u30E9\u30D6", new Vector3(-0.004f, -0.001f, -0.01f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0611", "Odogu_KousokuMachine_Console", "\u64CD\u4F5C\u7AEF\u672B", "SM\u30AF\u30E9\u30D6", new Vector3(-0.004f, -0.001f, 1.25f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0611", "Odogu_KousokuMachine_Armbase", "\u62D8\u675F\u30DE\u30B7\u30F3\u30A2\u30FC\u30E0\u30D9\u30FC\u30B9", null, new Vector3(-0.004f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0611", "Odogu_KousokuMachine_ArmDenma", "\u62D8\u675F\u30DE\u30B7\u30F3\u30A2\u30FC\u30E0\u96FB\u30DE", null, new Vector3(-0.004f, 0f, 0.01f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0612", "odogu_kousokumachine_chair", "\u62D8\u675F\u53F0", "SM\u30AF\u30E9\u30D6", new Vector3(-0.004f, -0.001f, -0.01f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0612", "Odogu_KousokuMachine_Armbase", "\u62D8\u675F\u30DE\u30B7\u30F3\u30A2\u30FC\u30E0\u30D9\u30FC\u30B9", null, new Vector3(-0.004f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0612", "Odogu_KousokuMachine_ArmVibe", "\u62D8\u675F\u30DE\u30B7\u30F3\u30A2\u30FC\u30E0\u30D0\u30A4\u30D6", null, new Vector3(-0.004f, 0f, 0.01f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0612", "Odogu_KousokuMachine_ArmVibe", "\u62D8\u675F\u30DE\u30B7\u30F3\u30A2\u30FC\u30E0\u30D0\u30A4\u30D6", "SM\u30AF\u30E9\u30D6", new Vector3(-0.004f, -0.001f, -0.01f), new Vector3(0f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0612", "Odogu_KousokuMachine_Armbase", "\u62D8\u675F\u30DE\u30B7\u30F3\u30A2\u30FC\u30E0\u30D9\u30FC\u30B9", null, new Vector3(-0.004f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0612", "Odogu_KousokuMachine_Console", "\u64CD\u4F5C\u7AEF\u672B", null, new Vector3(1.03f, 0f, 0.1f), new Vector3(-90f, -15f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0612", "odogu_kousokumachine_chair", "\u62D8\u675F\u53F0", null, new Vector3(-0.004f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0612", "Odogu_KousokuMachine_ArmVibe", "\u62D8\u675F\u30DE\u30B7\u30F3\u30A2\u30FC\u30E0\u30D0\u30A4\u30D6", null, new Vector3(-0.004f, 0f, 0.01f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0612", "Odogu_KousokuMachine_Console", "\u64CD\u4F5C\u7AEF\u672B", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(1.03f, 0f, 0.1f), new Vector3(-90f, -15f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0612", "Odogu_KousokuMachine_Console", "\u64CD\u4F5C\u7AEF\u672B", null, new Vector3(1.03f, 0f, 0.1f), new Vector3(-90f, -15f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0612", "Odogu_KousokuMachine_Armbase", "\u62D8\u675F\u30DE\u30B7\u30F3\u30A2\u30FC\u30E0\u30D9\u30FC\u30B9", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(-0.004f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0612", "odogu_KousokuMachine_armbase", "\u62D8\u675F\u30DE\u30B7\u30F3\u30A2\u30FC\u30E0\u30D9\u30FC\u30B9", "SM\u30AF\u30E9\u30D6", new Vector3(-0.004f, -0.001f, -0.01f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0612", "odogu_kousokumachine_chair", "\u62D8\u675F\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(-0.004f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0612", "Odogu_KousokuMachine_ArmVibe", "\u62D8\u675F\u30DE\u30B7\u30F3\u30A2\u30FC\u30E0\u30D0\u30A4\u30D6", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(-0.004f, 0f, 0.01f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0612", "Odogu_KousokuMachine_Console", "\u64CD\u4F5C\u7AEF\u672B", "SM\u30AF\u30E9\u30D6", new Vector3(-0.004f, -0.001f, 1.25f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0612", "odogu_kousokumachine_chair", "\u62D8\u675F\u53F0", null, new Vector3(-0.004f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0613", "Odogu_KousokuMachine_Console", "\u64CD\u4F5C\u7AEF\u672B", "SM\u30AF\u30E9\u30D6", new Vector3(-0.004f, -0.001f, 1.25f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0613", "odogu_kousokumachine_chair", "\u62D8\u675F\u53F0", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(-0.004f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0613", "odogu_kousokumachine_chair", "\u62D8\u675F\u53F0", null, new Vector3(-0.004f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0613", "Odogu_KousokuMachine_Console", "\u64CD\u4F5C\u7AEF\u672B", null, new Vector3(1.15f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0613", "Odogu_KousokuMachine_Console", "\u64CD\u4F5C\u7AEF\u672B", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(1.15f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0613", "Odogu_KousokuMachine_Console", "\u64CD\u4F5C\u7AEF\u672B", null, new Vector3(1.15f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0613", "odogu_kousokumachine_chair", "\u62D8\u675F\u53F0", "SM\u30AF\u30E9\u30D6", new Vector3(-0.004f, -0.001f, -0.01f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0613", "odogu_kousokumachine_chair", "\u62D8\u675F\u53F0", null, new Vector3(-0.004f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0623", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(-0.083f, 0f, 0.8f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0623", "Odogu_SimpleTable", "\u53F0", null, new Vector3(-0.083f, 0f, 0.8f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0623", "Odogu_SimpleTable", "\u53F0", null, new Vector3(-0.083f, 0f, 0.8f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0623", "Odogu_SimpleTable", "\u53F0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(-0.083f, 0f, 0.8f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0650", "Odogu_SMRoom2_Haritsuke", "\u78D4\u53F0\u5EA7", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(0f, 0f, 0.005f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0650", "Odogu_SMRoom_Haritsuke", "\u78D4\u53F0\u5EA7", null, new Vector3(0f, -0.01f, 0f), new Vector3(-90f, -90f, 0f), new Vector3(0f, -90f, 0f)),
            new PoseProp("0650", "Odogu_SMRoom2_Haritsuke", "\u78D4\u53F0\u5EA7", null, new Vector3(0f, 0f, 0.001f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0650", "Odogu_SMRoom2_Haritsuke", "\u78D4\u53F0\u5EA7", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(0f, 0f, 0.005f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0650", "Odogu_SMRoom2_Haritsuke", "\u78D4\u53F0\u5EA7", null, new Vector3(0f, 0f, 0.001f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0650", "Odogu_SMRoom_Haritsuke", "\u78D4\u53F0\u5EA7", null, new Vector3(0f, -0.01f, 0f), new Vector3(-90f, -90f, 0f), new Vector3(0f, -90f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", "SM\u30AF\u30E9\u30D6", new Vector3(-0.016018f, 0.01f, 0.56f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(-0.015679f, 0f, -0.499383f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", "\u5730\u4E0B\u5BA4", new Vector3(-0.574f, 0.0092f, 0.023f), new Vector3(-90f, 2.834246f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", "\u30C8\u30A4\u30EC", new Vector3(-2.911f, 0f, -0.003f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", new Vector3(-0.566f, -0.166f, -0.258f), new Vector3(-5.008986f, 5.008968f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", null, new Vector3(0.535f, 0.01f, -0.006f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", new Vector3(-0.302561f, -0.008338f, 0.410486f), new Vector3(-90f, -125f, 0f), new Vector3(0f, 325f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", "\u5C45\u9152\u5C4B", new Vector3(-0.01f, 0.013f, -0.544f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", new Vector3(-0.315f, 0.01f, 0.467f), new Vector3(-90f, 55f, 0f), new Vector3(0f, 325f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(-1.324319f, 0.001621f, 0.50097f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", null, new Vector3(0.535f, 0.01f, -0.006f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", null, new Vector3(0.484515f, -0.008387f, 0.000781f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(-0.011f, 0.02f, 0.56f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", "SM\u30AF\u30E9\u30D6", new Vector3(-0.016018f, 0.03f, 1.56f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(-0.02f, -0.01f, 0.5f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(-0.501848f, 0f, 0.000779f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", "\u5287\u5834", new Vector3(-0.014681f, 0.002f, 0.530953f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", "\u5C45\u9152\u5C4B", new Vector3(-0.01f, -0.007f, -0.49f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", "\u5730\u4E0B\u5BA4", new Vector3(-0.516f, -0.006f, 0.001f), new Vector3(-90f, -177.1658f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", "\u30C8\u30A4\u30EC", new Vector3(-2.951f, 0f, -0.003f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(-1.324319f, 0.001621f, 0.50097f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", null, new Vector3(0.484515f, -0.008387f, 0.000781f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", "\u5287\u5834", new Vector3(-0.009f, 0.0142f, 0.587f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(-0.017f, 0f, 0.552f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(-0.007f, 0.01f, 0.55f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(-0.011f, 0.02f, 0.56f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(-0.015681f, -0.003334f, 0.500937f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(-0.444f, 0.01f, -0.344f), new Vector3(-90f, -40f, 0f), new Vector3(0f, 230f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(-0.379f, -0.01f, -0.324f), new Vector3(-90f, 140f, 0f), new Vector3(0f, 230f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_A", "\u30AE\u30ED\u30C1\u30F3\u53F001", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", new Vector3(-0.5f, -0.153f, -0.28f), new Vector3(-5.008979f, 5.008965f, 180f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(-0.025f, 0.01f, -0.55f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0651", "Odogu_Girochin_B", "\u30AE\u30ED\u30C1\u30F3\u53F002", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(-0.555f, 0.01f, 0.009f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0652", "Odogu_SMRoom2_Haritsuke", "\u78D4\u53F0\u5EA7", null, new Vector3(0f, 0f, 0.001f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0652", "Odogu_SMRoom2_Haritsuke", "\u78D4\u53F0\u5EA7", null, new Vector3(0f, 0f, 0.001f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0652", "Odogu_SMRoom_Haritsuke", "\u78D4\u53F0\u5EA7", null, new Vector3(0f, -0.01f, 0f), new Vector3(-90f, -90f, 0f), new Vector3(0f, -90f, 0f)),
            new PoseProp("0652", "Odogu_SMRoom2_Haritsuke", "\u78D4\u53F0\u5EA7", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(0f, 0f, 0.005f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0652", "Odogu_SMRoom_Haritsuke", "\u78D4\u53F0\u5EA7", null, new Vector3(0f, -0.01f, 0f), new Vector3(-90f, -90f, 0f), new Vector3(0f, -90f, 0f)),
            new PoseProp("0652", "Odogu_SMRoom2_Haritsuke", "\u78D4\u53F0\u5EA7", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(0f, 0f, 0.005f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5C45\u9152\u5C4B", new Vector3(0.005f, 0f, 0.002f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(0f, 0f, 0.01f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(0.001f, 0f, -0.001f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30E1\u30A4\u30C9\u90E8\u5C4B", new Vector3(-0.001f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5730\u4E0B\u5BA4", new Vector3(0f, 0f, 0.005f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", new Vector3(-0.004f, 0f, -0.004f), new Vector3(-90f, 135f, 0f), new Vector3(0f, 135f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30C8\u30A4\u30EC", new Vector3(0.009f, 0f, -0.003f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(-0.004f, 0f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", new Vector3(0.01f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(0.001f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(-0.01f, 0f, 0f), new Vector3(-90f, 50f, 0f), new Vector3(0f, 50f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "SM\u30AF\u30E9\u30D6", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", new Vector3(0f, 0.008f, -0.05f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", new Vector3(-0.00514f, 0f, 0.031506f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(0f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0670", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", new Vector3(0f, 0f, 0f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5730\u4E0B\u5BA4", new Vector3(0f, 0f, 0.005f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", new Vector3(0.01f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u8ABF\u6559\u5730\u4E0B\u5BA4", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", new Vector3(-0.004f, 0f, -0.004f), new Vector3(-90f, 135f, 0f), new Vector3(0f, 135f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30C8\u30A4\u30EC", new Vector3(0.009f, 0f, -0.003f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "SM\u30AF\u30E9\u30D6", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5C45\u9152\u5C4B", new Vector3(0.005f, 0f, 0.002f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", new Vector3(0f, 0f, 0f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", null, new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", new Vector3(0f, 0.008f, -0.05f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", new Vector3(0f, 0f, 0.01f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", new Vector3(0.001f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", new Vector3(0f, 0f, 0f), new Vector3(-90f, -90f, 0f), new Vector3(0f, 270f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30E1\u30A4\u30C9\u90E8\u5C4B", new Vector3(-0.001f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30E9\u30D6\u30DB\u30C6\u30EB", new Vector3(-0.004f, 0f, 0f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", new Vector3(-0.01f, 0f, 0f), new Vector3(-90f, 50f, 0f), new Vector3(0f, 50f, 0f)),
            new PoseProp("0671", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", new Vector3(0f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0672", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.246f, -0.779f, -0.331f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0672", "Odogu_SimpleTable", "\u53F0", "\u5E1D\u56FD\u8358\uFF12", new Vector3(0.246f, -0.779f, -0.331f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0672", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.246f, -0.779f, -0.331f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0672a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.246f, -0.26f, -0.331f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0672a", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.246f, -0.26f, -0.331f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0672a", "Odogu_SimpleTable", "\u53F0", "\u5E1D\u56FD\u8358\uFF12", new Vector3(0.246f, -0.26f, -0.331f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0680", "Odogu_Apartment_Futon", "\u5E03\u56E3", null, new Vector3(0.001f, -0.029f, 0.096f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 358.6045f, 0f)),
            new PoseProp("0680", "Odogu_Apartment_Futon", "\u5E03\u56E3", "\u5E1D\u56FD\u8358\uFF12", new Vector3(0.001f, -0.029f, 0.096f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 358.6045f, 0f)),
            new PoseProp("0680", "Odogu_Apartment_Futon", "\u5E03\u56E3", null, new Vector3(0.001f, -0.029f, 0.096f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 358.6045f, 0f)),
            new PoseProp("0681", "Odogu_Apartment_Futon", "\u5E03\u56E3", null, new Vector3(-0.008f, -0.033f, 0.377f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0681", "Odogu_Apartment_Futon", "\u5E03\u56E3", null, new Vector3(-0.008f, -0.033f, 0.377f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0681", "Odogu_Apartment_Futon", "\u5E03\u56E3", "\u5E1D\u56FD\u8358\uFF12", new Vector3(-0.008f, -0.033f, 0.377f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0682", "Odogu_Apartment_Futon", "\u5E03\u56E3", null, new Vector3(-0.021f, -0.039f, -0.043f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0682", "Odogu_Apartment_Futon", "\u5E03\u56E3", "\u5E1D\u56FD\u8358\uFF12", new Vector3(-0.021f, -0.039f, -0.043f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0682", "Odogu_Apartment_Futon", "\u5E03\u56E3", null, new Vector3(-0.021f, -0.039f, -0.043f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0683", "Odogu_Apartment_Futon", "\u5E03\u56E3", null, new Vector3(0.192f, -0.02f, -0.043f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0683", "Odogu_Apartment_Futon", "\u5E03\u56E3", "\u5E1D\u56FD\u8358\uFF12", new Vector3(0.192f, -0.02f, -0.043f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0683", "Odogu_Apartment_Futon", "\u5E03\u56E3", null, new Vector3(0.192f, -0.02f, -0.043f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 180f, 0f)),
            new PoseProp("0695", "Odogu_Apartment_Cardboard_001", "\u30C0\u30F3\u30DC\u30FC\u30EBa", null, new Vector3(0.06f, 0f, -0.41f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0695", "Odogu_Apartment_Cardboard_001", "\u30C0\u30F3\u30DC\u30FC\u30EBa", null, new Vector3(0.06f, 0f, -0.41f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("0695", "Odogu_Apartment_Cardboard_001", "\u30C0\u30F3\u30DC\u30FC\u30EBa", "\u5E1D\u56FD\u8358\uFF12", new Vector3(0.06f, 0f, -0.41f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            new PoseProp("vr_0055rotenburo", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", null, new Vector3(0f, -0.011f, 0.016f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 178.8071f, 0f)),
            new PoseProp("vr_0056rotenburo", "Odogu_Mat", "\u30DE\u30C3\u30C8", null, new Vector3(0.006f, -0.12f, 0.198f), new Vector3(-90f, 0f, 0f), new Vector3(359.552f, 358.6837f, 0.928456f)),
            new PoseProp("vr_0058rotenburo", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", null, new Vector3(0f, -0.011f, 0.016f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 178.8071f, 0f)),
            new PoseProp("vr_0059rotenburo", "Odogu_Sukebeisu", "\u30B9\u30B1\u30D9\u6905\u5B50", null, new Vector3(0f, -0.011f, 0.016f), new Vector3(-90f, 180f, 0f), new Vector3(0f, 178.8071f, 0f)),
            new PoseProp("vr_0140rotenburo", "Odogu_Mat", "\u30DE\u30C3\u30C8", null, new Vector3(0.006f, -0.12f, 0.198f), new Vector3(-90f, 0f, 0f), new Vector3(359.552f, 358.6837f, 0.928456f)),
            new PoseProp("vr_0142villa_bedroom", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.729095f, -0.001f, 0.317405f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 272.8701f, 0f)),
            new PoseProp("vr_0143villa_bedroom", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.729095f, -0.001f, 0.317405f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 272.8701f, 0f)),
            new PoseProp("vr_0147villa_bedroom", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.322795f, -0.002f, 0.248405f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 272.8701f, 0f)),
            new PoseProp("vr_0148villa_bedroom", "Odogu_SimpleTable", "\u53F0", null, new Vector3(0.322795f, -0.002f, 0.248405f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 272.8701f, 0f)),
            new PoseProp("vr_0152rotenburo", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", null, new Vector3(0.005486f, -0.015f, 0.029694f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 2.679132f, 0f)),
            new PoseProp("vr_0152villa", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", null, new Vector3(0.036f, -0.017f, -0.004326f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90.97025f, 0f)),
            new PoseProp("vr_0152villa_bedroom", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", null, new Vector3(0.032953f, -0.016f, -0.00425f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 89.25026f, 0f)),
            new PoseProp("vr_0152villa_farm", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", null, new Vector3(0.004713f, -0.024f, 0.030888f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 3.875794f, 0f)),
            new PoseProp("vr_0153rotenburo", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", null, new Vector3(0.005486f, -0.015f, 0.029694f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 2.679132f, 0f)),
            new PoseProp("vr_0153villa", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", null, new Vector3(0.036f, -0.017f, -0.004326f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 90.97025f, 0f)),
            new PoseProp("vr_0153villa_bedroom", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", null, new Vector3(0.032953f, -0.016f, -0.00425f), new Vector3(-90f, 90f, 0f), new Vector3(0f, 89.25026f, 0f)),
            new PoseProp("vr_0153villa_farm", "Odogu_DildoBox", "\u30C7\u30A3\u30EB\u30C9\uFF06\u53F0", null, new Vector3(0.004713f, -0.024f, 0.030888f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 3.875794f, 0f)),
            new PoseProp("vr_0186villa_bedroom", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", null, new Vector3(-3.42f, -0.037f, 0.35f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 359.7036f, 0f)),
            new PoseProp("vr_0187villa_bedroom", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", null, new Vector3(-3.42f, -0.037f, 0.35f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 359.7036f, 0f)),
            new PoseProp("vr_0188villa_bedroom", "Odogu_LoveSofa", "\u30E9\u30D6\u30BD\u30D5\u30A1", null, new Vector3(-0.000105f, -0.042f, 0.023f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 359.606f, 0f)),
        };

        // v1.7.3 (issue 6): part -> stage names that have @if branches in the
        // part's FT variants (exact-entry eligibility + cross-stage position
        // preservation; generated by _scan_ft_full_regen.ps1 + _build_prop_table_regen.ps1).
        private static readonly string[][] PartStageNames = new string[][]
        {
// auto-generated: part -> branch stage names (v1.7.3)
            new string[] { "0001", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u548C\u98A8\u65C5\u9928", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0001_x1a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0001_x1b", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0001a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0001b", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0001c", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0002", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30AB\u30E9\u30AA\u30B1\u30EB\u30FC\u30E0", "\u30B3\u30B9\u30D7\u30EC\u4F1A\u5834", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C0\u30F3\u30B9\u30D0\u30FC", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30D6\u30C6\u30A3\u30C3\u30AF", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u548C\u98A8\u65C5\u9928", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6559\u5BA4", "\u65B0\u57F7\u52D9\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u796D\u308A\u306E\u795E\u793E", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F", "\u96FB\u8ECA" },
            new string[] { "0002_x1a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30AB\u30E9\u30AA\u30B1\u30EB\u30FC\u30E0", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30D6\u30C6\u30A3\u30C3\u30AF", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u65B0\u57F7\u52D9\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u796D\u308A\u306E\u795E\u793E", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0002a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u65B0\u57F7\u52D9\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0003", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0003_x1a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0003a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0003b", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0003c", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0003d", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0004", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0004a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0004b", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0004c", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0005", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30AB\u30E9\u30AA\u30B1\u30EB\u30FC\u30E0", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0005a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30AB\u30E9\u30AA\u30B1\u30EB\u30FC\u30E0", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0005c", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30AB\u30E9\u30AA\u30B1\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0006", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30AB\u30E9\u30AA\u30B1\u30EB\u30FC\u30E0", "\u30B3\u30B9\u30D7\u30EC\u4F1A\u5834", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C0\u30F3\u30B9\u30D0\u30FC", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30D6\u30C6\u30A3\u30C3\u30AF", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u548C\u98A8\u65C5\u9928", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6559\u5BA4", "\u65B0\u57F7\u52D9\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u796D\u308A\u306E\u795E\u793E", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F", "\u96FB\u8ECA" },
            new string[] { "0006_x1a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30AB\u30E9\u30AA\u30B1\u30EB\u30FC\u30E0", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30D6\u30C6\u30A3\u30C3\u30AF", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u65B0\u57F7\u52D9\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u796D\u308A\u306E\u795E\u793E", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0006a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0006b", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0006c", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0006d", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0007", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0008", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0009", "\u30B3\u30B9\u30D7\u30EC\u4F1A\u5834", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB" },
            new string[] { "0010", "\u30B3\u30B9\u30D7\u30EC\u4F1A\u5834", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0010a", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0011", "\u30B3\u30B9\u30D7\u30EC\u4F1A\u5834", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30D6\u30C6\u30A3\u30C3\u30AF" },
            new string[] { "0012", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB" },
            new string[] { "0012a", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB" },
            new string[] { "0013", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0013a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0014", "\u5287\u5834" },
            new string[] { "0015", "\u30B3\u30B9\u30D7\u30EC\u4F1A\u5834", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB" },
            new string[] { "0016", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30AB\u30E9\u30AA\u30B1\u30EB\u30FC\u30E0", "\u30B3\u30B9\u30D7\u30EC\u4F1A\u5834", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C0\u30F3\u30B9\u30D0\u30FC", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30D6\u30C6\u30A3\u30C3\u30AF", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u548C\u98A8\u65C5\u9928", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6559\u5BA4", "\u65B0\u57F7\u52D9\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u796D\u308A\u306E\u795E\u793E", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F", "\u96FB\u8ECA" },
            new string[] { "0016_x1a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30AB\u30E9\u30AA\u30B1\u30EB\u30FC\u30E0", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30D6\u30C6\u30A3\u30C3\u30AF", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u65B0\u57F7\u52D9\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u796D\u308A\u306E\u795E\u793E", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0017", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0018", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8" },
            new string[] { "0019", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0020", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8" },
            new string[] { "0020a", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8" },
            new string[] { "0021", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0022", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0023", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8" },
            new string[] { "0024", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0024a", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0025", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5730\u4E0B\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0025_x1a", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5730\u4E0B\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0025a", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5730\u4E0B\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0026", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0027", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0028", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0029", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0030", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0031", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0031a", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0032", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0032a", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0033", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0034", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0035", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0036", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0037", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0037a", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0038", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0039", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0040", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0040a", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0041", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0041a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0042", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0043", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0044", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30AB\u30E9\u30AA\u30B1\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0045", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0046", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0047", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u65B0\u57F7\u52D9\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0047a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u65B0\u57F7\u52D9\u5BA4" },
            new string[] { "0048", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0049", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B3\u30B9\u30D7\u30EC\u4F1A\u5834", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0050", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0050a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA" },
            new string[] { "0050b", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0051", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0052", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0053", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0053a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0055", "\u30B3\u30B9\u30D7\u30EC\u4F1A\u5834", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0055_x1a", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0056", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0057", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0058", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8" },
            new string[] { "0060", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0060_x1a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0060a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0061", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0062", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0062a", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0063", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0" },
            new string[] { "0065", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0065a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0066", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0066a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0067", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0067a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0068", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0068a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0069", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0069_x1a", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0069a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0069b", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0070", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0070_x1a", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0070a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0071", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0071a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0072", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0072a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0073", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0073a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0074", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0074a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0075", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0075a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0076", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0076a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0076b", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0077", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0077a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0078", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0078a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0079", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0079a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0080", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0080a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0081", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0081a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0082", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0082a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0083", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0083a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0084", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0084a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0085", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0085a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0086", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0086a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0087", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0087a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0087b", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0088", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0088_x1a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0089", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0089a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA" },
            new string[] { "0090", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0091", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0092", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0092a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA" },
            new string[] { "0092b", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0093", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0094", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0095", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0096", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0097", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5730\u4E0B\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0097_x1a", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5730\u4E0B\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0098", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0098_x1a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0099", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C0\u30F3\u30B9\u30D0\u30FC", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0100", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0101", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0102", "\u30B3\u30B9\u30D7\u30EC\u4F1A\u5834", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0103", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0104", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0105", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0106", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0110", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0111", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0111a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0112", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30AB\u30E9\u30AA\u30B1\u30EB\u30FC\u30E0", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C0\u30F3\u30B9\u30D0\u30FC", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30D6\u30C6\u30A3\u30C3\u30AF", "\u30D7\u30E9\u30A4\u30D9\u30FC\u30C8\u30EB\u30FC\u30E0", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u65B0\u57F7\u52D9\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u796D\u308A\u306E\u795E\u793E", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0113", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30D7\u30E9\u30A4\u30D9\u30FC\u30C8\u30EB\u30FC\u30E0", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0114", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30D7\u30E9\u30A4\u30D9\u30FC\u30C8\u30EB\u30FC\u30E0", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8" },
            new string[] { "0115", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30D7\u30E9\u30A4\u30D9\u30FC\u30C8\u30EB\u30FC\u30E0", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0116", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0116a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0117", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0117a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0118", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0118a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0119", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0119a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0120", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0120a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0122", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0122a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0123", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0123a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0124", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0124a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0125", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0125a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0126", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0126a", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0127", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0128", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0129", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0130", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0131", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0140", "\u30C8\u30A4\u30EC", "\u5973\u6027\u30C8\u30A4\u30EC" },
            new string[] { "0141", "\u30C8\u30A4\u30EC", "\u5973\u6027\u30C8\u30A4\u30EC" },
            new string[] { "0142", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0150", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0151", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0151a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0152", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0153", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0160", "SM\u30AF\u30E9\u30D6", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0160a", "SM\u30AF\u30E9\u30D6", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0161", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0161a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0162", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0163", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0170", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0170a", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0170b", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0171", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30D6\u30C6\u30A3\u30C3\u30AF", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0171a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30D6\u30C6\u30A3\u30C3\u30AF", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0172", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5730\u4E0B\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0173", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0173a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0174", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA" },
            new string[] { "0180", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0180_x1a", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0180a", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0190", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30D7\u30E9\u30A4\u30D9\u30FC\u30C8\u30EB\u30FC\u30E0", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u65B0\u57F7\u52D9\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0190_x1a", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30D7\u30E9\u30A4\u30D9\u30FC\u30C8\u30EB\u30FC\u30E0", "\u65B0\u57F7\u52D9\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0190a", "SM\u30AF\u30E9\u30D6", "\u30D7\u30E9\u30A4\u30D9\u30FC\u30C8\u30EB\u30FC\u30E0", "\u65B0\u57F7\u52D9\u5BA4" },
            new string[] { "0191", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30D7\u30E9\u30A4\u30D9\u30FC\u30C8\u30EB\u30FC\u30E0", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0192", "\u30D7\u30E9\u30A4\u30D9\u30FC\u30C8\u30EB\u30FC\u30E0" },
            new string[] { "0193", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30D7\u30E9\u30A4\u30D9\u30FC\u30C8\u30EB\u30FC\u30E0", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0194", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30D7\u30E9\u30A4\u30D9\u30FC\u30C8\u30EB\u30FC\u30E0", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0195", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30D7\u30E9\u30A4\u30D9\u30FC\u30C8\u30EB\u30FC\u30E0", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8" },
            new string[] { "0200", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0200a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0201", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0201a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0210", "SM\u30AF\u30E9\u30D6", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0210a", "SM\u30AF\u30E9\u30D6", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0211", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0220", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0221", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0230", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0231", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0240", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0241", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0250", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0250a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0251", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8" },
            new string[] { "0251a", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8" },
            new string[] { "0260", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0260a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0261", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0270", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u65B0\u57F7\u52D9\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0270_x1a", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u65B0\u57F7\u52D9\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0270a", "SM\u30AF\u30E9\u30D6", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u65B0\u57F7\u52D9\u5BA4" },
            new string[] { "0280", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0281", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0290", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0291", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA" },
            new string[] { "0300", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0301", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0310", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0311", "SM\u30AF\u30E9\u30D6" },
            new string[] { "0312", "SM\u30AF\u30E9\u30D6" },
            new string[] { "0313", "\u5287\u5834" },
            new string[] { "0314", "\u5287\u5834" },
            new string[] { "0315", "\u5287\u5834" },
            new string[] { "0316", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0316a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0317", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0318", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0319", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0320", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0320a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0321", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0321a", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0330", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0331", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0340", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0340a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0340b", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0341", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5730\u4E0B\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0350", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240" },
            new string[] { "0351", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF" },
            new string[] { "0352", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF" },
            new string[] { "0353", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF" },
            new string[] { "0354", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF" },
            new string[] { "0355", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF" },
            new string[] { "0356", "SM\u30AF\u30E9\u30D6" },
            new string[] { "0357", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0358", "SM\u30AF\u30E9\u30D6", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0" },
            new string[] { "0359", "SM\u30AF\u30E9\u30D6" },
            new string[] { "0360", "SM\u30AF\u30E9\u30D6" },
            new string[] { "0360a", "SM\u30AF\u30E9\u30D6" },
            new string[] { "0361", "SM\u30AF\u30E9\u30D6" },
            new string[] { "0370", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0370a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0370b", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0371", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0371a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0380", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF" },
            new string[] { "0381", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF" },
            new string[] { "0382", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0383", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0390", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u548C\u98A8\u65C5\u9928", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0391", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u548C\u98A8\u65C5\u9928", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0391a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA" },
            new string[] { "0400", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0401", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0410", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0411", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0411a", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0420", "\u4E3B\u4EBA\u516C\u90E8\u5C4B" },
            new string[] { "0420a", "SM\u30AF\u30E9\u30D6", "\u4E3B\u4EBA\u516C\u90E8\u5C4B" },
            new string[] { "0430", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4" },
            new string[] { "0431", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5730\u4E0B\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0441", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u548C\u98A8\u65C5\u9928", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0450", "\u30B3\u30B9\u30D7\u30EC\u4F1A\u5834", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u796D\u308A\u306E\u795E\u793E", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0451", "\u30B3\u30B9\u30D7\u30EC\u4F1A\u5834", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u796D\u308A\u306E\u795E\u793E", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0451a", "\u30B3\u30B9\u30D7\u30EC\u4F1A\u5834", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u796D\u308A\u306E\u795E\u793E", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0460", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0460a", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0461", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0462", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0463", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0464", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0465", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0470", "\u30AB\u30E9\u30AA\u30B1\u30EB\u30FC\u30E0", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12" },
            new string[] { "0471", "\u30AB\u30E9\u30AA\u30B1\u30EB\u30FC\u30E0", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0480", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0490", "SM\u30AF\u30E9\u30D6", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u5287\u5834" },
            new string[] { "0500", "\u30AB\u30E9\u30AA\u30B1\u30EB\u30FC\u30E0" },
            new string[] { "0501", "\u30AB\u30E9\u30AA\u30B1\u30EB\u30FC\u30E0" },
            new string[] { "0510", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C0\u30F3\u30B9\u30D0\u30FC", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0511", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0520", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0521", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0522", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C0\u30F3\u30B9\u30D0\u30FC", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0523", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF" },
            new string[] { "0524", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF" },
            new string[] { "0525", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0526", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0527", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0530", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12" },
            new string[] { "0540", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0541", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0542", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0543", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0544", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0545", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0546", "SM\u30AF\u30E9\u30D6" },
            new string[] { "0547", "SM\u30AF\u30E9\u30D6" },
            new string[] { "0548", "SM\u30AF\u30E9\u30D6" },
            new string[] { "0549", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0549a", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0550", "SM\u30AF\u30E9\u30D6" },
            new string[] { "0551", "SM\u30AF\u30E9\u30D6" },
            new string[] { "0552", "SM\u30AF\u30E9\u30D6" },
            new string[] { "0553", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0554", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0555", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0560", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0561", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0570", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0571", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0580", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF" },
            new string[] { "0581", "\u30C0\u30F3\u30B9\u30D0\u30FC" },
            new string[] { "0582", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB" },
            new string[] { "0583", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30AB\u30E9\u30AA\u30B1\u30EB\u30FC\u30E0", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30D6\u30C6\u30A3\u30C3\u30AF", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30E9\u30D6\u30DB\u30C6\u30EB\uFF12", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u65B0\u57F7\u52D9\u5BA4", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u796D\u308A\u306E\u795E\u793E", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0584", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8" },
            new string[] { "0585", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0586", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E3\u30EF\u30FC\u30EB\u30FC\u30E0", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CF\u30CD\u30E0\u30FC\u30F3\u30DB\u30C6\u30EB", "\u30D7\u30E9\u30A4\u30D9\u30FC\u30C8\u30EB\u30FC\u30E0", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u6D77\u4E0A\u30B3\u30C6\u30FC\u30B8", "\u96A0\u308C\u5165\u308A\u6C5F" },
            new string[] { "0600", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0601", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5287\u5834", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0610", "SM\u30AF\u30E9\u30D6", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0611", "SM\u30AF\u30E9\u30D6", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0612", "SM\u30AF\u30E9\u30D6", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0613", "SM\u30AF\u30E9\u30D6", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0620", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3" },
            new string[] { "0621", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3" },
            new string[] { "0622", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3" },
            new string[] { "0623", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3" },
            new string[] { "0630", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0631", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0640", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240" },
            new string[] { "0641", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240" },
            new string[] { "0642", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240" },
            new string[] { "0642a", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240" },
            new string[] { "0650", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0651", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C8\u30A4\u30EC", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0652", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0660", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0661", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0662", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0663", "SM\u30AF\u30E9\u30D6", "\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0670", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0671", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0672", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0672a", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0680", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0681", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0682", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0683", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0684", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0685", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0686", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0686a", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0687", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0688", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0689", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0690", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0691", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0692", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0693", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0694", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0695", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0696", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0697", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0697a", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0698", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0699", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0700", "\u5E1D\u56FD\u8358", "\u5E1D\u56FD\u8358\uFF12", "\u5E1D\u56FD\u8358BBQ", "\u5E1D\u56FD\u8358\u30B5\u30A6\u30CA", "\u5E1D\u56FD\u8358\u9732\u5929\u98A8\u5442" },
            new string[] { "0710", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5287\u5834", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0720", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0721", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0722", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0723", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0724", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0725", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0726", "SM\u30AF\u30E9\u30D6", "\u30A2\u30C0\u30EB\u30C8\u30B7\u30E7\u30C3\u30D7", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5973\u6027\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0732", "\u96FB\u8ECA" },
            new string[] { "0733", "\u96FB\u8ECA" },
            new string[] { "0740", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0740a", "\u30C8\u30A4\u30EC", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u30E9\u30D6\u30DB\u30C6\u30EB", "\u30ED\u30C3\u30AB\u30FC\u30EB\u30FC\u30E0", "\u5287\u5834", "\u5730\u4E0B\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5C45\u9152\u5C4B", "\u64AE\u5F71\u30B9\u30BF\u30B8\u30AA", "\u8ABF\u6559\u5730\u4E0B\u5BA4", "\u8ABF\u6559\u5730\u4E0B\u5BA4\uFF12" },
            new string[] { "0741", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0741a", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30AA\u30EC\u30F3\u30B8", "\u30CA\u30A4\u30C8\u30D7\u30FC\u30EB\u30B0\u30EA\u30FC\u30F3", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4" },
            new string[] { "0750", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834" },
            new string[] { "0751", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4BIF", "\u5287\u5834" },
            new string[] { "100000", "SM\u30AF\u30E9\u30D6", "\u30B7\u30E7\u30C3\u30D4\u30F3\u30B0\u30E2\u30FC\u30EB", "\u30BD\u30FC\u30D7\u30E9\u30F3\u30C9", "\u30E1\u30A4\u30C9\u90E8\u5C4B", "\u4E3B\u4EBA\u516C\u90E8\u5C4B", "\u5287\u5834", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30C8\u30A4\u30EC", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u30EA\u30D3\u30F3\u30B0", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u5BDD\u5BA4", "\u5BBF\u6CCA\u65BD\u8A2D\u30FB\u6D17\u9762\u6240" },
        };

        private static Dictionary<string, List<PoseProp>> s_posePropIndex;
        private static Dictionary<string, HashSet<string>> s_partStages;
        private static readonly List<string> s_poseResNames = new List<string>();

        // v1.7.7 (P42) + v1.7.8（融合）: FT 同步段舞台名覆盖窗口 ——
        // CallNormalFile prefix 置位、postfix/finalizer 清零。窗口内
        // TJSFuncGetYotogiSelectStageName 返回「当前技能第一个可玩舞台」
        // 的 uniqueName → FT 的 @if 舞台门控在任何地图上都按官方分支
        // 执行。v1.7.8 融合: 门控块内的定位全是「相对布局」（@Offset 双对
        // 分离/RotateLink/PhisicsHit 相对高差/道具贴锚点摆放）, 但 @AllPos
        // 锚点是官方舞台的绝对坐标 → 后置把双层根还原到 FT 之前的位姿
        // （保留锚点）, 并把窗口内新挂的道具与物理地面高度从「FT 锚点系」
        // 重基底到「保留锚点系」。效果 = 官方相对布局 + 当前地图位置,
        // 角色不浮空/不出图、道具贴角色、物理正常。
        private static bool s_ftStageOverride;
        // v1.7.8: 融合用的前置捕获（FT 之前的位姿与道具快照）
        private static bool s_ftPreValid;
        private static Vector3 s_ftPrePos;
        private static Vector3 s_ftPreRot;
        private static Vector3 s_ftPreOffs;      // m_goAllOffset（@AllOffSet 层）
        private static List<string> s_ftPreProps = new List<string>(); // 挂载道具名字快照
        // v1.8.0 (P43): 归零前位姿保持 —— 官方 OnFinish 在每次切姿势前
        // SetCharaAllPos(0) 归零根, FT prefix 捕获到的 pre 永远是原点
        // （在用户所选地图上未必有效 = 位置突跳/浮空的根源）。P43 在
        // OnFinish 归零之前捕获双层根, FT prefix 优先使用该 hold 值
        // → 重基底还原到上一姿势的实际位置（视角连续）。
        private static bool s_ftHoldValid;
        private static Vector3 s_ftHoldPos;
        private static Vector3 s_ftHoldRot;
        private static Vector3 s_ftHoldOffs;

        // FT raw name -> skill part key. "ft_0610.ks" / "?_FT_04130.ks" /
        // "?1_FT_x.ks" / "FT_VR_0055rotenburo.ks" -> "0610" / "04130" /
        // "x" / "vr_0055rotenburo". Non-FT names (faint system script
        // etc) -> null (no mount trigger).
        private static string DeriveFtPart(string rawFile)
        {
            if (string.IsNullOrEmpty(rawFile))
            {
                return null;
            }
            string s = rawFile.Trim().ToLowerInvariant();
            if (s.EndsWith(".ks"))
            {
                s = s.Substring(0, s.Length - 3);
            }
            if (s.Length == 0)
            {
                return null;
            }
            if (s[0] == '?')
            {
                int i = 1;
                if (i < s.Length && s[i] >= '0' && s[i] <= '4')
                {
                    i++;
                }
                if (!(s.Length > i + 3 && s[i] == '_' && s[i + 1] == 'f' && s[i + 2] == 't' && s[i + 3] == '_'))
                {
                    return null;
                }
                s = s.Substring(i + 4);
            }
            else if (s.Length > 3 && s[0] == 'f' && s[1] == 't' && s[2] == '_')
            {
                s = s.Substring(3);
            }
            else
            {
                return null;
            }
            return (s.Length > 0) ? s : null;
        }

        private static void PoseResBuildIndex()
        {
            if (s_posePropIndex != null)
            {
                return;
            }
            s_posePropIndex = new Dictionary<string, List<PoseProp>>();
            for (int i = 0; i < PoseProps.Length; i++)
            {
                List<PoseProp> lst;
                if (!s_posePropIndex.TryGetValue(PoseProps[i].part, out lst))
                {
                    lst = new List<PoseProp>();
                    s_posePropIndex[PoseProps[i].part] = lst;
                }
                lst.Add(PoseProps[i]);
            }
            s_partStages = new Dictionary<string, HashSet<string>>();
            for (int j = 0; j < PartStageNames.Length; j++)
            {
                string[] row = PartStageNames[j];
                if (row == null || row.Length < 2)
                {
                    continue;
                }
                HashSet<string> set;
                if (!s_partStages.TryGetValue(row[0], out set))
                {
                    set = new HashSet<string>();
                    s_partStages[row[0]] = set;
                }
                for (int k = 1; k < row.Length; k++)
                {
                    set.Add(row[k]);
                }
            }
        }

        // v1.7.3 (issue 6): 当前舞台 uniqueName（与 FT 分支条件
        // GetYotogiSelectStageName()=='...' 的取值同源）。
        private static string CurrentStageName()
        {
            try
            {
                YotogiStageSelectManager.StageExpansionPack sel = YotogiStageSelectManager.SelectedStage;
                if (object.ReferenceEquals(sel, null) || sel.stageData == null)
                {
                    return null;
                }
                return sel.stageData.uniqueName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // One mount pass for a skill part. Event callers: FT postfix /
        // stage switch / cfg re-enable. Anchor = GetCharaAllPos (the
        // same accessor @AllPos writes through SetCharaAllPos; the FT
        // mounts its props in the same stage-root local space).
        // v1.7.3 (issue 6): stage-keyed selection + rotation-aware
        // placement. Exact stage entry when the current stage hits one
        // of the part's FT branches (official branch geometry, identity
        // rebase); otherwise the cross-stage fallback row REBASED from
        // the branch anchor rotation frame into the current chara-root
        // rotation frame: pos = anchor + R_now * (R_branch^-1 * off),
        // rot = R_now * R_branch^-1 * Euler(entry.rot). Fixes the
        // "random prop offset" (branch anchors face different ways per
        // stage; applying a world-frame offset from another branch
        // misplaces large offsets).
        private static void PoseResMountPass(string part, YotogiManager ym)
        {
            try
            {
                if (string.IsNullOrEmpty(part) || ym == null)
                {
                    return;
                }
                PoseResBuildIndex();
                List<PoseProp> props;
                if (!s_posePropIndex.TryGetValue(part, out props) || props == null)
                {
                    return; // this skill has no props in any FT variant
                }
                GameMain gm = GameMain.Instance;
                if (gm == null || gm.BgMgr == null || gm.CharacterMgr == null)
                {
                    return;
                }
                if (ym.IsAllCharaBusy())
                {
                    return; // pose transition in progress (anchor not stable)
                }
                CharacterMgr cm = gm.CharacterMgr;
                string stageNow = CurrentStageName();
                bool partBranchedHere = false;
                HashSet<string> st;
                if (stageNow != null && s_partStages.TryGetValue(part, out st) && st != null)
                {
                    partBranchedHere = st.Contains(stageNow);
                }
                // per-src choice: exact branch entry (stage hit) or the
                // cross-stage fallback row (stage miss). On a branch stage
                // a src without an exact entry is NOT part of this stage's
                // official layout -> skip it.
                // v1.8.0 (修复2): cross-stage 时 null 兜底条目优先, 仍缺的
                // src 回退到第一个 stage 条目 —— 旧逻辑非对应地图只挂
                // stage==null 条目, 姿势道具只存在于 stage 分支时全部
                // 漏挂 = 「有些物品没有实时加载」。回退条目的位置数学
                // 基于已还原的根（postfix 重基底后）, 语义一致。
                Dictionary<string, PoseProp> chosen = new Dictionary<string, PoseProp>();
                for (int k = 0; k < props.Count; k++)
                {
                    PoseProp pp = props[k];
                    if (partBranchedHere)
                    {
                        if (pp.stage != null && pp.stage == stageNow)
                        {
                            chosen[pp.src] = pp;
                        }
                    }
                    else if (pp.stage == null)
                    {
                        if (!chosen.ContainsKey(pp.src))
                        {
                            chosen[pp.src] = pp;
                        }
                    }
                }
                if (!partBranchedHere)
                {
                    for (int k2 = 0; k2 < props.Count; k2++)
                    {
                        PoseProp pp2 = props[k2];
                        if (pp2.stage != null && !chosen.ContainsKey(pp2.src))
                        {
                            chosen[pp2.src] = pp2; // cross-stage 回退: stage 条目
                        }
                    }
                }
                if (chosen.Count == 0)
                {
                    return;
                }
                Vector3 anchor = cm.GetCharaAllPos();
                Quaternion rotNow = Quaternion.Euler(cm.GetCharaAllRot());
                foreach (KeyValuePair<string, PoseProp> kv in chosen)
                {
                    PoseProp pp = kv.Value;
                    if (gm.BgMgr.GetPrefabFromBg(pp.name) != null)
                    {
                        continue; // already mounted by official FT script
                    }
                    string myName = "YU_" + pp.src;
                    if (gm.BgMgr.GetPrefabFromBg(myName) != null)
                    {
                        continue; // already mounted by this feature
                    }
                    Quaternion rel = Quaternion.Inverse(Quaternion.Euler(pp.anchorRot));
                    Vector3 pos = anchor + rotNow * (rel * pp.off);
                    Quaternion rotQ = rotNow * rel * Quaternion.Euler(pp.rot);
                    GameObject go = gm.BgMgr.AddPrefabToBg(pp.src, myName, "",
                        pos, rotQ.eulerAngles);
                    if (go != null && !s_poseResNames.Contains(myName))
                    {
                        s_poseResNames.Add(myName);
                        if (!object.ReferenceEquals(log, null))
                        {
                            log.LogInfo("YotogiUnlimited: pose resource mounted [" + pp.src + "] for ft " + part
                                + ((pp.stage != null) ? (" @" + pp.stage) : " (cross-stage)"));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogOnce("PoseResMountPass: " + e.Message);
            }
        }

        // Mount the CURRENT playing skill's props (stage-switch / toggle channels).
        private static void PoseResMountCurrent(YotogiManager ym)
        {
            try
            {
                if (ym == null || ym.playing_skill == null || ym.playing_skill.skill_pair == null
                    || ym.playing_skill.skill_pair.base_data == null)
                {
                    return;
                }
                Skill.Data sd = ym.playing_skill.skill_pair.base_data;
                if (string.IsNullOrEmpty(sd.start_call_file))
                {
                    return;
                }
                string part = DeriveFtPart(sd.start_call_file);
                if (part == null)
                {
                    return;
                }
                PoseResMountPass(part, ym);
            }
            catch (Exception)
            {
            }
        }

        private static void PoseResClear()
        {
            try
            {
                GameMain gm = GameMain.Instance;
                if (gm != null && gm.BgMgr != null)
                {
                    for (int i = 0; i < s_poseResNames.Count; i++)
                    {
                        gm.BgMgr.DelPrefabFromBg(s_poseResNames[i]);
                    }
                }
            }
            catch (Exception)
            {
            }
            s_poseResNames.Clear();
        }

        // =================================================================
        // P31 (v1.7.2): ScriptManager.TJSFuncGetMaidStatus null guard.
        // =================================================================
        public static bool GetMaidStatusSafe_Prefix(TJSVariantRef[] tjs_param, TJSVariantRef result)
        {
            try
            {
                if (tjs_param == null || tjs_param.Length < 2)
                {
                    return true; // let the original assert handle arg issues
                }
                CharacterMgr cm = (GameMain.Instance != null) ? GameMain.Instance.CharacterMgr : null;
                Maid m = (cm != null) ? cm.GetMaid(tjs_param[0].AsInteger()) : null;
                if (m == null)
                {
                    if (result != null)
                    {
                        result.SetString("");
                    }
                    return false; // skip original (the NRE source)
                }
            }
            catch (Exception)
            {
                return true; // never block the original on our own errors
            }
            return true;
        }

        // =================================================================
        // P32 (v1.7.2→v1.7.3): CallNormalFile prefix（跨舞台位置捕获）
        // + postfix（位置还原 + P30v2 事件触发挂载）+ finalizer（未知 NRE
        // 不再能杀死 FT 协程/卡死画面）。
        // =================================================================
        // v1.7.3 (issue 5): 当前舞台不在该技能原始可玩舞台表
        // （Skill.Data.playable_stageid_list, P1/P2 解锁只旁路判定不破坏
        // 数据）内 = 「非对应地图」——FT 的舞台无关 @AllPos 会把角色根
        // 传送到「姿势的原坐标」（为官方舞台设计, 在本地图上错位）。
        // prefix 捕获根位姿, postfix 在 FT 同步段执行完后还原（姿势在
        // 当前位置播放）; BoneHitHeightY 一并回退。对应地图（表内含
        // 当前舞台）保持官方锚定——床等舞台内置家具对齐不受影响。
        private static bool StageIsPlayableForCurrentSkill(YotogiManager ym)
        {
            // true = 对应地图（保持官方锚定）; false = 非对应（还原位置）
            try
            {
                if (ym == null || ym.playing_skill == null || ym.playing_skill.skill_pair == null
                    || ym.playing_skill.skill_pair.base_data == null)
                {
                    return true; // 数据不明: 不干预（原版行为）
                }
                YotogiStageSelectManager.StageExpansionPack sel = YotogiStageSelectManager.SelectedStage;
                if (object.ReferenceEquals(sel, null) || sel.stageData == null)
                {
                    return true;
                }
                Skill.Data sd = ym.playing_skill.skill_pair.base_data;
                if (sd.playable_stageid_list == null || sd.playable_stageid_list.Count == 0)
                {
                    return true; // 无舞台绑定信息: 原版行为（FT 锚定任意舞台）
                }
                return sd.playable_stageid_list.Contains(sel.stageData.id);
            }
            catch (Exception)
            {
                return true;
            }
        }

        public static void CallNormalFile_Prefix(string file, string label)
        {
            s_ftStageOverride = false;
            // v1.7.6 (P40/P41 配套): FT2/KA 带 label 调用（*KA0/*KA1/*KA2/*RR*）
            // 的入口记录——3P 动作链的第一环。若用户日志中切姿势时缺此行
            // = start_call_file2 未加载（性格变体缺失或 CallFtFiles 卡 busy）。
            if (!string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(file))
            {
                try
                {
                    if (!object.ReferenceEquals(log, null) && YotogiManager.instans != null)
                    {
                        log.LogInfo("yotogicall: file=" + file + " label=" + label);
                    }
                }
                catch (Exception)
                {
                }
            }
            try
            {
                if (!string.IsNullOrEmpty(label) || string.IsNullOrEmpty(file))
                {
                    return; // FT2/KA labeled calls don't trigger
                }
                if (object.ReferenceEquals(cfgPoseResource, null) || !cfgPoseResource.Value)
                {
                    return;
                }
                YotogiManager ym = YotogiManager.instans;
                if (ym == null)
                {
                    return;
                }
                if (DeriveFtPart(file) == null)
                {
                    return; // not FT-shaped (faint system script etc.)
                }
                GameMain gm = GameMain.Instance;
                if (gm == null || gm.CharacterMgr == null)
                {
                    return;
                }
                if (StageIsPlayableForCurrentSkill(ym))
                {
                    return; // 对应地图: FT 锚定保持官方行为
                }
                // v1.7.7 (P42): 非对应地图 → 舞台名覆盖窗口开启。FT 同步段的
                // 舞台门控（@if GetYotogiSelectStageName() == '官方舞台'）在
                // CallNormalFile 内同步执行（v1.7.2 NRE 栈实证），窗口内
                // TJSFuncGetYotogiSelectStageName 返回官方舞台名 → 定位块
                // （@AllPos 锚点 + @Offset 双对分离 + 道具 + 相机 +
                // AddAllOffset_Ignore 旗标）在任何地图上都按官方分支执行。
                // v1.7.8（融合）: 同时捕获 FT 之前的双层根位姿与挂载道具
                // 名字快照——门控块执行后（postfix）把绝对锚点重基底回
                // 保留位置（官方相对布局 + 当前地图位置, 角色不浮空）。
                s_ftStageOverride = true;
                try
                {
                    CharacterMgr cmPre = gm.CharacterMgr;
                    // v1.8.0 (P43): 优先使用归零前的 hold 位姿（上一姿势的
                    // 实际位置）—— 官方 OnFinish 在 FT 之前已把双层根归零,
                    // 此刻的 GetCharaAllPos 是原点, 直接用会导致重基底还原
                    // 到「地图原点」而非姿势开始前的位置。hold 捕获于
                    // OnFinish 归零之前（OnFinish_HoldPrefix）。
                    if (s_ftHoldValid)
                    {
                        s_ftPrePos = s_ftHoldPos;
                        s_ftPreRot = s_ftHoldRot;
                        s_ftPreOffs = s_ftHoldOffs;
                        s_ftHoldValid = false; // 一次性消费
                    }
                    else
                    {
                        s_ftPrePos = cmPre.GetCharaAllPos();
                        s_ftPreRot = cmPre.GetCharaAllRot();
                        s_ftPreOffs = cmPre.GetCharaAllOfsetPos();
                    }
                    // 道具快照仍在窗口开始时捕获（ChangeBG 之后 = 新舞台
                    // 的挂载状态, 窗口内新挂的才是 FT 门控的道具）。
                    s_ftPreProps.Clear();
                    Dictionary<string, GameObject> attachPre = gm.BgMgr.m_DicAttachObj;
                    if (attachPre != null)
                    {
                        foreach (string key in attachPre.Keys)
                        {
                            s_ftPreProps.Add(key);
                        }
                    }
                    s_ftPreValid = true;
                }
                catch (Exception)
                {
                    s_ftPreValid = false;
                }
            }
            catch (Exception)
            {
                s_ftStageOverride = false;
            }
        }

        public static void CallNormalFile_Postfix(string file, string label)
        {
            s_ftStageOverride = false; // v1.7.7 (P42): 窗口关闭（异常路径由 finalizer 兜底）
            try
            {
                if (!string.IsNullOrEmpty(label) || string.IsNullOrEmpty(file))
                {
                    return; // FT2/KA labeled calls don't trigger
                }
                if (object.ReferenceEquals(cfgPoseResource, null) || !cfgPoseResource.Value)
                {
                    return;
                }
                YotogiManager ym = YotogiManager.instans;
                if (ym == null)
                {
                    return;
                }
                string part = DeriveFtPart(file);
                if (part == null)
                {
                    return; // not FT-shaped (faint system script etc.)
                }
                // =============================================================
                // v1.7.8（融合·问题1+1.1+1.2）: 官方相对布局 + 保留锚点。
                // 门控块（P42 强开）已执行: @AllPos 把双层根传送到官方舞台
                // 绝对坐标、@Offset 分离了双对、@PhisicsHit 设了物理地面、
                // @AddPrefabBg 挂了道具。这里把绝对部分重基底回 FT 之前
                // 的位置, 相对布局原样保留:
                //   ① 窗口内新挂的道具: pos = pre + R_pre·(R_ft⁻¹·(p-ft)),
                //      rot = R_pre·R_ft⁻¹·R_prop（旋转感知重基底, 与
                //      PoseResMountPass 同式数学）;
                //   ② 每角色物理地面保持相对高差: floor = preY + (floor_ft - ftY);
                //   ③ 双层根还原到 FT 之前的位姿;
                //   ④ 相机回官方标准取景（对准还原后的全角色中心, 与
                //      CallFtFiles cameraSet 同源——FT 的 @Camera 指向官方
                //      坐标, 还原后必然错位）。
                // 全部在淡出遮罩内完成（doneFtFileCall 在 CallFtFiles 尾部
                // 才置位）, 玩家看不到中间态。官方每(舞台×技能)自定义位置
                // 系统（CallFtFiles 尾部 yotogiCustomList）在其后应用,
                // 层级关系正确。
                // =============================================================
                if (s_ftPreValid && !StageIsPlayableForCurrentSkill(ym))
                {
                    s_ftPreValid = false;
                    GameMain gmR = GameMain.Instance;
                    CharacterMgr cmR = (gmR != null) ? gmR.CharacterMgr : null;
                    if (cmR != null)
                    {
                        try
                        {
                            // FT 执行后的锚点（官方绝对坐标）与旋转
                            Vector3 ftPos = cmR.GetCharaAllPos();
                            Vector3 ftRotE = cmR.GetCharaAllRot();
                            Vector3 ftOffs = cmR.GetCharaAllOfsetPos();
                            Quaternion ftRotQ = Quaternion.Euler(ftRotE);
                            Quaternion preRotQ = Quaternion.Euler(s_ftPreRot);
                            // 有效锚点（含第二层抬升）: 近似 pos + rot·offs
                            Vector3 ftAnchor = ftPos + ftRotQ * ftOffs;
                            Vector3 preAnchor = s_ftPrePos + preRotQ * s_ftPreOffs;
                            // ① 新挂道具重基底
                            int rebased = 0;
                            if (gmR != null && gmR.BgMgr != null)
                            {
                                Dictionary<string, GameObject> attach = gmR.BgMgr.m_DicAttachObj;
                                if (attach != null)
                                {
                                    List<string> keys = new List<string>(attach.Keys);
                                    for (int k = 0; k < keys.Count; k++)
                                    {
                                        string key = keys[k];
                                        if (s_ftPreProps.Contains(key))
                                        {
                                            continue; // 窗口前就存在的道具
                                        }
                                        GameObject go = attach[key];
                                        if (go == null)
                                        {
                                            continue;
                                        }
                                        Vector3 off = Quaternion.Inverse(ftRotQ) * (go.transform.position - ftAnchor);
                                        go.transform.position = preAnchor + preRotQ * off;
                                        go.transform.rotation = preRotQ * Quaternion.Inverse(ftRotQ) * go.transform.rotation;
                                        rebased++;
                                    }
                                }
                            }
                            // ② 物理地面高度保持相对差 + ③ 双层根还原
                            for (int mi = 0; mi < cmR.GetMaidCount(); mi++)
                            {
                                Maid mm = cmR.GetMaid(mi);
                                if (mm != null && mm.body0 != null)
                                {
                                    float rel = mm.body0.BoneHitHeightY - ftAnchor.y;
                                    mm.body0.SetBoneHitHeightY(preAnchor.y + rel);
                                }
                            }
                            cmR.SetCharaAllPos(s_ftPrePos);
                            cmR.SetCharaAllRot(s_ftPreRot);
                            cmR.CharaAllOfsetPos(s_ftPreOffs);
                            // ④ 相机回官方标准取景
                            try
                            {
                                CameraMain cmr = gmR.MainCamera;
                                cmr.SetTargetPos(new Vector3(s_ftPrePos.x, s_ftPrePos.y + 0.8f, s_ftPrePos.z));
                                cmr.SetDistance(3f);
                                cmr.SetAroundAngle(new Vector2(180f, 11f));
                            }
                            catch (Exception)
                            {
                            }
                            if (!object.ReferenceEquals(log, null))
                            {
                                log.LogInfo("YotogiUnlimited: FT layout rebased to preserved anchor for ft " + part
                                    + " (" + rebased + " prop(s))");
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
                PoseResMountPass(part, ym);
            }
            catch (Exception)
            {
            }
        }

        public static void CallNormalFile_Finalizer(Exception __exception)
        {
            s_ftStageOverride = false; // v1.7.7 (P42): 异常路径也关窗（防旗标泄漏）
            s_ftPreValid = false;      // v1.7.8: 捕获状态一并作废（postfix 未跑时）
            if (__exception == null)
            {
                return;
            }
            if (!(__exception is NullReferenceException))
            {
                return; // only contain the observed crash class; others surface
            }
            try
            {
                if (!object.ReferenceEquals(log, null))
                {
                    log.LogWarning("YotogiUnlimited: FT/KA sync segment NRE contained (script continues next frame): "
                        + __exception.Message);
                }
            }
            catch (Exception)
            {
            }
        }

        // =================================================================
        // P42 (v1.7.7): TJSFuncGetYotogiSelectStageName prefix ——
        // FT 同步段舞台名覆盖。根因: 多对姿势（スワッピング系等）的 FT
        // 同步段把全部定位（@AllResetPos + @AllPos 锚点 + @Offset maid=1/
        // man=1 双对分离 + @RotateLink + @AddPrefabBg 道具 + 相机 +
        // SetTmpFlag('AddAllOffset_Ignore',1)）写在
        // @if GetYotogiSelectStageName() == '官方舞台' 门控内。非对应地图
        // 上门控整块关闭 → 第二对角色无分离定位（全部叠在同一锚点 =
        // 「人物重叠」）, 旗标未立（= isGP001Mode=false）→ OM 系动作
        // 脚本的 @autooffset/@inverseoffset 走旧语义（「姿势扭曲」）。
        // 修复: s_ftStageOverride 窗口内（CallNormalFile prefix→postfix,
        // 无 label 的 FT 整文件调用）, 返回值覆盖为「当前技能
        // playable_stageid_list 第一个舞台的 uniqueName」→ 门控按官方
        // 分支执行。对应地图（StageIsPlayable=true 时窗口不开）/无舞台
        // 绑定技能（表空 → 原值返回）不受影响。
        // =================================================================
        public static bool YotogiStageNameOverride_Prefix(TJSVariantRef result)
        {
            if (!s_ftStageOverride || result == null)
            {
                return true; // 窗口外: 原版行为
            }
            try
            {
                YotogiManager ym = YotogiManager.instans;
                if (ym == null || ym.playing_skill == null
                    || ym.playing_skill.skill_pair == null
                    || ym.playing_skill.skill_pair.base_data == null)
                {
                    return true;
                }
                Skill.Data sd = ym.playing_skill.skill_pair.base_data;
                if (sd.playable_stageid_list == null || sd.playable_stageid_list.Count == 0)
                {
                    return true; // 无舞台绑定: 原值（FT 无门控或按真实舞台分支）
                }
                // playable_stageid_list 是 HashSet<int>: 取第一个元素
                int firstStageId = -1;
                foreach (int sid in sd.playable_stageid_list)
                {
                    firstStageId = sid;
                    break;
                }
                if (firstStageId < 0)
                {
                    return true;
                }
                string official = YotogiStage.IdToUniqueName(firstStageId);
                if (string.IsNullOrEmpty(official))
                {
                    return true; // 舞台数据缺失: 原值
                }
                result.SetString(official);
                return false; // 跳过原版（已写入官方舞台名）
            }
            catch (Exception)
            {
                return true; // 任何异常: 原版行为
            }
        }

        // =================================================================
        // P33 (v1.7.3): YotogiPlayManager 语音数据入口 null 防护。
        // 官方边界缺陷: 3P系技能的 FT/KA 标签向 SetAdditionalFtVoice /
        // SetRepeatVoiceFile / AddRepeatVoiceFile 写入 maid 槽位号(1..)
        // 的语音数据; 单人会话 GetMaid(1)=null → Update() 每帧
        // GetMaid(no).AudioMan 解引用 NRE 刷屏（additionalFtVoice 的
        // isPlaying 永不置位 → 无限循环重试）。prefix = 槽位女仆缺席时
        // 丢弃该条数据（无语音, 无 NRE）。
        // =================================================================
        public static bool VoiceDataMaid_Prefix(int maid_no)
        {
            if (maid_no <= 0)
            {
                return true; // 主女仆槽位恒存在
            }
            try
            {
                CharacterMgr cm = (GameMain.Instance != null) ? GameMain.Instance.CharacterMgr : null;
                if (cm != null && cm.GetMaid(maid_no) == null)
                {
                    return false; // drop the entry (Update() NRE source)
                }
            }
            catch (Exception)
            {
            }
            return true;
        }

        public static bool VoiceDataAddFt_Prefix(int maidNo)
        {
            if (maidNo <= 0)
            {
                return true;
            }
            try
            {
                CharacterMgr cm = (GameMain.Instance != null) ? GameMain.Instance.CharacterMgr : null;
                if (cm != null && cm.GetMaid(maidNo) == null)
                {
                    return false; // drop (additionalFtVoice Update() NRE source)
                }
            }
            catch (Exception)
            {
            }
            return true;
        }

        // =================================================================
        // P34 (v1.7.3): YotogiKagManager talk/voice 标签目标女仆 null 防护。
        // 官方边界缺陷: TagTalk/TagTalkAddFt 的 GetVoiceTargetMaid 返回
        // null 时直接 .AudioMan/.ActiveSlotNo 解引用 → KAG 同步段 NRE
        // （CallNormalFile finalizer 只能兜住, 脚本仍卡在该行）;
        // TagVoiceWait 的 GetMaid(no).VoicePitch 同理。prefix = 目标女仆
        // 缺席时跳过标签（无语音/无等待, 脚本继续走）。
        // =================================================================
        public static bool TagTalkSafe_Prefix(KagTagSupport tag_data)
        {
            try
            {
                if (tag_data == null || !tag_data.IsValid("voice"))
                {
                    return true;
                }
                Maid vm = BaseKagManager.GetVoiceTargetMaid(tag_data);
                if (vm == null)
                {
                    return false; // target absent: skip tag (NRE source)
                }
            }
            catch (Exception)
            {
                return true; // never block the original on our own errors
            }
            return true;
        }

        public static bool TagTalkMaidSafe_Prefix(KagTagSupport tag_data)
        {
            try
            {
                if (tag_data == null || !tag_data.IsValid("maid"))
                {
                    return true;
                }
                int no = tag_data.GetTagProperty("maid").AsInteger();
                if (no <= 0)
                {
                    return true; // main maid slot always present
                }
                CharacterMgr cm = (GameMain.Instance != null) ? GameMain.Instance.CharacterMgr : null;
                if (cm != null && cm.GetMaid(no) == null)
                {
                    return false; // target absent: skip (repeat/wait NRE source)
                }
            }
            catch (Exception)
            {
                return true;
            }
            return true;
        }

        // =================================================================
        // P35/P36 (v1.7.3): 官方「夜伽追加女仆选择」介入（问题3）。
        // 点击 3P 系姿势且在场女仆不足时:
        //   - 预瞄技能索引（playing_skill_no_=k-1 / 塔位0协议）,
        //     TryOpenSubCharaSelect 打开官方 SubCharaSelect 屏;
        //   - P36 GetPlayerSelectNum prefix = 精确返回所需人数
        //     （原版按全会话数组取 max, 会要求过多女仆）;
        //   - 选完 OK → OnFinish 把所选女仆 SetActiveMaid 入槽 →
        //     P35 OnFadeEnd prefix 重定向回 Play 屏: OnCall→NextSkill()
        //     的 ++ 恰好落在预瞄姿势上 → 3P 姿势带副女仆完整成立。
        // 未选人（边缘）→ 回落到介入时的当前姿势重播。
        // =================================================================
        private static int CountMissingSubMaids(Skill.Data sd)
        {
            if (sd == null || sd.player_num <= 1)
            {
                return 0;
            }
            CharacterMgr cm = (GameMain.Instance != null) ? GameMain.Instance.CharacterMgr : null;
            if (cm == null)
            {
                return 0;
            }
            int missing = 0;
            for (int i = 1; i < sd.player_num; i++)
            {
                if (cm.GetMaid(i) == null)
                {
                    missing++;
                }
            }
            return missing;
        }

        private static bool TryOpenSubCharaSelect(YotogiManager ym, Skill.Data sd, int curNo)
        {
            try
            {
                int needed = sd.player_num - 1;
                // 容量检查: 俱乐部可选女仆不足以满足 OK 键时不介入
                // （否则官方多选屏的 OK 永不点亮 = 死界面）。
                List<Maid> draw = new List<Maid>();
                ym.GetSubMaidList(draw);
                int selectable = 0;
                for (int i = 0; i < draw.Count; i++)
                {
                    if (draw[i] != null)
                    {
                        selectable++;
                    }
                }
                if (selectable < needed)
                {
                    return false;
                }
                YotogiSubCharacterSelectManager sub = null;
                if (!object.ReferenceEquals(fiSubCharaSelectMgr, null))
                {
                    sub = fiSubCharaSelectMgr.GetValue(ym) as YotogiSubCharacterSelectManager;
                }
                if (sub == null)
                {
                    sub = ym.GetComponentInChildren<YotogiSubCharacterSelectManager>(true);
                }
                if (sub == null)
                {
                    return false;
                }
                sub.cancel_label = string.Empty; // 直接调用路径无 ADV 回退标签
                s_subSelectActive = true;
                s_subSelectNeeded = needed;
                s_subPrevNo = curNo;
                ym.CallScreen("SubCharaSelect");
                if (!object.ReferenceEquals(log, null))
                {
                    log.LogInfo("YotogiUnlimited: sub-maid select opened for skill [" + sd.name
                        + "] (" + needed + " needed)");
                }
                return true;
            }
            catch (Exception e)
            {
                LogOnce("SubCharaSelect: " + e.Message);
                return false;
            }
        }

        // P35: OnFadeEnd 重定向（仅介入中的调用被接管）。
        public static bool SubCharaFadeEnd_Prefix()
        {
            if (!s_subSelectActive)
            {
                return true; // 官方流程原样（ADV 入口等）
            }
            s_subSelectActive = false;
            try
            {
                YotogiManager ym = YotogiManager.instans;
                if (ym == null)
                {
                    return false;
                }
                CharacterMgr cm = (GameMain.Instance != null) ? GameMain.Instance.CharacterMgr : null;
                bool joined = (cm != null && cm.GetMaid(1) != null);
                if (!joined)
                {
                    // 未选到人（边缘路径）: 回落重播介入时的当前姿势,
                    // 不让预瞄的多女仆姿势在缺员状态下加载。
                    YotogiPlayManager pm = ym.play_mgr;
                    if (pm != null && !object.ReferenceEquals(fiPlayingSkillNo, null) && s_subPrevNo >= 0)
                    {
                        fiPlayingSkillNo.SetValue(pm, s_subPrevNo - 1);
                    }
                    s_forceSkillZero = false;
                }
                else
                {
                    // v1.8.0 (追加2): 选人成功 → 回 Play 首次加载完成后
                    // 自动重播一遍当前姿势（用户方案: 选人后仍扭曲时,
                    // 重载等价于「换姿势再切回来」, 一步到位消除扭曲）。
                    // Update 检测全员空闲稳定 0.6s 后触发 ReloadCurrentPose。
                    s_replayAfterJoin = true;
                    s_replaySettleSince = -1f;
                }
                ym.CallScreen("Play");
                if (!object.ReferenceEquals(log, null))
                {
                    log.LogInfo("YotogiUnlimited: sub-maid select finished -> back to Play ("
                        + (joined ? "maid joined" : "no maid, replay current") + ")");
                }
            }
            catch (Exception e)
            {
                LogOnce("SubCharaFadeEnd: " + e.Message);
            }
            return false; // 跳过官方路由（SkillSelect/Null）
        }

        // P36: GetPlayerSelectNum 精确化（仅介入中）。
        public static bool SubCharaSelectNum_Prefix(ref int __result)
        {
            if (!s_subSelectActive)
            {
                return true;
            }
            __result = s_subSelectNeeded;
            return false;
        }

        // =================================================================
        // P37 (v1.7.5): 中途入会女仆本地位姿清零（问题「选择女仆后角色
        // 扭曲 / 吃醋等姿势位置不正确」的根治之一）。
        //
        // 官方体系: 角色的多角色布局 = 共享根（m_goActive/@AllPos +
        // m_goAllOffset/@AddAllOffset）+ 每角色自身的本地位姿
        // （SetPos/SetRot/baseOffset/SetPosOffset/motionOffsetGP03）。
        // Deactivate 把女仆停回库存容器时「保持本地位姿不变」,
        // SetActiveMaid 激活时同样「保持不变」——官方依赖
        // ResetCharaPosAll() 在场景/夜伽会话开始时清零一切（含库存）,
        // 但【中途入会】没有这道清零:
        //   - 嫉妒/吃醋系、スワッピング系等元技能的 FT 用
        //     @CharaActivate maid=1 npc=... 在 FT 执行中途把 NPC 女仆
        //     拉入槽位（此类流程大多不再经过 Play.OnCall→ResetWorld）;
        //   - P35/P36 官方选人屏的 OnFinish SetActiveMaid。
        // 带着上个场景残留的本地位姿入会 → 该角色在共享根下被平移/旋转
        // 到错误位置（TMO 布局按「角色本地位姿=零」设计）→ 位置错位/
        // 角色扭曲。
        //
        // prefix = 夜伽会话中（YotogiManager.instans != null）入会时,
        // 对该女仆执行与 ResetCharaPosAll 库存清零段同构的本地位姿清零
        // （不动共享根, 不动其他角色）。夜伽外不介入（官方场景流程
        // 自带 ResetCharaPosAll）。
        // =================================================================
        public static bool SetActiveMaid_JoinReset_Prefix(Maid f_maid, int f_nActiveSlotNo)
        {
            try
            {
                if (YotogiManager.instans == null || f_maid == null)
                {
                    return true;
                }
                f_maid.SetPos(Vector3.zero);
                f_maid.SetRot(Vector3.zero);
                f_maid.baseOffset = Vector3.zero;
                f_maid.baseEulerAngles = Vector3.zero;
                f_maid.rotateLinkMaid = string.Empty;
                f_maid.SetPosOffset(Vector3.zero);
                f_maid.prevMotionOffsetGP03 = f_maid.motionOffsetGP03;
                f_maid.motionOffsetGP03 = Vector3.zero;
                if (f_maid.body0 != null)
                {
                    f_maid.body0.SetBoneHitHeightY(0f);
                }
                if (!object.ReferenceEquals(log, null))
                {
                    log.LogInfo("YotogiUnlimited: mid-session maid join position reset (slot "
                        + f_nActiveSlotNo + ")");
                }
            }
            catch (Exception)
            {
                // 清零失败不阻断激活
            }
            return true;
        }

        // =================================================================
        // P38/P39 (v1.7.5): スワッピング系元技能链的确定性落位（问题
        // 「はるの達とスワッピング…等姿势位置不正确」的根治之二）。
        //
        // 元技能结构（逆向实证, swap_yotogi_gn2.ks 等）: スワッピング系
        // 的 FT 是 ADV 包装脚本——@CharaActivate 把 NPC 女仆/男拉入槽位,
        // @YotogiCall name=Play skillid=XXXX 启动实际子技能（書えM字等）,
        // 子技能结束后 @ReStart skillid=YYYY 链入第二个子技能（台上側位）。
        //
        // 官方缺陷: @YotogiCall/@ReStart 的子技能插入是「找 play_skill_array
        // 第一个空槽, 找不到就静默放弃」; 而后续技能推进是
        //   YotogiCall: CallScreen("Play") → OnCall → NextSkill 的 ++
        //   ReStart:    play_mgr.ReStart() → 扫描「第一个未播技能」
        // 两者都要求插入位恰好紧跟当前位置——只有「数组后半为空」的官方
        // 排程（选定技能≤7且未满）才成立。全解锁玩家通常选满 7 技能:
        //   - 子技能静默未插入 → ++/扫描落到用户技能 → 元技能链断裂;
        //   - NPC 女仆已入会且可见, 但错误技能的 FT 只布局主女仆/男主
        //     → NPC 停在共享根原点, 与其他角色重叠 = 「扭曲/位置不正确」;
        //   - AddAllOffset_Ignore 旗标残留 1 → 后续所有姿势的
        //     @AddAllOffset 被跳过 → 角色沉入床/台 = 「位置不正确」。
        //
        // 修复（预瞄技术与 P35 同源）: prefix 接管插入——
        //   - 有空槽 → 按原版语义插入该槽; 无空槽 → AddPlaySkill 追加
        //     （v1.7.0 塔扩展已支持数组超过塔子数）;
        //   - 插入后把 playing_skill_no_ 预瞄到 idx-1（idx==0 时置 -1,
        //     走官方首技能 Initialize 路径）→ NextSkill 的 ++ 精确落在
        //     插入位——无论数组多满;
        //   - ReStart 版复刻原版守卫（fade 中/非中断恢复态 → 放行原版
        //     走其错误路径）后, 直接预瞄 + 反射调用 OnNextSkillMove
        //     （跳过 ReStart 的「第一个未播」扫描——满数组时它永远找
        //     不到我们的插入位）。
        // 其他调用形态（name≠Play / 无 skillid）一律放行原版。
        // =================================================================
        private static int InsertChainSkill(YotogiManager ym, Skill.Data data)
        {
            YotogiManager.PlayingSkillData[] arr = ym.play_skill_array;
            int idx = -1;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].skill_pair.base_data == null)
                {
                    idx = i;
                    break;
                }
            }
            if (idx < 0)
            {
                // 数组满（全解锁玩家常态）→ 官方在此静默放弃; 追加替代
                ym.AddPlaySkill(data);
                idx = ym.play_skill_array.Length - 1;
            }
            else
            {
                YotogiManager.PlayingSkillData psd = new YotogiManager.PlayingSkillData();
                psd.skill_pair = Yotogi.SkillDataPair.Create(ym.maid, data);
                psd.skill_pair.lock_skill_exp = true;
                if (psd.skill_pair.skill_data != null)
                {
                    // 元技能 FT 在 @YotogiCall 前已 LearningYotogiSkill——
                    // 正常必有习得数据; 防御性 null 检查防未习得边缘
                    psd.backup_total_exp = psd.skill_pair.skill_data.expSystem.GetTotalExp();
                }
                ym.play_skill_array[idx] = psd;
            }
            return idx;
        }

        // P38: @YotogiCall（name=Play + skillid 的元技能链调用）
        public static bool YotogiCallChain_Prefix(KagTagSupport tag_data)
        {
            try
            {
                YotogiManager ym = YotogiManager.instans;
                if (ym == null || tag_data == null || !tag_data.IsValid("name"))
                {
                    return true;
                }
                if (tag_data.GetTagProperty("name").AsString() != "Play" || !tag_data.IsValid("skillid"))
                {
                    return true; // SubCharaSelect/SkillSelect 等其他形态: 原版
                }
                Skill.Data data = Skill.Get(tag_data.GetTagProperty("skillid").AsInteger());
                if (data == null)
                {
                    return true; // 原版走 not-find 错误路径
                }
                int idx = InsertChainSkill(ym, data);
                YotogiPlayManager pm = ym.play_mgr;
                if (pm == null || object.ReferenceEquals(fiPlayingSkillNo, null))
                {
                    return true;
                }
                // 预瞄: OnCall → NextSkill 的 ++ 恰好落在插入位
                fiPlayingSkillNo.SetValue(pm, (idx == 0) ? (-1) : (idx - 1));
                ym.null_mgr.SetNextLabel(tag_data.GetTagProperty("label").AsString());
                ym.CallScreen("Play");
                if (!object.ReferenceEquals(log, null))
                {
                    log.LogInfo("YotogiUnlimited: meta chain @YotogiCall skill "
                        + data.id + " aimed at slot " + idx);
                }
                return false;
            }
            catch (Exception e)
            {
                LogOnce("YotogiCallChain: " + e.Message);
                return true;
            }
        }

        // P39: @ReStart（元技能链的第二个子技能转接）
        public static bool ReStartChain_Prefix(KagTagSupport tag_data)
        {
            try
            {
                YotogiManager ym = YotogiManager.instans;
                if (ym == null || tag_data == null || !tag_data.IsValid("skillid"))
                {
                    return true;
                }
                YotogiPlayManager pm = ym.play_mgr;
                if (pm == null || object.ReferenceEquals(fiPlayingSkillNo, null)
                    || object.ReferenceEquals(miOnNextSkillMove, null))
                {
                    return true;
                }
                // 复刻原版守卫: fade 处理中/非中断恢复态 → 放行原版（其内部
                // 会 LogError 后返回, 与官方行为一致）
                try
                {
                    if (GameMain.Instance.MainCamera.IsFadeProc()
                        || object.ReferenceEquals(fiSuspendStore.GetValue(pm), null))
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                    return true;
                }
                Skill.Data data = Skill.Get(tag_data.GetTagProperty("skillid").AsInteger());
                if (data == null)
                {
                    return true;
                }
                int idx = InsertChainSkill(ym, data);
                // 直接预瞄 + OnNextSkillMove（跳过 ReStart 的「第一个未播」
                // 扫描——满数组时它找不到插入位）
                fiPlayingSkillNo.SetValue(pm, (idx == 0) ? (-1) : (idx - 1));
                if (tag_data.IsValid("label"))
                {
                    string lbl = tag_data.GetTagProperty("label").AsString();
                    if (!string.IsNullOrEmpty(lbl))
                    {
                        ym.null_mgr.SetNextLabel(lbl);
                    }
                }
                miOnNextSkillMove.Invoke(pm, null);
                if (!object.ReferenceEquals(log, null))
                {
                    log.LogInfo("YotogiUnlimited: meta chain @ReStart skill "
                        + data.id + " aimed at slot " + idx);
                }
                return false;
            }
            catch (Exception e)
            {
                LogOnce("ReStartChain: " + e.Message);
                return true;
            }
        }

        // =================================================================
        // P40/P41 (v1.7.6): 3P 动作链诊断（只读日志, 不改行为）。
        // 用户实测: P35 选人入会后副女仆「站着不进动作」、スワッピング
        // 「人物混在一起」。动作链 = personality FT 的 *KA 段 @MotionScript
        // → ScriptManager.LoadMotionScript(maid_guid/man_guid/sloat)
        // → MotionKagManager.LoadScriptFile（ActiveMaidList/ActiveManList
        // = 实际应用名单, GetMaid(0)→main_maid_ 局部索引）。两个观察点
        // 记录到日志后, 下一次实测即可精确定位断在哪一层:
        //   - motionload 行缺失 → *KA 段根本没执行（CallFtFiles 卡在
        //     IsAllCharaBusy 或 start_call_file2 为空/缺性格变体）;
        //   - motionload 有 maid=[-]（guid 空）→ 标签没写 maid 参数
        //     （按 meta.maidCount 分发, 看 motionapply 的名单）;
        //   - motionapply 名单缺 slot1 → LoadActiveChara 可见性/busy
        //     或同帧去重把它移除;
        //   - 名单齐全但仍然站着 → .anm 加载层（TagMotion/IsMotionScript
        //     同帧去重）或定位层（@SetMaidOffsetMultiPos 位置模式）。
        // =================================================================
        public static void LoadMotionScriptDiag_Prefix(int sloat, string file_name,
            string label_name, string maid_guid, string man_guid)
        {
            try
            {
                if (object.ReferenceEquals(log, null) || YotogiManager.instans == null)
                {
                    return;
                }
                CharacterMgr cm = (GameMain.Instance != null) ? GameMain.Instance.CharacterMgr : null;
                string maidName = "-";
                string manName = "-";
                if (cm != null)
                {
                    for (int i = 0; i < cm.GetMaidCount(); i++)
                    {
                        Maid mm = cm.GetMaid(i);
                        if (mm != null && mm.status != null && mm.status.guid == maid_guid)
                        {
                            maidName = "slot" + mm.ActiveSlotNo;
                            break;
                        }
                    }
                    for (int j = 0; j < cm.GetManCount(); j++)
                    {
                        Maid mn = cm.GetMan(j);
                        if (mn != null && mn.status != null && mn.status.guid == man_guid)
                        {
                            manName = "slot" + mn.ActiveSlotNo;
                            break;
                        }
                    }
                }
                log.LogInfo("motionload: sloat=" + sloat + " file=" + file_name
                    + " label=" + label_name + " maid=[" + maidName + "] man=[" + manName + "]");
            }
            catch (Exception)
            {
            }
        }

        public static void MotionLoadDiag_Postfix(MotionKagManager __instance,
            string fileName, string labelName)
        {
            try
            {
                if (object.ReferenceEquals(log, null) || YotogiManager.instans == null)
                {
                    return;
                }
                if (object.ReferenceEquals(fiMotionActiveMaids, null)
                    || object.ReferenceEquals(fiMotionActiveMen, null))
                {
                    return;
                }
                List<Maid> maids = fiMotionActiveMaids.GetValue(__instance) as List<Maid>;
                List<Maid> men = fiMotionActiveMen.GetValue(__instance) as List<Maid>;
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.Append("motionapply: file=").Append(fileName).Append(" label=").Append(labelName);
                sb.Append(" maids=[");
                if (maids != null)
                {
                    for (int i = 0; i < maids.Count; i++)
                    {
                        Maid m = maids[i];
                        if (m != null)
                        {
                            sb.Append(m.ActiveSlotNo).Append((m.Visible) ? "" : "(inv)");
                            if (i < maids.Count - 1) { sb.Append(","); }
                        }
                    }
                }
                sb.Append("] men=[");
                if (men != null)
                {
                    for (int j = 0; j < men.Count; j++)
                    {
                        Maid n = men[j];
                        if (n != null)
                        {
                            sb.Append(n.ActiveSlotNo).Append((n.Visible) ? "" : "(inv)");
                            if (j < men.Count - 1) { sb.Append(","); }
                        }
                    }
                }
                sb.Append("]");
                log.LogInfo(sb.ToString());
            }
            catch (Exception)
            {
            }
        }

        // =================================================================
        // 内容36(v1.7.0): 场景切换 —— 官方舞台选择屏同源序列。
        // =================================================================
        private static void BuildStageList()
        {
            s_stageList.Clear();
            s_bgList.Clear();
            try
            {
                // v1.8.1 (追加): GetAllDatas(false) = 全表 —— 旧参数 true 只
                // 返回「当前已解锁」部分, DLC/兼容舞台不全。false 返回
                // yotogi_stage 表全部条目（含未解锁/DLC/兼容 = 游戏内全部）。
                List<YotogiStage.Data> all = YotogiStage.GetAllDatas(false);
                if (all != null)
                {
                    all.Sort();
                    // v1.8.2 (修复): 空场景过滤 —— 表内存在但 prefab 资源
                    // 缺失的条目（調教地下室2 以下全空的舞台）点击后
                    // ChangeBg 加载失败 = 空场景。按当前时段的 prefabName
                    // 预检（与 BgMgr.ChangeBg 加载链同源）。
                    bool isNoonL = GameMain.Instance.CharacterMgr.status.isDaytime;
                    for (int i = 0; i < all.Count; i++)
                    {
                        YotogiStage.Data d = all[i];
                        if (d == null)
                        {
                            continue;
                        }
                        string prefab = (d.prefabName != null && d.prefabName.Length > 0)
                            ? d.prefabName[isNoonL ? 0 : 1]
                            : null;
                        if (StagePrefabExists(prefab))
                        {
                            s_stageList.Add(d);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogOnce("BuildStageList: " + e.Message);
            }
            // v1.8.3 (修复): 「其他背景」数据源改为官方摄影背景表 ——
            // v1.8.2 用 GameUty.BgFiles（全部 .asset_bg 资源索引）会把
            // 道具/物件类资源（Odogu 等 OBJ）也列进来（用户实测）,
            // 且条目数量巨大导致 IMGUI 渲染掉帧。
            // PhotoBGData = 官方摄影模式背景表（phot_bg_list.nei + 启用
            // 列表）: 只有真正的场景背景（Live/餐厅/剧场/夜伽舞台背景等）,
            // 条目数适中; create_prefab_name = ChangeBg 用资源名,
            // name = 官方日文显示名。排除 MyRoom 类（prefab 名为空）。
            try
            {
                HashSet<string> used = new HashSet<string>();
                for (int s = 0; s < s_stageList.Count; s++)
                {
                    YotogiStage.Data d2 = s_stageList[s];
                    if (d2 == null || d2.prefabName == null)
                    {
                        continue;
                    }
                    for (int p = 0; p < d2.prefabName.Length; p++)
                    {
                        string pn = d2.prefabName[p];
                        if (!string.IsNullOrEmpty(pn))
                        {
                            used.Add(pn.ToLower());
                        }
                    }
                }
                List<PhotoBGData> pl = PhotoBGData.data;
                if (pl == null || pl.Count == 0)
                {
                    PhotoBGData.Create(); // 懒加载（游戏未进过摄影模式时）
                    pl = PhotoBGData.data;
                }
                if (pl != null)
                {
                    for (int b = 0; b < pl.Count; b++)
                    {
                        PhotoBGData bg = pl[b];
                        if (bg == null || string.IsNullOrEmpty(bg.create_prefab_name))
                        {
                            continue; // MyRoom 类（无 prefab）跳过
                        }
                        if (used.Contains(bg.create_prefab_name.ToLower()))
                        {
                            continue; // 夜伽舞台已在用（防重复条目）
                        }
                        s_bgList.Add(bg);
                    }
                }
                if (!object.ReferenceEquals(log, null))
                {
                    log.LogInfo("YotogiUnlimited: stage list built ("
                        + s_stageList.Count + " stages + " + s_bgList.Count + " backgrounds)");
                }
            }
            catch (Exception e)
            {
                LogOnce("BuildStageList(bg): " + e.Message);
            }
        }

        // =================================================================
        // v1.8.0 (追加1): 重新载入当前姿势 —— 完整重播协议。
        // playing_skill_no_ = cur-1（NextSkill 的 ++ 恰落回当前姿势,
        // 塔位0协议同 P35/IconClick）→ OnNextSkillMove → Finish →
        // OnFinish（P43 归零前捕获本姿势位置）→ CallScreen(Play) →
        // NextSkill → CallFtFiles → FT 门控 → 重基底 → 动作全链重跑。
        // 用于: 手动重载按钮 + 选女仆后自动热加载（追加2）。
        // =================================================================
        private static void ReloadCurrentPose()
        {
            YotogiManager ym = YotogiManager.instans;
            if (ym == null)
            {
                return;
            }
            YotogiPlayManager pm = ym.play_mgr;
            if (pm == null || object.ReferenceEquals(fiPlayingSkillNo, null)
                || object.ReferenceEquals(miOnNextSkillMove, null))
            {
                return;
            }
            if (ym.IsAllCharaBusy())
            {
                return; // 姿势切换中: 等稳定（自动重载路径由调用方重试）
            }
            int cur = (int)fiPlayingSkillNo.GetValue(pm);
            if (cur < 0)
            {
                LogOnce("reload pose: no active skill");
                return;
            }
            // v1.8.1 (修复): 塔位0 协议 —— cur==0 时若直接设 playing_skill_no_
            // = -1, OnNextSkillMove/OnFinish 都会把它判定为「结束分支」
            // → 夜伽直接终止（用户实测: 点重载后夜伽结束）。与 IconClick
            // 塔位0 同式: 设 0 + s_forceSkillZero, NextSkill_Prefix 消费时
            // cur==0 → 改设 -1 → 官方 ++ 后恰落回 0。
            if (cur == 0)
            {
                fiPlayingSkillNo.SetValue(pm, 0);
                s_forceSkillZero = true;
            }
            else
            {
                fiPlayingSkillNo.SetValue(pm, cur - 1);   // ++ 后回到 cur
                s_forceSkillZero = false;                 // 防旧标志干扰
            }
            miOnNextSkillMove.Invoke(pm, null);
            if (!object.ReferenceEquals(log, null))
            {
                log.LogInfo("YotogiUnlimited: pose reload triggered (skill idx " + cur + ")");
            }
        }

        private static void StageSwitch(YotogiStage.Data data)
        {
            try
            {
                YotogiManager ym = YotogiManager.instans;
                if (ym == null || data == null || ym.maid == null)
                {
                    return;
                }
                bool isNoon = GameMain.Instance.CharacterMgr.status.isDaytime;
                YotogiStageSelectManager.SelectStage(data, null, isNoon);
                s_bgOverride = null; // v1.8.3 (P45): 切回夜伽舞台 → 撤销纯背景保持
                data.skillSelectcharacterData.Apply();   // 角色根锚定新舞台坐标系
                data.skillSelectLightData.Apply();       // 灯光
                YotogiStageSelectManager.SelectedStage.ChangeBG(ym.maid); // 加载预制体+维护舞台静态
                GameMain.Instance.SoundMgr.PlayBGM(data.bgmFileName, 1f);
                // v1.7.8 (问题1.2): 物理状态随舞台重置 —— Apply() 只搬根,
                // 不动 BoneHitHeightY/第二层抬升; 旧舞台的地面高度与床台
                // 抬升残留 = 姿势中切场景后裙子/头发物理崩坏的根源。
                // 重置为「根高度地面」（@AllPos 的默认关系）+ 抬升层清零。
                try
                {
                    CharacterMgr cmSw = GameMain.Instance.CharacterMgr;
                    float floorY = cmSw.GetCharaAllPos().y;
                    for (int sw = 0; sw < cmSw.GetMaidCount(); sw++)
                    {
                        Maid mm = cmSw.GetMaid(sw);
                        if (mm != null && mm.body0 != null)
                        {
                            mm.body0.SetBoneHitHeightY(floorY);
                        }
                    }
                    cmSw.CharaAllOfsetPos(Vector3.zero);
                }
                catch (Exception)
                {
                }
                // P30v2 (v1.7.2): ChangeBG 清空了全部挂载道具（官方
                // DelPrefabFromBgAll）且本切换路径不重放 FT —— 直接对
                // 当前姿势触发一次补挂（锚点已由 Apply() 重设完毕）。
                PoseResMountCurrent(ym);
                if (!object.ReferenceEquals(log, null))
                {
                    log.LogInfo("YotogiUnlimited: stage switched to " + data.uniqueName);
                }
            }
            catch (Exception e)
            {
                LogOnce("StageSwitch: " + e.Message);
            }
        }

        // =================================================================
        // v1.8.2: 场景 prefab 存在性预检 —— 表内存在但资源缺失的舞台
        // （調教地下室2 以下的空场景）在 BuildStageList 时被过滤。
        // v1.8.3: 只查 GameUty.BgFiles 索引（HasAssetBundle, 纯字典查询）,
        // 不再调用 Resources.Load —— 后者会把 prefab 真实加载进内存
        // （逐条预检 = 资源堆积/掉帧源）; 官方夜伽舞台全部以 .asset_bg
        // 资源包形式安装, 索引查询已足够准确。
        // =================================================================
        private static bool StagePrefabExists(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }
            try
            {
                if (0 <= name.IndexOf("?"))
                {
                    name = ScriptManager.ReplacePersonal(GameMain.Instance.CharacterMgr.GetMaid(0), name);
                }
                GameMain gm = GameMain.Instance;
                if (gm != null && gm.BgMgr != null && gm.BgMgr.HasAssetBundle(name))
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        // =================================================================
        // v1.8.2: 纯背景切换（官方摄影背景表条目, 非夜伽舞台表）。
        // 无 Data（相机/灯光/BGM 数据）——直接 BgMgr.ChangeBg + 物理重置
        // + 相机默认取景 + 道具补挂（与 StageSwitch 的后半段同构）。
        // v1.8.3: 设 s_bgOverride（P45 保持）—— 官方 NextSkill 在每次
        // 换姿势/重载时会 ChangeBG(SelectedStage) 把场景拉回夜伽舞台,
        // P45 把 Play 画面内的 ChangeBg 重定向回本背景。
        // =================================================================
        private static void StageSwitchBg(PhotoBGData bg)
        {
            try
            {
                YotogiManager ym = YotogiManager.instans;
                GameMain gm = GameMain.Instance;
                if (ym == null || gm == null || ym.maid == null || bg == null
                    || string.IsNullOrEmpty(bg.create_prefab_name))
                {
                    return;
                }
                string bgName = bg.create_prefab_name;
                s_bgOverride = bgName;               // v1.8.3 (P45): 保持登记
                gm.BgMgr.ChangeBg(bgName);           // 与 override 同名 → P45 不干预
                // 物理重置（与 StageSwitch 同构: 根高度地面 + 抬升清零）
                try
                {
                    CharacterMgr cmBg = gm.CharacterMgr;
                    float floorY2 = cmBg.GetCharaAllPos().y;
                    for (int sw2 = 0; sw2 < cmBg.GetMaidCount(); sw2++)
                    {
                        Maid mm2 = cmBg.GetMaid(sw2);
                        if (mm2 != null && mm2.body0 != null)
                        {
                            mm2.body0.SetBoneHitHeightY(floorY2);
                        }
                    }
                    cmBg.CharaAllOfsetPos(Vector3.zero);
                }
                catch (Exception)
                {
                }
                // 相机默认取景（对准全角色中心）
                try
                {
                    CameraMain cmrBg = gm.MainCamera;
                    Vector3 apBg = gm.CharacterMgr.GetCharaAllPos();
                    cmrBg.SetTargetPos(new Vector3(apBg.x, apBg.y + 0.8f, apBg.z));
                    cmrBg.SetDistance(3f);
                    cmrBg.SetAroundAngle(new Vector2(180f, 11f));
                }
                catch (Exception)
                {
                }
                PoseResMountCurrent(ym);             // 道具补挂（锚点=当前根）
                if (!object.ReferenceEquals(log, null))
                {
                    log.LogInfo("YotogiUnlimited: bg switched to " + bgName);
                }
            }
            catch (Exception e)
            {
                LogOnce("StageSwitchBg: " + e.Message);
            }
        }

        // =================================================================
        // v1.8.4: 多言語対応（简体中文 / English / 日本語）
        // -----------------------------------------------------------------
        // 埋め込みテーブル方式: 1行 = { key, zh-CN, en, ja }。
        //   L(key)             = 現在言語の文字列（未登録キーは key を返す）
        //   LF(key, args...)   = string.Format 版（{0} プレースホルダ）
        // 外部ファイル不要（DLL 単体で完結）。切替は F7 先頭の
        // 「言語」行 / F1 の UiLanguage（AcceptableValueList）の両方。
        // 既定 = zh-CN（従来の中国語 UI と同一文字列を zh 列へ移設）。
        // 注: Add-Type の GBK 経路を通るため, GBK 外の記号は \uXXXX
        //     エスケープで記述する（既存ソースと同じ流儀）。
        // =================================================================
        private static readonly string[][] LangRows = new string[][]
        {
            // ---- F7 メイン窓 ----
            new string[] { "win.main", "夜伽调节 {0}（{1} 开关·标题栏拖动）", "Yotogi Tuner {0} ({1} to toggle · drag title bar)", "夜伽調整 {0}（{1} で開閉・タイトルバー移動）" },
            new string[] { "win.browser", "姿势一览（点击即切换·搜索过滤）", "Pose list (click to switch · search filter)", "姿勢一覧（クリックで切替・検索フィルタ）" },
            new string[] { "win.noMaid", "(无女仆)", "(no maid)", "(メイドなし)" },
            new string[] { "win.playOnly", "(仅夜伽Play画面可用)", "(available only on the yotogi Play screen)", "(夜伽プレイ画面のみ有効)" },
            new string[] { "lang.title", "语言", "Language", "言語" },
            // ---- § 参数 ----
            new string[] { "param.excite", "兴奋 {0}/300", "Excitement {0}/300", "興奮 {0}/300" },
            new string[] { "param.sensual", "感性 {0}/300", "Sensuality {0}/300", "感性 {0}/300" },
            // ---- § 速度 ----
            new string[] { "speed.onegai", "【拜托了】拟人节奏", "[Please] Auto rhythm", "【お願い】自動リズム" },
            new string[] { "speed.plain", "速度 {0}x", "Speed {0}x", "速度 {0}x" },
            new string[] { "speed.phase", "速度 {0}x（{1}）", "Speed {0}x ({1})", "速度 {0}x（{1}）" },
            new string[] { "ph.0", "温存", "Gentle", "温存" },
            new string[] { "ph.1", "律动", "Rhythm", "律動" },
            new string[] { "ph.2", "加速", "Accelerate", "加速" },
            new string[] { "ph.3", "突击", "Thrust", "突撃" },
            new string[] { "ph.4", "缓和", "Ease", "緩和" },
            // ---- § POV ----
            new string[] { "pov.label", "POV（{0} 下一个·右键转视角）", "POV ({0} next · right-drag to look)", "POV（{0} で次へ・右ドラッグで視点）" },
            new string[] { "pov.active", "[POV中]", "[POV on]", "[POV中]" },
            new string[] { "pov.fov", "视野角 {0}°", "Field of view {0}°", "視野角 {0}°" },
            new string[] { "pov.forward", "前方偏移 {0}", "Forward offset {0}", "前方オフセット {0}" },
            new string[] { "pov.up", "上下偏移 {0}", "Vertical offset {0}", "上下オフセット {0}" },
            new string[] { "pov.smooth", "位置平滑 {0}", "Position smoothing {0}", "位置スムージング {0}" },
            new string[] { "pov.lookHead", "女仆头部朝向镜头", "Maids' heads look at camera", "メイドの頭部をカメラへ" },
            new string[] { "pov.lookEye", "女仆眼睛朝向镜头", "Maids' eyes look at camera", "メイドの目をカメラへ" },
            new string[] { "pov.reset", "POV设置还原默认", "Reset POV defaults", "POV設定を既定値へ" },
            // ---- § 功能 ----
            new string[] { "fn.forbidClimax", "禁止自动高潮", "Block auto climax", "自動絶頂を禁止" },
            new string[] { "fn.autoOpen", "进入Play自动打开本菜单", "Auto-open this menu on Play", "Play 進入時に自動で開く" },
            new string[] { "fn.safeScript", "缺失脚本安全处理", "Missing-script safety", "欠損スクリプト安全処理" },
            new string[] { "fn.dedup", "姿势去重", "De-duplicate poses", "姿勢の重複排除" },
            new string[] { "fn.voiceSweep", "清除残留语音", "Clear lingering voices", "残留ボイスを消去" },
            new string[] { "fn.poseRes", "姿势道具自动加载（场景除外）", "Auto-mount pose props (scenes excluded)", "姿勢アイテム自動読込（シーン除く）" },
            new string[] { "fn.nameExport", "导出名称表（翻译参考）", "Export name list (translation reference)", "名称リストを出力（翻訳参考）" },
            new string[] { "fn.nameReload", "重新载入名称翻译", "Reload name translations", "名称翻訳を再読込" },
            // ---- § 姿势与场景 ----
            new string[] { "ps.reload", "重新载入当前姿势", "Reload current pose", "現在の姿勢を再読込" },
            new string[] { "ps.browser", "姿势一览（独立窗口·实时切换）", "Pose list (separate window · live switch)", "姿勢一覧（別窓・リアルタイム切替）" },
            new string[] { "ps.stage", "场景切换（实时）", "Scene switch (live)", "シーン切替（リアルタイム）" },
            new string[] { "ps.posChanger", "夜伽位置调整（官方UI·含道具）", "Yotogi position adjust (official UI · incl. props)", "夜伽位置調整（公式UI・アイテム含む）" },
            new string[] { "ps.bgSep", "── 其他背景（官方背景表·Live/餐厅等）──", "── Other backgrounds (official list · Live/restaurant etc.) ──", "── その他背景（公式リスト・Live/レストラン等）──" },
            // ---- 姿势一覧窓 ----
            new string[] { "br.search", "搜索:", "Search:", "検索:" },
            new string[] { "br.count", "共 {0} 项（\u25B6 = 播放中）", "{0} item(s) (\u25B6 = playing)", "全 {0} 件（\u25B6 = 再生中）" },
            new string[] { "br.close", "关闭", "Close", "閉じる" },
            // ---- F1 設定説明文（Bind 時に生成 = 変更後は次回起動で反映）----
            new string[] { "cfg.lang", "插件界面语言 / plugin UI language: zh-CN=简体中文, en=English, ja=日本語。F7菜单内也可实时切换；F1的说明文本在下一次启动后生效", "Plugin UI language: zh-CN / en / ja. Also switchable from the F7 menu; F1 descriptions apply on the next launch", "プラグインUI言語：zh-CN / en / ja。F7メニューからも切替可、F1の説明文は次回起動時に反映" },
            new string[] { "cfg.anyStage", "true=夜伽技能选择不再受地图/场景限制，任何舞台可选任何已习得姿势", "true = skill selection is no longer limited by map/scene; any learned pose can be picked on any stage", "true=夜伽スキル選択がマップ/シーン制限を受けず、どの舞台でも習得済みの任意の姿勢を選択可能" },
            new string[] { "cfg.unlockStages", "true=夜伽舞台(地图/场景)选择全开：等级/设施/私人房间等条件全部忽略", "true = all yotogi stages (maps/scenes) unlocked; level, facility and private-room conditions ignored", "true=夜伽舞台（マップ/シーン）選択を全解放：レベル/施設/自室などの条件をすべて無視" },
            new string[] { "cfg.reuse", "true=已播放姿势的技能塔图标恢复可点击，每个所选姿势可无限次重播", "true = played pose icons on the skill tower become clickable again; every selected pose can be replayed unlimited times", "true=再生済み姿勢のスキルタワーアイコンが再びクリック可能になり、選択した姿勢を何度でも再生可能" },
            new string[] { "cfg.mix", "true=普通/微醺/遮掩/魅药/告白五种特殊条件技能混合出现在同一列表，可同场混用", "true = the five special-condition skill types (normal / tipsy / covered / aphrodisiac / confession) are mixed into one list and can be combined", "true=通常/ほろ酔い/隠し/媚薬/告白の5種の特殊条件スキルが同じリストに混在し、同席で併用可能" },
            new string[] { "cfg.multi", "true=3P等多人姿势按全俱乐部女仆数判定开放(不再要求当天排夜勤)，子角色从全俱乐部挑选", "true = multi-player (3P) poses unlock by the whole club's maid count (no night-shift requirement); sub characters are picked from the whole club", "true=3P等多人数姿勢をクラブ全体のメイド数で開放（当日の夜勤配置不要）、サブキャラはクラブ全体から選択" },
            new string[] { "cfg.ntr", "true=NTR解锁(交换/乱交分类、NTR相关内容按未锁定处理；全局生效，存档旗标不改写)", "true = NTR unlocked (swapping / group categories treated as unlocked; global, save flags are not rewritten)", "true=NTR解放（交換/乱交カテゴリ・NTR関連を未ロック扱い。全体に有効、セーブフラグは書き換えない）" },
            new string[] { "cfg.ignoreAll", "true=无视一切需求(条件)：所有姿势(含未习得/条件不符/隐藏类型)全部列出且可选，体力消耗不限制选择数", "true = ignore every requirement: all poses (unlearned / condition-mismatched / hidden) are listed and selectable; stamina does not limit the count", "true=一切の条件を無視：全姿勢（未習得/条件不一致/隠しタイプ含む）を一覧・選択可能、体力消費で選択数が制限されない" },
            new string[] { "cfg.mind", "true=夜伽中精神无限(不减耗，不会気絶)", "true = infinite mind during yotogi (no drain, no fainting)", "true=夜伽中は精神無限（減耗なし・気絶しない）" },
            new string[] { "cfg.sliders", "true=夜伽播放画面显示兴奋/感性实时调节滑动条窗口", "true = show the excitement / sensuality live slider window on the yotogi Play screen", "true=夜伽プレイ画面に興奮/感性のリアルタイム調整スライダー窓を表示" },
            new string[] { "cfg.sliderKey", "滑动条窗口 开/关 热键(仅夜伽播放画面有效)", "Slider window on/off hotkey (yotogi Play screen only)", "調整窓の開閉ホットキー（夜伽プレイ画面のみ有効）" },
            new string[] { "cfg.forbidClimax", "true=禁止自动高潮：兴奋值被限制在274以下，连续指令不再自动触发绝顶寸前反应；绝顶指令仍可手动点击", "true = block auto climax: excitement is capped below 274 so chained commands no longer trigger the near-climax reaction; climax commands still work when clicked", "true=自動絶頂を禁止：興奮値を274以下に制限し、連続コマンドで絶頂寸前反応が自動発火しない（絶頂コマンドの手動クリックは可）" },
            new string[] { "cfg.onegai", "true=【拜托了】自动拟人节奏(温存→律動→加速→突撃→緩和循环)接管动作速度", "true = [Please] auto human-like rhythm (gentle→rhythm→accelerate→thrust→ease) takes over the motion speed", "true=【お願い】自動擬人リズム（温存→律動→加速→突撃→緩和の循環）が動作速度を制御" },
            new string[] { "cfg.autoOpen", "true=每次进入夜伽Play画面自动打开调节菜单", "true = automatically open the tuner menu whenever the yotogi Play screen is entered", "true=夜伽プレイ画面に入るたびに調整メニューを自動で開く" },
            new string[] { "cfg.safeScript", "true=缺失脚本安全处理：夜伽中性格不匹配的脚本(如 B1_FT_07550.ks)自动回退到已有性格版本(如 w1_FT_07550.ks，姿势动作可正常播放)，全无变体时用空脚本替代，并抑制原生KAG报错刷屏；同时抑制夜伽中缺失OBJ/资源类报错", "true = missing-script safety: personality-mismatched scripts (e.g. B1_FT_07550.ks) fall back to an existing personality variant (e.g. w1_FT_07550.ks) so the pose still plays, an empty stub is used when no variant exists, and native KAG error spam plus missing OBJ/asset errors are suppressed", "true=欠損スクリプト安全処理：性格不一致のスクリプト（例 B1_FT_07550.ks）は既存の性格版（例 w1_FT_07550.ks）へ自動フォールバックして姿勢を再生、変体が無ければ空スクリプトで代替し、KAGエラー連投と欠損OBJ/リソースのエラーを抑制" },
            new string[] { "cfg.dedup", "true=姿势去重：混合列表中同名姿势(普通/微醺/遮掩/魅药/告白等变体)只保留一项(优先已习得>普通版)，列表更短更省性能", "true = de-duplicate poses: same-named variants (normal / tipsy / covered / aphrodisiac / confession) keep a single entry (learned > normal), making the list shorter and lighter", "true=姿勢の重複排除：同名の変体（通常/ほろ酔い/隠し/媚薬/告白等）を1件に統合（習得済み＞通常版を優先）、リストが短く軽くなる" },
            new string[] { "cfg.povKey", "夜伽Play画面POV循环键：第一人=主人公(男主)，继续按循环在场所有人(女仆/其他男)，一周后退出；2.0/3.0身体全适配，头部动画不带动镜头", "POV cycle key on the yotogi Play screen: first = protagonist (male), press again to cycle everyone present (maids / other men) and exit after a full loop; works with 2.0/3.0 bodies, head animation does not drag the camera", "夜伽プレイ画面のPOV切替キー：1人目=主人公（男）、押すごとに在场全員（メイド/他の男）を巡回し一周で終了。2.0/3.0ボディ両対応、頭部アニメでカメラは動かない" },
            new string[] { "cfg.povFov", "POV视野角(度)。F7菜单滑条实时调整(20~110)", "POV field of view (degrees). Adjustable live with the F7 slider (20-110)", "POV視野角（度）。F7メニューのスライダーでリアルタイム調整（20〜110）" },
            new string[] { "cfg.povForward", "POV相机沿当前视线方向的前移量(米)。看到头内侧时增大（默认0.040=眼位稍前移, 视野更自然）", "POV camera forward shift along the view direction (meters). Increase if you see the inside of the head (default 0.040)", "POVカメラの視線方向への前進量（メートル）。頭部内側が見える場合は増やす（既定 0.040）" },
            new string[] { "cfg.povUp", "POV相机相对眼线的上下偏移(米)", "POV camera vertical offset from the eye line (meters)", "POVカメラの目線に対する上下オフセット（メートル）" },
            new string[] { "cfg.povSmooth", "POV位置平滑度(吸收眼位跟随的抖动)：0=完全即时跟随, 1=最强平滑。鼠标转视角不受平滑影响(始终即时)", "POV position smoothing (absorbs eye-tracking jitter): 0 = instant follow, 1 = strongest smoothing. Mouse look is never smoothed", "POV位置スムージング（眼位置追従の揺れを吸収）：0=即時追従、1=最大平滑。マウスの視点回転は影響を受けない" },
            new string[] { "cfg.povSens", "POV视角鼠标右键拖动灵敏度", "POV mouse right-drag look sensitivity", "POV視点の右ドラッグ感度" },
            new string[] { "cfg.povNear", "POV相机近裁剪面：越小越不容易裁掉贴脸的物体。自身剔除功能已于 v1.7.8 移除——如塌缩残骸入镜可调大到0.1", "POV near clip plane: smaller clips less geometry close to the face. Self-culling was removed in v1.7.8; raise to 0.1 if collapsed remnants enter the view", "POVカメラの近クリップ面：小さいほど顔に近い物が裁かれにくい。v1.7.8で自身剔除は廃止——縮小残骸が映り込む場合は0.1へ" },
            new string[] { "cfg.lookHead", "true=夜伽Play画面中在场女仆头部始终朝向镜头(目标=真实渲染相机, POV中也看向玩家视点；瞄准增益/锥角=官方值0.4倍/±60°, 默认关闭；关闭时平滑复位)", "true = maids present on the yotogi Play screen always turn their heads to the camera (aims at the real render camera, works in POV too; gain 0.4x / cone ±60° as vanilla; off = smooth reset)", "true=夜伽プレイ画面の在场メイドの頭部を常にカメラへ（実際のレンダリングカメラを狙う・POV中も有効。ゲイン0.4倍/±60°の公式値、オフで滑らかに復帰）" },
            new string[] { "cfg.lookEye", "true=夜伽Play画面中在场女仆眼睛始终朝向镜头(同上, 眼球瞄准增益=官方值0.2/0.1, 默认关闭；关闭时平滑复位)", "true = maids' eyes on the yotogi Play screen always look at the camera (same as above; eye gain 0.2/0.1 as vanilla; off = smooth reset)", "true=夜伽プレイ画面の在场メイドの目を常にカメラへ（上と同じ・眼球ゲイン0.2/0.1の公式値、オフで滑らかに復帰）" },
            new string[] { "cfg.lookHeadGlobal", "女仆头部朝向镜头-全局：true=所有场景(夜伽以外也含)的在场女仆头部强制朝向镜头；关闭时平滑复位。与夜伽版独立叠加", "Heads look at camera - global: true = maids' heads always face the camera in every scene (not only yotogi); off = smooth reset. Stacks independently with the Play-screen version", "頭部カメラ注視-全体：true=全シーン（夜伽以外も）でメイドの頭部をカメラへ強制；オフで滑らかに復帰。夜伽版とは独立して併用可" },
            new string[] { "cfg.lookEyeGlobal", "女仆眼睛朝向镜头-全局：true=所有场景的在场女仆眼睛强制朝向镜头；关闭时平滑复位。与夜伽版独立叠加", "Eyes look at camera - global: true = maids' eyes always face the camera in every scene; off = smooth reset. Stacks independently with the Play-screen version", "目線カメラ注視-全体：true=全シーンでメイドの目をカメラへ強制；オフで滑らかに復帰。夜伽版とは独立して併用可" },
            new string[] { "cfg.uiHide", "true=夜伽Play画面可用热键（默认Tab）整体隐藏/恢复夜伽UI（指令菜单/技能塔/参数条/消息窗口）", "true = a hotkey (default Tab) hides / restores the whole yotogi UI on the Play screen (command menu / skill tower / parameter bar / message window)", "true=夜伽プレイ画面でホットキー（既定Tab）により夜伽UI全体（コマンドメニュー/スキルタワー/パラメータバー/メッセージ窓）を隠す/戻す" },
            new string[] { "cfg.uiHideKey", "隐藏UI的热键（仅夜伽Play画面生效）", "Hide-UI hotkey (yotogi Play screen only)", "UI非表示のホットキー（夜伽プレイ画面のみ有効）" },
            new string[] { "cfg.poseRes", "true=进入姿势/切换场景时自动补挂该姿势所需道具（垫子/猥亵椅/拘束机器等；不切场景，官方FT分支未覆盖的舞台也补全；事件驱动无轮询）", "true = auto-mount the props a pose needs (mat / lewd chair / restraint machine etc.) on pose entry and stage switch; no scene change, also covers stages the official FT branches miss; event-driven, no polling", "true=姿勢入場/シーン切替時にその姿勢に必要なアイテム（マット/猥褻椅子/拘束マシン等）を自動補掛；シーンは切り替えず、公式FT分岐が無い舞台も補完（イベント駆動・ポーリングなし）" }
        };

        // key → {zh, en, ja}（初回参照時に遅延構築; 以後は O(1) 参照）
        private static void BuildLangIndex()
        {
            Dictionary<string, string[]> idx = new Dictionary<string, string[]>(LangRows.Length);
            for (int i = 0; i < LangRows.Length; i++)
            {
                string[] row = LangRows[i];
                if (row == null || row.Length < 4)
                {
                    continue;
                }
                if (!idx.ContainsKey(row[0]))
                {
                    idx.Add(row[0], row);
                }
            }
            s_langIndex = idx;
        }

        // 現在言語の文字列（未登録キーは key をそのまま返す = 画面に
        // キーが出ればテーブル追加漏れが即分かる）
        private static string L(string key)
        {
            if (object.ReferenceEquals(s_langIndex, null))
            {
                BuildLangIndex();
            }
            string[] row;
            if (s_langIndex.TryGetValue(key, out row))
            {
                int li = s_lang;
                if (li < 0 || li >= 3)
                {
                    li = LangZh;
                }
                string v = row[1 + li];
                if (string.IsNullOrEmpty(v))
                {
                    v = row[1];   // 空欄は zh へフォールバック
                }
                if (!string.IsNullOrEmpty(v))
                {
                    return v;
                }
            }
            return key;
        }

        // プレースホルダ付き（{0}/{1}）版
        private static string LF(string key, params object[] args)
        {
            string fmt = L(key);
            try
            {
                return string.Format(fmt, args);
            }
            catch (Exception)
            {
                return fmt;
            }
        }

        // 表記ゆれ吸収（zh/zh-CN/簡体字, en/English, ja/jp/日本語…）
        private static string NormalizeLangCode(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return "zh-CN";
            }
            string s = raw.Trim().ToLowerInvariant();
            if (s.StartsWith("zh") || s.StartsWith("cn") || s.StartsWith("chinese") || s.StartsWith("简") || s.StartsWith("中"))
            {
                return "zh-CN";
            }
            if (s.StartsWith("ja") || s.StartsWith("jp") || s.StartsWith("japanese") || s.StartsWith("日"))
            {
                return "ja";
            }
            if (s.StartsWith("en") || s.StartsWith("english") || s.StartsWith("英"))
            {
                return "en";
            }
            return "zh-CN";
        }

        private static int LangIndexFromCode(string code)
        {
            if (string.Equals(code, "en"))
            {
                return LangEn;
            }
            if (string.Equals(code, "ja"))
            {
                return LangJa;
            }
            return LangZh;
        }

        // ログ用の逆引き（起動ログの [lang=xx] 表記）
        private static string LanguageCodeOf(int idx)
        {
            if (idx == LangEn)
            {
                return "en";
            }
            if (idx == LangJa)
            {
                return "ja";
            }
            return "zh-CN";
        }

        // 言語適用。save=true のとき cfg へ即保存（次回起動でも保持）
        private static void ApplyLanguageValue(string raw, bool save)
        {
            string norm = NormalizeLangCode(raw);
            s_lang = LangIndexFromCode(norm);
            if (save && !object.ReferenceEquals(cfgUiLanguage, null)
                && !string.Equals(cfgUiLanguage.Value, norm))
            {
                cfgUiLanguage.Value = norm;
            }
        }

        // F7 の言語ボタンから呼ぶ（適用 + 保存 + ログ + F1 即時反映）
        private static void SetUiLanguage(string code)
        {
            ApplyLanguageValue(code, true);
            s_nameCache = null;               // v1.8.5: 名称の表示キャッシュも破棄
            s_nameCacheLang = -1;
            if (!object.ReferenceEquals(log, null))
            {
                log.LogInfo("YotogiUnlimited: UI language = " + NormalizeLangCode(code));
            }
            // v1.8.5 (追加2): F1 の説明文を選択言語へ即時反映
            // ① 全 cfg 項目の ConfigDescription を貼り替え
            // ② ConfigurationManager の一覧を再構築（生成時コピーのため）
            // ③ cfg ファイルを保存（コメントも新言語へ）
            RefreshLangEntryDescriptions();
            RefreshConfigManagerList();
            s_langRefreshPending = false;      // 追加2b の遅延反映は不要（実行済み）
            try
            {
                if (!object.ReferenceEquals(s_cfgFile, null))
                {
                    s_cfgFile.Save();
                }
            }
            catch (Exception)
            {
            }
        }

        // v1.8.5 (追加2b): cfg の UiLanguage が F1 側で変更された時のフック。
        // ConfigurationManager の OnGUI 中に呼ばれるため, ここでは即時処理
        // せずフラグのみ（次フレームの Update で貼替＋一覧再構築を行う）。
        private static void OnUiLanguageSettingChanged(object sender, EventArgs e)
        {
            s_langRefreshPending = true;
        }

        // 遅延反映の実行（Update から毎フレーム呼ばれる）
        private static void ProcessLangRefreshIfPending()
        {
            if (!s_langRefreshPending)
            {
                return;
            }
            s_langRefreshPending = false;
            ApplyLanguageValue(object.ReferenceEquals(cfgUiLanguage, null)
                ? null : cfgUiLanguage.Value, false);
            s_nameCache = null;
            s_nameCacheLang = -1;
            RefreshLangEntryDescriptions();
            RefreshConfigManagerList();
            if (!object.ReferenceEquals(log, null))
            {
                log.LogInfo("YotogiUnlimited: UI language changed from F1 -> "
                    + LanguageCodeOf(s_lang) + " (descriptions refreshed)");
            }
        }

        // 言語切替の対象となる cfg 項目を登録（v1.8.5）
        private static void RegisterLangEntry(ConfigEntryBase entry, string key)
        {
            if (object.ReferenceEquals(entry, null))
            {
                return;
            }
            if (object.ReferenceEquals(s_langEntries, null))
            {
                s_langEntries = new List<ConfigEntryBase>();
                s_langEntryKeys = new List<string>();
            }
            s_langEntries.Add(entry);
            s_langEntryKeys.Add(key);
        }

        // ConfigEntryBase.Description は get のみ（BepInEx 5.4 実測: setter
        // 不在）—— backing field を反射更新する。AcceptableValues（範囲/
        // リスト）と Tags は既存の ConfigDescription から引き継ぐ。
        private static void RefreshLangEntryDescriptions()
        {
            try
            {
                if (object.ReferenceEquals(s_langEntries, null))
                {
                    return;
                }
                FieldInfo fi = typeof(ConfigEntryBase).GetField("<Description>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (object.ReferenceEquals(fi, null))
                {
                    return;
                }
                for (int i = 0; i < s_langEntries.Count; i++)
                {
                    ConfigEntryBase e = s_langEntries[i];
                    if (object.ReferenceEquals(e, null))
                    {
                        continue;
                    }
                    string key = (i < s_langEntryKeys.Count) ? s_langEntryKeys[i] : null;
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }
                    ConfigDescription old = e.Description;
                    if (object.ReferenceEquals(old, null))
                    {
                        fi.SetValue(e, new ConfigDescription(L(key)));
                    }
                    else
                    {
                        fi.SetValue(e, new ConfigDescription(L(key), old.AcceptableValues, old.Tags));
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        // ConfigurationManager（F1）の設定一覧を再構築させる ——
        // SettingEntryBase.Description は ConfigSettingEntry 生成時に
        // コピーされるため, 説明文の差し替えだけでは画面に反映されない。
        // 型/GUID を直参照せず（未導入環境でも無害にするため）:
        // ① 読み込み済みアセンブリから ConfigurationManager 型を名前で探索
        // ② FindObjectOfType でシーン上のインスタンスを取得
        // ③ public void BuildSettingList() を呼ぶ
        private static void RefreshConfigManagerList()
        {
            try
            {
                Type cmType = null;
                System.Reflection.Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < asms.Length; i++)
                {
                    try
                    {
                        Type t = asms[i].GetType("ConfigurationManager.ConfigurationManager", false);
                        if (!object.ReferenceEquals(t, null))
                        {
                            cmType = t;
                            break;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
                if (object.ReferenceEquals(cmType, null))
                {
                    return;
                }
                UnityEngine.Object inst = UnityEngine.Object.FindObjectOfType(cmType);
                if (object.ReferenceEquals(inst, null))
                {
                    return;
                }
                MethodInfo mi = cmType.GetMethod("BuildSettingList",
                    BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (object.ReferenceEquals(mi, null))
                {
                    return;
                }
                mi.Invoke(inst, null);
                if (!object.ReferenceEquals(log, null))
                {
                    log.LogInfo("YotogiUnlimited: ConfigurationManager list rebuilt (language switched)");
                }
            }
            catch (Exception)
            {
            }
        }

        // 言語のネイティブ名（折り畳みヘッダ表示用）
        private static string LangNativeName(int idx)
        {
            if (idx == LangEn)
            {
                return "English";
            }
            if (idx == LangJa)
            {
                return "日本語";
            }
            return "简体中文";
        }

        // =================================================================
        // v1.8.5: 名称翻訳（姿勢 / 夜伽舞台 / 摄影背景）
        // -----------------------------------------------------------------
        // 1行 = { 原文, zh-CN, en }。原文 = ゲームデータの日本語名そのまま
        // （Skill.Data.name / YotogiStage.Data.drawName / PhotoBGData.name）。
        //   NLoc(jp)     = 現在言語の表示名（ja=原文, 未登録=原文）
        //   NameMatch()  = 検索一致（原文・zh・en の何れでもヒット）
        // 名称一覧はゲーム側 .nei（独自バイナリ）から外部抽出できないため,
        // 実行時に ExportNameList() が列挙して書き出す（下記）。
        // =================================================================
        private static readonly string[][] NameRows = new string[][]
        {
            // { "原文", "简体中文", "English" }
                        new string[] { "劇場", "剧场", "Theater" },
            new string[] { "ソープランド", "泡泡浴店", "Soapland" },
            new string[] { "SMクラブ", "SM俱乐部", "SM Club" },
            new string[] { "主人公部屋", "主人公房间", "Protagonist's Room" },
            new string[] { "ショッピングモール", "购物中心", "Shopping Mall" },
            new string[] { "メイド部屋", "女仆房间", "Maid's Room" },
            new string[] { "ホテル\u30FB寝室", "酒店·卧室", "Hotel - Bedroom" },
            new string[] { "ホテル\u30FBリビング", "酒店·客厅", "Hotel - Living Room" },
            new string[] { "ホテル\u30FBトイレ", "酒店·厕所", "Hotel - Toilet" },
            new string[] { "ホテル\u30FB洗面所", "酒店·盥洗室", "Hotel - Washroom" },
            new string[] { "地下室", "地下室", "Basement" },
            new string[] { "ロッカールーム", "更衣室", "Locker Room" },
            new string[] { "プライベートルーム", "私人房间", "Private Room" },
            new string[] { "トイレ", "厕所", "Toilet" },
            new string[] { "女性トイレ", "女厕所", "Women's Toilet" },
            new string[] { "アダルトショップ", "成人用品店", "Adult Shop" },
            new string[] { "居酒屋", "居酒屋", "Izakaya" },
            new string[] { "撮影スタジオ", "摄影棚", "Photo Studio" },
            new string[] { "ラブホテル", "情侣酒店", "Love Hotel" },
            new string[] { "執務室", "办公室", "Office" },
            new string[] { "ブティック", "精品店", "Boutique" },
            new string[] { "祭りの神社", "祭典神社", "Festival Shrine" },
            new string[] { "カラオケルーム", "卡拉OK包间", "Karaoke Room" },
            new string[] { "ラブホテル２", "情侣酒店2", "Love Hotel 2" },
            new string[] { "ハネムーンホテル", "蜜月酒店", "Honeymoon Hotel" },
            new string[] { "海上コテージ", "海上小屋", "Sea Cottage" },
            new string[] { "隠れ入り江", "隐秘海湾", "Hidden Inlet" },
            new string[] { "シャワールーム", "淋浴间", "Shower Room" },
            new string[] { "ダンスバー", "舞吧", "Dance Bar" },
            new string[] { "主人公部屋IF", "主人公房间IF", "Protagonist's Room IF" },
            new string[] { "調教地下室", "调教地下室", "Training Basement" },
            new string[] { "調教地下室２", "调教地下室2", "Training Basement 2" },
            new string[] { "ナイトプール", "夜间泳池", "Night Pool" },
            new string[] { "ナイトプールオレンジ", "夜间泳池·橙", "Night Pool (Orange)" },
            new string[] { "帝国荘", "帝国庄", "Teikoku Villa" },
            new string[] { "電車", "电车", "Train" },
            new string[] { "教室", "教室", "Classroom" },
            new string[] { "和風旅館", "和风旅馆", "Japanese Inn" },
            new string[] { "コスプレ会場", "Cosplay会场", "Cosplay Venue" },
            new string[] { "ナイトプールグリーン", "夜间泳池·绿", "Night Pool (Green)" },
            new string[] { "帝国荘サウナ", "帝国庄桑拿", "Teikoku Villa Sauna" },
            new string[] { "帝国荘BBQ", "帝国庄BBQ", "Teikoku Villa BBQ" },
            new string[] { "帝国荘露天風呂", "帝国庄露天浴池", "Teikoku Villa Open-Air Bath" },
            new string[] { "帝国荘２", "帝国庄2", "Teikoku Villa 2" },
            new string[] { "エンパイアクラブ-ロータリー", "帝国俱乐部·圆形大厅", "Empire Club - Rotunda" },
            new string[] { "エンパイアクラブ-エントランス", "帝国俱乐部·入口", "Empire Club - Entrance" },
            new string[] { "トレーニングルーム", "训练室", "Training Room" },
            new string[] { "カジノ", "赌场", "Casino" },
            new string[] { "カジノミニ", "迷你赌场", "Mini Casino" },
            new string[] { "ライブステージ", "演播舞台", "Live Stage" },
            new string[] { "ライブステージ裏", "舞台后台", "Backstage" },
            new string[] { "バーラウンジ", "酒吧休息厅", "Bar Lounge" },
            new string[] { "キッチン", "厨房", "Kitchen" },
            new string[] { "オープンカフェ", "露天咖啡座", "Open Cafe" },
            new string[] { "レストラン", "餐厅", "Restaurant" },
            new string[] { "宿泊部屋-ベッドルーム", "住宿部屋·卧室", "Lodging - Bedroom" },
            new string[] { "宿泊部屋-リビング", "住宿部屋·客厅", "Lodging - Living Room" },
            new string[] { "宿泊部屋-トイレ", "住宿部屋·厕所", "Lodging - Toilet" },
            new string[] { "宿泊部屋-洗面所", "住宿部屋·盥洗室", "Lodging - Washroom" },
            new string[] { "ソープ", "泡泡浴", "Soap" },
            new string[] { "スパ", "水疗会所", "Spa" },
            new string[] { "ランス10コラボカフェ", "兰斯10联动咖啡厅", "Rance 10 Collab Cafe" },
            new string[] { "シーカフェ", "海景咖啡厅", "Sea Cafe" },
            new string[] { "リドルジョーカーコラボカフェ", "谜语小丑联动咖啡厅", "Riddle Joker Collab Cafe" },
            new string[] { "プール", "泳池", "Pool" },
            new string[] { "わんこの嫁入りコラボカフェ", "犬娘出嫁联动咖啡厅", "Wanko no Yomeiri Collab Cafe" },
            new string[] { "神社", "神社", "Shrine" },
            new string[] { "ラズベリーキューブコラボカフェ", "树莓立方体联动咖啡厅", "Raspberry Cube Collab Cafe" },
            new string[] { "みにくいモジカの子コラボカフェ", "丑陋的莫吉卡之子联动咖啡厅", "Minikui Mojika no Ko Collab Cafe" },
            new string[] { "未来ラジオと人工鳩コラボカフェ", "未来广播与人工鸽联动咖啡厅", "Future Radio & Artificial Pigeon Collab Cafe" },
            new string[] { "ポールダンスステージ", "钢管舞舞台", "Pole Dance Stage" },
            new string[] { "エンパイアクラブ-廊下", "帝国俱乐部·走廊", "Empire Club - Hallway" },
            new string[] { "エンパイアクラブ-エレベーター", "帝国俱乐部·电梯", "Empire Club - Elevator" },
            new string[] { "ゲレンデ", "滑雪场", "Ski Slope" },
            new string[] { "冒険者の酒場", "冒险者酒馆", "Adventurers' Tavern" },
            new string[] { "金色ラブリッチェGTコラボカフェ", "金色Loveriche GT联动咖啡厅", "Kiniro Loveriche GT Collab Cafe" },
            new string[] { "絆きらめく恋いろはコラボカフェ", "羁绊闪耀恋色伊吕波联动咖啡厅", "Kizuna Kirameku Koi Iroha Collab Cafe" },
            new string[] { "春の庭園", "春日庭园", "Spring Garden" },
            new string[] { "春の庭園_屋台無し", "春日庭园（无摊位）", "Spring Garden (No Stalls)" },
            new string[] { "イブニクル2コラボカフェ", "Evenicle2联动咖啡厅", "Evenicle 2 Collab Cafe" },
            new string[] { "コロラム！コラボカフェ", "Korolum联动咖啡厅", "Korolum! Collab Cafe" },
            new string[] { "コロラム！コラボカフェ2", "Korolum联动咖啡厅2", "Korolum! Collab Cafe 2" },
            new string[] { "日本家屋", "日本家屋", "Japanese House" },
            new string[] { "わんこの嫁入りコラボカフェ2", "犬娘出嫁联动咖啡厅2", "Wanko no Yomeiri Collab Cafe 2" },
            new string[] { "あいミス！コラボカフェ", "爱MISU联动咖啡厅", "Ai Misu! Collab Cafe" },
            new string[] { "ネコぱらコラボカフェ", "猫娘乐园联动咖啡厅", "Nekopara Collab Cafe" },
            new string[] { "大聖堂", "大教堂", "Cathedral" },
            new string[] { "プライベートルーム-消灯", "私人房间·熄灯", "Private Room (Lights Off)" },
            new string[] { "ホームレステント", "流浪者帐篷", "Homeless Tent" },
            new string[] { "アイキスコラボカフェ", "爱Kiss联动咖啡厅", "Aikiss Collab Cafe" },
            new string[] { "公園", "公园", "Park" },
            new string[] { "公園-キッチンカー無し", "公园（无餐车）", "Park (No Food Truck)" },
            new string[] { "水族館", "水族馆", "Aquarium" },
            new string[] { "水族館-椅子無し", "水族馆（无座椅）", "Aquarium (No Chairs)" },
            new string[] { "居酒屋-散らかり", "居酒屋（杂乱）", "Izakaya (Messy)" },
            new string[] { "街角", "街角", "Street Corner" },
            new string[] { "マリーさんコラボカフェ", "玛丽小姐联动咖啡厅", "Marie-san Collab Cafe" },
            new string[] { "怪しげなバー", "可疑酒吧", "Suspicious Bar" },
            new string[] { "キャンプ場-昼", "露营地·白天", "Campsite - Day" },
            new string[] { "キャンプ場-夕方", "露营地·傍晚", "Campsite - Evening" },
            new string[] { "キャンプ場-夜", "露营地·夜晚", "Campsite - Night" },
            new string[] { "アイキス2コラボカフェ", "爱Kiss2联动咖啡厅", "Aikiss 2 Collab Cafe" },
            new string[] { "水上コテージ", "水上小屋", "Water Cottage" },
            new string[] { "モダン旅館", "现代旅馆", "Modern Inn" },
            new string[] { "ハロウィンカフェ", "万圣节咖啡厅", "Halloween Cafe" },
            new string[] { "ラブホテル2-ベッド回転ON", "情侣酒店2·床旋转ON", "Love Hotel 2 - Bed Rotate ON" },
            new string[] { "ラブホテル2-ベッド回転OFF", "情侣酒店2·床旋转OFF", "Love Hotel 2 - Bed Rotate OFF" },
            new string[] { "同棲ルーム", "同居房间", "Cohabitation Room" },
            new string[] { "純真メイド部屋", "纯真女仆房间", "Innocent Maid's Room" },
            new string[] { "ツンデレメイド部屋", "傲娇女仆房间", "Tsundere Maid's Room" },
            new string[] { "ウェディングビーチ-昼", "婚礼海滩·白天", "Wedding Beach - Day" },
            new string[] { "ウェディングビーチ-夕方", "婚礼海滩·傍晚", "Wedding Beach - Evening" },
            new string[] { "ウェディングビーチ-夜", "婚礼海滩·夜晚", "Wedding Beach - Night" },
            new string[] { "ハネムーンホテル-昼", "蜜月酒店·白天", "Honeymoon Hotel - Day" },
            new string[] { "ハネムーンホテル-夕方", "蜜月酒店·傍晚", "Honeymoon Hotel - Evening" },
            new string[] { "ハネムーンホテル-夜", "蜜月酒店·夜晚", "Honeymoon Hotel - Night" },
            new string[] { "シャワールーム-昼", "淋浴间·白天", "Shower Room - Day" },
            new string[] { "シャワールーム-夜", "淋浴间·夜晚", "Shower Room - Night" },
            new string[] { "隠れ入り江-昼", "隐秘海湾·白天", "Hidden Inlet - Day" },
            new string[] { "隠れ入り江-夕方", "隐秘海湾·傍晚", "Hidden Inlet - Evening" },
            new string[] { "隠れ入り江-夜", "隐秘海湾·夜晚", "Hidden Inlet - Night" },
            new string[] { "海上コテージ-昼", "海上小屋·白天", "Sea Cottage - Day" },
            new string[] { "海上コテージ-夜", "海上小屋·夜晚", "Sea Cottage - Night" },
            new string[] { "クーデレメイド部屋", "冷娇女仆房间", "Kuudere Maid's Room" },
            new string[] { "小悪魔色の同棲ルーム", "小恶魔风同居房间", "Little Devil Cohabitation Room" },
            new string[] { "ドS色の同棲ルーム", "抖S风同居房间", "Sadist Cohabitation Room" },
            new string[] { "無愛想色の同棲ルーム", "冷淡风同居房间", "Curt Cohabitation Room" },
            new string[] { "文学少女色の同棲ルーム", "文学少女风同居房间", "Bookish Girl Cohabitation Room" },
            new string[] { "ご主人様部屋IF", "主人房间IF", "Master's Room IF" },
            new string[] { "ご主人様部屋IF-消灯", "主人房间IF·熄灯", "Master's Room IF (Lights Off)" },
            new string[] { "おしとやか色の同棲ルーム", "文雅风同居房间", "Ladylike Cohabitation Room" },
            new string[] { "お嬢様色の同棲ルーム", "大小姐风同居房间", "Young Lady Cohabitation Room" },
            new string[] { "大きな居酒屋", "大居酒屋", "Large Izakaya" },
            new string[] { "メイド秘書色の同棲ルーム", "女仆秘书风同居房间", "Maid Secretary Cohabitation Room" },
            new string[] { "地下調教室【通常】", "地下调教室【普通】", "Underground Training Room [Normal]" },
            new string[] { "地下調教室【汚れ】", "地下调教室【脏污】", "Underground Training Room [Dirty]" },
            new string[] { "バレンタインカフェ", "情人节咖啡厅", "Valentine Cafe" },
            new string[] { "幼馴染色の同棲ルーム", "青梅竹马风同居房间", "Childhood Friend Cohabitation Room" },
            new string[] { "ふわふわ妹色の同棲ルーム", "软萌妹妹风同居房间", "Fluffy Sister Cohabitation Room" },
            new string[] { "ヤンデレ色の同棲ルーム", "病娇风同居房间", "Yandere Cohabitation Room" },
            new string[] { "女子トイレ", "女厕所", "Women's Toilet" },
            new string[] { "プライベートビーチ", "私人海滩", "Private Beach" },
            new string[] { "カラオケルームDX", "卡拉OK包间DX", "Karaoke Room DX" },        };

        // 原文 → {zh,en}（初回参照時に遅延構築）
        private static void BuildNameIndex()
        {
            Dictionary<string, string[]> idx = new Dictionary<string, string[]>(NameRows.Length);
            for (int i = 0; i < NameRows.Length; i++)
            {
                string[] row = NameRows[i];
                if (row == null || row.Length < 3 || string.IsNullOrEmpty(row[0]))
                {
                    continue;
                }
                if (!idx.ContainsKey(row[0]))
                {
                    idx.Add(row[0], row);
                }
            }
            s_nameIndex = idx;
        }

        private static string[] NameLookup(string jpName)
        {
            if (string.IsNullOrEmpty(jpName))
            {
                return null;
            }
            if (!s_nameExtLoaded)
            {
                LoadNameTranslationFile();
            }
            string[] ext = null;
            if (!object.ReferenceEquals(s_nameExt, null))
            {
                s_nameExt.TryGetValue(jpName, out ext);
            }
            if (object.ReferenceEquals(s_nameIndex, null))
            {
                BuildNameIndex();
            }
            string[] emb;
            s_nameIndex.TryGetValue(jpName, out emb);
            if (object.ReferenceEquals(ext, null))
            {
                return emb;
            }
            if (object.ReferenceEquals(emb, null))
            {
                return ext;
            }
            // 両方にある場合は外部ファイルの非空セルを優先
            // （ユーザーが埋め込み訳を上書き修正できる）
            string zh = string.IsNullOrEmpty(ext[1]) ? emb[1] : ext[1];
            string en = string.IsNullOrEmpty(ext[2]) ? emb[2] : ext[2];
            return new string[] { jpName, zh, en };
        }

        // 現在言語での表示名（ja / 未登録 = 原文のまま）
        private static string NLoc(string jpName)
        {
            if (string.IsNullOrEmpty(jpName) || s_lang == LangJa)
            {
                return jpName;
            }
            if (object.ReferenceEquals(s_nameCache, null) || s_nameCacheLang != s_lang)
            {
                s_nameCache = new Dictionary<string, string>();
                s_nameCacheLang = s_lang;
            }
            string cached;
            if (s_nameCache.TryGetValue(jpName, out cached))
            {
                return cached;
            }
            string v = jpName;
            string[] row = NameLookup(jpName);
            if (!object.ReferenceEquals(row, null))
            {
                string t = (s_lang == LangEn) ? row[2] : row[1];
                if (!string.IsNullOrEmpty(t))
                {
                    v = t;
                }
            }
            s_nameCache.Add(jpName, v);
            return v;
        }

        // 検索一致: 原文（日本語）+ zh + en のどれに含まれてもヒット
        private static bool NameMatch(string jpName, string query)
        {
            if (string.IsNullOrEmpty(jpName) || string.IsNullOrEmpty(query))
            {
                return false;
            }
            if (jpName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                return true;
            }
            string[] row = NameLookup(jpName);
            if (object.ReferenceEquals(row, null))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(row[1])
                && row[1].IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                return true;
            }
            if (!string.IsNullOrEmpty(row[2])
                && row[2].IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                return true;
            }
            return false;
        }

        // 名称リスト出力先（BepInEx\config\YotogiUnlimited.name_i18n.txt）
        private static string NameListPath()
        {
            try
            {
                return System.IO.Path.Combine(BepInEx.Paths.ConfigPath, NameListFile);
            }
            catch (Exception)
            {
                return NameListFile;
            }
        }

        // 外部翻訳ファイル読込（タブ区切り: 原文<TAB>简体中文<TAB>English）
        //   [stage] / [background] / [skill] のセクションヘッダは無視,
        //   '#' 行はコメント。空セルは埋め込み訳へのフォールバック。
        private static void LoadNameTranslationFile()
        {
            try
            {
                string path = NameListPath();
                Dictionary<string, string[]> map = new Dictionary<string, string[]>();
                if (System.IO.File.Exists(path))
                {
                    s_nameExtStampTicks = System.IO.File.GetLastWriteTimeUtc(path).Ticks;
                    string[] lines = System.IO.File.ReadAllLines(path, System.Text.Encoding.UTF8);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        if (string.IsNullOrEmpty(line) || line[0] == '#')
                        {
                            continue;
                        }
                        string[] cells = line.Split('\t');
                        if (cells.Length < 2 || string.IsNullOrEmpty(cells[0]))
                        {
                            continue;   // セクションヘッダ等
                        }
                        string zh = (cells.Length > 1) ? cells[1] : string.Empty;
                        string en = (cells.Length > 2) ? cells[2] : string.Empty;
                        if (string.IsNullOrEmpty(zh) && string.IsNullOrEmpty(en))
                        {
                            continue;   // 未記入行は無視（埋め込み訳を使用）
                        }
                        map[cells[0]] = new string[] { cells[0], zh, en };
                    }
                }
                s_nameExt = map;
                s_nameExtLoaded = true;
                if (!object.ReferenceEquals(log, null))
                {
                    log.LogInfo("YotogiUnlimited: name translations loaded ("
                        + map.Count + " rows from " + path + ")");
                }
            }
            catch (Exception e)
            {
                s_nameExtLoaded = true;
                LogOnce("LoadNameTranslationFile: " + e.Message);
            }
        }

        // F7 ボタン: 名称翻訳の手動再読込（編集内容を即反映）
        private static void ReloadNameTranslations()
        {
            s_nameExtLoaded = false;
            s_nameCache = null;
            s_nameCacheLang = -1;
            LoadNameTranslationFile();
        }

        // 外部ファイルの更新を自動検知（2秒スロットル）—— ファイルを
        // 保存し直すだけで反映される（F7 ボタンは保険）。
        private static void CheckNameTranslationAutoReload()
        {
            if (Time.time - s_nameExtCheckTime < 2f)
            {
                return;
            }
            s_nameExtCheckTime = Time.time;
            try
            {
                string path = NameListPath();
                if (!System.IO.File.Exists(path))
                {
                    return;
                }
                long ticks = System.IO.File.GetLastWriteTimeUtc(path).Ticks;
                if (s_nameExtLoaded && ticks == s_nameExtStampTicks)
                {
                    return;
                }
                s_nameExtStampTicks = ticks;
                s_nameExtLoaded = false;
                LoadNameTranslationFile();
                s_nameCache = null;
                s_nameCacheLang = -1;
                if (!object.ReferenceEquals(log, null))
                {
                    log.LogInfo("YotogiUnlimited: name translation file changed -> reloaded");
                }
            }
            catch (Exception)
            {
            }
        }

        // 起動後1回だけ自動出力（ファイルが既にあれば何もしない）
        private static void AutoExportNameListOnce()
        {
            if (s_nameExportTried)
            {
                return;
            }
            try
            {
                if (System.IO.File.Exists(NameListPath()))
                {
                    s_nameExportTried = true;
                    return;
                }
                // テーブル準備待ち（GameMain が Skill.CreateData を終えるまで）
                if (object.ReferenceEquals(Skill.skill_data_list, null))
                {
                    return;
                }
                s_nameExportTried = true;
                ExportNameList(true);
            }
            catch (Exception)
            {
                s_nameExportTried = true;
            }
        }

        // 埋め込みテーブル（NameRows）のみの参照（出力時の補完用）
        private static string[] NameLookupEmbedded(string jpName)
        {
            if (string.IsNullOrEmpty(jpName))
            {
                return null;
            }
            if (object.ReferenceEquals(s_nameIndex, null))
            {
                BuildNameIndex();
            }
            string[] row;
            s_nameIndex.TryGetValue(jpName, out row);
            return row;
        }

        // 出力1行分（既存ファイル → 埋め込み訳 の順でセルを補完）。
        // 戻り値: 1 = 訳が入った行, 0 = 未記入行。
        private static int AppendNameRow(System.Text.StringBuilder sb,
            Dictionary<string, string[]> keep, string jp)
        {
            if (string.IsNullOrEmpty(jp))
            {
                return 0;
            }
            string zh = string.Empty;
            string en = string.Empty;
            string[] kept;
            if (keep.TryGetValue(jp, out kept))
            {
                if (kept.Length > 1)
                {
                    zh = kept[1];
                }
                if (kept.Length > 2)
                {
                    en = kept[2];
                }
            }
            string[] emb = NameLookupEmbedded(jp);
            if (!object.ReferenceEquals(emb, null))
            {
                if (string.IsNullOrEmpty(zh))
                {
                    zh = emb[1];
                }
                if (string.IsNullOrEmpty(en))
                {
                    en = emb[2];
                }
            }
            sb.AppendLine(jp + "\t" + zh + "\t" + en);
            return (string.IsNullOrEmpty(zh) && string.IsNullOrEmpty(en)) ? 0 : 1;
        }

        // 名称リストを「翻訳ファイル形式」で書き出す ——
        //   原文<TAB>简体中文<TAB>English（[stage]/[background]/[skill]）
        // 既存ファイルの記入済みセルは保持（ユーザー訳を壊さない）,
        // 空セルは埋め込み訳で補完。保存すれば自動再読込される。
        private static void ExportNameList(bool overwrite)
        {
            try
            {
                string path = NameListPath();
                if (!overwrite && System.IO.File.Exists(path))
                {
                    return;
                }
                // 既存の記入内容を退避（原文 → セル配列）
                Dictionary<string, string[]> keep = new Dictionary<string, string[]>();
                if (System.IO.File.Exists(path))
                {
                    string[] oldLines = System.IO.File.ReadAllLines(path, System.Text.Encoding.UTF8);
                    for (int i = 0; i < oldLines.Length; i++)
                    {
                        string line = oldLines[i];
                        if (string.IsNullOrEmpty(line) || line[0] == '#')
                        {
                            continue;
                        }
                        string[] c = line.Split('\t');
                        if (c.Length < 2 || string.IsNullOrEmpty(c[0]))
                        {
                            continue;
                        }
                        keep[c[0]] = c;
                    }
                }

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("# COM3D2.YotogiUnlimited 名称翻译表 (plugin v" + PluginVersion + ")");
                sb.AppendLine("# 用 TAB 分隔 3 列：  原文 <TAB> 简体中文 <TAB> English");
                sb.AppendLine("# ・留空 = 依次回退到 内置译文 → 原文（舞台/背景已内置译文，可自行修改）");
                sb.AppendLine("# ・保存本文件后约 2 秒自动重新载入；也可按 F7 §功能 的「重新载入名称翻译」");
                sb.AppendLine("# ・[stage] / [background] / [skill] 是分节标题行（读取时忽略，可随意增删空格）");
                sb.AppendLine("# ・姿势一览的搜索会匹配 原文 / 中文 / English 任意写法");
                sb.AppendLine("# ・[skill] 区共 1841 行，按需填写即可（未填写的行仍显示日文原名）");
                int nStage = 0;
                int nBg = 0;
                int nSkill = 0;
                int nFilled = 0;

                sb.AppendLine();
                sb.AppendLine("[stage]");
                try
                {
                    List<YotogiStage.Data> stages = YotogiStage.GetAllDatas(false);
                    if (stages != null)
                    {
                        for (int i = 0; i < stages.Count; i++)
                        {
                            YotogiStage.Data d = stages[i];
                            if (d == null || string.IsNullOrEmpty(d.drawName))
                            {
                                continue;
                            }
                            nStage++;
                            nFilled += AppendNameRow(sb, keep, d.drawName);
                        }
                    }
                }
                catch (Exception e)
                {
                    sb.AppendLine("# stage 列挙失敗: " + e.Message);
                }

                sb.AppendLine();
                sb.AppendLine("[background]");
                try
                {
                    List<PhotoBGData> pbs = PhotoBGData.data;
                    if (pbs == null || pbs.Count == 0)
                    {
                        PhotoBGData.Create();
                        pbs = PhotoBGData.data;
                    }
                    if (pbs != null)
                    {
                        for (int i = 0; i < pbs.Count; i++)
                        {
                            PhotoBGData bg = pbs[i];
                            if (bg == null || string.IsNullOrEmpty(bg.create_prefab_name)
                                || string.IsNullOrEmpty(bg.name))
                            {
                                continue;
                            }
                            nBg++;
                            nFilled += AppendNameRow(sb, keep, bg.name);
                        }
                    }
                }
                catch (Exception e)
                {
                    sb.AppendLine("# background 列挙失敗: " + e.Message);
                }

                sb.AppendLine();
                sb.AppendLine("[skill]");
                try
                {
                    SortedDictionary<int, Skill.Data>[] cats = Skill.skill_data_list;
                    if (cats != null)
                    {
                        HashSet<string> seen = new HashSet<string>();
                        for (int c = 0; c < cats.Length; c++)
                        {
                            if (cats[c] == null)
                            {
                                continue;
                            }
                            foreach (Skill.Data sd in cats[c].Values)
                            {
                                if (sd == null || string.IsNullOrEmpty(sd.name))
                                {
                                    continue;
                                }
                                if (!seen.Add(sd.name))
                                {
                                    continue; // 同名変体は1行に集約
                                }
                                nSkill++;
                                nFilled += AppendNameRow(sb, keep, sd.name);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    sb.AppendLine("# skill 列挙失敗: " + e.Message);
                }

                System.IO.File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(true));
                s_nameExtLoaded = false;   // 次回参照時に読み直す
                if (!object.ReferenceEquals(log, null))
                {
                    log.LogInfo("YotogiUnlimited: name translation file exported -> " + path
                        + " (" + nStage + " stages / " + nBg + " backgrounds / " + nSkill
                        + " skills, " + nFilled + " translated)");
                }
            }
            catch (Exception e)
            {
                LogOnce("ExportNameList: " + e.Message);
            }
        }

        // =================================================================
        // OnGUI: 調整メニュー（v1.6.1 排版精簡：四区・ヒント削除）。
        // POV常時角標は撤去済み（状态は §POV の [POV中] のみ）。
        // =================================================================
        private void OnGUI()
        {
            if (object.ReferenceEquals(cfgSliders, null) || !cfgSliders.Value || !slidersVisible)
            {
                return;
            }
            try
            {
                YotogiManager ym = YotogiManager.instans;
                if (ym == null || ym.cur_call_screen_name != "Play")
                {
                    return;
                }
                Maid m = ym.maid;
                if (m == null || m.status == null)
                {
                    return;
                }
                // 内容19: ウィンドウ位置を画面内にクランプ（他プラグイン
                // ウィンドウに押し出されても常時見える位置に留まる）
                float maxX = (float)Screen.width - 100f;
                float maxY = (float)Screen.height - 60f;
                if (maxX < 0f) { maxX = 0f; }
                if (maxY < 0f) { maxY = 0f; }
                sliderWinRect.x = Mathf.Clamp(sliderWinRect.x, 0f, maxX);
                sliderWinRect.y = Mathf.Clamp(sliderWinRect.y, 0f, maxY);
                sliderWinRect = GUILayout.Window(sliderWinId, sliderWinRect, DrawSliderWindow,
                    LF("win.main", "v" + PluginVersion, cfgSliderKey.Value));
                // v1.8.2 (P44): 更新 IMGUI 鼠标悬停标志 —— 鼠标在主窗或
                // 姿势一览窗 rect 内时, UltimateOrbitCamera.Update prefix
                // 据此屏蔽游戏相机的滚轮缩放/左右键输入（P44）。
                try
                {
                    Vector3 mp = Input.mousePosition;
                    Vector2 guiPos = new Vector2(mp.x, (float)Screen.height - mp.y);
                    s_imGuiMouseOver = sliderWinRect.Contains(guiPos);
                    if (!s_imGuiMouseOver && s_skillBrowserOpen && s_browserRectLast.width > 1f)
                    {
                        s_imGuiMouseOver = s_browserRectLast.Contains(guiPos);
                    }
                }
                catch (Exception)
                {
                    s_imGuiMouseOver = false;
                }
                // v1.7.3 (issue 1.1): 姿势一览 = 独立大窗, 附着在主菜单
                // 左侧, 平行一体（随主窗移动; 屏幕左缘放不下时贴右侧）。
                if (s_skillBrowserOpen)
                {
                    float bw = 380f;
                    float bh = Mathf.Min(560f, (float)Screen.height - 40f);
                    float bx = sliderWinRect.x - bw - 6f;
                    if (bx < 0f)
                    {
                        bx = sliderWinRect.x + sliderWinRect.width + 6f;
                    }
                    if (bx + bw > (float)Screen.width)
                    {
                        bx = Mathf.Max(0f, (float)Screen.width - bw);
                    }
                    float by = Mathf.Min(sliderWinRect.y, (float)Screen.height - bh);
                    if (by < 0f)
                    {
                        by = 0f;
                    }
                    Rect br = new Rect(bx, by, bw, bh);
                    s_browserRectLast = br;
                    br = GUILayout.Window(browserWinId, br, DrawSkillBrowserWindow,
                        L("win.browser"));
                }
            }
            catch (Exception e)
            {
                LogOnce("Sliders: " + e.Message);
            }
        }

        private void DrawSliderWindow(int id)
        {
            YotogiManager ym = YotogiManager.instans;
            if (ym == null)
            {
                return;
            }
            Maid m = ym.maid;
            if (m == null || m.status == null)
            {
                GUILayout.Label(L("win.noMaid"));
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
                return;
            }

            // ---- § 语言 (v1.8.5: クリックで開く折り畳み + 即時反映) ----
            GUILayout.BeginVertical(GUI.skin.box);
            string langArrow = s_langOpen ? "\u25BC " : "\u25B6 ";
            if (GUILayout.Button(langArrow + L("lang.title") + "：" + LangNativeName(s_lang)))
            {
                s_langOpen = !s_langOpen;
            }
            if (s_langOpen)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button((s_lang == LangZh ? "● " : "") + "简体中文"))
                {
                    SetUiLanguage("zh-CN");
                }
                if (GUILayout.Button((s_lang == LangEn ? "● " : "") + "English"))
                {
                    SetUiLanguage("en");
                }
                if (GUILayout.Button((s_lang == LangJa ? "● " : "") + "日本語"))
                {
                    SetUiLanguage("ja");
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            // ---- § 参数（内容8; v1.8.1 回退两行式: 标签行+滑条行）----
            GUILayout.BeginVertical(GUI.skin.box);
            int excite = m.status.currentExcite;
            int sensual = m.status.currentSensual;
            GUILayout.Label(LF("param.excite", excite));
            int newExcite = (int)GUILayout.HorizontalSlider((float)excite, -100f, 300f);
            GUILayout.Label(LF("param.sensual", sensual));
            int newSensual = (int)GUILayout.HorizontalSlider((float)sensual, 0f, 300f);
            if (newExcite != excite || newSensual != sensual)
            {
                m.status.currentExcite = newExcite;
                m.status.currentSensual = newSensual;
                YotogiPlayManager pm = ym.play_mgr;
                if (pm != null)
                {
                    object bar = fiParamBasicBar.GetValue(pm);
                    if (bar is YotogiParamBasicBar)
                    {
                        ((YotogiParamBasicBar)bar).SetCurrentExcite(newExcite, false);
                        ((YotogiParamBasicBar)bar).SetCurrentSensual(newSensual, false);
                    }
                    try
                    {
                        pm.UpdateCommand();
                    }
                    catch (Exception e)
                    {
                        LogOnce("UpdateCommand: " + e.Message);
                    }
                }
            }
            GUILayout.EndVertical();

            // ---- § 速度（内容11/12/17/18; v1.8.1 回退两行式）----
            GUILayout.BeginVertical(GUI.skin.box);
            bool onegai = GUILayout.Toggle(cfgOnegai.Value, L("speed.onegai"));
            if (onegai != cfgOnegai.Value)
            {
                cfgOnegai.Value = onegai;
                if (onegai)
                {
                    onegaiPhase = -1;
                    onegaiTimer = 0f;
                }
            }
            if (cfgOnegai.Value)
            {
                int pk = (onegaiPhase >= 0 && onegaiPhase < OnegaiPhaseKeys.Length) ? onegaiPhase : 0;
                GUILayout.Label(LF("speed.phase", s_speedMul.ToString("F2"), L(OnegaiPhaseKeys[pk])));
            }
            else
            {
                GUILayout.Label(LF("speed.plain", s_speedMul.ToString("F2")));
                float newMul = GUILayout.HorizontalSlider(s_speedMul, SpeedMin, SpeedMax);
                if (Mathf.Abs(newMul - s_speedMul) > 0.0001f)
                {
                    s_speedMul = Mathf.Clamp(newMul, SpeedMin, SpeedMax);
                }
            }
            GUILayout.EndVertical();

            // ---- § POV（内容26; v1.8.1 回退两行式: 标签行+滑条行）----
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(LF("pov.label", cfgPovKey.Value) + (s_povActive ? L("pov.active") : ""));
            GUILayout.Label(LF("pov.fov", cfgPovFov.Value.ToString("F0")));
            float fovN = GUILayout.HorizontalSlider(cfgPovFov.Value, 20f, 110f);
            if (Mathf.Abs(fovN - cfgPovFov.Value) > 0.01f)
            {
                cfgPovFov.Value = fovN;
            }
            GUILayout.Label(LF("pov.forward", cfgPovForward.Value.ToString("F3")));
            float fwdN = GUILayout.HorizontalSlider(cfgPovForward.Value, 0f, 0.2f);
            if (Mathf.Abs(fwdN - cfgPovForward.Value) > 0.0005f)
            {
                cfgPovForward.Value = fwdN;
            }
            GUILayout.Label(LF("pov.up", cfgPovUp.Value.ToString("F3")));
            float upN = GUILayout.HorizontalSlider(cfgPovUp.Value, -0.1f, 0.15f);
            if (Mathf.Abs(upN - cfgPovUp.Value) > 0.0005f)
            {
                cfgPovUp.Value = upN;
            }
            GUILayout.Label(LF("pov.smooth", cfgPovSmooth.Value.ToString("F2")));
            float smoothN = GUILayout.HorizontalSlider(cfgPovSmooth.Value, 0f, 1f);
            if (Mathf.Abs(smoothN - cfgPovSmooth.Value) > 0.005f)
            {
                cfgPovSmooth.Value = smoothN;
            }
            bool lookHeadN = GUILayout.Toggle(cfgLookHead.Value, L("pov.lookHead"));
            if (lookHeadN != cfgLookHead.Value)
            {
                cfgLookHead.Value = lookHeadN;
            }
            bool lookEyeN = GUILayout.Toggle(cfgLookEye.Value, L("pov.lookEye"));
            if (lookEyeN != cfgLookEye.Value)
            {
                cfgLookEye.Value = lookEyeN;
            }
            // v1.8.0 (更改2): 前方偏移默认 0.040 同步到还原默认。
            if (GUILayout.Button(L("pov.reset")))
            {
                cfgPovFov.Value = 70f;
                cfgPovForward.Value = 0.04f;
                cfgPovUp.Value = 0f;
                cfgPovSmooth.Value = 0.65f;
                cfgPovSens.Value = 1f;
                cfgPovNear.Value = 0.01f;
                cfgLookHead.Value = false;
                cfgLookEye.Value = false;
            }
            GUILayout.EndVertical();

            // ---- § 功能 ----
            GUILayout.BeginVertical(GUI.skin.box);
            bool forbid = GUILayout.Toggle(cfgForbidAutoClimax.Value, L("fn.forbidClimax"));
            if (forbid != cfgForbidAutoClimax.Value)
            {
                cfgForbidAutoClimax.Value = forbid;
            }
            bool autoOpen = GUILayout.Toggle(cfgMenuAutoOpen.Value, L("fn.autoOpen"));
            if (autoOpen != cfgMenuAutoOpen.Value)
            {
                cfgMenuAutoOpen.Value = autoOpen;
            }
            bool safe = GUILayout.Toggle(cfgSafeScript.Value, L("fn.safeScript"));
            if (safe != cfgSafeScript.Value)
            {
                cfgSafeScript.Value = safe;
            }
            bool dedup = GUILayout.Toggle(cfgDedup.Value, L("fn.dedup"));
            if (dedup != cfgDedup.Value)
            {
                cfgDedup.Value = dedup;
            }
            // 内容27(v1.5.2): 残留音声の手動急救（自動掃除は撤回済み）
            if (GUILayout.Button(L("fn.voiceSweep")))
            {
                VoiceHygieneSweep();
            }
            // v1.8.5 (追加4d): 名称リスト出力（翻訳表の更新用・手動再出力）
            if (GUILayout.Button(L("fn.nameExport")))
            {
                ExportNameList(true);
            }
            // v1.8.5 (追加4d): 名称翻訳の再読込（外部ファイル編集の即時反映）
            if (GUILayout.Button(L("fn.nameReload")))
            {
                ReloadNameTranslations();
            }
            // P30 (v1.7.2 #37): pose prop auto-mount toggle (event-driven)
            if (!object.ReferenceEquals(cfgPoseResource, null))
            {
                bool prN = GUILayout.Toggle(cfgPoseResource.Value, L("fn.poseRes"));
                if (prN != cfgPoseResource.Value)
                {
                    cfgPoseResource.Value = prN;
                    if (prN)
                    {
                        // 重新开启：对当前姿势立即补挂一次（事件驱动）
                        PoseResMountCurrent(YotogiManager.instans);
                    }
                    else
                    {
                        PoseResClear();
                    }
                }
            }
            GUILayout.EndVertical();

            // ---- § 姿势与场景（v1.8.0: 两区合并 + 重载按钮）----
            GUILayout.BeginVertical(GUI.skin.box);
            // v1.8.0 (追加1): 重新载入当前姿势 —— 完整重播当前技能
            // （归零→FT→门控→重基底→动作）, 选人后扭曲/道具错位时一步修复。
            if (GUILayout.Button(L("ps.reload")))
            {
                ReloadCurrentPose();
            }
            bool sbOpen = GUILayout.Toggle(s_skillBrowserOpen, L("ps.browser"));
            if (sbOpen != s_skillBrowserOpen)
            {
                s_skillBrowserOpen = sbOpen;
                if (s_skillBrowserOpen)
                {
                    BuildSkillBrowserList();
                    s_skillFilteredDirty = true;
                    s_skillBrowserScroll = Vector2.zero;
                }
            }
            bool stOpen = GUILayout.Toggle(s_stageBrowserOpen, L("ps.stage"));
            if (stOpen != s_stageBrowserOpen)
            {
                s_stageBrowserOpen = stOpen;
                if (s_stageBrowserOpen)
                {
                    BuildStageList();
                }
            }
            // v1.7.8 追加2: 官方夜伽位置调整 UI（YotogiPositionChanger）。
            // 官方原生功能（移动/旋转 gizmo, 含道具; 返回时按(舞台×技能)
            // 保存自定义位置, CallFtFiles 尾部自动应用）——入口按钮被
            // PluginData X001 门控隐藏, 这里直接调用打开。
            if (GUILayout.Button(L("ps.posChanger")))
            {
                try
                {
                    YotogiManager ymPc = YotogiManager.instans;
                    YotogiPlayManager pmPc = (ymPc != null) ? ymPc.play_mgr : null;
                    YotogiPositionChanger changer = null;
                    if (pmPc != null && !object.ReferenceEquals(fiPositionChanger, null))
                    {
                        changer = fiPositionChanger.GetValue(pmPc) as YotogiPositionChanger;
                    }
                    if (changer != null && changer.m_YotogiPositionChangerObj != null)
                    {
                        s_posChanger = changer; // v1.8.1: 记录引用（Update 驱动旋转 gizmo 固定大小）
                        changer.OnClickAppearButton();
                    }
                    else
                    {
                        LogOnce("position changer unavailable (Play screen only)");
                    }
                }
                catch (Exception e)
                {
                    LogOnce("PositionChanger: " + e.Message);
                }
            }
            if (s_stageBrowserOpen)
            {
                if (s_stageList.Count == 0)
                {
                    BuildStageList();
                }
                // v1.8.3 (性能): 虚拟化渲染 —— v1.8.2 的全量 GUILayout.Button
                // 循环（夜伽舞台 + 数百背景条目）每帧全量绘制 = 打开列表即
                // 掉帧（与 v1.7.3 前姿势一览相同的问题, 用户实测）。
                // 固定行高 + 只绘制可视区（同 BrowserRowH 模式）。
                s_stageBrowserScroll = GUILayout.BeginScrollView(s_stageBrowserScroll, false, true,
                    GUILayout.Height(StageListH));
                int sepRow = (s_bgList.Count > 0) ? 1 : 0;
                int totalRows = s_stageList.Count + sepRow + s_bgList.Count;
                float totalH = (float)totalRows * BrowserRowH;
                Rect lc = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                    GUILayout.ExpandWidth(true), GUILayout.Height(totalH));
                int lFirst = (int)(s_stageBrowserScroll.y / BrowserRowH);
                if (lFirst < 0)
                {
                    lFirst = 0;
                }
                if (lFirst > totalRows)
                {
                    lFirst = totalRows;
                }
                int lVis = (int)(StageListH / BrowserRowH) + 2;
                int lLast = lFirst + lVis;
                if (lLast > totalRows)
                {
                    lLast = totalRows;
                }
                for (int ri = lFirst; ri < lLast; ri++)
                {
                    Rect rr = new Rect(lc.x, lc.y + (float)ri * BrowserRowH,
                        lc.width, BrowserRowH - 2f);
                    if (ri < s_stageList.Count)
                    {
                        YotogiStage.Data d = s_stageList[ri];
                        if (d == null)
                        {
                            continue;
                        }
                        if (GUI.Button(rr, "  " + NLoc(d.drawName)))
                        {
                            StageSwitch(d);
                        }
                    }
                    else if (sepRow == 1 && ri == s_stageList.Count)
                    {
                        GUI.Label(rr, L("ps.bgSep"));
                    }
                    else
                    {
                        int bi = ri - s_stageList.Count - sepRow;
                        PhotoBGData bg = s_bgList[bi];
                        if (bg == null)
                        {
                            continue;
                        }
                        string bgLabel = string.IsNullOrEmpty(bg.name) ? bg.create_prefab_name : bg.name;
                        if (GUI.Button(rr, "◇ " + NLoc(bgLabel)))
                        {
                            StageSwitchBg(bg);
                        }
                    }
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        // v1.7.3 (问题1/1.1): 姿势一览独立窗口内容 —— 搜索框 + 虚拟化列表
        // （固定行高, 只绘制可视区约20行; 全量 ~700 行 IMGUI = 打开即掉帧
        // 的根源）。点击行 = BrowserSwitchSkill 局内实时切换。
        private static void UpdateSkillFilter()
        {
            s_skillFiltered.Clear();
            string f = (s_skillFilter ?? string.Empty).Trim();
            for (int i = 0; i < s_skillList.Count; i++)
            {
                Skill.Data sd = s_skillList[i];
                if (sd == null || string.IsNullOrEmpty(sd.name))
                {
                    continue;
                }
                if (f.Length == 0 || NameMatch(sd.name, f))
                {
                    s_skillFiltered.Add(sd);
                }
            }
            s_skillFilteredDirty = false;
        }

        private static void DrawSkillBrowserWindow(int id)
        {
            try
            {
                YotogiManager ym = YotogiManager.instans;
                if (ym == null || ym.cur_call_screen_name != "Play")
                {
                    GUILayout.Label(L("win.playOnly"));
                    return;
                }
                if (s_skillList.Count == 0)
                {
                    BuildSkillBrowserList();
                    s_skillFilteredDirty = true;
                }
                // 搜索框（实时过滤; IMGUI 输入框获得焦点时 POV/热键已由
                // keyboardControl 守卫排除）
                GUILayout.Label(L("br.search"));
                string nf = GUILayout.TextField(s_skillFilter ?? "", 64);
                if (!string.Equals(nf, s_skillFilter))
                {
                    s_skillFilter = nf;
                    s_skillFilteredDirty = true;
                    s_skillBrowserScroll = Vector2.zero;
                }
                if (s_skillFilteredDirty)
                {
                    UpdateSkillFilter();
                }
                int curId = -999;
                if (ym.playing_skill != null && ym.playing_skill.skill_pair != null
                    && ym.playing_skill.skill_pair.base_data != null)
                {
                    curId = ym.playing_skill.skill_pair.base_data.id;
                }
                GUILayout.Label(LF("br.count", s_skillFiltered.Count));
                // ---- 虚拟化滚动列表: 预留总高度 + 只绘制可视行 ----
                s_skillBrowserScroll = GUILayout.BeginScrollView(s_skillBrowserScroll, false, true);
                float totalH = (float)s_skillFiltered.Count * BrowserRowH;
                Rect content = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                    GUILayout.ExpandWidth(true), GUILayout.Height(totalH));
                // 可视高度 = 窗口高 - 标题/搜索/统计/关闭按钮的固定开销
                float viewH = s_browserRectLast.height - 170f;
                if (viewH < 100f)
                {
                    viewH = 100f;
                }
                int first = (int)(s_skillBrowserScroll.y / BrowserRowH);
                if (first < 0)
                {
                    first = 0;
                }
                if (first > s_skillFiltered.Count)
                {
                    first = s_skillFiltered.Count;
                }
                int vis = (int)(viewH / BrowserRowH) + 2;
                int last = first + vis;
                if (last > s_skillFiltered.Count)
                {
                    last = s_skillFiltered.Count;
                }
                for (int i = first; i < last; i++)
                {
                    Skill.Data bsd = s_skillFiltered[i];
                    if (bsd == null)
                    {
                        continue;
                    }
                    string label = ((bsd.id == curId) ? "\u25B6 " : "  ") + NLoc(bsd.name);
                    Rect r = new Rect(content.x, content.y + (float)i * BrowserRowH,
                        content.width, BrowserRowH - 2f);
                    if (GUI.Button(r, label))
                    {
                        BrowserSwitchSkill(bsd);
                    }
                }
                GUILayout.EndScrollView();
                if (GUILayout.Button(L("br.close")))
                {
                    s_skillBrowserOpen = false;
                }
                // 不设 DragWindow: 窗口粘着主菜单（一体移动）; 关闭走按钮/主菜单开关
            }
            catch (Exception e)
            {
                LogOnce("SkillBrowser: " + e.Message);
            }
        }

        private static void LogOnce(string message)
        {
            if (s_loggedOnce)
            {
                return;
            }
            s_loggedOnce = true;
            if (!object.ReferenceEquals(log, null))
            {
                log.LogWarning("YotogiUnlimited runtime warning (once): " + message);
            }
        }

        private static bool s_loggedOnce;
    }
}
