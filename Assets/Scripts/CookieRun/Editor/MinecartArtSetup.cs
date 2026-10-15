using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CookieRun.Editor
{
    public static class MinecartArtSetup
    {
        public const string SheetPath = "Assets/3.Sprite/MinecartExplorer/MinerCartSheet.png";
        public const string PrefabPath = "Assets/2.Prefab/Characters/Cookie.prefab";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly string[] Groups = { "Ride", "Jump", "Duck", "Exhaustion" };

        [MenuItem("Tools/CookieRun/광산 카트 캐릭터 이미지 적용")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            ImportSheet();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().ToDictionary(s => s.name);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var player = root.GetComponent<PlayerCookie>();
                player.ridingFrames = Frames(sprites, "Ride");
                player.ridingFramesPerSecond = 8f;
                player.doubleJumpFrames = Frames(sprites, "Jump");
                player.slideFrames = Frames(sprites, "Duck");
                player.exhaustionFrames = Frames(sprites, "Exhaustion");
                player.slideSprite = player.slideFrames[1];
                player.slideSpritePosition = Vector2.zero;
                player.body.localPosition = Vector3.zero;
                player.body.localRotation = Quaternion.identity;
                player.body.localScale = Vector3.one;
                player.body.GetComponent<SpriteRenderer>().sprite = player.ridingFrames[0];
                foreach (var leg in new[] { player.legBack, player.legFront })
                {
                    var renderer = leg.GetComponent<SpriteRenderer>();
                    renderer.sprite = null;
                    renderer.enabled = false;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("MINECART_ART_APPLIED: 16 sprites, shared character prefab; gameplay values preserved");
        }

        static Sprite[] Frames(Dictionary<string, Sprite> sprites, string group) =>
            Enumerable.Range(0, 4).Select(i => sprites[group + "_" + i]).ToArray();

        static void ImportSheet()
        {
            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
            if (importer == null) throw new InvalidOperationException("Minecart sheet is missing.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            var regions = Regions(texture);
            if (regions.Count != 16) throw new InvalidOperationException("Expected 16 separate sprites, found " + regions.Count);
            var sorted = regions.OrderByDescending(r => r.center.y).ToArray();
            var ordered = new List<Rect>();
            for (int row = 0; row < 4; row++) ordered.AddRange(sorted.Skip(row * 4).Take(4).OrderBy(r => r.center.x));
            importer.spritePixelsPerUnit = ordered.Take(4).Max(r => r.height) / 1.7f;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            var metadata = new SpriteMetaData[16];
            for (int i = 0; i < 16; i++)
            {
                var raw = ordered[i];
                float left = Mathf.Max(0, raw.xMin - 2), bottom = Mathf.Max(0, raw.yMin - 2);
                var rect = Rect.MinMaxRect(left, bottom, Mathf.Min(texture.width, raw.xMax + 2), Mathf.Min(texture.height, raw.yMax + 2));
                metadata[i] = new SpriteMetaData
                {
                    name = Groups[i / 4] + "_" + i % 4,
                    rect = rect,
                    alignment = (int)SpriteAlignment.Custom,
                    // Opaque wheel bottoms, rather than cell edges, share the ground baseline.
                    pivot = new Vector2(0.5f, (raw.yMin - rect.yMin) / rect.height)
                };
            }
#pragma warning disable CS0618 // Supported by the project's Unity importer; avoids a Sprite Editor package dependency.
            importer.spritesheet = metadata;
#pragma warning restore CS0618
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        static List<Rect> Regions(Texture2D texture)
        {
            // Read alpha only; the generated PNG is never cropped, rescaled or repainted.
            var pixels = texture.GetPixels32();
            var visited = new bool[pixels.Length];
            var pending = new Queue<int>();
            var regions = new List<Rect>();
            int width = texture.width, height = texture.height;
            for (int seed = 0; seed < pixels.Length; seed++)
            {
                if (visited[seed] || pixels[seed].a <= 32) continue;
                visited[seed] = true;
                pending.Enqueue(seed);
                int minX = width, minY = height, maxX = 0, maxY = 0, area = 0;
                while (pending.Count > 0)
                {
                    int index = pending.Dequeue(), x = index % width, y = index / width;
                    minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                    area++;
                    if (x > 0) Visit(index - 1, pixels, visited, pending);
                    if (x + 1 < width) Visit(index + 1, pixels, visited, pending);
                    if (y > 0) Visit(index - width, pixels, visited, pending);
                    if (y + 1 < height) Visit(index + width, pixels, visited, pending);
                }
                if (area > 1024) regions.Add(new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));
            }
            return regions;
        }

        static void Visit(int index, Color32[] pixels, bool[] visited, Queue<int> queue)
        {
            if (visited[index] || pixels[index].a <= 32) return;
            visited[index] = true;
            queue.Enqueue(index);
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode copy.");
            try
            {
                Apply();
                Validate();
                const string result = "MINECART_ART_VALIDATION_PASS: 16 transparent sprites, Title/MainGame shared prefab, consistent scale, fixed player X, ride/jump/double jump/duck/exhaustion/retry, no legacy legs, unchanged gameplay settings";
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/MinecartArtValidation.result.txt", result);
                Debug.Log(result); EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/MinecartArtValidation.result.txt", ex.ToString());
                Debug.LogException(ex); EditorApplication.Exit(1);
            }
        }

        static void Validate()
        {
            Check(AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().Count() == 16, "16 atlas sprites");
            EditorSceneManager.OpenScene("Assets/1.Scene/Title.unity");
            var titlePlayer = Object.FindFirstObjectByType<PlayerCookie>();
            Check(titlePlayer != null && titlePlayer.body.localPosition == Vector3.zero, "Title avatar uses bottom-aligned cart");
            Check(AssetDatabase.GetAssetPath(titlePlayer.body.GetComponent<SpriteRenderer>().sprite) == SheetPath, "Title minecart artwork");
            Check(!titlePlayer.legBack.GetComponent<SpriteRenderer>().enabled && !titlePlayer.legFront.GetComponent<SpriteRenderer>().enabled, "Title has no old legs");
            EditorSceneManager.OpenScene("Assets/1.Scene/MainGame.unity");
            var game = Object.FindFirstObjectByType<RunnerGame>();
            var player = game.player;
            Check(player.ridingFrames.Length == 4 && player.doubleJumpFrames.Length == 4 && player.slideFrames.Length == 4 && player.exhaustionFrames.Length == 4, "all frame arrays");
            foreach (var frame in player.ridingFrames.Concat(player.doubleJumpFrames).Concat(player.slideFrames).Concat(player.exhaustionFrames))
                Check(frame != null && AssetDatabase.GetAssetPath(frame) == SheetPath, "all states use new character");
            Check(player.gravity == -48 && player.jumpVelocity == 15.5f && player.doubleJumpVelocity == 14, "jump physics unchanged");
            Check(player.standHitCenter == new Vector2(0, 0.8f) && player.standHitHalf == new Vector2(0.33f, 0.76f) &&
                player.slideHitCenter == new Vector2(0, 0.64f) && player.slideHitHalf == new Vector2(0.95f, 0.6f), "collision sizes unchanged");
            float width = player.ridingFrames[0].bounds.size.x;
            foreach (var frame in player.ridingFrames.Concat(player.slideFrames).Concat(player.exhaustionFrames))
                Check(Mathf.Abs(frame.bounds.size.x - width) < width * 0.15f, "cart proportions remain consistent");
            Check(player.ridingFrames.Max(s => s.bounds.size.y) <= 1.75f, "standing visual size");
            Check(player.slideFrames[1].bounds.max.y < game.hangClearance, "duck fits below upper obstacles");
            Call(game, "Awake"); Call(player, "Awake"); Call(game, "SetupThemes"); Call(game, "ResetRun");
            var renderer = player.body.GetComponent<SpriteRenderer>();
            Check(renderer.sprite == player.ridingFrames[0] && player.body.localScale == Vector3.one, "initial riding pose");
            player.Tick(0.13f, false, false, true);
            Check(renderer.sprite == player.ridingFrames[1], "riding frames cycle");
            player.Tick(0.02f, true, false, true);
            Check(!player.Grounded && renderer.sprite == player.doubleJumpFrames[0], "first jump artwork");
            player.Tick(0.02f, true, false, true);
            Check(renderer.sprite == player.doubleJumpFrames[0], "second jump artwork");
            player.Tick(0.12f, false, false, true);
            Check(renderer.sprite == player.doubleJumpFrames[1], "second jump advances");
            player.ResetState();
            player.Tick(0.02f, false, true, true);
            Check(player.Sliding && renderer.sprite == player.slideFrames[1], "duck pose");
            for (int i = 0; i < 20; i++)
            {
                player.Tick(0.05f, false, true, true);
                Check(renderer.sprite == player.slideFrames[1] && player.body.localScale == Vector3.one, "duck stays fixed without deformation");
            }
            player.Tick(0.02f, true, false, true);
            Check(!player.Sliding && renderer.sprite == player.doubleJumpFrames[0], "jump exits duck");
            player.ResetState();
            Call(game, "BeginDeath", 0);
            Check(renderer.sprite == player.exhaustionFrames[0] && !game.hud.gameOverPanel.activeSelf, "exhaustion starts before results");
            player.TickExhaustion(0.5f);
            Check(renderer.sprite == player.exhaustionFrames[1], "exhaustion advances");
            player.TickExhaustion(player.exhaustionDuration - 0.6f);
            Check(!player.ExhaustionComplete && !game.hud.gameOverPanel.activeSelf, "exhaustion delay preserved");
            player.TickExhaustion(0.11f);
            Check(player.ExhaustionComplete && renderer.sprite == player.exhaustionFrames[3], "final exhausted pose");
            Call(game, "ResetRun");
            Check(renderer.sprite == player.ridingFrames[0] && player.Grounded && !player.Sliding && !game.hud.gameOverPanel.activeSelf, "retry restores riding pose");
            Check(!player.legBack.GetComponent<SpriteRenderer>().enabled && !player.legFront.GetComponent<SpriteRenderer>().enabled, "no old legs in gameplay");
            Check(player.transform.position.x == PlayerCookie.X && player.body.localScale == Vector3.one, "fixed X and no image squashing");
        }

        static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("Minecart art test failed: " + message); }
    }
}
