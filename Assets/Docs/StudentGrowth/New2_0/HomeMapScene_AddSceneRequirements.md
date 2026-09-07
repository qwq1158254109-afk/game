# HomeMapScene 添加与替换场景需求整理

## 任务范围

- 目标场景：`Assets/Scenes/StudentGrowth/Maps/HomeMapScene.unity`
- 场景类型：学生成长系统中的家庭室内 RPG Tilemap 场景。
- 执行方式：替换当前 `HomeMapScene.unity`，旧场景内容不保留在主场景中。
- 旧背景处理：删除旧的 RPG 地图背景图，不再使用单张旧背景作为室内场景底图。

## 美术资源要求

- 使用用户提供并迁移到项目内的 RPG Maker MV 室内资源。
- 迁移后的新资源文件名必须带有 `new` 标记，例如 `_new` 或位于 `New2_0` 目录下。
- 新资源应放入对应的美术资源目录中，避免混放到无关目录。
- 项目中用于本次场景的旧美术资源不得继续被引用。
- 清理同目录下没有 `new` 标记且属于本次替换范围的旧美术资源。
- 不得删除 Unity `.meta` 文件，除非资源删除是用户明确要求且对应 `.meta` 同步处理。

## 家具与物体表现要求

- 家具 PNG 必须完整展示家具样貌。
- 不允许为了适配场景比例，把家具 PNG 裁成局部图。
- Prefab 中的 `Visual` 子物体应直接使用完整家具 Sprite。
- 场景内比例通过 `Visual.localScale` 或摆放位置控制，而不是通过裁掉原图实现。
- 关键家具至少包括：
  - 床
  - 书桌或桌柜
  - 餐桌
  - 衣柜
  - 出口门
- 资源预览中不能出现黑底、旧背景块或明显不属于家具本体的裁剪残留。

## 场景结构要求

- 使用 Tilemap 重建室内场景，不使用旧单张背景图承载房间。
- 保留并生成清晰的 Tilemap 层级：
  - `Ground_Tilemap`
  - `Wall_Tilemap`
  - `FloorTrim_Tilemap`
  - `WallTop_Tilemap`
  - `Decoration_Tilemap`
  - `FurnitureBase_Tilemap`
  - `Collision_Tilemap`
  - `AbovePlayer_Tilemap`
- Grid Cell Size 使用 `1 x 1`。
- 像素资源导入规则：
  - PPU：`16`
  - Filter Mode：`Point`
  - Compression：`Uncompressed`
  - Mip Maps：关闭

## 场景比例要求

- 房间整体按 `24 x 16` tile 规划。
- 玩家、NPC、家具、门的比例必须统一，不能出现家具过大、过小或被墙体遮挡的情况。
- 完整家具图如果尺寸偏大，应通过 `Visual` 子物体等比缩放控制。
- 场景中主通道至少保持可通行。
- 出生点不能与阻挡碰撞重叠。
- 出口门必须在相机范围内可见。

## 交互要求

- 床、书桌、餐桌、NPC、出口门应保留交互能力。
- 交互对象需要包含：
  - `MapInteractionPoint`
  - `MapInteractionTarget`
  - `Visual`
  - `BlockingCollider`
  - `InteractionTrigger`
- 出口门为返回阶段地图入口，不消耗时间。
- 家具交互保持原有活动含义：
  - 床：休息
  - 书桌：整理计划
  - 餐桌或家人 NPC：家庭晚饭聊天

## 生成与备份要求

- 替换 `HomeMapScene.unity` 前应生成备份场景。
- 新生成的资源统一放入 `New2_0` 相关目录。
- 生成器、报告、Prefab、Tile、TilePalette 应保持可重复生成。
- 不修改 `Packages/` 或 `ProjectSettings/`，除非用户另行明确要求。

## 验证要求

- 需要运行 Unity batchmode 生成场景。
- 需要运行场景自检，确认对象、交互、碰撞组件存在。
- 需要运行比例验证，确认：
  - 场景结构完整
  - Sprite 导入设置正确
  - Transform Scale 合理
  - Object Size 合理
  - Spawn 不被阻挡
  - Camera 能看到出口门
  - 主通道可行走
  - InteractionTrigger 尺寸合理
- Unity 自动验证通过后，还需要在 Unity Editor 的 Scene/Game 视图进行人工视觉检查。

## 最终交付说明

- 汇报修改了哪些文件。
- 说明为什么改这些文件。
- 说明是否影响 Scene、Prefab、序列化字段或资源引用。
- 汇报已运行的验证命令或 Unity batchmode 方法。
- 明确说明未能验证的部分，尤其是 Unity Editor 人工视觉检查。
