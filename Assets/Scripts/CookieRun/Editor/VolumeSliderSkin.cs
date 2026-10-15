using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CookieRun
{
    public static class VolumeSliderSkin
    {
        const string ArtPath = "Assets/3.Sprite/TopHUD/Layers/";
        [MenuItem("CookieRun/Validate Volume Sliders")]
        static void Validate()
        {
            var game = Object.FindFirstObjectByType<RunnerGame>();
            if (!Application.isPlaying || game == null) return;
            game.Retry();
            game.Pause();
            Canvas.ForceUpdateCanvases();
            foreach (var slider in new[] { game.hud.settingsPanel.bgmSlider, game.hud.settingsPanel.sfxSlider })
            {
                float original = slider.value;
                var track = (RectTransform)slider.handleRect.parent;
                var corners = new Vector3[4];
                track.GetWorldCorners(corners);
                foreach (float value in new[] { 0.1f, 0.5f, 0.9f })
                {
                    var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
                    pointer.position = RectTransformUtility.WorldToScreenPoint(null, Vector3.Lerp((corners[0] + corners[1]) / 2, (corners[2] + corners[3]) / 2, value));
                    slider.OnPointerDown(pointer);
                    if (Mathf.Abs(slider.value - value) > 0.03f)
                        throw new System.Exception("Volume track click failed: " + slider.name);
                    slider.OnPointerUp(pointer);
                }
                slider.value = original;
                if (!slider.transform.Find("VolumeFrame") || !slider.transform.Find("Background").GetComponent<Image>().raycastTarget)
                    throw new System.Exception("Volume layers/input missing");
            }
            Debug.Log("VOLUME_SLIDER_VALIDATION_PASS: both tracks respond at 10/50/90 percent; original volumes restored");
        }
        [MenuItem("CookieRun/Apply Volume Slider Skin")]
        public static void Apply()
        {
            CreateControlArt();
            foreach (string name in new[] { "SettingsPanel", "TitleUI", "GameHUD" })
            {
                string path = "Assets/2.Prefab/UI/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var slider in root.GetComponentsInChildren<Slider>(true)) Style(slider);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("VOLUME_SLIDER_SKIN_APPLIED");
        }

        public static void Style(Slider slider)
        {
            var background = slider.transform.Find("Background").GetComponent<Image>();
            Set(background, "Background");
            background.raycastTarget = true;
            Stretch(background.rectTransform, slider.transform, new Vector2(0, 0));
            var fillArea = (RectTransform)slider.fillRect.parent;
            Stretch(fillArea, slider.transform, new Vector2(12, 10));
            var fill = slider.fillRect.GetComponent<Image>();
            Set(fill, "Fill");
            fill.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/3.Sprite/CasualUI/VolumeOrange.png");
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            fill.type = Image.Type.Sliced;

            var frameTransform = slider.transform.Find("VolumeFrame");
            Image frame;
            if (frameTransform == null)
                frame = new GameObject("VolumeFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            else frame = frameTransform.GetComponent<Image>();
            Set(frame, "Frame");
            Stretch(frame.rectTransform, slider.transform, Vector2.zero);
            frame.fillCenter = false;
            frame.transform.SetAsLastSibling();

            var handle = slider.handleRect.GetComponent<Image>();
            handle.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/3.Sprite/CasualUI/VolumeCircle.png");
            handle.color = Color.white;
            handle.type = Image.Type.Simple;
            handle.preserveAspect = true;
            handle.raycastTarget = true;
            handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(slider.normalizedValue, 0.5f);
            handle.rectTransform.sizeDelta = new Vector2(54, 54);
            handle.rectTransform.anchoredPosition = Vector2.zero;
            handle.transform.parent.SetAsLastSibling();
            slider.targetGraphic = background;
            slider.transition = Selectable.Transition.None;
            var rt = (RectTransform)slider.transform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 62);
        }

        static void CreateControlArt()
        {
            foreach (bool circle in new[] { false, true })
            {
                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f);
                    float distance = circle ? point.magnitude : new Vector2(Mathf.Max(Mathf.Abs(point.x) - 35, 0), Mathf.Max(Mathf.Abs(point.y) - 35, 0)).magnitude;
                    float radius = circle ? 63 : 28;
                    Color color = circle
                        ? Color.Lerp(new Color(1, 0.79f, 0.35f), new Color(1, 0.99f, 0.85f), y / 127f)
                        : Color.Lerp(new Color(0.87f, 0.28f, 0.025f), new Color(1, 0.69f, 0.16f), y / 127f);
                    if (circle && distance > 56) color = new Color(0.35f, 0.16f, 0.055f);
                    else if (circle && distance > 51) color = new Color(1, 0.78f, 0.32f);
                    if (!circle && y > 91 && y < 109 && distance < 24) color = Color.Lerp(color, Color.white, 0.32f);
                    color.a = Mathf.Clamp01(radius - distance);
                    texture.SetPixel(x, y, color);
                }
                texture.Apply();
                string path = "Assets/3.Sprite/CasualUI/" + (circle ? "VolumeCircle" : "VolumeOrange") + ".png";
                System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.spritePixelsPerUnit = 100;
                importer.spriteBorder = circle ? Vector4.zero : new Vector4(30, 30, 30, 30);
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        static void Set(Image image, string name)
        {
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + name + ".png");
            image.color = Color.white;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
        }

        static void Stretch(RectTransform rt, Transform parent, Vector2 inset)
        {
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = inset;
            rt.offsetMax = -inset;
            rt.localScale = Vector3.one;
        }
    }
}
