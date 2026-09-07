# Background Detailing Checklist

依据 `codex_background_detailing_guide.md` 与项目现有非卡牌、非人物像素美术规范整理。

## 执行范围

- 仅处理 `Assets/Art/Pixel/Backgrounds/` 下现有背景 PNG。
- 保留原资源路径、原图片尺寸、原 `.meta` 文件与 GUID。
- 未修改 Scene、Prefab、Tilemap、Camera、Sorting Layer、碰撞、UI、ProjectSettings 或 Packages。
- 背景保持低饱和复古像素风、CRT 监控窗口感、校园学习/考试压力氛围。

## 背景清单

| 背景名称 | 资源路径 | 使用场景 | 当前问题 | 需要增加的细节 | 处理方式 | 是否已完成 | 检查结果 |
|---|---|---|---|---|---|---|---|
| 旧电脑主菜单背景 | `Assets/Art/Pixel/Backgrounds/bg_mainmenu_old_monitor.png` | 主菜单 | 原图材质和桌面层次较简化 | 旧塑料显示器磨损、桌面木纹、键盘高光、纸张边缘、CRT 噪点和暗角 | 同尺寸 960x540 替换 PNG | 是 | 可读取；无人物、无可读文字、无 Logo |
| 阶段监控中控台 | `Assets/Art/Pixel/Backgrounds/bg_stage_monitor_dashboard.png` | 阶段主界面 | 监控窗口和面板较平 | 面板内阴影、窗口边框磨损、扫描线、警报槽暗纹、屏幕噪点 | 同尺寸 960x540 替换 PNG | 是 | 可读取；保留多窗口结构；无可读文字 |
| 学生卧室 | `Assets/Art/Pixel/Backgrounds/bg_room_student_bedroom.png` | 休息、夜晚、压力事件 | 墙面、床、柜子和地板细节较少 | 墙面旧化纹理、床品暗部、柜体木纹、地板缝隙、接触阴影、夜间窗光 | 同尺寸 640x360 替换 PNG | 是 | 可读取；无人物、无可读文字 |
| 书桌学习区 | `Assets/Art/Pixel/Backgrounds/bg_room_study_desk.png` | 刷题、复习、单词记忆 | 桌面和书本材质较简单 | 桌面木纹、书本纸张层次、台灯阴影、墙面噪点、暗角和环境光 | 同尺寸 640x360 替换 PNG | 是 | 可读取；书页为抽象线条，无可读文字 |
| 教室 | `Assets/Art/Pixel/Backgrounds/bg_school_classroom.png` | 课堂、老师事件、小测 | 黑板、课桌和地板层次不足 | 黑板擦痕、课桌木纹、桌椅投影、地板透视线、墙面暗纹 | 同尺寸 640x360 替换 PNG | 是 | 可读取；无人物、无可读文字 |
| 家庭餐桌 | `Assets/Art/Pixel/Backgrounds/bg_home_dining_room.png` | 父母沟通、家庭期待 | 家庭空间材质和光影较平 | 餐桌木纹、杯子高光、纸张边缘、柜体阴影、墙面轻微旧化 | 同尺寸 640x360 替换 PNG | 是 | 可读取；无人物、无可读文字 |
| 校园小卖部 | `Assets/Art/Pixel/Backgrounds/bg_school_shop.png` | 商店、道具购买 | 货架和柜台缺少真实材质 | 货架木纹、商品色块差异、柜台划痕、接触阴影、墙面噪点 | 同尺寸 640x360 替换 PNG | 是 | 可读取；无品牌、无可读文字 |
| 考场 | `Assets/Art/Pixel/Backgrounds/bg_exam_room.png` | 月考、期末 Boss | 原占位图细节和压迫氛围不足 | 黑板擦痕、考桌木纹、试卷剪影、挂钟阴影、红色警示边框、地板缝隙 | 同尺寸 640x360 替换 PNG | 是 | 可读取；无人物、无可读文字 |

## 统一检查

- 背景均保持 16:9。
- `bg_mainmenu_old_monitor.png` 与 `bg_stage_monitor_dashboard.png` 保持 960x540。
- 其余背景保持 640x360。
- 未新增 overlay 层，因为当前背景均为单张 Sprite，直接同尺寸替换风险最低。
- 未修改 Sprite import settings；导入设置仍由现有 `.meta` 控制。
- 未在 Unity Editor 中进行场景实机检查。

## 建议 Unity 侧验证

- 打开 Project 视图检查 8 张背景 Sprite 预览。
- 确认导入设置仍保持项目规范：Sprite、Point Filter、Compression 规则不变。
- 在对应 UI/场景中检查背景是否被拉伸、是否出现黑边、是否遮挡 UI 或角色。
