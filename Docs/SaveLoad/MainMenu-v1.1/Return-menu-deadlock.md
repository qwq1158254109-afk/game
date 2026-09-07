# 返回主菜单卡死：定位与修复

## 复现证据

Unity 6000.4.6f1，地图返回 MainMenuScene。Temp/MainMenuFlow.log 显示在同一帧 3 中依次结束会话、对玩家调用 Destroy、启动 LoadSceneAsync；之后没有 scene load completed。

卡死期间读取了托管和原生线程栈（只输出方法和位置，没有导出进程内存）：

- 主线程 18364：DelayedDestroyCallback → DestroyObjectHighLevel → SpriteRenderer::MainThreadCleanup → AssetChangedNotifier::UnsubscribeFromAll → GetGUIDFromEntityId → PersistentManager::Lock → WaitOnAddress。
- 预加载线程 35324：PreloadManager::Run → LoadSceneOperation::Perform → PersistentManager::LoadFileCompletelyThreaded → ReadAndActivateObjectThreaded → SetObjectLockForRead → Semaphore::WaitForSignal。

对应证据：hang-native-stack.txt；辅助证据 hang-managed-stack.txt。这是延迟销毁与异步场景加载的原生锁竞争。DX11 下也复现，不能归因为 DX12 专属问题；设置测试互斥修复也没有消除该阻塞。

## 修改

- PlayerManager：记录待销毁的玩家对象，直到 Unity 完成真正销毁后才报告清理完成；重复清理幂等。
- UnityGameFlowBackend：在启动 LoadSceneAsync 前等待玩家销毁完成，使用实时钟的清理超时。这个屏障只用于旧对象销毁，不替代目标场景就绪检查。不使用 DestroyImmediate，也不要求更换图形 API。
- MainMenuController：跨场景协程改由常驻应用宿主执行，避免场景卸载时销毁菜单控制器并中断流程。
- GameFlowDiagnostics：仅 Editor 的轻量诊断，记录关键阶段和帧号到 Temp/MainMenuFlow.log。

## 回归验证

以下结果由修复后运行补充，旧 play.txt 失败记录不得作为修复后的通过证据。

修复后已验证（2026-09-05，Unity 6000.4.6f1，Direct3D12）：

- 完成 5 轮主菜单 → 新局家地图 → 手动保存 → 属性直接修改 → 读档恢复 → 返回主菜单，play.txt 为 PASS ALL。
- 每轮恰好一个玩家；返回后零玩家；应用宿主保持唯一。
- 输入解除、Time.timeScale=1；存档基线和读档恢复通过。
- Unity MCP Console 最终 Error / Exception 为 0。
- return-menu-fixed-flow.log 可见第 85 帧排队销毁，第 86 帧确认销毁完成后才请求异步加载，第 88 帧主菜单 Start，第 89 帧完成就绪。

使用方式不变：暂停菜单 → 返回主菜单 → 按未保存提示选择保存、不保存或取消。无需更改图形 API。编辑器已退出测试 Play，回到原社团场景。
