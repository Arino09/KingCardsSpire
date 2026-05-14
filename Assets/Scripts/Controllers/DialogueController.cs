using System;
using System.Collections;
using System.Collections.Generic;
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
            var hasShownFirstLine = false;

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

                ResolveCharacterPresentation(cfg, line, out var speakerName, out var portraitId);
                var bgAddr = DialogueArtResolver.ResolveBackgroundAddress(line.BackgroundId);
                var portraitAddr = DialogueArtResolver.ResolveCharacterAddress(portraitId);
                var needBg = !string.IsNullOrEmpty(bgAddr);
                var needPortrait = !string.IsNullOrEmpty(portraitAddr);
                if (needBg)
                {
                    bgHandle = Addressables.LoadAssetAsync<Sprite>(bgAddr);
                    hasBg = true;
                }

                if (needPortrait)
                {
                    portraitHandle = Addressables.LoadAssetAsync<Sprite>(portraitAddr);
                    hasPortrait = true;
                }

                if (needBg)
                    yield return bgHandle;
                if (needPortrait)
                    yield return portraitHandle;

                if (needBg)
                {
                    if (bgHandle.Status == AsyncOperationStatus.Succeeded)
                        view.SetBackgroundSprite(bgHandle.Result);
                    else
                        view.SetBackgroundSprite(null);
                }
                else
                {
                    view.SetBackgroundSprite(null);
                }

                if (needPortrait)
                {
                    if (portraitHandle.Status == AsyncOperationStatus.Succeeded)
                        view.SetPortraitSprite(portraitHandle.Result);
                    else
                        view.SetPortraitSprite(null);
                }
                else
                {
                    view.SetPortraitSprite(null);
                }

                view.ApplyTexts(line, speakerName);
                view.ConfigureMode(line.IsChoice, line.Choices);

                if (!hasShownFirstLine)
                {
                    view.SetPresentationReady(true);
                    hasShownFirstLine = true;
                }

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
                    if (!TryResolveTerminalLineId(cfg, line.NextId, out var terminalId))
                    {
                        skipWhole = true;
                        break;
                    }

                    currentId = terminalId;
                    continue;
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

                if (view.HasPendingChoice)
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

        public static string BuildHeroStoryStartId(int heroSlotIndex, int storyIndex)
        {
            return StoryDialogueRules.BuildHeroStoryStartId(heroSlotIndex, storyIndex);
        }

        /// <inheritdoc cref="StoryDialogueRules.BuildNpcStoryStartId(string, int)"/>
        public static string BuildNpcStoryStartId(string npcId, int storyIndex)
        {
            return StoryDialogueRules.BuildNpcStoryStartId(npcId, storyIndex);
        }

        public static bool TryParseHeroStoryId(string id, out int heroSlotIndex, out int storyIndex)
        {
            return StoryDialogueRules.TryParseHeroStoryId(id, out heroSlotIndex, out storyIndex);
        }

        public static bool TryParseNpcStoryId(string id, out string npcId, out int storyIndex)
        {
            return StoryDialogueRules.TryParseNpcStoryId(id, out npcId, out storyIndex);
        }

        public static bool TryParseNpcStoryId(string id, out string npcId, out int storyIndex, out int lineIndex)
        {
            return StoryDialogueRules.TryParseNpcStoryId(id, out npcId, out storyIndex, out lineIndex);
        }

        private static void ResolveCharacterPresentation(
            ConfigManager cfg,
            DialogueLineEntry line,
            out string speakerName,
            out string portraitId)
        {
            speakerName = line != null ? line.SpeakerName : string.Empty;
            portraitId = line != null ? line.CharacterId : string.Empty;
            var characterId = line != null ? line.CharacterId : string.Empty;
            if (string.IsNullOrWhiteSpace(characterId))
                return;

            if (characterId == StoryDialogueRules.NarratorCharacterId)
            {
                speakerName = string.Empty;
                portraitId = string.Empty;
                return;
            }

            if (cfg != null && cfg.TryGetHero(characterId, out var hero))
            {
                speakerName = hero.DisplayName;
                portraitId = hero.PortraitId;
                return;
            }

            if (cfg != null && cfg.TryGetNpc(characterId, out var npc))
            {
                speakerName = npc.DisplayName;
                portraitId = npc.PortraitId;
            }
        }

        private static bool TryResolveTerminalLineId(ConfigManager cfg, string nextId, out string terminalId)
        {
            terminalId = null;
            if (cfg == null || string.IsNullOrEmpty(nextId))
                return false;

            var visited = new HashSet<string>();
            var currentId = nextId;
            while (!string.IsNullOrEmpty(currentId) && visited.Add(currentId))
            {
                if (!cfg.TryGetDialogueLine(currentId, out var line))
                    return false;

                if (line.IsChoice || string.IsNullOrEmpty(line.NextId))
                {
                    terminalId = currentId;
                    return true;
                }

                currentId = line.NextId;
            }

            return false;
        }
    }
}
