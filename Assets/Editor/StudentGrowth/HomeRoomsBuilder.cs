using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace StudentGrowth.EditorTools
{
    /// <summary>
    /// 把家拆成房间场景（样张：客厅 + 卧室）。
    /// 做法：以 HomeMapScene 为模板打开 → 换房间底图 → 裁剪家具/交互物 → 重排坐标
    /// → 重建碰撞墙 → 加出生点与房间门 → 另存为新场景。可重复执行（每次从模板重来）。
    /// 只写 Console，不弹模态框。
    /// </summary>
    public static class HomeRoomsBuilder
    {
        private const string TemplateScene = "Assets/Scenes/StudentGrowth/Maps/HomeMapScene.unity";
        private const string ArtDir = "Assets/Art/Pixel/Generated/HomeRooms";
        private const string PropDir = "Assets/Art/Pixel/Generated/HomeReference/Props";
        private const string SceneDir = "Assets/Scenes/StudentGrowth/Maps";
        private const string RootName = "HomeMapSceneRoot";
        /// <summary>墙带厚度（世界单位）：画布 28.5 - 2×内半宽 12.75 = 1.5，正好等于门素材的 96px。</summary>
        private const float WallThick = 2.5f;
        /// <summary>门素材：make_rooms.py 按 DOOR_CROP 导出的 128×96 帧（@64PPU = 2.0×1.5 世界单位，
        /// 与门洞严丝合缝）。必须用裁剪版，直接用 home_ref_exit_door.png 会大一圈（150×125）。</summary>
        private const string DoorClosedSprite = PropDir + "/home_ref_door_inwall.png";
        private static readonly string[] DoorOpenSprites =
        {
            PropDir + "/home_ref_door_inwall_open_01.png",
            PropDir + "/home_ref_door_inwall_open_02.png",
            PropDir + "/home_ref_door_inwall_open_03.png",
        };

        private sealed class PropSpec
        {
            public string name;
            public Vector2 position;
            public float scale;

            public PropSpec(string n, float x, float y, float s = 1f)
            {
                name = n; position = new Vector2(x, y); scale = s;
            }
        }

        private sealed class DecorSpec
        {
            public string spriteName;
            public Vector2 pos;
            public bool collide;
            public float scale;

            public DecorSpec(string n, float x, float y, bool c = false, float s = 1f)
            {
                spriteName = n; pos = new Vector2(x, y); collide = c; scale = s;
            }
        }

        private sealed class SpawnSpec
        {
            public string id;
            public Vector2 position;
            public bool isDefault;

            public SpawnSpec(string i, float x, float y, bool dflt = false) { id = i; position = new Vector2(x, y); isDefault = dflt; }
        }

        private sealed class DoorSpec
        {
            public string name;
            public Vector2 position;
            public Vector2 size;
            public string targetScene;
            public string targetSpawn;
            /// <summary>提示栏里显示的目地地中文名（如「卧室」）。留空则从场景名推导。</summary>
            public string displayName;

            public DoorSpec(string n, float x, float y, float w, float h, string scene, string spawn, string display = null)
            {
                name = n; position = new Vector2(x, y); size = new Vector2(w, h);
                targetScene = scene; targetSpawn = spawn; displayName = display;
            }
        }

        private sealed class WallSpec
        {
            public Vector2 center;
            public Vector2 size;

            public WallSpec(float cx, float cy, float w, float h) { center = new Vector2(cx, cy); size = new Vector2(w, h); }
        }

        // ── 门的可视对象 + 开门动画 ───────────────────────────────────────────
        // 底图上已经烘了一扇静态门（兜底：万一场景里的门对象没生成，门口也不是空的），
        // 这里再叠一个同尺寸的门对象：MapDoorAnimator 的四帧全是不透明像素，能整块盖住底图那扇。

        /// <summary>门面在墙体里的世界中心：横门（宽>高）在南北墙，竖门在东西墙。</summary>
        private static Vector3 DoorWallCenter(Vector2 triggerPos, Vector2 triggerSize)
        {
            if (triggerSize.x < triggerSize.y)   // .7 x 1.4 → 竖门（东西墙）
            {
                float x = triggerPos.x > 0f ? InnerHalfW + WallThick * .5f : -(InnerHalfW + WallThick * .5f);
                return new Vector3(x, triggerPos.y, 0f);
            }

            float y = triggerPos.y > 0f ? InnerTop + WallThick * .5f : InnerBottom - WallThick * .5f;
            return new Vector3(triggerPos.x, y, 0f);
        }

        /// <summary>
        /// 房间门的**触发区**中心（可走区内侧）。
        ///
        /// 2026-10-21 修（用户报"走到门上没反应"）：
        ///   `DoorSpec.position` 原来直接给**墙中心**（x=±14.305 / y=±8.0），
        ///   而玩家移动边界是 `InnerHalfW-0.9 = 12.155` / `InnerTop-0.9 = 5.85` ——
        ///   触发区整个埋在墙里，玩家**永远走不进去**，门自然没反应。
        ///
        /// 现在把「触发区」与「门面」分离：
        ///   触发区 = 可走区内侧（玩家踩得到）；门面 = 由 DoorWallCenter 贴进墙洞（视觉正确）。
        /// </summary>
        private static Vector3 DoorTriggerCenter(Vector2 triggerPos, Vector2 triggerSize)
        {
            const float PlayerMargin = .9f;   // 与 rpg.movementMin/Max 的内缩一致

            if (triggerSize.x < triggerSize.y)   // 竖门（东西墙），沿 x 收半个触发区宽
            {
                float limit = InnerHalfW - PlayerMargin;
                float x = limit - triggerSize.x * .5f;
                return new Vector3(triggerPos.x > 0f ? x : -x, triggerPos.y, 0f);
            }

            float limitY = InnerTop - PlayerMargin;
            float y = limitY - triggerSize.y * .5f;
            return new Vector3(triggerPos.x, triggerPos.y > 0f ? y : -y, 0f);
        }

        /// <summary>
        /// 出生点必须落在**所有门触发区之外**，否则「进门之后马上能再走出去」这条最自然的操作会失效。
        ///
        /// 2026-10-21 用户报：「进入下一个场景之后还得先离开交互范围才能回去，我想要直接能回去」。
        /// 根因就是这条约束被破了 —— 四个房间的落地出生点**全都在各自那扇门的触发区里面**：
        ///   客厅 spawn_from_bedroom x=11.95，东门触发区 x∈[11.255, 12.155]
        ///   卧室 spawn_from_living  x=-11.95，西门触发区 x∈[-12.155, -11.255]
        ///   书房 spawn_from_living  x=11.95， 东门触发区 x∈[11.255, 12.155]
        ///   厨房 spawn_from_living  y=-4.9，  南门触发区 y∈[-5.85, -4.95]
        /// 玩家一落地就**已经在**触发区里，`OnTriggerEnter2D` 不会触发（没有"进入"这个事件）；
        /// 而 `RoomDoor.armDelay` 又把落地那一瞬间挡掉了 → 门永远没反应，必须先走出去再走回来。
        ///
        /// 这条校验在每次重建场景时跑，命中就报错（**不静默通过**），
        /// 免得以后调家具/调门洞时又悄悄把出生点挪回门里。
        /// </summary>
        private static void ValidateSpawnClearance(RoomSpec spec, GameObject doorRoot)
        {
            // 与 PlayerRpg 场景里的 CapsuleCollider2D 一致：size (0.68, 1.05)，offset (0, -0.32)。
            const float PlayerHalfW = .34f;
            const float PlayerBottom = -.845f;   // -0.32 - 1.05/2
            const float PlayerTop = .205f;       // -0.32 + 1.05/2

            foreach (SpawnSpec spawn in spec.spawns)
            {
                float px0 = spawn.position.x - PlayerHalfW;
                float px1 = spawn.position.x + PlayerHalfW;
                float py0 = spawn.position.y + PlayerBottom;
                float py1 = spawn.position.y + PlayerTop;

                foreach (DoorSpec door in spec.doors)
                {
                    Vector2 c = DoorTriggerCenter(door.position, door.size);
                    float dx0 = c.x - door.size.x * .5f;
                    float dx1 = c.x + door.size.x * .5f;
                    float dy0 = c.y - door.size.y * .5f;
                    float dy1 = c.y + door.size.y * .5f;

                    float overlapX = Mathf.Min(px1, dx1) - Mathf.Max(px0, dx0);
                    float overlapY = Mathf.Min(py1, dy1) - Mathf.Max(py0, dy0);
                    if (overlapX > 0f && overlapY > 0f)
                    {
                        Debug.LogError("[Rooms] " + spec.label + " 的出生点 " + spawn.id
                            + " (" + spawn.position.x + ", " + spawn.position.y + ") 落在门 "
                            + door.name + " 的触发区内（重叠 " + overlapX.ToString("F2") + " x "
                            + overlapY.ToString("F2") + "）。落地即在门里 → 玩家必须先走出去才能用这扇门。"
                            + " 把出生点往房间内侧挪，至少离开触发区 " + Mathf.Max(overlapX, overlapY).ToString("F2") + " 单位。");
                    }
                }
            }
        }

        /// <summary>门素材的旋转：西墙 +90、东墙 -90（窗朝墙外、门底朝房间），南北墙不转。</summary>
        private static float DoorZRotation(Vector2 triggerPos, Vector2 triggerSize)
        {
            if (triggerSize.x >= triggerSize.y)
            {
                return 0f;
            }

            return triggerPos.x > 0f ? -90f : 90f;
        }

        /// <summary>挂一扇门面（只建房面和排序，动画组件由调用方决定挂哪）。</summary>
        private static SpriteRenderer AddDoorVisual(Transform parent, Vector3 localPosition, float zRotation)
        {
            Sprite closed = AssetDatabase.LoadAssetAtPath<Sprite>(DoorClosedSprite);
            if (closed == null)
            {
                return null;
            }

            GameObject visual = new GameObject("DoorVisual");
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
            // 门面必须严格 2.0×1.5 世界单位（和门洞一致）。ExitDoorObject 自己带 0.68 缩放，
            // 不补偿的话门会缩成 1.36×1.02，比门洞小一圈。
            float parentScale = Mathf.Max(0.0001f, parent.lossyScale.x);
            visual.transform.localScale = Vector3.one / parentScale;

            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = closed;

            MapObjectYSorter sorter = visual.AddComponent<MapObjectYSorter>();
            sorter.renderers = new[] { renderer };
            sorter.baseOrder = 1000;
            sorter.orderScale = 100f;
            return renderer;
        }

        /// <summary>把开门帧配到动画组件上（MapDoorAnimator 的约定：openFrames[0] 就是关闭帧）。</summary>
        private static void ConfigureDoorAnimator(MapDoorAnimator animator, SpriteRenderer renderer)
        {
            if (animator == null || renderer == null)
            {
                return;
            }

            animator.targetRenderer = renderer;
            animator.framesPerSecond = 12f;
            animator.resetClosedOnEnable = true;

            List<Sprite> open = new List<Sprite> { renderer.sprite };
            List<Sprite> close = new List<Sprite>();
            for (int i = 0; i < DoorOpenSprites.Length; i++)
            {
                Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>(DoorOpenSprites[i]);
                if (frame != null)
                {
                    open.Add(frame);
                }
            }

            for (int i = open.Count - 2; i >= 0; i--)   // 关门 = 开门倒放
            {
                close.Add(open[i]);
            }

            animator.openFrames = open.ToArray();
            animator.closeFrames = close.ToArray();
        }

        /// <summary>房间门用：门面 + 自带一个动画组件（RoomDoor 靠 GetComponentInChildren 找它）。</summary>
        private static void AddAnimatedDoorVisual(Transform parent, Vector3 localPosition, float zRotation)
        {
            SpriteRenderer renderer = AddDoorVisual(parent, localPosition, zRotation);
            if (renderer == null)
            {
                return;
            }

            ConfigureDoorAnimator(renderer.gameObject.AddComponent<MapDoorAnimator>(), renderer);
        }

        private sealed class RoomSpec
        {
            public string label;
            public string scenePath;
            public string artPath;
            public Vector2 size;
            public Vector2 walkSize;
            public string[] keepProps;
            public string[] keepInteractables;
            public PropSpec[] props;
            public PropSpec[] interactables;
            public DecorSpec[] decor;
            public float cameraOrtho;
            public SpawnSpec[] spawns;
            public WallSpec[] walls;
            public DoorSpec[] doors;
            public string defaultSpawnId;
        }

        private const string LivingScene = SceneDir + "/HomeLivingRoomScene.unity";
        private const string BedroomScene = SceneDir + "/HomeBedroomScene.unity";
        private const string StudyScene = SceneDir + "/HomeStudyRoomScene.unity";
        private const string KitchenScene = SceneDir + "/HomeKitchenScene.unity";

        // 2026-10-20 第三轮改版（墙加厚到 2.0，四间统一）
        //   画幅 29.5x17.5（1888x1120 px @64PPU），相机 ortho 8.75（视野 31.09 x 17.5）。
        //   四面墙统一 2.0 单位（128px），门同步 128x128（1.5 x 2.0 世界单位）。
        //   室内可走 = 25.5 x 13.5（x ±12.75，y -6.75 .. 6.75）。
        //   加厚 +1.0 全部给了墙：2.0*2 + 25.5 = 29.5，2.0*2 + 13.5 = 17.5 —— 可走区一个数没动。
        //   加厚必须同时把 ortho 从 8 抬到 8.75，否则 17.5 高的画布会被裁掉 0.75/边、等于白加厚。
        //   底图由 Tools/make_rooms.py 烘焙，墙体参数必须与它一致，改一边就要改另一边。
        //
        // 布局分区（所有房间统一遵循：3 个功能组 + 角落绿植，家具不落中场）：
        //   客厅 = 玄关组（南墙）+ 起居组（北墙）+ 东西墙窄柜 + 南角绿植
        //   卧室 = 睡眠组（北墙：床+两床头柜）+ 衣帽（北墙右侧衣柜）+ 西墙边桌灯
        //   书房 = 工作区（北墙：书桌+电脑+椅）+ 书墙（北墙右侧两排书柜）+ 西南阅读角
        //   厨房 = 北墙一排操作台 + 中岛 + 东南备餐桌
        // 所有装饰不参与碰撞，保证主通道 ≥ 2.5 单位。
        private const float RoomW = 31.11f;
        private const float RoomH = 18.5f;
        private const float Ortho = 9.25f;        // = RoomH/2：视野高 17.5，正好装下加厚后的画布
        private const float LivingW = 29.5f;
        private const float LivingH = 17.5f;
        private const float WalkW = 26.11f;        // 可走区不变（宽高各 +1.0 全给了墙）
        private const float WalkH = 13.5f;
        private const float LivingOrtho = 9.25f;
        private const float InnerHalfW = 13.055f;  // 室内 x 边界（±）
        private const float InnerTop = 6.75f;     // 室内 y 上边界（北墙 2.0）
        private const float InnerBottom = -6.75f; // 室内 y 下边界（南墙 2.0）

        private static RoomSpec Living()
        {
            return new RoomSpec
            {
                label = "客厅",
                scenePath = LivingScene,
                artPath = ArtDir + "/room_living.png",
                size = new Vector2(LivingW, LivingH),
                walkSize = new Vector2(WalkW, WalkH),
                keepProps = new[]
                {
                    "LivingRoomRug", "EntryMat", "LivingSofa", "SofaSideTable", "CoffeeTableObjectVisual",
                    "LeftPlant", "LowLivingCabinet", "EntryWallShelf", "EntrySmallCabinet",
                    "CoffeeGreenCushion", "CoffeeYellowCushion", "ShoeCabinet"
                },
                keepInteractables = new[] { "FamilyNpcObject", "ExitDoorObject" },
                cameraOrtho = LivingOrtho,
                interactables = new[]
                {
                    new PropSpec("FamilyNpcObject", -4.4f, 2.9f),
                    new PropSpec("ExitDoorObject", 0f, -5.55f, .68f),
                },
                props = new[]
                {
                    // ── 玄关组（南墙，门 x=0；从门往左：鞋柜→鞋架→门垫，往右：小柜→衣帽架）──
                    new PropSpec("EntryMat", 0f, -5.65f),             // 门垫（中心锚点）
                    new PropSpec("ShoeCabinet", -3.2f, -6.25f),       // 鞋柜（底边锚点）
                    new PropSpec("EntrySmallCabinet", 3.0f, -6.25f),
                    new PropSpec("EntryWallShelf", -8.0f, -6.25f),    // 壁架（宽件，只能上南墙）
                    // ── 起居组（北墙：沙发居中，茶几压在地毯正中，边几贴沙发扶手）──
                    new PropSpec("LivingSofa", 0f, 4.35f),
                    new PropSpec("SofaSideTable", 2.35f, 4.4f),
                    new PropSpec("LivingRoomRug", 0f, 2.85f),         // 地毯（中心锚点）
                    new PropSpec("CoffeeTableObjectVisual", 0f, 2.55f),
                    new PropSpec("CoffeeGreenCushion", -1.7f, 2.5f),
                    new PropSpec("CoffeeYellowCushion", 1.7f, 2.35f),
                    // ── 北墙左侧矮柜（避开 x=-6 的厨房门洞）──────────────────
                    new PropSpec("LowLivingCabinet", -8.6f, 4.65f),
                    new PropSpec("LeftPlant", -12.2f, 4.6f),
                },
                decor = new[]
                {
                    // 玄关组补齐（底边中心锚点）
                    new DecorSpec("shoe_rack", -1.7f, -6.25f, true, 1f),
                    new DecorSpec("coat_rack", 4.7f, -6.25f, true, 1f),
                    // 起居组：灯紧贴边几右侧 + 单人沙发对着茶几
                    new DecorSpec("floor_lamp", 3.05f, 4.45f, true, 1f),
                    // 北墙右侧边柜（与左侧矮柜对称，避开厨房门洞）
                    new DecorSpec("table_shelf", 8.4f, 5.2f, true, 1f),
                    // 东西墙只放窄柜（宽 1.25），y 对齐起居组中线
                    new DecorSpec("small_cabinet", -12.405f, 3.2f, true, 1f),
                    new DecorSpec("small_cabinet", 12.405f, 3.2f, true, 1f),
                    // 墙面装饰：挂画在矮柜上方、挂钟在右侧边柜上方
                    // 茶几上的茶具
                    // 南角两盆
                    new DecorSpec("small_plant", -12.505f, -6.0f, false, 1f),
                    new DecorSpec("tall_plant", 12.505f, -6.0f, true, 1f),
                },
                spawns = new[]
                {
                    new SpawnSpec("spawn_entry", 0f, -4.9f, true),
                    // 房间门的落地点必须在触发区**外**（理由见 ValidateSpawnClearance）。
                    // 东西门触发区 x 边沿 11.255，玩家半宽 0.34 → 中心 ≤ 10.915 才不重叠。
                    new SpawnSpec("spawn_from_bedroom", 10.7f, 0f),
                    new SpawnSpec("spawn_from_study", -10.7f, 0f),
                    // 北门触发区 y 下沿 4.95，玩家胶囊顶 +0.205 → 中心 ≤ 4.745。
                    new SpawnSpec("spawn_from_kitchen", -6f, 4.5f),
                },
                walls = new[]
                {
                    // 南墙：外沿 -8.75，内沿 -6.75（厚 2.0），门洞 x=0（宽 2.0）
                    new WallSpec(-7.7775f, -8f, 13.055f, 2.5f),
                    new WallSpec(7.7775f, -8f, 13.055f, 2.5f),
                    // 北墙：内沿 6.75，外沿 8.75（厚 2.0），门洞 x=-6（宽 2.0）
                    new WallSpec(-10.7775f, 8f, 7.055f, 2.5f),
                    new WallSpec(4.7775f, 8f, 19.055f, 2.5f),
                    // 西墙 / 东墙：x ±(12.75..14.75)（厚 2.0），门洞 y=0（高 2.0）
                    new WallSpec(-14.305f, -4f, 2.5f, 5.5f),
                    new WallSpec(-14.305f, 4f, 2.5f, 5.5f),
                    new WallSpec(14.305f, -4f, 2.5f, 5.5f),
                    new WallSpec(14.305f, 4f, 2.5f, 5.5f),
                },
                doors = new[]
                {
                    new DoorSpec("DoorToBedroom", 14.305f, 0f, .9f, 1.9f, "HomeBedroomScene", "spawn_from_living", "卧室"),
                    new DoorSpec("DoorToStudy", -14.305f, 0f, .9f, 1.9f, "HomeStudyRoomScene", "spawn_from_living", "书房"),
                    new DoorSpec("DoorToKitchen", -6f, 8.0f, 1.9f, .9f, "HomeKitchenScene", "spawn_from_living", "厨房"),
                },
                defaultSpawnId = "spawn_entry",
            };
        }

        private static RoomSpec Bedroom()
        {
            return new RoomSpec
            {
                label = "卧室",
                scenePath = BedroomScene,
                artPath = ArtDir + "/room_bedroom.png",
                size = new Vector2(RoomW, RoomH),
                keepProps = new[] { "BedroomRunnerRug", "BedroomNightstand", "WardrobeRug" },
                keepInteractables = new[] { "BedObject", "WardrobeObject" },
                cameraOrtho = Ortho,
                interactables = new[]
                {
                    // 睡眠组：床贴北墙偏西（贴图 3.06x4.30），两侧床头柜紧贴床沿
                    new PropSpec("BedObject", -3.8f, 2.45f),
                    new PropSpec("BedroomNightstand", -6.25f, 4.52f),
                    // 衣帽区：衣柜贴北墙右侧，与床之间留 1.0 走道
                    new PropSpec("WardrobeObject", 5.0f, 2.39f),
                },
                props = new[]
                {
                    // 地毯在床尾（中心对齐床），不是压在床或衣柜下面
                    new PropSpec("BedroomRunnerRug", -3.8f, 1.2f),
                    // 衣柜地毯改作西门口的地垫
                    new PropSpec("WardrobeRug", -10.6f, 0f),
                },
                decor = new[]
                {
                    // 床右侧补一个小柜（与左侧床头柜对称）
                    new DecorSpec("small_cabinet", -1.4f, 4.52f, true, 1f),
                    // 落地灯贴左侧床头柜外侧（床头灯）
                    new DecorSpec("floor_lamp", -7.6f, 4.6f, true, 1f),
                    // 床尾长凳（压在床尾地毯上）
                    // 南墙：左小柜 + 右矮柜（宽件只上南墙）
                    new DecorSpec("small_cabinet", -7.0f, -6.25f, true, 1f),
                    new DecorSpec("low_cabinet", 6.5f, -6.25f, true, 1f),
                    // 两个南角各一盆
                    new DecorSpec("small_plant", -12.505f, -6.0f, false, 1f),
                    new DecorSpec("tall_plant", 12.505f, -6.0f, true, 1f),
                },
                spawns = new[]
                {
                    // 西墙门触发区 x 边沿 -11.255 → 落在 -10.7（区外），否则玩家落地即在门里。
                    new SpawnSpec("spawn_from_living", -10.7f, 0f, true),
                },
                walls = new[]
                {
                    // 北墙 y 6.75..8.75（厚 2.0，无门）
                    new WallSpec(0f, 8.0f, RoomW, 2.5f),
                    // 南墙 y -8.75..-6.75（厚 2.0，无门）
                    new WallSpec(0f, -8.0f, RoomW, 2.5f),
                    // 东墙 x 12.75..14.75（厚 2.0，无门）
                    new WallSpec(14.305f, 0f, 2.5f, RoomH),
                    // 西墙 x -14.75..-12.75（厚 2.0），门洞 y=0（高 2.0）→ 对应客厅东门
                    new WallSpec(-14.305f, -4f, 2.5f, 5.5f),
                    new WallSpec(-14.305f, 4f, 2.5f, 5.5f),
                },
                doors = new[]
                {
                    new DoorSpec("DoorToLiving", -14.305f, 0f, .9f, 1.9f, "HomeLivingRoomScene", "spawn_from_bedroom", "客厅"),
                },
                defaultSpawnId = "spawn_from_living",
            };
        }

        private static RoomSpec Study()
        {
            return new RoomSpec
            {
                label = "书房",
                scenePath = StudyScene,
                artPath = ArtDir + "/room_study.png",
                size = new Vector2(RoomW, RoomH),
                keepProps = new[] { "DeskChair", "StudyBookshelf", "StudyTallPlant" },
                keepInteractables = new[] { "DeskObject", "ComputerStudyPoint" },
                cameraOrtho = Ortho,
                interactables = new[]
                {
                    // 工作区：书桌居中贴北墙（贴图 3.44x4.44），电脑在桌面右侧，椅子正对书桌
                    new PropSpec("DeskObject", -2.0f, 2.31f),
                    new PropSpec("ComputerStudyPoint", -0.2f, 4.2f),
                },
                props = new[]
                {
                    new PropSpec("DeskChair", -2.0f, 0.8f),
                    // 书墙：矮书柜 + 高书柜并排贴北墙右段
                    new PropSpec("StudyBookshelf", 3.4f, 4.42f),
                    new PropSpec("StudyTallPlant", -12.505f, 4.7f),
                },
                decor = new[]
                {
                    new DecorSpec("tall_bookshelf", 6.4f, 2.94f, true, 1f),
                    // 阅读角（西南）：转椅 + 落地灯（灯贴椅）
                    new DecorSpec("chair", -9.8f, -4.2f, true, 1f),
                    new DecorSpec("floor_lamp", -11.3f, -4.5f, true, 1f),
                    new DecorSpec("small_cabinet", -6.2f, -6.25f, true, 1f),
                    // 书桌上的台灯 + 地上的书堆 + 北墙挂画
                    // 东墙只放窄柜（避开 y=0 的门）
                    new DecorSpec("small_cabinet", 12.405f, -3.6f, true, 1f),
                    new DecorSpec("small_plant", 12.2f, -6.0f, false, 1f),
                },
                spawns = new[]
                {
                    // 东墙门触发区 x 边沿 11.255 → 落在 10.7（区外）。
                    new SpawnSpec("spawn_from_living", 10.7f, 0f, true),
                },
                walls = new[]
                {
                    // 北墙 y 6.75..8.75（厚 2.0，无门）
                    new WallSpec(0f, 8.0f, RoomW, 2.5f),
                    // 南墙 y -8.75..-6.75（厚 2.0，无门）
                    new WallSpec(0f, -8.0f, RoomW, 2.5f),
                    // 西墙 x -14.75..-12.75（厚 2.0，无门）
                    new WallSpec(-14.305f, 0f, 2.5f, RoomH),
                    // 东墙 x 12.75..14.75（厚 2.0），门洞 y=0（高 2.0）
                    new WallSpec(14.305f, -4f, 2.5f, 5.5f),
                    new WallSpec(14.305f, 4f, 2.5f, 5.5f),
                },
                doors = new[]
                {
                    new DoorSpec("DoorToLiving", 14.305f, 0f, .9f, 1.9f, "HomeLivingRoomScene", "spawn_from_study", "客厅"),
                },
                defaultSpawnId = "spawn_from_living",
            };
        }

        private static RoomSpec Kitchen()
        {
            return new RoomSpec
            {
                label = "厨房",
                scenePath = KitchenScene,
                artPath = ArtDir + "/room_kitchen.png",
                size = new Vector2(RoomW, RoomH),
                keepProps = new[] { "KitchenStove", "KitchenSink", "KitchenFridge", "KitchenDrawer" },
                keepInteractables = new string[0],
                cameraOrtho = Ortho,
                props = new[]
                {
                    // ── 北墙操作台：四件紧挨成一条（顶边对齐 6.95），左侧整段连续 ──────
                    new PropSpec("KitchenDrawer", -9.59f, 4.43f),   // 1.42x2.52
                    new PropSpec("KitchenStove", -7.96f, 3.95f),    // 1.84x3.00
                    new PropSpec("KitchenSink", -5.56f, 4.36f),     // 2.97x2.59
                    new PropSpec("KitchenFridge", -3.38f, 3.95f),   // 1.39x3.00
                },
                decor = new[]
                {
                    // 中岛：与操作台平行，中间留 0.5 走道；两把凳子摆在中岛南侧
                    new DecorSpec("kitchen_island", -0.5f, 1.6f, true, 1.15f),
                    // 餐区：地毯中心与餐桌中心对齐（毯压桌下），桌上茶具
                    new DecorSpec("green_rug", 7.5f, -3.5f, false, 1.2f),
                    new DecorSpec("coffee_table", 7.5f, -3.5f, true, 1.0f),
                    // 餐桌旁的落地灯（间距 0.4）+ 北墙挂钟
                    new DecorSpec("floor_lamp", 10.1f, -2.6f, true, 1f),
                    // 南墙小柜 + 两角绿植
                    new DecorSpec("small_cabinet", -8.0f, -6.25f, true, 1f),
                    new DecorSpec("small_plant", -12.505f, -6.0f, false, 1f),
                    new DecorSpec("tall_plant", 12.505f, 4.7f, true, 1f),
                },
                spawns = new[]
                {
                    // 南墙门触发区 y 上沿 -4.95，玩家胶囊底 -0.845 → 中心 ≥ -4.1 才不重叠。
                    new SpawnSpec("spawn_from_living", 0f, -3.9f, true),
                },
                walls = new[]
                {
                    // 北墙 y 6.75..8.75（厚 2.0，无门）
                    new WallSpec(0f, 8.0f, RoomW, 2.5f),
                    // 南墙 y -8.75..-6.75（厚 2.0），门洞 x=0（宽 2.0）
                    new WallSpec(-7.7775f, -8f, 13.055f, 2.5f),
                    new WallSpec(7.7775f, -8f, 13.055f, 2.5f),
                    // 东西墙 x ±(12.75..14.75)（厚 2.0）
                    new WallSpec(-14.305f, 0f, 2.5f, RoomH),
                    new WallSpec(14.305f, 0f, 2.5f, RoomH),
                },
                doors = new[]
                {
                    new DoorSpec("DoorToLiving", 0f, -8.0f, 1.9f, .9f, "HomeLivingRoomScene", "spawn_from_kitchen", "客厅"),
                },
                defaultSpawnId = "spawn_from_living",
            };
        }

        /// <summary>只重建客厅（样板迭代 / batch 入口，不弹模态框）。</summary>
        [MenuItem("Tools/Student Growth/Maps/Rebuild Living Room Only")]
        public static void BuildLivingOnly()
        {
            PrepareArt();
            BuildRoom(Living());
            RegisterScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[Rooms] 客厅已重建：画幅 28.5x16.5 / 可走 25.5x13 / ortho 8（薄墙版）");
        }

        /// <summary>刷新并强制重导四张房间底图（PNG 变了但导入参数没变时 Unity 会静默跳过）。</summary>
        private static void PrepareArt()
        {
            // 先 Refresh 让新 PNG 进入资产库，否则 GetAtPath 为空、重导会被静默跳过
            AssetDatabase.Refresh();
            EnsureSpriteImport(ArtDir + "/room_living.png");
            EnsureSpriteImport(ArtDir + "/room_bedroom.png");
            EnsureSpriteImport(ArtDir + "/room_study.png");
            EnsureSpriteImport(ArtDir + "/room_kitchen.png");
            AssetDatabase.Refresh();

            // 不用 SaveCurrentModifiedScenesIfUserWantsTo：batch 模式下会弹模态框
            if (SceneManager.GetActiveScene().isDirty)
            {
                EditorSceneManager.SaveOpenScenes();
            }
        }

        [MenuItem("Tools/Student Growth/Maps/Build Home Room Scenes")]
        public static void Build()
        {
            PrepareArt();
            BuildRoom(Living());
            BuildRoom(Bedroom());
            BuildRoom(Study());
            BuildRoom(Kitchen());
            RegisterScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[Rooms] 客厅 / 卧室 / 书房 / 厨房 四个房间场景已生成（模板 " + TemplateScene + "）");
        }

        private static void BuildRoom(RoomSpec spec)
        {
            Scene scene = EditorSceneManager.OpenScene(TemplateScene, OpenSceneMode.Single);
            Sprite art = AssetDatabase.LoadAssetAtPath<Sprite>(spec.artPath);
            if (art == null)
            {
                Debug.LogError("[Rooms] 底图未导入为 Sprite：" + spec.artPath);
                return;
            }

            // 1) 背景换成房间底图；关掉对位参考图
            GameObject background = GameObject.Find("RoomSpace_Background");
            if (background != null)
            {
                background.GetComponent<SpriteRenderer>().sprite = art;
            }

            GameObject guide = GameObject.Find("RoomReference_Guide_Disabled");
            if (guide != null)
            {
                Object.DestroyImmediate(guide);
            }

            // 2) 家具与交互物：保留清单内的，其余删掉；保留的按新坐标摆放
            PruneChildren("DecorativeObjects", spec.keepProps, out _);
            PruneChildren("InteractableObjects", spec.keepInteractables, out _);
            foreach (PropSpec p in spec.props)
            {
                GameObject go = FindDeep(p.name);
                if (go != null)
                {
                    go.transform.localPosition = new Vector3(p.position.x, p.position.y, 0f);
                }
                else
                {
                    Debug.LogWarning("[Rooms] " + spec.label + " 缺少家具 " + p.name);
                }
            }

            if (spec.interactables != null)
            {
                foreach (PropSpec p in spec.interactables)
                {
                    GameObject go = FindDeep(p.name);
                    if (go != null)
                    {
                        go.transform.localPosition = new Vector3(p.position.x, p.position.y, 0f);
                        if (!Mathf.Approximately(p.scale, 1f))
                        {
                            go.transform.localScale = new Vector3(p.scale, p.scale, 1f);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[Rooms] " + spec.label + " 缺少交互物 " + p.name);
                    }
                }
            }

            // 2.5) 装饰物：复用裁好的 Props sprite（pivot 在左下角，pos 表示落地点/底边中心）
            GameObject decorRoot = GameObject.Find("DecorativeObjects");
            if (decorRoot != null && spec.decor != null)
            {
                foreach (DecorSpec d in spec.decor)
                {
                    Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(PropDir + "/home_ref_" + d.spriteName + ".png");
                    if (sp == null)
                    {
                        Debug.LogWarning("[Rooms] " + spec.label + " 缺少装饰 sprite " + d.spriteName);
                        continue;
                    }

                    float w = sp.rect.width / sp.pixelsPerUnit;
                    float h = sp.rect.height / sp.pixelsPerUnit;
                    GameObject go = new GameObject("Decor_" + d.spriteName);
                    go.transform.SetParent(decorRoot.transform, false);
                    // 2026-10-19 修：Props sprite 的 pivot 是 (0.5,0.5)（中心），不是左下角。
                    // 旧写法只减了半个宽度、没处理高度，导致每件装饰都左移 w/2、且 y 被当成中心。
                    // 现在按 Sprite.bounds 推：让 (pos.x, pos.y) 精确落在「底边中心」。
                    go.transform.localPosition = new Vector3(
                        d.pos.x - sp.bounds.center.x,
                        d.pos.y - sp.bounds.min.y,
                        0f);

                    SpriteRenderer decorRenderer = go.AddComponent<SpriteRenderer>();
                    decorRenderer.sprite = sp;
                    if (!Mathf.Approximately(d.scale, 1f))
                    {
                        go.transform.localScale = new Vector3(d.scale, d.scale, 1f);
                    }

                    if (d.collide)
                    {
                        // 精灵 pivot 在中心（transform 就是精灵中心），所以碰撞盒要居中在下部：
                        // 旧的 offset=(w/2, h/4) 是按"左下 pivot"写的，加在中心 pivot 上会整体偏到右下角。
                        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
                        box.size = new Vector2(w * .82f, h * .42f);
                        box.offset = new Vector2(0f, -h * .29f);
                    }

                    MapObjectYSorter sorter = go.AddComponent<MapObjectYSorter>();
                    sorter.renderers = new[] { decorRenderer };
                    sorter.baseOrder = 1000;
                    sorter.orderScale = 100f;
                }
            }

            // 2.8) 大门（出口）：旧贴图关掉，换成新素材的门面 + 开门动画。
            //      MapInteractionFlowController 会自己找交互目标身上的 MapDoorAnimator 播开门
            //      （见 MapInteractionFlowController.TryPlayDoorAnimationBeforeExecution），所以这里
            //      只要把动画组件挂到 ExitDoorObject 上就行，不用改交互流程。
            GameObject exitDoor = FindDeep("ExitDoorObject");
            if (exitDoor != null)
            {
                SpriteRenderer[] doorRenderers = exitDoor.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < doorRenderers.Length; i++)
                {
                    doorRenderers[i].enabled = false;
                }

                // 门面放在南墙里（底图上烘的那扇门的位置），世界坐标换算成 ExitDoorObject 的局部坐标
                Vector3 exitDoorCenter = new Vector3(0f, InnerBottom - WallThick * .5f, 0f);
                Vector3 exitLocal = exitDoor.transform.InverseTransformPoint(exitDoorCenter);
                SpriteRenderer exitVisual = AddDoorVisual(exitDoor.transform, exitLocal, 0f);

                // 关键：交互流程找的是**交互目标自己身上**的 MapDoorAnimator
                // （MapInteractionFlowController.TryPlayDoorAnimationBeforeExecution），
                // 而 ExitDoorObject 模板里本来就带了一个（原来指向被禁用的旧 Visual），
                // 所以要复用它、把帧配到新门面上，而不是在子物体上另挂一个。
                MapDoorAnimator exitAnimator = exitDoor.GetComponent<MapDoorAnimator>();
                if (exitAnimator == null)
                {
                    exitAnimator = exitDoor.AddComponent<MapDoorAnimator>();
                }

                ConfigureDoorAnimator(exitAnimator, exitVisual);
            }

            // 3) 碰撞：清空模板的碰撞瓦片与旧碰撞体，按房间重铺四面墙
            GameObject collisionTilemap = GameObject.Find("Collision_Tilemap");
            if (collisionTilemap != null)
            {
                Tilemap tilemap = collisionTilemap.GetComponent<Tilemap>();
                if (tilemap != null)
                {
                    tilemap.ClearAllTiles();
                }

                // 关键：清空瓦片不会立刻清掉 CompositeCollider 的旧几何（原家的 5 道墙会变成隐形墙），
                // 房间碰撞完全由下面的 BoxCollider 负责，这里把瓦片碰撞组件直接移除。
                TilemapCollider2D tilemapCollider2D = collisionTilemap.GetComponent<TilemapCollider2D>();
                if (tilemapCollider2D != null) { Object.DestroyImmediate(tilemapCollider2D); }
                CompositeCollider2D compositeCollider2D = collisionTilemap.GetComponent<CompositeCollider2D>();
                if (compositeCollider2D != null) { Object.DestroyImmediate(compositeCollider2D); }
                Rigidbody2D collisionBody = collisionTilemap.GetComponent<Rigidbody2D>();
                if (collisionBody != null) { Object.DestroyImmediate(collisionBody); }
            }

            GameObject collisionBounds = GameObject.Find("CollisionBounds");
            if (collisionBounds != null)
            {
                for (int i = collisionBounds.transform.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(collisionBounds.transform.GetChild(i).gameObject);
                }

                foreach (WallSpec wall in spec.walls)
                {
                    GameObject go = new GameObject("Wall");
                    go.transform.SetParent(collisionBounds.transform, false);
                    go.transform.localPosition = wall.center;
                    BoxCollider2D box = go.AddComponent<BoxCollider2D>();
                    box.size = wall.size;
                }
            }

            // 3.5) 相机取景 = 房间尺度（模板是 30x20 大堂，房间必须拉近）
            Camera camera = Camera.main;
            if (camera != null && spec.cameraOrtho > 0f)
            {
                camera.orthographicSize = spec.cameraOrtho;
            }

            // 4) 相机边界 = 房间尺寸
            GameObject bounds = GameObject.Find("CameraBounds");
            if (bounds != null)
            {
                BoxCollider2D box = bounds.GetComponent<BoxCollider2D>();
                if (box != null)
                {
                    box.size = spec.size;
                }
            }

            // 5) 出生点
            GameObject spawnRoot = GameObject.Find("PlayerSpawnPoints");
            if (spawnRoot != null)
            {
                for (int i = spawnRoot.transform.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(spawnRoot.transform.GetChild(i).gameObject);
                }

                foreach (SpawnSpec spawn in spec.spawns)
                {
                    GameObject go = new GameObject("Spawn_" + spawn.id);
                    go.transform.SetParent(spawnRoot.transform, false);
                    go.transform.localPosition = spawn.position;
                    PlayerSpawnPoint point = go.AddComponent<PlayerSpawnPoint>();
                    point.spawnPointId = spawn.id;
                }
            }

            // 6) 房间门
            GameObject doorRoot = GameObject.Find("RoomDoors");
            if (doorRoot != null)
            {
                Object.DestroyImmediate(doorRoot);
            }

            doorRoot = new GameObject("RoomDoors");
            foreach (DoorSpec door in spec.doors)
            {
                GameObject go = new GameObject(door.name);
                go.transform.SetParent(doorRoot.transform, false);

                // 触发区放在**可走区内侧**（玩家踩得到）；门面另外贴进墙洞。
                // 2026-10-21 修：原先两者同用墙中心，触发区埋在墙里，玩家永远走不进去。
                Vector3 triggerCenter = DoorTriggerCenter(door.position, door.size);
                go.transform.localPosition = triggerCenter;

                BoxCollider2D box = go.AddComponent<BoxCollider2D>();
                box.size = door.size;
                box.isTrigger = true;

                // 2026-10-21：房间门改成**按 E 交互**（用户要求）。
                // 接入统一交互系统：MapInteractionPoint + MapInteractionTarget 由 RoomDoor
                // 自己在 Awake/Reset 里配好，这里只要把组件挂上即可。
                // 不再依赖 OnTriggerEnter2D 自动切场景。
                MapInteractionPoint point = go.AddComponent<MapInteractionPoint>();
                point.locationId = "location_home";
                point.actionId = string.Empty;

                MapInteractionTarget target = go.AddComponent<MapInteractionTarget>();
                target.interactionId = "room_door_" + door.name;

                RoomDoor component = go.AddComponent<RoomDoor>();
                component.targetSceneName = door.targetScene;
                component.targetSpawnPointId = door.targetSpawn;
                component.destinationDisplayName = door.displayName;
                // 挂完组件后立刻配一次（Awake 在编辑器里不会跑，场景保存的是这里的值）
                component.ConfigureInteraction();

                // 门面：贴到墙体里（和底图那扇同位），带开门动画；触发区本身仍然不可见
                Vector3 wallCenter = DoorWallCenter(door.position, door.size);
                Vector3 localOffset = wallCenter - triggerCenter;
                AddAnimatedDoorVisual(go.transform, localOffset, DoorZRotation(door.position, door.size));
            }

            ValidateSpawnClearance(spec, doorRoot);

            // 6.5) 玩家移动边界：等于墙内可走范围（模板的旧值会把玩家锁在原大堂里）
            GameObject playerObject = FindDeep("PlayerRpg");
            if (playerObject != null)
            {
                RpgPlayerController rpg = playerObject.GetComponent<RpgPlayerController>();
                if (rpg != null)
                {
                    rpg.useMovementBounds = true;
                    // 可走区不再是"以原点对称"的矩形（南墙 2.0 / 北墙 1.5，室内 y 是 -7..7.5），
                    // 所以直接用内沿常量算，别再用 walkSize 对折。
                    rpg.movementMin = new Vector2(-InnerHalfW + .9f, InnerBottom + .9f);
                    rpg.movementMax = new Vector2(InnerHalfW - .9f, InnerTop - .9f);
                }
            }

            // 7) 控制器默认出生点
            FullscreenMapSceneController controller = Object.FindAnyObjectByType<FullscreenMapSceneController>();
            if (controller != null)
            {
                controller.defaultSpawnPointId = spec.defaultSpawnId;
            }

            GameObject root = GameObject.Find(RootName);
            if (root != null)
            {
                root.name = "Home" + spec.label + "Root";
            }

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, spec.scenePath);
            Debug.Log("[Rooms] " + spec.label + " → " + spec.scenePath + (saved ? " 已保存" : " 保存失败"));
        }

        private static void PruneChildren(string parentName, string[] keep, out int removed)
        {
            removed = 0;
            GameObject parent = GameObject.Find(parentName);
            if (parent == null)
            {
                return;
            }

            for (int i = parent.transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.transform.GetChild(i).gameObject;
                bool keepIt = false;
                for (int k = 0; k < keep.Length; k++)
                {
                    if (child.name == keep[k])
                    {
                        keepIt = true;
                        break;
                    }
                }

                if (!keepIt)
                {
                    Object.DestroyImmediate(child);
                    removed++;
                }
            }
        }

        private static GameObject FindDeep(string name)
        {
            GameObject direct = GameObject.Find(name);
            if (direct != null)
            {
                return direct;
            }

            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name)
                {
                    return all[i].gameObject;
                }
            }

            return null;
        }

        private static void RegisterScenes()
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            string[] add = { LivingScene, BedroomScene, StudyScene, KitchenScene };
            foreach (string path in add)
            {
                bool exists = false;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].path == path)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    list.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            EditorBuildSettings.scenes = list.ToArray();
        }

        private static void EnsureSpriteImport(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            // 关键：Multiple 模式下取到的是旧图切分结果（1448x1086），必须 Single
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; }
            if (importer.maxTextureSize < 4096) { importer.maxTextureSize = 4096; }
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; }
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, 64f)) { importer.spritePixelsPerUnit = 64f; }
            if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; }
            // PNG 内容变了但导入参数没变时 SaveAndReimport 可能不重建 artifact → 用 ForceUpdate 强制重导
            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
