using System.Collections.Generic;

namespace StudentGrowth
{
    public static class GameArtAssetCatalog
    {
        public const string CardReferenceRoot = "Assets/Art/CardReferences/SVG";
        public const string CardPixelRoot = "Assets/Art/CardReferences/FanworkPixel/Generated/PNG";
        public const string PixelArtRoot = "Assets/Art/Pixel";
        public const string OldUiArtRoot = PixelArtRoot + "/UI_Old/LegacyPixelUI";
        public const string CharacterPortraitRoot = "Assets/Art/CharacterReferences/Portraits";
        public const string CharacterPixelPortraitRoot = "Assets/Art/CharacterReferences/FanworkPixel/Generated/Portraits";
        public const string CharacterPixelBossRoot = "Assets/Art/CharacterReferences/FanworkPixel/Generated/Bosses";
        public const string CharacterPixelStatusRoot = "Assets/Art/CharacterReferences/FanworkPixel/Generated/Status";

        public static readonly IReadOnlyDictionary<string, string> CardImagePaths = new Dictionary<string, string>
        {
            { "card_study_after_class_review", CardPixelRoot + "/card_study_after_class_review.png" },
            { "card_study_class_focus", CardPixelRoot + "/card_study_class_focus.png" },
            { "card_study_english_words", CardPixelRoot + "/card_study_english_words.png" },
            { "card_study_math_practice", CardPixelRoot + "/card_study_math_practice.png" },
            { "card_study_mock_exam", CardPixelRoot + "/card_study_mock_exam.png" },
            { "card_study_wrong_book", CardPixelRoot + "/card_study_wrong_book.png" },
            { "card_rest_daydream", CardPixelRoot + "/card_rest_daydream.png" },
            { "card_rest_good_sleep", CardPixelRoot + "/card_rest_good_sleep.png" },
            { "card_rest_music_relax", CardPixelRoot + "/card_rest_music_relax.png" },
            { "card_rest_walk_outside", CardPixelRoot + "/card_rest_walk_outside.png" },
            { "card_social_borrow_notes", CardPixelRoot + "/card_social_borrow_notes.png" },
            { "card_social_club_activity", CardPixelRoot + "/card_social_club_activity.png" },
            { "card_social_group_discussion", CardPixelRoot + "/card_social_group_discussion.png" },
            { "card_social_help_classmate", CardPixelRoot + "/card_social_help_classmate.png" },
            { "card_family_dinner_talk", CardPixelRoot + "/card_family_dinner_talk.png" },
            { "card_family_do_housework", CardPixelRoot + "/card_family_do_housework.png" },
            { "card_family_parent_support", CardPixelRoot + "/card_family_parent_support.png" },
            { "card_family_talk_parent", CardPixelRoot + "/card_family_talk_parent.png" },
            { "card_item_lucky_pen", CardPixelRoot + "/card_item_lucky_pen.png" },
            { "card_item_old_alarm_clock", CardPixelRoot + "/card_item_old_alarm_clock.png" },
            { "card_item_school_snack", CardPixelRoot + "/card_item_school_snack.png" },
            { "card_item_warm_tea", CardPixelRoot + "/card_item_warm_tea.png" },
            { "card_item_wrong_note", CardPixelRoot + "/card_item_wrong_note.png" },
            { "card_status_distraction", CardPixelRoot + "/card_status_distraction.png" },
            { "card_status_fatigue", CardPixelRoot + "/card_status_fatigue.png" },
            { "card_status_pressure", CardPixelRoot + "/card_status_pressure.png" },
            { "card_status_procrastination", CardPixelRoot + "/card_status_procrastination.png" },
            { "card_boss_class_quiz", CardPixelRoot + "/card_boss_class_quiz.png" },
            { "card_boss_family_expectation", CardPixelRoot + "/card_boss_family_expectation.png" },
            { "card_boss_final_exam", CardPixelRoot + "/card_boss_final_exam.png" },
            { "card_boss_month_exam", CardPixelRoot + "/card_boss_month_exam.png" },
            { "card_boss_social_crisis", CardPixelRoot + "/card_boss_social_crisis.png" }
        };

        public static readonly IReadOnlyDictionary<string, string> ItemIconPaths = new Dictionary<string, string>
        {
            { "item_alarm_clock", PixelArtRoot + "/Icons/Items/icon_item_alarm_clock.png" },
            { "item_energy_drink", PixelArtRoot + "/Icons/Items/icon_item_energy_drink.png" },
            { "item_exam_ticket", PixelArtRoot + "/Icons/Items/icon_item_exam_ticket.png" },
            { "item_headphones", PixelArtRoot + "/Icons/Items/icon_item_headphones.png" },
            { "item_lucky_pen", PixelArtRoot + "/Icons/Items/icon_item_lucky_pen.png" },
            { "item_parent_note", PixelArtRoot + "/Icons/Items/icon_item_parent_note.png" },
            { "item_pencil_case", PixelArtRoot + "/Icons/Items/icon_item_pencil_case.png" },
            { "item_sticker_star", PixelArtRoot + "/Icons/Items/icon_item_sticker_star.png" },
            { "item_warm_tea", PixelArtRoot + "/Icons/Items/icon_item_warm_tea.png" },
            { "item_wrong_book", PixelArtRoot + "/Icons/Items/icon_item_wrong_book.png" }
        };

        public static readonly IReadOnlyDictionary<PlayerStatKey, string> StatIconPaths = new Dictionary<PlayerStatKey, string>
        {
            { PlayerStatKey.Chinese, PixelArtRoot + "/Icons/Stats/icon_stat_chinese.png" },
            { PlayerStatKey.Math, PixelArtRoot + "/Icons/Stats/icon_stat_math.png" },
            { PlayerStatKey.English, PixelArtRoot + "/Icons/Stats/icon_stat_english.png" },
            { PlayerStatKey.Energy, PixelArtRoot + "/Icons/Stats/icon_stat_energy.png" },
            { PlayerStatKey.MaxEnergy, PixelArtRoot + "/Icons/Stats/icon_stat_energy.png" },
            { PlayerStatKey.Pressure, PixelArtRoot + "/Icons/Stats/icon_stat_pressure.png" },
            { PlayerStatKey.Mood, PixelArtRoot + "/Icons/Stats/icon_stat_mood.png" },
            { PlayerStatKey.ParentRelation, PixelArtRoot + "/Icons/Stats/icon_stat_parent.png" },
            { PlayerStatKey.Social, PixelArtRoot + "/Icons/Stats/icon_stat_social.png" },
            { PlayerStatKey.Interest, PixelArtRoot + "/Icons/Stats/icon_stat_interest.png" },
            { PlayerStatKey.Confidence, PixelArtRoot + "/Icons/Stats/icon_stat_confidence.png" },
            { PlayerStatKey.Autonomy, PixelArtRoot + "/Icons/Stats/icon_stat_confidence.png" },
            { PlayerStatKey.Money, PixelArtRoot + "/Icons/Stats/icon_stat_money.png" }
        };

        public static readonly IReadOnlyDictionary<string, string> BackgroundPaths = new Dictionary<string, string>
        {
            { "main_menu", PixelArtRoot + "/Backgrounds/bg_mainmenu_old_monitor.png" },
            { "stage_dashboard", PixelArtRoot + "/Backgrounds/bg_stage_monitor_dashboard_rpg_map.png" },
            { "student_bedroom", PixelArtRoot + "/Backgrounds/bg_room_student_bedroom_rpg_map.png" },
            { "study_desk", PixelArtRoot + "/Backgrounds/bg_room_study_desk_rpg_map.png" },
            { "school_classroom", PixelArtRoot + "/Backgrounds/bg_school_classroom_rpg_map.png" },
            { "home_dining_room", PixelArtRoot + "/Backgrounds/bg_home_dining_room.png" },
            { "school_shop", PixelArtRoot + "/Backgrounds/bg_school_shop_rpg_map.png" },
            { "exam_room", PixelArtRoot + "/Backgrounds/bg_exam_room_rpg_map.png" }
        };

        public static readonly IReadOnlyDictionary<string, string> StageBackgroundPaths = new Dictionary<string, string>
        {
            { "stage_01_adaptation", PixelArtRoot + "/Backgrounds/bg_school_classroom_rpg_map.png" },
            { "stage_02_month_exam", PixelArtRoot + "/Backgrounds/bg_stage_monitor_dashboard_rpg_map.png" },
            { "stage_03_final_sprint", PixelArtRoot + "/Backgrounds/bg_exam_room_rpg_map.png" }
        };

        public static readonly IReadOnlyDictionary<string, string> ChallengeImagePaths = new Dictionary<string, string>
        {
            { "boss_class_quiz", CardPixelRoot + "/card_boss_class_quiz.png" },
            { "boss_month_exam", CardPixelRoot + "/card_boss_month_exam.png" },
            { "boss_final_exam", CardPixelRoot + "/card_boss_final_exam.png" },
            { "boss_family_expectation", CardPixelRoot + "/card_boss_family_expectation.png" },
            { "boss_social_crisis", CardPixelRoot + "/card_boss_social_crisis.png" }
        };

        public static readonly IReadOnlyDictionary<string, string> ChallengeIconPaths = new Dictionary<string, string>
        {
            { "boss_class_quiz", CharacterPixelBossRoot + "/char_boss_class_quiz.png" },
            { "boss_month_exam", CharacterPixelBossRoot + "/char_boss_month_exam_pressure.png" },
            { "boss_final_exam", CharacterPixelBossRoot + "/char_boss_final_exam.png" },
            { "boss_family_expectation", CharacterPixelBossRoot + "/char_boss_family_expectation.png" },
            { "boss_social_crisis", CharacterPixelBossRoot + "/char_boss_social_crisis.png" }
        };

        public static readonly IReadOnlyDictionary<string, string> UiSpritePaths = new Dictionary<string, string>
        {
            { "window_base_dark", OldUiArtRoot + "/Common/ui_window_base_dark.png" },
            { "window_base_green", OldUiArtRoot + "/Common/ui_window_base_green.png" },
            { "button_default", OldUiArtRoot + "/Common/ui_button_monitor_default.png" },
            { "button_hover", OldUiArtRoot + "/Common/ui_button_monitor_hover.png" },
            { "button_pressed", OldUiArtRoot + "/Common/ui_button_monitor_pressed.png" },
            { "button_disabled", OldUiArtRoot + "/Common/ui_button_monitor_disabled.png" },
            { "button_warning", OldUiArtRoot + "/Common/ui_button_monitor_warning.png" },
            { "activity_card_frame", OldUiArtRoot + "/Activity/ui_activity_card_frame.png" },
            { "stage_status_panel", OldUiArtRoot + "/StageHUD/ui_stage_status_panel.png" },
            { "stage_log_panel", OldUiArtRoot + "/StageHUD/ui_stage_log_panel.png" },
            { "stage_timer_panel", OldUiArtRoot + "/StageHUD/ui_stage_timer_panel.png" },
            { "boss_warning_slot", OldUiArtRoot + "/StageHUD/ui_boss_warning_slot.png" },
            { "minigame_window", OldUiArtRoot + "/MiniGame/ui_minigame_window.png" },
            { "event_window", OldUiArtRoot + "/EventDialog/ui_event_window.png" },
            { "shop_window", OldUiArtRoot + "/ShopInventory/ui_shop_window.png" },
            { "inventory_window", OldUiArtRoot + "/ShopInventory/ui_inventory_window.png" },
            { "stage_result_window", OldUiArtRoot + "/ResultEnding/ui_stage_result_window.png" }
        };

        public static readonly IReadOnlyDictionary<string, string> EffectSpritePaths = new Dictionary<string, string>
        {
            { "pressure_warning_frame", PixelArtRoot + "/FX/Alert/fx_pressure_warning_frame.png" },
            { "boss_warning_flash", PixelArtRoot + "/FX/Boss/fx_boss_warning_flash.png" },
            { "crt_scanline_overlay", PixelArtRoot + "/FX/CRT/fx_crt_scanline_overlay.png" },
            { "screen_noise", PixelArtRoot + "/FX/CRT/fx_screen_noise.png" },
            { "signal_lost", PixelArtRoot + "/FX/CRT/fx_signal_lost.png" },
            { "reward_popup", PixelArtRoot + "/FX/Reward/fx_reward_popup.png" }
        };

        public static readonly IReadOnlyDictionary<string, string> CharacterPortraitPaths = new Dictionary<string, string>
        {
            { "player_student_v01", CharacterPixelPortraitRoot + "/char_player_student_avatar_pixel.png" },
            { "char_arcade_owner", CharacterPixelPortraitRoot + "/char_arcade_owner.png" },
            { "char_classmate_art_student", CharacterPixelPortraitRoot + "/char_classmate_art_student.png" },
            { "char_classmate_procrastinator", CharacterPixelPortraitRoot + "/char_classmate_procrastinator.png" },
            { "char_classmate_social", CharacterPixelPortraitRoot + "/char_classmate_social.png" },
            { "char_classmate_sports_rep", CharacterPixelPortraitRoot + "/char_classmate_sports_rep.png" },
            { "char_classmate_top_student", CharacterPixelPortraitRoot + "/char_classmate_top_student.png" },
            { "char_club_leader", CharacterPixelPortraitRoot + "/char_club_leader.png" },
            { "char_family_grandmother", CharacterPixelPortraitRoot + "/char_family_grandmother.png" },
            { "char_growth_archivist", CharacterPixelPortraitRoot + "/char_growth_archivist.png" },
            { "char_neighbor_aunt", CharacterPixelPortraitRoot + "/char_neighbor_aunt.png" },
            { "char_parent_anxious_mother", CharacterPixelPortraitRoot + "/char_parent_anxious_mother.png" },
            { "char_parent_strict_father", CharacterPixelPortraitRoot + "/char_parent_strict_father.png" },
            { "char_parent_supportive", CharacterPixelPortraitRoot + "/char_parent_supportive.png" },
            { "char_player_student", CharacterPixelPortraitRoot + "/char_player_student_avatar_pixel.png" },
            { "char_shop_owner", CharacterPixelPortraitRoot + "/char_shop_owner.png" },
            { "char_system_observer", CharacterPixelPortraitRoot + "/char_system_observer.png" },
            { "char_teacher_counselor", CharacterPixelPortraitRoot + "/char_teacher_counselor.png" },
            { "char_teacher_english", CharacterPixelPortraitRoot + "/char_teacher_english.png" },
            { "char_teacher_head", CharacterPixelPortraitRoot + "/char_teacher_head.png" },
            { "char_teacher_math", CharacterPixelPortraitRoot + "/char_teacher_math.png" },
            { "char_transfer_student", CharacterPixelPortraitRoot + "/char_transfer_student.png" },
            { "char_tutor", CharacterPixelPortraitRoot + "/char_tutor.png" },
            { "char_status_distraction", CharacterPixelStatusRoot + "/char_status_distraction.png" },
            { "char_status_fatigue", CharacterPixelStatusRoot + "/char_status_fatigue.png" },
            { "char_status_pressure", CharacterPixelStatusRoot + "/char_status_pressure.png" },
            { "char_status_procrastination", CharacterPixelStatusRoot + "/char_status_procrastination.png" },
            { "char_status_self_discipline", CharacterPixelStatusRoot + "/char_status_self_discipline.png" }
        };

        public static bool TryGetCardImagePath(string cardId, out string path)
        {
            return CardImagePaths.TryGetValue(cardId, out path);
        }

        public static bool TryGetItemIconPath(string itemId, out string path)
        {
            return ItemIconPaths.TryGetValue(itemId, out path);
        }

        public static bool TryGetStatIconPath(PlayerStatKey statKey, out string path)
        {
            return StatIconPaths.TryGetValue(statKey, out path);
        }

        public static bool TryGetStageBackgroundPath(string stageId, out string path)
        {
            return StageBackgroundPaths.TryGetValue(stageId, out path);
        }

        public static bool TryGetChallengeImagePath(string challengeId, out string path)
        {
            return ChallengeImagePaths.TryGetValue(challengeId, out path);
        }

        public static bool TryGetChallengeIconPath(string challengeId, out string path)
        {
            return ChallengeIconPaths.TryGetValue(challengeId, out path);
        }
    }
}
