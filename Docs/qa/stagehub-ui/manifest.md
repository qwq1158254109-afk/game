# Stage Hub UI 固定分辨率验收基线

- Unity 版本：`6000.4.6f1`
- 场景：`Assets/Scenes/StudentGrowth/Maps/StageHubMapScene.unity`
- 截图时间：`2026-08-19T01:13:44+08:00`
- 工程版本（Prefab 依赖哈希）：`64893cd68e8b5b54258ae7bcd0cc87e0`
- 截图校验：通过（实际 PNG 宽高与目标值完全一致）
- EditMode：`PASS 14/14`（`StudentGrowth.Tests.StageHubSelectionUiTests`）
- PlayMode：`PASS 1/1`（`StudentGrowth.Tests.StageHubLocationInfoViewPlayModeTests`）
- 人工视觉复核：`PASS`（四张截图无技术场景名、状态文本裁切、面板重叠或越界）

| 文件 | Game View 预设 | 目标尺寸 | 实际尺寸 | 结果 |
| --- | --- | --- | --- | --- |
| `stagehub_1920x1080.png` | `1920x1080` | `1920×1080` | `1920×1080` | PASS |
| `stagehub_2560x1440.png` | `2560x1440` | `2560×1440` | `2560×1440` | PASS |
| `stagehub_1366x768.png` | `1366x768` | `1366×768` | `1366×768` | PASS |
| `stagehub_1920x1200.png` | `1920x1200` | `1920×1200` | `1920×1200` | PASS |
