using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace StudentGrowth.EditorTools
{
    public static class BattleArtImporter
    {
        private const string Folder = "Assets/Art/StudentGrowth/Battle/SourceSheets/";
        private const string ConfigPath = "Assets/Resources/StudentGrowth/Battle/BattleUIConfig.asset";
        private static readonly List<Sprite> all = new List<Sprite>();
        private static readonly List<string> manifest = new List<string>();
        private static SpriteRect Rect(string name, int x, int y, int w, int h, int height = 1024, Vector4 border = default)
        { return new SpriteRect { name = name, rect = new Rect(x, height-y-h, w, h), pivot = new Vector2(.5f,.5f), alignment = 0, border = border }; }

        [MenuItem("Tools/Student Growth/Battle/Import supplied v03 artwork")]
        public static void Import()
        {
            all.Clear(); manifest.Clear();
            Slice("battle_card_art_v01", new[] {
                Rect("card_art_basic_practice",24,112,462,370), Rect("card_art_organize_thoughts",539,119,441,358),
                Rect("card_art_browse_notes",1045,41,478,432), Rect("card_art_adjust_rhythm",22,522,454,463),
                Rect("card_art_focused_review",520,521,495,461), Rect("card_art_enter_focus",1040,537,474,447) });
            string[] icons = { "long_term_stamina","battle_stamina","action_energy","challenge_progress","block","turns",
                "status_focus","status_distraction","status_weak_defense","keyword_retain","keyword_ethereal","keyword_exhaust",
                "intent_attack","intent_multi_attack","intent_distraction","intent_weak_defense","intent_wait","challenge_target",
                "pile_draw","pile_hand","pile_discard","pile_exhaust","reward","result_complete" };
            var iconRects = new List<SpriteRect>();
            int[] centersX = {152,400,645,893,1137,1388}; int[] tops = {53,298,541,771};
            for (int i=0;i<icons.Length;i++) iconRects.Add(Rect("icon_"+icons[i],centersX[i%6]-115,tops[i/6],230,215));
            Slice("battle_icons_v01",iconRects.ToArray());
            Slice("battle_hud_v01", new[] {
                Rect("hud_progress_track",35,86,700,110,1024,new Vector4(38,30,38,30)),
                Rect("hud_stamina_track",796,86,704,110,1024,new Vector4(38,30,38,30)),
                Rect("hud_progress_fill_source",79,278,619,60), Rect("hud_stamina_fill_source",839,279,619,60),
                Rect("hud_turns_plate",142,465,420,132,1024,new Vector4(30,30,30,30)),
                Rect("hud_energy_socket",699,430,208,203),Rect("hud_block_socket",1088,409,196,236),
                Rect("hud_tooltip_panel",27,697,521,246,1024,new Vector4(38,38,38,38)),
                Rect("hud_intent_bubble",585,709,427,229), Rect("hud_notice_plate",1040,745,469,153,1024,new Vector4(32,30,32,30)),
                Rect("hud_progress_fill",118,294,538,25),Rect("hud_stamina_fill",880,295,533,24) });
            var buttons = new List<SpriteRect>();
            string[] rows = {"primary","secondary","icon","close","prev","next"};
            string[] states = {"normal","hover","pressed","disabled"};
            int[] buttonY = {40,198,347,558,704,849};
            for(int row=0;row<6;row++) for(int col=0;col<4;col++)
            {
                int cx = new[] {222,587,952,1317}[col];
                int w = row<2 ? 334 : row==2 ? 184 : 142;
                int h = row<2 ? 140 : row==2 ? 194 : 143;
                buttons.Add(Rect("btn_"+rows[row]+"_"+states[col],cx-w/2,buttonY[row],w,h,1024,row<2?new Vector4(35,35,35,35):Vector4.zero));
            }
            Slice("battle_buttons_v01",buttons.ToArray());
            Slice("battle_windows_v01",new[] {
                Rect("window_challenge_complete",26,73,579,513,1254), Rect("window_challenge_incomplete",650,72,588,514,1254),
                Rect("window_card_choice",25,674,626,490,1254),Rect("window_recovery",692,672,521,493,1254) });

            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)); AssetDatabase.Refresh();
            var config = AssetDatabase.LoadAssetAtPath<BattleUIConfig>(ConfigPath);
            if(config == null) { config = ScriptableObject.CreateInstance<BattleUIConfig>(); AssetDatabase.CreateAsset(config,ConfigPath); }
            config.artwork = all.Select(s=>new BattleUIConfig.NamedSprite {key=s.name,sprite=s}).ToArray();
            // Explicit, reviewed IDs from MvpSampleDataFactory. No display-name matching.
            config.cardArtwork = new[] {
                Binding("card_study_math_practice","card_art_basic_practice"),
                Binding("card_study_wrong_book","card_art_organize_thoughts"),
                Binding("card_study_after_class_review","card_art_browse_notes"),
                Binding("card_study_mock_exam","card_art_focused_review") };
            config.showHandAreaBorder = false;
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
            Directory.CreateDirectory(BattleP1Verification.Output);
            File.WriteAllLines(BattleP1Verification.Output+"/art-slices.tsv",new[]{"sheet\tsprite\tx\ty_bottom\twidth\theight\tborder"}.Concat(manifest));
            Debug.Log("Battle artwork: "+all.Count+" sprites, 5 original PNGs, config="+ConfigPath);
        }

        private static BattleUIConfig.CardArtBinding Binding(string cardId,string spriteName)
        { return new BattleUIConfig.CardArtBinding {cardId=cardId,sprite=all.Single(s=>s.name==spriteName)}; }
        private static void Slice(string sheet,SpriteRect[] rects)
        {
            string path=Folder+sheet+".png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Multiple;
            importer.filterMode=FilterMode.Point; importer.wrapMode=TextureWrapMode.Clamp;
            importer.mipmapEnabled=false; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency=true; importer.maxTextureSize=2048; importer.spritePixelsPerUnit=100;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            var factory=new SpriteDataProviderFactories();factory.Init();
            var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var existing=provider.GetSpriteRects().ToDictionary(s=>s.name,s=>s.spriteID);
            foreach(var rect in rects)
            {
                rect.spriteID=existing.TryGetValue(rect.name,out var id)?id:GUID.Generate();
                manifest.Add(sheet+"\t"+rect.name+"\t"+rect.rect.x+"\t"+rect.rect.y+"\t"+rect.rect.width+"\t"+rect.rect.height+"\t"+rect.border);
            }
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
            provider.Apply();importer.SaveAndReimport();
            var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            if(sprites.Length!=rects.Length) throw new InvalidOperationException(sheet+" sprite count mismatch");
            all.AddRange(sprites);
        }
    }
}
