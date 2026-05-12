using System;
using System.Collections;
using KingCardsSpire.Configs;
using KingCardsSpire.Core;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using KingCardsSpire.Views.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace KingCardsSpire.Controllers
{
    /// <summary>对话加载、播放与结束回调（由 UI 协程驱动）。</summary>
    public sealed class DialogueController
    {
        private readonly GameManager _game;

        public DialogueController(EventManager events, GameManager game)
        {
            if (events == null)
                Debug.LogWarning("[DialogueController] EventManager 为 null。");
            _game = game;
        }

        /// <summary>从 <paramref name="startId"/> 开始播放一条链，结束后关闭 Dialog 面板并调用 <paramref name="onCompleted"/>。</summary>
        public IEnumerator PlayDialogue(string startId, Action onCompleted = null)
        {
            var ui = UIManager.Instance;
            var cfg = ConfigManager.Instance;
            if (ui == null || cfg == null || string.IsNullOrEmpty(startId))
            {
                onCompleted?.Invoke();
                yield break;
            }

            yield return ui.OpenAsync(UIPanelId.Dialog);
            if (!ui.TryGetView(UIPanelId.Dialog, out DialogView view))
            {
                onCompleted?.Invoke();
                yield break;
            }

            var skipWhole = false;
            var currentId = startId;
            AsyncOperationHandle<Sprite> bgHandle = default;
            AsyncOperationHandle<Sprite> portraitHandle = default;
            var hasBg = false;
            var hasPortrait = false;

            while (!string.IsNullOrEmpty(currentId) && !skipWhole)
            {
                if (!cfg.TryGetDialogueLine(currentId, out var line))
                {
                    Debug.LogWarning($"[DialogueController] 未找到对话行 id={currentId}");
                    break;
                }

                _game?.RegisterDialogueLineSeen(line.Id);

                ReleaseSpriteHandle(ref bgHandle, ref hasBg);
                ReleaseSpriteHandle(ref portraitHandle, ref hasPortrait);

                var bgAddr = DialogueArtResolver.ResolveBackgroundAddress(line.BackgroundId);
                if (!string.IsNullOrEmpty(bgAddr))
                {
                    bgHandle = Addressables.LoadAssetAsync<Sprite>(bgAddr);
                    hasBg = true;
                    yield return bgHandle;
                    if (bgHandle.Status == AsyncOperationStatus.Succeeded)
                        view.SetBackgroundSprite(bgHandle.Result);
                    else
                        view.SetBackgroundSprite(null);
                }
                else
                {
                    view.SetBackgroundSprite(null);
                }

                var portraitAddr = DialogueArtResolver.ResolveCharacterAddress(line.CharacterId);
                if (!string.IsNullOrEmpty(portraitAddr))
                {
                    portraitHandle = Addressables.LoadAssetAsync<Sprite>(portraitAddr);
                    hasPortrait = true;
                    yield return portraitHandle;
                    if (portraitHandle.Status == AsyncOperationStatus.Succeeded)
                        view.SetPortraitSprite(portraitHandle.Result);
                    else
                        view.SetPortraitSprite(null);
                }
                else
                {
                    view.SetPortraitSprite(null);
                }

                view.ApplyTexts(line);
                view.ConfigureMode(line.IsChoice, line.Choices);

                if (line.IsChoice)
                {
                    yield return WaitForChoice(view);
                    if (view.SkipRequested)
                    {
                        skipWhole = true;
                        break;
                    }

                    if (!view.TryConsumeChoice(out var picked) || picked == null)
                    {
                        Debug.LogWarning("[DialogueController] 选择支未收到有效选项。");
                        break;
                    }

                    currentId = picked.NextDialogueId;
                    continue;
                }

                yield return WaitForContinueOrSkip(view);
                if (view.SkipRequested)
                {
                    skipWhole = true;
                    break;
                }

                if (string.IsNullOrEmpty(line.NextId))
                    break;

                currentId = line.NextId;
            }

            ReleaseSpriteHandle(ref bgHandle, ref hasBg);
            ReleaseSpriteHandle(ref portraitHandle, ref hasPortrait);

            ui.Close(UIPanelId.Dialog);
            onCompleted?.Invoke();
        }

        private static IEnumerator WaitForContinueOrSkip(DialogView view)
        {
            view.ResetSignals();
            while (true)
            {
                if (view.TryConsumeContinue(out var skipAll))
                {
                    if (skipAll)
                        yield break;
                    yield break;
                }

                yield return null;
            }
        }

        private static IEnumerator WaitForChoice(DialogView view)
        {
            view.ResetSignals();
            while (true)
            {
                if (view.SkipRequested)
                    yield break;

                if (view.TryConsumeChoice(out _))
                    yield break;

                yield return null;
            }
        }

        private static void ReleaseSpriteHandle(ref AsyncOperationHandle<Sprite> handle, ref bool has)
        {
            if (!has)
                return;

            if (handle.IsValid())
                Addressables.Release(handle);

            handle = default;
            has = false;
        }

        /// <summary>
        /// 战前 BOSS 对话入口 id 解析顺序：
        /// 1) <c>boss_prefight_{bossId}</c>（与塔层 <see cref="TowerFloorEntry.BossId"/> 一致）；
        /// 2) <c>boss_prefight_f{层}</c>；
        /// 3) <c>boss_prefight</c> 通用。
        /// </summary>
        public static string ResolveBossPreFightStartId(ConfigManager cfg, int floorIndex1Based, string bossId)
        {
            if (cfg == null)
                return "boss_prefight";

            if (!string.IsNullOrEmpty(bossId))
            {
                var byBoss = $"boss_prefight_{bossId}";
                if (cfg.TryGetDialogueLine(byBoss, out _))
                    return byBoss;
            }

            var keyed = $"boss_prefight_f{floorIndex1Based}";
            if (cfg.TryGetDialogueLine(keyed, out _))
                return keyed;

            return "boss_prefight";
        }

        /// <summary>
        /// 与参赛者「交谈」入口 id 解析顺序：
        /// 1) <c>hero_talk_{slot}_f{层}</c>（同槽不同层可写不同入口）；
        /// 2) <c>hero_talk_{slot}</c>。
        /// </summary>
        public static string ResolveHeroTalkStartId(ConfigManager cfg, int heroSlotIndex, int floorIndex1Based)
        {
            if (cfg == null)
                return $"hero_talk_{heroSlotIndex}";

            var byFloor = $"hero_talk_{heroSlotIndex}_f{floorIndex1Based}";
            if (cfg.TryGetDialogueLine(byFloor, out _))
                return byFloor;

            return $"hero_talk_{heroSlotIndex}";
        }

        /// <summary>原住民遭遇入口 id；无专表行时回退默认。</summary>
        public static string ResolveNpcVisitStartId(ConfigManager cfg, string npcId)
        {
            if (cfg == null || string.IsNullOrEmpty(npcId))
                return "npc_default_visit";

            var key = $"npc_{npcId}_visit";
            if (cfg.TryGetDialogueLine(key, out _))
                return key;

            return "npc_default_visit";
        }
    }
}
