# 战斗 UI（BattleView）说明

## 预制体与 Addressables（须在 Unity 编辑器内操作）

根据项目约定：**禁止用文本/YAML 直接改 `*.prefab`**，助手不会在仓库里写入预制体引用；你需要在本机 Unity 中完成下列步骤。

1. 打开 [`Assets/GameAssets/UI/BattleView.prefab`](../Assets/GameAssets/UI/BattleView.prefab)。
2. 根节点若误挂了 **`MainHubView`**，请 **Remove Component**，再 **Add Component** → **`BattleView`**。
3. 在 Inspector 中将界面上的 **Text / Button / RectTransform（OppositeHand、MyHand）** 与 **`Card` 预制体** 拖到 `BattleView` 对应 **`[SerializeField]`** 字段（见 [`BattleView.cs`](../Assets/Scripts/Views/UI/BattleView.cs)）。
4. 打开 **Window → Asset Management → Addressables → Groups**，在 **UI** 分组中找到地址 **`UI/Panel_Battle`**，将资源条目设为 **`BattleView.prefab`**（或新建同地址条目并指向该预制体）；勿手写 `UI.asset` YAML，除非你清楚自己在做什么。

弃牌子面板与文案、`xRayHintText`、`closeBattleButton` 等均须在预制体上绑定引用（可为默认隐藏的占位节点）；参见 `.cursor/rules/ui-view-binding.mdc` 中「序列化引用假设」。
