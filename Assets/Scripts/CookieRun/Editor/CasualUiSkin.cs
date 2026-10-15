using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CookieRun
{
    // Reapply from the menu after changing the source art. Existing events and references survive.
    public static class CasualUiSkin
    {
        const string Dir = "Assets/3.Sprite/CasualUI/";
        static readonly string[] Names = { "Green", "Gold", "Coral", "Panel", "RoundGreen", "RoundBrown", "Plaque", "Knob" };
        static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Dir + name + ".png");

        [MenuItem("CookieRun/Apply Casual UI Skin")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            atlas.LoadImage(File.ReadAllBytes(Dir + "Atlas.png"));
            int cw = atlas.width / 4, ch = atlas.height / 2;
            for (int i = 0; i < Names.Length; i++)
            {
                // Keep replacement art and its carefully authored slice borders.
                if (File.Exists(Dir + Names[i] + ".png")) continue;
                var pixels = atlas.GetPixels((i % 4) * cw, (1 - i / 4) * ch, cw, ch);
                int minX = cw, minY = ch, maxX = 0, maxY = 0;
                for (int y = 0; y < ch; y++) for (int x = 0; x < cw; x++)
                    if (pixels[y * cw + x].a > 0.08f) { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
                int w = maxX - minX + 1, h = maxY - minY + 1;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var cropped = new Color[w * h];
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) cropped[y * w + x] = pixels[(y + minY) * cw + x + minX];
                tex.SetPixels(cropped); tex.Apply();
                string path = Dir + Names[i] + ".png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spriteBorder = i == 4 || i == 5 || i == 7 ? Vector4.zero : new Vector4(w * .24f, h * .35f, w * .24f, h * .35f);
                importer.SaveAndReimport();
            }
            Object.DestroyImmediate(atlas);
            foreach (string name in new[] { "SettingsPanel", "RankingPanel", "TitleUI", "GameHUD" }) Skin(name);
            AssetDatabase.SaveAssets();
            Debug.Log("[CasualUI] Applied to all four UI prefabs.");
        }

        static void Set(Image image, string art, bool sliced = true)
        {
            if (image == null) return;
            image.sprite = Art(art); image.color = Color.white;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        }

        static void Skin(string name)
        {
            string path = "Assets/2.Prefab/UI/" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/CasualKoreanBold.ttf");
                foreach (var text in root.GetComponentsInChildren<Text>(true))
                {
                    text.font = font;
                    text.color = new Color(1, .96f, .84f);
                    var outline = text.GetComponent<Outline>();
                    if (outline != null) { outline.effectColor = new Color(.24f, .12f, .06f); outline.effectDistance = new Vector2(2, -2); }
                    if (text.name == "BgmLabel" || text.name == "SfxLabel" || text.name == "List")
                    { text.color = new Color(.26f, .14f, .08f); if (outline != null) outline.effectDistance = Vector2.zero; }
                }
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                {
                    if (image.name == "Window") Set(image, "Panel");
                }
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    if (button.name == "TapArea") continue;
                    if (button.name == "PauseButton") continue;
                    bool round = button.name == "XButton";
                    string art = round ? "RoundBrown" : "Green";
                    if (button.name == "SettingsButton" || button.name == "RankingButton") art = "Plaque";
                    if (button.name == "QuitButton" || button.name == "ConfirmYesButton" || button.name == "XButton") art = round ? "RoundBrown" : "Coral";
                    if (button.name == "TitleButton") art = "Gold";
                    Set(button.GetComponent<Image>(), art, !round);
                    var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1, 1, .9f); colors.pressedColor = new Color(.78f, .8f, .7f); button.colors = colors;
                }
                foreach (var hold in root.GetComponentsInChildren<HoldButton>(true)) Set(hold.GetComponent<Image>(), "RoundGreen", false);
                foreach (var slider in root.GetComponentsInChildren<Slider>(true))
                {
                    VolumeSliderSkin.Style(slider);
                }
                var title = root.GetComponent<TitleScreen>();
                if (title != null)
                {
                    var heading = root.transform.Find("Title").GetComponent<Text>();
                    heading.text = "Juwel\nRUN"; heading.fontSize = 138;
                    heading.rectTransform.sizeDelta = new Vector2(850, 340);
                    heading.rectTransform.anchoredPosition = new Vector2(0, 200);
                    heading.color = new Color(1, .77f, .3f);
                    title.bestText.rectTransform.anchoredPosition = Vector2.zero;
                    var rt = (RectTransform)title.startButton.transform;
                    rt.sizeDelta = new Vector2(580, 130); rt.anchoredPosition = Vector2.zero;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
