# 可编辑学校地图

`SchoolMapScene` 使用用户提供的空教室背景和家具素材表构建。最终效果图仅保存在 `Reference` 目录用于对照，没有作为运行时合成背景使用。

## 编辑入口

1. 打开 `Assets/Scenes/StudentGrowth/Maps/SchoolMapScene.unity`。
2. 在 Hierarchy 展开 `SchoolMapRpgRoot`。
3. 在 `EditableSchoolFurniture` 中单独移动课桌、椅子、柜子、绿植和清洁工具。
4. 在 `EditableWallDecor` 中调整黑板和公告板。
5. 在 `Collision` 中调整教室边界碰撞；家具碰撞位于各家具自身。

## 重建与验证

- `Tools/Student Growth/School/Rebuild Editable School Map`
- `Tools/Student Growth/School/Validate Editable School Map`

重建会重新生成由家具素材表派生的透明 Sprite，并覆盖学校场景中的可编辑布局。原始背景、素材表和最终效果参考图不会被改写。
