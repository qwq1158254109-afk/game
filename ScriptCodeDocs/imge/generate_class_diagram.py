from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import math


ROOT = Path(r"E:\unity\ML-project")
OUT_DIR = ROOT / "ScriptCodeDocs" / "imge"
OUT_DIR.mkdir(parents=True, exist_ok=True)
OUT_PATH = OUT_DIR / "student_growth_class_diagram.png"

W, H = 3900, 2620
img = Image.new("RGB", (W, H), "white")
d = ImageDraw.Draw(img)

FONT_CANDIDATES = [
    r"C:\Windows\Fonts\msyh.ttc",
    r"C:\Windows\Fonts\simhei.ttf",
    r"C:\Windows\Fonts\Deng.ttf",
    r"C:\Windows\Fonts\simsun.ttc",
]


def load_font(size, bold=False):
    names = []
    if bold:
        names = [
            r"C:\Windows\Fonts\msyhbd.ttc",
            r"C:\Windows\Fonts\simhei.ttf",
        ]
    for path in names + FONT_CANDIDATES:
        if Path(path).exists():
            try:
                return ImageFont.truetype(path, size=size)
            except OSError:
                pass
    return ImageFont.load_default()


F_TITLE = load_font(48, True)
F_SUB = load_font(22)
F_PACKAGE = load_font(24, True)
F_CLASS = load_font(20)
F_CLASS_BOLD = load_font(20, True)
F_STEREO = load_font(16)
F_MEMBER = load_font(16)
F_LABEL = load_font(16)
F_FOOT = load_font(18)

BLACK = (20, 20, 20)
GRAY = (95, 95, 95)
BOX = (244, 244, 244)
HEADER = (238, 238, 238)
LINE = (115, 115, 115)

PACKAGE_COLORS = {
    "data": (232, 244, 232),
    "runtime": (255, 244, 224),
    "service": (244, 232, 248),
    "controller": (255, 250, 202),
    "map": (226, 242, 250),
    "ui": (238, 238, 238),
    "editor": (252, 232, 238),
    "legend": (245, 245, 245),
}

ICON_COLORS = {
    "C": (151, 205, 165),
    "S": (159, 190, 230),
    "M": (245, 205, 130),
    "E": (205, 180, 235),
    "U": (194, 194, 194),
}

boxes = {}
class_specs = []


def text_size(text, font):
    bbox = d.multiline_textbbox((0, 0), text, font=font, spacing=4)
    return bbox[2] - bbox[0], bbox[3] - bbox[1]


def draw_center_text(box, text, font, fill=BLACK):
    x1, y1, x2, y2 = box
    tw, th = text_size(text, font)
    d.multiline_text(
        (x1 + (x2 - x1 - tw) / 2, y1 + (y2 - y1 - th) / 2),
        text,
        font=font,
        fill=fill,
        align="center",
        spacing=4,
    )


def package(x, y, w, h, title, color):
    d.rounded_rectangle((x, y, x + w, y + h), radius=8, fill=color, outline=BLACK, width=3)
    tab_w = max(250, text_size(title, F_PACKAGE)[0] + 54)
    tab_h = 38
    tab = [(x, y), (x + tab_w, y), (x + tab_w + 24, y + tab_h), (x, y + tab_h)]
    d.polygon(tab, fill=color, outline=BLACK)
    d.line((x, y + tab_h, x + tab_w + 24, y + tab_h), fill=BLACK, width=3)
    d.text((x + 12, y + 6), title, font=F_PACKAGE, fill=BLACK)


def class_box(key, x, y, w, name, stereotype="", fields=None, methods=None, icon="C", record=True):
    fields = fields or []
    methods = methods or []
    header_h = 56
    field_h = max(42, len(fields) * 24 + 18)
    method_h = max(42, len(methods) * 24 + 18)
    h = header_h + field_h + method_h

    d.rectangle((x, y, x + w, y + h), fill=BOX, outline=LINE, width=2)
    d.rectangle((x, y, x + w, y + header_h), fill=HEADER, outline=LINE, width=1)
    d.line((x, y + header_h + field_h, x + w, y + header_h + field_h), fill=LINE, width=1)

    cx = x + 30
    cy = y + header_h / 2
    d.ellipse((cx - 13, cy - 13, cx + 13, cy + 13), fill=ICON_COLORS.get(icon, ICON_COLORS["C"]), outline=BLACK, width=2)
    draw_center_text((cx - 13, cy - 13, cx + 13, cy + 13), icon, F_STEREO, BLACK)

    if stereotype:
        draw_center_text((x + 54, y + 5, x + w - 8, y + 25), stereotype, F_STEREO)
        draw_center_text((x + 54, y + 24, x + w - 8, y + header_h - 4), name, F_CLASS_BOLD)
    else:
        draw_center_text((x + 54, y + 5, x + w - 8, y + header_h - 4), name, F_CLASS_BOLD)

    ty = y + header_h + 10
    for line in fields:
        d.text((x + 14, ty), line, font=F_MEMBER, fill=BLACK)
        ty += 24

    ty = y + header_h + field_h + 10
    for line in methods:
        d.text((x + 14, ty), line, font=F_MEMBER, fill=BLACK)
        ty += 24

    boxes[key] = (x, y, x + w, y + h)
    if record:
        class_specs.append((key, x, y, w, name, stereotype, list(fields), list(methods), icon))
    return boxes[key]


def redraw_class_boxes():
    for spec in class_specs:
        class_box(*spec, record=False)


def anchor(box, side):
    x1, y1, x2, y2 = box
    if side == "left":
        return x1, (y1 + y2) / 2
    if side == "right":
        return x2, (y1 + y2) / 2
    if side == "top":
        return (x1 + x2) / 2, y1
    if side == "bottom":
        return (x1 + x2) / 2, y2
    return (x1 + x2) / 2, (y1 + y2) / 2


def draw_dashed_line(p1, p2, width=2):
    x1, y1 = p1
    x2, y2 = p2
    dx = x2 - x1
    dy = y2 - y1
    dist = math.hypot(dx, dy)
    if dist <= 0:
        return
    vx = dx / dist
    vy = dy / dist
    dash, gap = 14, 9
    t = 0
    while t < dist:
        a = t
        b = min(t + dash, dist)
        d.line((x1 + vx * a, y1 + vy * a, x1 + vx * b, y1 + vy * b), fill=BLACK, width=width)
        t += dash + gap


def draw_polyline(points, dashed=False, width=2):
    for a, b in zip(points[:-1], points[1:]):
        if dashed:
            draw_dashed_line(a, b, width)
        else:
            d.line((a[0], a[1], b[0], b[1]), fill=BLACK, width=width)


def arrow_head(p1, p2, open_head=False):
    angle = math.atan2(p2[1] - p1[1], p2[0] - p1[0])
    size = 15
    spread = math.radians(28)
    pts = []
    for sign in (1, -1):
        a = angle + math.pi + sign * spread
        pts.append((p2[0] + size * math.cos(a), p2[1] + size * math.sin(a)))
    if open_head:
        d.line((pts[0][0], pts[0][1], p2[0], p2[1], pts[1][0], pts[1][1]), fill=BLACK, width=2)
    else:
        d.polygon([(p2[0], p2[1]), pts[0], pts[1]], fill=BLACK)


def label(text, x, y):
    tw, th = text_size(text, F_LABEL)
    d.rectangle((x - tw / 2 - 6, y - th / 2 - 3, x + tw / 2 + 6, y + th / 2 + 3), fill="white")
    d.text((x - tw / 2, y - th / 2), text, font=F_LABEL, fill=BLACK)


def connect(src, dst, src_side="right", dst_side="left", label_text="", dashed=False, via=None, open_head=False):
    start = anchor(boxes[src], src_side)
    end = anchor(boxes[dst], dst_side)
    points = [start] + (via or []) + [end]
    draw_polyline(points, dashed=dashed)
    arrow_head(points[-2], points[-1], open_head=open_head)
    if label_text:
        mid = points[len(points) // 2]
        label(label_text, mid[0], mid[1] - 16)


def section_title(x, y, text):
    d.text((x, y), text, font=F_PACKAGE, fill=BLACK)
    d.line((x, y + 32, x + text_size(text, F_PACKAGE)[0] + 30, y + 32), fill=BLACK, width=2)


draw_center_text((0, 35, W, 100), "学生成长模拟卡牌游戏类图", F_TITLE)
draw_center_text((0, 105, W, 145), "参考《电影评论系统类图.docx》的分层类图样式；内容基于当前 Unity 项目 StudentGrowth 代码结构。", F_SUB, GRAY)

package(40, 160, 1440, 900, "data（数据资产层）", PACKAGE_COLORS["data"])
package(1520, 160, 900, 900, "runtime（运行时状态层）", PACKAGE_COLORS["runtime"])
package(2460, 160, 1380, 900, "service（规则/流程层）", PACKAGE_COLORS["service"])
package(40, 1110, 1900, 820, "controller（控制层）", PACKAGE_COLORS["controller"])
package(1980, 1110, 1860, 820, "map scene（地图场景层）", PACKAGE_COLORS["map"])
package(40, 1980, 1300, 560, "view（UI 表现层）", PACKAGE_COLORS["ui"])
package(1380, 1980, 1260, 560, "editor/tools（编辑器工具层）", PACKAGE_COLORS["editor"])
package(2680, 1980, 1160, 560, "common / enum（通用与枚举）", PACKAGE_COLORS["legend"])

class_box("GameBootstrapperData", 80, 230, 330, "GameBootstrapperData", "<<ScriptableObject>>", [
    "+List<CardData> starterDeck",
    "+List<ItemData> starterItems",
    "+List<StageData> stages",
    "+List<LocationData> locations",
    "+StageData firstStage",
], [], "S")
class_box("StageData", 430, 230, 330, "StageData", "<<ScriptableObject>>", [
    "+string stageId",
    "+Sprite backgroundSprite",
    "+List<ActivityData> activities",
    "+ChallengeData bossChallenge",
    "+List<RewardData> rewards",
], [], "S")
class_box("ActivityData", 780, 230, 330, "ActivityData", "<<ScriptableObject>>", [
    "+ActivityType activityType",
    "+ChallengeData linkedChallenge",
    "+GameEventData linkedEvent",
    "+MiniGameData linkedMiniGame",
    "+List<RewardData> rewards",
], [], "S")
class_box("CardData", 1130, 230, 310, "CardData", "<<ScriptableObject>>", [
    "+CardType cardType",
    "+CardRarity rarity",
    "+Sprite cardSprite",
    "+List<CardEffectData> effects",
    "+CardData upgradedVersion",
], [], "S")

class_box("ChallengeData", 80, 520, 330, "ChallengeData", "<<ScriptableObject>>", [
    "+bool isBoss",
    "+int targetProgress",
    "+List<IntentData> intents",
    "+List<RewardData> successRewards",
    "+List<RewardData> bossRewards",
], [], "S")
class_box("ItemData", 430, 520, 330, "ItemData", "<<ScriptableObject>>", [
    "+ItemType itemType",
    "+Sprite icon",
    "+List<CardEffectData> effects",
    "+ItemTriggerTiming triggerTiming",
    "+bool consumedOnUse",
], [], "S")
class_box("GameEventData", 780, 520, 330, "GameEventData", "<<ScriptableObject>>", [
    "+string eventId",
    "+Sprite backgroundSprite",
    "+List<ChoiceData> choices",
    "+List<ConditionData> conditions",
], [], "S")
class_box("EndingData", 1130, 520, 310, "EndingData", "<<ScriptableObject>>", [
    "+string endingId",
    "+Sprite illustrationSprite",
    "+List<ConditionData> conditions",
    "+int priority",
], [], "S")

class_box("LocationData", 80, 790, 330, "LocationData", "<<ScriptableObject>>", [
    "+LocationType locationType",
    "+Sprite icon/background",
    "+List<ActionData> actions",
    "+List<ConditionData> unlocks",
], [], "S")
class_box("LocationActionData", 430, 790, 330, "LocationActionData", "<<ScriptableObject>>", [
    "+LocationActionType actionType",
    "+int time/energy/moneyCost",
    "+ActivityData linkedActivity",
    "+ChallengeData linkedChallenge",
    "+List<RewardData> rewards",
], [], "S")
class_box("StageMapData", 780, 790, 330, "StageMapData", "<<ScriptableObject>>", [
    "+string stageId",
    "+string title",
    "+List<LocationEntry> locations",
], [], "S")
class_box("MapSceneConfigData", 1130, 790, 310, "MapSceneConfigData", "<<ScriptableObject>>", [
    "+string mapId",
    "+string sceneName",
    "+Vector2 cameraMin/cameraMax",
    "+bool allowReturnToStageMap",
], [], "S")

class_box("GameSessionState", 1560, 235, 390, "GameSessionState", "<<Serializable>>", [
    "+PlayerState playerState",
    "+StageState stageState",
    "+LocationState locationState",
    "+DeckState deckState",
    "+ItemInventory itemInventory",
    "+EndingData currentEnding",
], ["+ClampAll()"], "C")
class_box("PlayerState", 1990, 235, 360, "PlayerState", "<<Serializable>>", [
    "+int chinese/math/english",
    "+int energy/maxEnergy",
    "+int pressure/mood",
    "+int money",
], ["+GetStat(key) : int", "+SetStat(key,value)", "+AddStat(key,delta)"], "C")
class_box("StageState", 1560, 535, 360, "StageState", "<<Serializable>>", [
    "+StageData currentStage",
    "+int remainingTime",
    "+bool bossUnlocked",
    "+int clearedStageCount",
], [], "C")
class_box("DeckState", 1990, 535, 360, "DeckState", "<<Serializable>>", [
    "+List<CardData> masterDeck",
    "+List<CardData> drawPile",
    "+List<CardData> hand",
    "+List<CardData> discardPile",
], ["+AddCardToMasterDeck()", "+UpgradeCard()", "+ClearBattlePiles()"], "C")
class_box("ChallengeRuntimeState", 1560, 790, 360, "ChallengeRuntimeState", "<<Serializable>>", [
    "+ChallengeData data",
    "+int currentProgress",
    "+int currentTurn/currentEnergy",
    "+CombatPhase phase",
], [], "C")
class_box("LocationState", 1990, 790, 360, "LocationState", "<<Serializable>>", [
    "+string currentLocationId",
    "+List<string> unlockedIds",
    "+List<string> visitedIds",
    "+Dictionary actionUseCount",
], [], "C")

class_box("RewardResolver", 2500, 230, 350, "RewardResolver", "", [], [
    "+ApplyReward(reward, state)",
    "+ApplyRewards(rewards, state)",
], "C")
class_box("CardEffectResolver", 2880, 230, 350, "CardEffectResolver", "", [], [
    "+ApplyCard(card, state, runtime)",
    "+ApplyEffect(effect, state, runtime)",
], "C")
class_box("ItemEffectResolver", 3260, 230, 350, "ItemEffectResolver", "", [], [
    "+UseItem(item, state)",
    "+ApplyPassiveItems()",
    "+ClearStageOnlyItems()",
], "C")
class_box("EventConditionResolver", 2500, 500, 350, "EventConditionResolver", "<<static>>", [], [
    "+CanUseConditions()",
    "+CanUseCondition()",
], "C")
class_box("LocationActionExecutor", 2880, 500, 350, "LocationActionExecutor", "<<MonoBehaviour>>", [
    "+GameSessionState sessionState",
    "+List<LocationData> locations",
], ["+Configure(...)", "+TryExecute(request)"], "M")
class_box("StageMapLocationAvailabilityService", 3260, 500, 350, "StageMapLocation\nAvailabilityService", "", [], [
    "+ResolveStatus(entry,state)",
    "+ResolveReason(entry,state)",
    "+CanEnter(entry,state,... )",
], "C")
class_box("ValueClampUtility", 2880, 785, 350, "ValueClampUtility", "<<static>>", [
    "+const int StatMin/StatMax",
    "+const int MoneyMax",
], ["+ClampPlayerState()", "+ClampPercentStat()"], "C")

class_box("GameBootstrapper", 80, 1190, 345, "GameBootstrapper", "<<MonoBehaviour>>", [
    "+GameBootstrapperData data",
    "+GameSessionState sessionState",
], ["+BuildSession()", "+WireControllers()"], "M")
class_box("StageController", 455, 1190, 345, "StageController", "<<MonoBehaviour>>", [
    "+GameSessionState sessionState",
], ["+StartStage(stage)", "+CompleteActivity()", "+CompleteBoss()", "+MoveToNextStage()"], "M")
class_box("ChallengeController", 830, 1190, 345, "ChallengeController", "<<MonoBehaviour>>", [
    "+GameSessionState sessionState",
    "+ChallengeRuntimeState runtimeState",
], ["+StartChallenge()", "+TryPlayCard()", "+EndTurn()", "+CompleteChallenge()"], "M")
class_box("ActivityController", 1205, 1190, 345, "ActivityController", "<<MonoBehaviour>>", [
    "+GameSessionState sessionState",
], ["+ResolveActivity(data)", "+CanActivityAppear(data)"], "M")
class_box("StudentGrowthMvpRuntimeUi", 1580, 1190, 320, "StudentGrowthMvp\nRuntimeUi", "<<MonoBehaviour>>", [
    "+GameBootstrapper bootstrapper",
    "+PanelSwitch state",
], ["+BuildRuntimeUi()", "+RefreshAll()"], "M")

class_box("DeckController", 80, 1545, 345, "DeckController", "<<MonoBehaviour>>", [
    "+DeckState deckState",
], ["+InitializeBattleDeck()", "+DrawCards()", "+PlayCard()", "+UpgradeCard()"], "M")
class_box("MiniGameController", 455, 1545, 345, "MiniGameController", "<<MonoBehaviour>>", [], [
    "+ResolveMiniGame(data,state,random)",
    "+CalculateSuccessChance()",
], "M")
class_box("EndingController", 830, 1545, 345, "EndingController", "<<MonoBehaviour>>", [
    "+GameSessionState sessionState",
    "+List<EndingData> endingOptions",
], ["+SelectEnding()", "+CanUseEnding()"], "M")
class_box("SceneTransitionController", 1205, 1545, 345, "SceneTransitionController", "<<MonoBehaviour>>", [], [
    "+CanUseSceneTransition()",
    "+LoadScene()",
    "+ReturnToMap()",
], "M")
class_box("LocationSceneRouter", 1580, 1545, 320, "LocationSceneRouter", "<<MonoBehaviour>>", [
    "+StageMapData stageMapData",
    "+SceneTransitionController transition",
], ["+EnterLocationScene(id)", "+ReturnToStageMap()", "+CanEnterLocationScene()"], "M")

class_box("StageHubMapSceneController", 2020, 1190, 360, "StageHubMapScene\nController", "<<MonoBehaviour>>", [
    "+GameSessionState sessionState",
    "+StageMapData stageMapData",
], ["+RefreshPortals()"], "M")
class_box("FullscreenMapSceneController", 2410, 1190, 360, "FullscreenMapScene\nController", "<<MonoBehaviour>>", [
    "+StageMapData stageMapData",
    "+FullscreenMapHudView hudView",
], ["+Refresh()", "+TryEnterLocation()"], "M")
class_box("MapInteractionController", 2800, 1190, 360, "MapInteractionController", "<<MonoBehaviour>>", [
    "+LocationActionExecutor executor",
    "+LocationSceneRouter router",
], ["+SetCurrentInteractionPoint()", "+TryInteractCurrent()", "+TryInteract(target)"], "M")
class_box("RpgPlayerController", 3190, 1190, 360, "RpgPlayerController", "<<MonoBehaviour>>", [
    "+float moveSpeed",
    "+PlayerFacingInteractor interactor",
    "+MapInteractionPromptView promptView",
], ["+SetFacingDirection()", "+TeleportTo()", "+ClampMovePosition()"], "M")

class_box("PlayerFacingInteractor", 2020, 1545, 360, "PlayerFacingInteractor", "<<MonoBehaviour>>", [
    "+MapInteractionController controller",
    "+MapInteractionTarget[] targets",
], ["+RefreshCurrentTarget()", "+TryInteractCurrent()"], "M")
class_box("MapInteractionTarget", 2410, 1545, 360, "MapInteractionTarget", "<<MonoBehaviour>>", [
    "+LocationActionData locationAction",
    "+ActivityData fallbackActivity",
    "+bool isExitPortal",
], ["+CanSelect(position,facing)"], "M")
class_box("MapInteractionPoint", 2800, 1545, 360, "MapInteractionPoint", "<<MonoBehaviour>>", [
    "+string locationId",
    "+string actionId",
    "+MapInteractionType type",
], ["+CreateExecutionRequest(source)"], "M")
class_box("MapLocationPortal", 3190, 1545, 360, "MapLocationPortal", "<<MonoBehaviour>>", [
    "+string locationId",
    "+StageMapLocationStatus CurrentStatus",
], ["+ApplyStatus(status,reason)"], "M")

class_box("BattleCardView", 80, 2065, 320, "BattleCardView", "<<MonoBehaviour>>", [
    "+CardData cardData",
    "+Image artImage",
], ["+Bind(card)", "+SetPlayable()"], "M")
class_box("BattleTooltipView", 420, 2065, 300, "BattleTooltipView", "<<MonoBehaviour>>", [], [
    "+Show(text)",
    "+Hide()",
], "M")
class_box("LocationView", 740, 2065, 280, "LocationView", "<<MonoBehaviour>>", [
    "+LocationType locationType",
    "+LocationData data",
], ["+Bind(data)", "+Show()", "+Hide()"], "M")
class_box("FullscreenMapHudView", 1040, 2065, 270, "FullscreenMapHudView", "<<MonoBehaviour>>", [], [
    "+BindStageMap()",
    "+SetPrompt()",
], "M")

class_box("MvpSampleAssetGenerator", 1420, 2065, 360, "MvpSampleAssetGenerator", "<<Editor static>>", [], [
    "+GenerateMvpSampleAssets()",
    "-GetOrCreateAsset<T>()",
], "C")
class_box("GameArtBindingEditorUtility", 1800, 2065, 360, "GameArtBinding\nEditorUtility", "<<Editor static>>", [], [
    "+BindSelectedArtReferences()",
    "+BindAllKnownDataAssets()",
], "C")
class_box("MvpSampleDataFactory", 2180, 2065, 360, "MvpSampleDataFactory", "<<static>>", [], [
    "+CreateDefaultGameData()",
    "+CreateDefaultStageMapData()",
], "C")

class_box("Enums", 2720, 2065, 330, "Enums", "<<enum>>", [
    "PlayerStatKey / ActivityType",
    "CardType / CardEffectType",
    "RewardType / ItemType",
    "LocationType / LocationActionType",
    "StageMapLocationStatus",
], [], "E")
class_box("ArtUtilities", 3070, 2065, 340, "GameArt* Utilities", "<<static>>", [
    "GameArtAssetCatalog",
    "GameArtBindingUtility",
], ["+ResolveSprite()", "+TryBindKnownArt()"], "C")
class_box("SampleResultTypes", 3430, 2065, 330, "Result DTOs", "<<Serializable>>", [
    "ActivityResult",
    "MiniGameResult",
    "ItemUseResult",
    "LocationActionResult",
], [], "C")

# Data associations
connect("GameBootstrapperData", "StageData", "right", "left", "stages", dashed=True)
connect("StageData", "ActivityData", "right", "left", "activities", dashed=True)
connect("LocationData", "LocationActionData", "right", "left", "actions", dashed=True)
connect("StageMapData", "MapSceneConfigData", "right", "left", "sceneName", dashed=True)

# Runtime composition/dependency
connect("GameSessionState", "PlayerState", "right", "left", "has", dashed=False)
connect("GameSessionState", "StageState", "bottom", "top", "has", dashed=False, via=[(1755, 505)])
connect("GameSessionState", "DeckState", "right", "left", "has", dashed=False, via=[(1962, 435), (1962, 655)])
connect("GameSessionState", "LocationState", "right", "left", "has", dashed=False, via=[(1962, 435), (1962, 890)])

# Services and controllers
connect("ChallengeController", "DeckController", "bottom", "top", "deck", dashed=False)
connect("LocationActionExecutor", "RewardResolver", "top", "bottom", "applies", dashed=True, via=[(3055, 430), (2675, 430)])
connect("StudentGrowthMvpRuntimeUi", "StageController", "left", "right", "orchestrates", dashed=True)
connect("StudentGrowthMvpRuntimeUi", "LocationSceneRouter", "bottom", "top", "routes", dashed=True, via=[(1740, 1510)])

# Map and UI dependencies
connect("MapInteractionController", "LocationSceneRouter", "left", "right", "route", dashed=True)
connect("RpgPlayerController", "PlayerFacingInteractor", "bottom", "top", "facing", dashed=False)
connect("PlayerFacingInteractor", "MapInteractionTarget", "right", "left", "selects", dashed=False)
connect("MapInteractionTarget", "MapInteractionPoint", "right", "left", "point", dashed=True)

# Editor/tools dependencies
connect("GameArtBindingEditorUtility", "ArtUtilities", "right", "left", "uses", dashed=True)

redraw_class_boxes()

section_title(2720, 2250, "关系说明")
d.text((2720, 2300), "实线箭头：主要持有/调用关系", font=F_MEMBER, fill=BLACK)
draw_polyline([(2720, 2350), (2880, 2350)], dashed=False)
arrow_head((2820, 2350), (2880, 2350))
d.text((2720, 2388), "虚线箭头：数据引用或依赖调用", font=F_MEMBER, fill=BLACK)
draw_polyline([(2720, 2438), (2880, 2438)], dashed=True)
arrow_head((2820, 2438), (2880, 2438))
d.text((3040, 2300), "图中保留核心类与关键字段/方法，\n未展开所有 UI 细节类与私有辅助方法。", font=F_MEMBER, fill=BLACK, spacing=6)

d.text((70, H - 55), "保存位置：ScriptCodeDocs/imge/student_growth_class_diagram.png", font=F_FOOT, fill=GRAY)

img.save(OUT_PATH)
print(OUT_PATH)
