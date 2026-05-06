# GameAssets（Addressables 资源根）

将可寻址资源放入对应子目录，并在 Addressables Groups 窗口中为资源设置 **Address** 与 **Labels**。

## 标签约定（与 `AddressableLabels` 常量一致）

| 标签 | 用途 |
|------|------|
| `config_card` | 卡牌 ScriptableObject |
| `config_buff` | Buff 配置 |
| `config_weather` | 天气配置 |
| `config_shop` | 商店配置 |
| `config_game` | 全局游戏参数 |
| `ui_panel` | UI 预制体 |
| `audio_bgm` | 背景音乐 |
| `audio_sfx` | 音效 |
| `dialogue` | 对话 JSON（TextAsset） |

分组 **Configs / UI / Audio / Dialogues** 由编辑器脚本 `KingCardsAddressablesBootstrap` 在首次导入工程时自动创建。
