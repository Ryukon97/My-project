using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CookieRun.Editor
{
    public static class MapPreviewValidation
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode copy.");
            try
            {
                EditorSceneManager.OpenScene(MapThemeMigration.ScenePath);
                var game = RunnerMapPreview.FindLoadedGame();
                Call(game, "Awake"); Call(game.player, "Awake"); Call(game, "SetupThemes"); Call(game, "ResetRun");
                ValidateGround(game);
                Directory.CreateDirectory("Logs/MapPreviewQA");
                using (var preview = new RunnerMapPreview())
                {
                    foreach (var pair in new[] { (game.mapThemes[0], "basic"), (game.mapThemes[1], "forest"), (game.bonusMap, "bonus") })
                    {
                        var theme = pair.Item1;
                        Set(game, "activeThemes", new[] { theme });
                        Set(game, "bonusT", theme.bonusOnly ? 10f : 0f);
                        Set(game, "activeBonusTheme", theme.bonusOnly ? theme : null);
                        Set(game, "themeIndex", -1);
                        Call(game, "UpdateTheme", 0f);
                        Stage(game, theme, 16f / 9f);
                        var image = preview.Capture(960, 540, theme, game);
                        var runtime = Capture(game, image.width, image.height);
                        Save(image, pair.Item2 + "-preview"); Save(runtime, pair.Item2 + "-runtime");
                        Compare(image, runtime, pair.Item2);
                        Object.DestroyImmediate(image); Object.DestroyImmediate(runtime);
                    }
                    var adjusted = Object.Instantiate(game.mapThemes[2]);
                    try
                    {
                        // Exercise the exact SerializedObject fields used by the map editor.
                        var serialized = new SerializedObject(adjusted);
                        serialized.FindProperty("upperObstacleYOffset").floatValue = 0.45f;
                        serialized.FindProperty("lowerObstacleYOffset").floatValue = -0.25f;
                        serialized.FindProperty("tallObstacleYOffset").floatValue = -0.4f;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        Set(game, "bonusT", 0f); Set(game, "activeBonusTheme", null);
                        Set(game, "activeThemes", new[] { adjusted }); Set(game, "themeIndex", -1); Call(game, "UpdateTheme", 0f);
                        Stage(game, adjusted, 16f / 9f);
                        var shifted = preview.Capture(960, 540, adjusted, game);
                        var shiftedRuntime = Capture(game, shifted.width, shifted.height);
                        Save(shifted, "obstacle-offsets-preview"); Save(shiftedRuntime, "obstacle-offsets-runtime");
                        Compare(shifted, shiftedRuntime, "obstacle-offsets");
                        Object.DestroyImmediate(shifted); Object.DestroyImmediate(shiftedRuntime);
                    }
                    finally { Object.DestroyImmediate(adjusted); }
                    var basic = game.mapThemes[0];
                    Set(game, "bonusT", 0f); Set(game, "activeBonusTheme", null);
                    Set(game, "activeThemes", new[] { basic }); Set(game, "themeIndex", -1); Call(game, "UpdateTheme", 0f);
                    Stage(game, basic, 9f / 16f);
                    var portrait = preview.Capture(360, 640, basic, game);
                    var portraitRuntime = Capture(game, portrait.width, portrait.height);
                    Save(portrait, "portrait-preview"); Save(portraitRuntime, "portrait-runtime");
                    Compare(portrait, portraitRuntime, "portrait");
                    Object.DestroyImmediate(portrait); Object.DestroyImmediate(portraitRuntime);

                    Stage(game, basic, 16f / 9f, true);
                    var item = game.ItemFor(EntityKind.Potion);
                    var items = preview.Capture(960, 540, basic, game, item, false);
                    var itemRuntime = Capture(game, items.width, items.height);
                    Save(items, "items-preview"); Save(itemRuntime, "items-runtime");
                    Compare(items, itemRuntime, "items");
                    Object.DestroyImmediate(items); Object.DestroyImmediate(itemRuntime);

                    var saved = preview.Capture(960, 540, basic, null, item, false);
                    Check(RunnerMapPreview.FindLoadedGame() == game, "read-only source preview is not exposed as the live game");
                    Object.DestroyImmediate(saved);
                }
                MapThemeValidation.RunValidation();
                const string result = "MAP_PREVIEW_VALIDATION_PASS: single vertical ground tile, ground top/segment alignment, custom pivots/scales, real SpriteRenderer previews, matching camera/aspect/projection; map, adjusted obstacle Y offsets, item, portrait and bonus rendering compared with runtime; map regression passed";
                File.WriteAllText("Logs/MapPreviewValidation.result.txt", result); Debug.Log(result); EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/MapPreviewValidation.result.txt", ex.ToString());
                Debug.LogException(ex); EditorApplication.Exit(1);
            }
        }

        static void ValidateGround(RunnerGame game)
        {
            foreach (var value in Get<IList>(game, "grounds"))
            {
                var renderer = GroundObject(value).GetComponent<SpriteRenderer>();
                Near(renderer.size.y, renderer.sprite.bounds.size.y, "one native vertical tile");
                Near(renderer.bounds.max.y, RunnerGame.GroundY, "ground top matches player baseline");
                Near(renderer.bounds.min.x, (float)value.GetType().GetField("x0").GetValue(value), "ground starts at segment boundary");
            }
            var texture = new Texture2D(32, 16);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 16), new Vector2(0.3f, 0.5f), 8);
            var parent = new GameObject("GroundTestParent"); parent.transform.position = new Vector3(-30, 2);
            var test = Object.Instantiate(game.groundPrefab, parent.transform);
            test.transform.localScale = new Vector3(2, 1.5f, 1);
            RunnerMapVisuals.ConfigureGround(test, sprite, 12, 7, RunnerGame.GroundY);
            Near(test.bounds.min.x, -18, "custom pivot/scaled ground left");
            Near(test.bounds.max.y, RunnerGame.GroundY + 2, "custom pivot/scaled ground top");
            Near(test.bounds.size.x, 7, "ground horizontal segment length");
            Near(test.size.y, 2, "custom sprite native vertical size");
            Object.DestroyImmediate(parent); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture);
            var frame = new Rect(0, 0, 960, 540);
            var projected = RunnerMapPreview.Project(frame, 16f / 9f, new Vector2(PlayerCookie.X, RunnerGame.GroundY));
            Near(projected.y, 405, "16:9 ground projection");
            Near(projected.x, 210, "16:9 character projection");
        }

        static void Stage(RunnerGame game, MapThemeData theme, float aspect, bool items = false)
        {
            var entities = Get<IList>(game, "entities");
            for (int i = entities.Count - 1; i >= 0; i--) Call(game, "Release", i);
            var grounds = Get<IList>(game, "grounds");
            foreach (var value in grounds) Object.DestroyImmediate(GroundObject(value));
            grounds.Clear();
            Set(game, "scroll", 0f); Set(game, "genX", -16f); Set(game, "shake", 0f);
            game.world.localPosition = Vector3.zero;
            game.player.ResetState();
            Call(game, "UpdatePlayerTint");
            Call(game, "AddGround", 30f);
            game.cam.aspect = aspect; Call(game, "UpdateCamera", 0f);
            if (items)
            {
                var item = game.ItemFor(EntityKind.Potion);
                foreach (var point in item.positions) Call(game, "Spawn", item.kind, RunnerMapPreview.PatternStart + point.x, RunnerGame.GroundY + point.y);
                foreach (var point in item.guideJellies) Call(game, "Spawn", EntityKind.Jelly, RunnerMapPreview.PatternStart + point.x, RunnerGame.GroundY + point.y);
            }
            else if (theme.bonusOnly)
                for (int i = 0; i < 8; i++) for (int row = 0; row < 3; row++)
                    Call(game, "Spawn", EntityKind.Jelly, RunnerMapPreview.PatternStart + 0.4f + i * 0.75f, RunnerGame.GroundY + 0.7f + row * 1.1f);
            else
            {
                Call(game, "Spawn", EntityKind.ObstacleLow, 0f, RunnerGame.GroundY);
                Call(game, "Spawn", EntityKind.ObstacleTall, game.cam.orthographicSize * aspect - 1.7f, RunnerGame.GroundY);
                for (int i = 0; i < 3; i++) Call(game, "Spawn", EntityKind.ObstacleHang, 2.2f + i * Mathf.Max(1.4f, game.hangObstacleWidth), RunnerGame.GroundY + game.hangClearance);
            }
        }

        static Texture2D Capture(RunnerGame game, int width, int height)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            game.cam.targetTexture = target;
            game.cam.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            RenderTexture.active = previous; game.cam.targetTexture = null;
            target.Release(); Object.DestroyImmediate(target);
            return image;
        }

        static void Compare(Texture2D preview, Texture2D runtime, string name)
        {
            var a = preview.GetPixels32(); var b = runtime.GetPixels32();
            Check(a.Length == b.Length, "capture size " + name);
            long error = 0;
            for (int i = 0; i < a.Length; i++) error += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
            double mean = error / (a.Length * 3.0);
            Debug.Log("PREVIEW_RUNTIME_PIXEL_ERROR " + name + ": " + mean.ToString("F4"));
            Check(mean < 3, "native preview/runtime rendering agreement " + name + " (" + mean + ")");
        }

        static void Save(Texture2D image, string name) => File.WriteAllBytes("Logs/MapPreviewQA/" + name + ".png", image.EncodeToPNG());
        static GameObject GroundObject(object ground) => (GameObject)ground.GetType().GetField("go").GetValue(ground);
        static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        static void Near(float actual, float expected, string name) => Check(Mathf.Abs(actual - expected) < 0.001f, name + " (" + actual + "/" + expected + ")");
        static void Check(bool value, string name) { if (!value) throw new InvalidOperationException("Map preview test failed: " + name); }
    }
}
