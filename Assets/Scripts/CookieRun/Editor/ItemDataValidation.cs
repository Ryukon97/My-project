using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CookieRun.Editor
{
    public static class ItemDataValidation
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void RunBatch()
        {
            RunValidation(true);
        }

        public static void RunValidation(bool exitAfter = false)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project copy.");
            try
            {
                EditorSceneManager.OpenScene(MapThemeMigration.ScenePath);
                var game = Object.FindFirstObjectByType<RunnerGame>();
                Check(game != null && game.items.Length == 6, "six connected SO assets");
                Check(game.items.All(i => i != null && i.IsValid && i.previewTheme != null), "prefabs, kinds, and preview map references");
                Check(game.items.Select(i => i.kind).Distinct().Count() == 6, "unique kinds");
                Check(!ItemDataMigration.EnsureForGame(game), "migration preserves existing SOs");
                var data = game.items.ToDictionary(i => i.kind);
                Check(data[EntityKind.Jelly].score == 10 && data[EntityKind.BigJelly].score == 100, "original scores");
                Check(data[EntityKind.Potion].healAmount == game.potionHeal && data[EntityKind.Magnet].duration == game.magnetTime, "original healing/magnet");
                Check(data[EntityKind.Boost].duration == game.boostTime && data[EntityKind.Boost].speedMultiplier == game.boostSpeedScale, "original booster");
                Check(data[EntityKind.Dash].duration == game.dashTime && data[EntityKind.Dash].speedMultiplier == game.dashSpeedScale, "original dash");
                Call(game, "Awake");
                Call(game.player, "Awake");
                Call(game, "SetupThemes");
                Call(game, "ResetRun");
                Clear(game);
                foreach (var item in game.items)
                    Check(game.ResolveEntityPrefab(item.kind, 510) == item.prefab, "SO prefab resolution");

                data[EntityKind.Jelly].score = 137;
                data[EntityKind.BigJelly].score = 247;
                data[EntityKind.Potion].healAmount = 33;
                data[EntityKind.Magnet].duration = 2.75f;
                data[EntityKind.Magnet].magnetRadius = 8;
                data[EntityKind.Magnet].magnetPullSpeed = 22;
                data[EntityKind.Boost].duration = 4;
                data[EntityKind.Boost].speedMultiplier = 2.6f;
                data[EntityKind.Dash].duration = 2;
                data[EntityKind.Dash].speedMultiplier = 3;
                foreach (var kind in ItemDataMigration.Kinds)
                {
                    Set(game, "hp", 40f); Set(game, "bonusScore", 0);
                    Set(game, "magnetT", 0f); Set(game, "boostT", 0f); Set(game, "dashT", 0f);
                    Call(game, "Spawn", kind, PlayerCookie.X, game.player.HitCenter.y);
                    Check(((RunnerEntity)Entities(game)[0]).Item == data[kind], "spawn stores SO effect data");
                    Call(game, "HandleEntities", 0f, 9f);
                    Check(Entities(game).Count == 0, "pickup returns to pool");
                    switch (kind)
                    {
                        case EntityKind.Jelly: Check(Get<int>(game, "bonusScore") == 137, "SO jelly score on collision"); break;
                        case EntityKind.BigJelly: Check(Get<int>(game, "bonusScore") == 247, "SO big jelly score on collision"); break;
                        case EntityKind.Potion: Near(Get<float>(game, "hp"), 73, "SO heal on collision"); break;
                        case EntityKind.Magnet:
                            Near(Get<float>(game, "magnetT"), 2.75f, "SO magnet duration");
                            Near(Get<float>(game, "activeMagnetRadius"), 8, "SO magnet radius");
                            Near(Get<float>(game, "activeMagnetPullSpeed"), 22, "SO magnet speed");
                            float origin = PlayerCookie.X + Get<float>(game, "scroll");
                            Call(game, "Spawn", EntityKind.Jelly, origin + 7, game.player.HitCenter.y);
                            var attracted = (RunnerEntity)Entities(game)[0];
                            Call(game, "HandleEntities", 0.1f, 9f);
                            Near(attracted.transform.localPosition.x, origin + 3.9f, "SO radius/pull speed move pickups");
                            Clear(game);
                            Call(game, "Spawn", EntityKind.Jelly, origin + 9, game.player.HitCenter.y);
                            var outside = (RunnerEntity)Entities(game)[0];
                            Call(game, "HandleEntities", 0.1f, 9f);
                            Near(outside.transform.localPosition.x, origin + 9, "outside magnet radius stays still");
                            Clear(game);
                            break;
                        case EntityKind.Boost:
                        case EntityKind.Dash:
                            float multiplier = kind == EntityKind.Boost ? 2.6f : 3;
                            Set(game, "scroll", 0f); Set(game, "mapSpeed", 9f); Set(game, "speedTimer", 0f);
                            Call(game, "TickPlaying", 0.01f, false, false);
                            Near(Get<float>(game, "scroll"), 9 * multiplier * 0.01f, "SO changes actual map scroll speed");
                            Check(game.player.transform.position.x == PlayerCookie.X, "player X stays fixed");
                            Clear(game);
                            break;
                    }
                }
                Set(game, "hp", 95f); Call(game, "ApplyItem", data[EntityKind.Potion]);
                Near(Get<float>(game, "hp"), game.maxHp, "healing is capped");
                Set(game, "scroll", 0f); Set(game, "hp", 50f); Set(game, "boostT", 0f); Set(game, "dashT", 1f); Set(game, "invincibleT", 0f); Set(game, "magnetT", 0f);
                Call(game, "Spawn", EntityKind.ObstacleLow, PlayerCookie.X, RunnerGame.GroundY);
                Call(game, "HandleEntities", 0f, 9f);
                Near(Get<float>(game, "hp"), 50, "dash immunity");
                Check(Entities(game).Count == 1, "dash passes through obstacles");
                Set(game, "boostT", 1f); Set(game, "dashT", 0f);
                Call(game, "HandleEntities", 0f, 9f);
                Check(Entities(game).Count == 0, "booster breaks obstacles");

                var potion = data[EntityKind.Potion];
                var original = potion.prefab.GetComponent<SpriteRenderer>().sprite;
                potion.sprite = data[EntityKind.Jelly].prefab.GetComponent<SpriteRenderer>().sprite;
                Call(game, "Spawn", potion.kind, 10f, 0f);
                var first = (RunnerEntity)Entities(game)[0];
                Check(first.GetComponent<SpriteRenderer>().sprite == potion.sprite, "SO image override");
                Clear(game); potion.sprite = null;
                Call(game, "Spawn", potion.kind, 10f, 0f);
                Check((RunnerEntity)Entities(game)[0] == first && first.GetComponent<SpriteRenderer>().sprite == original, "pooled image resets to prefab");
                Clear(game);

                potion.patternLength = 8;
                potion.positions = new[] { new Vector2(1, 0.8f), new Vector2(4, 2.2f), new Vector2(7, 1.4f) };
                potion.guideJellies = new[] { new Vector2(2, 1), new Vector2(6, 1) };
                potion.previewTheme = game.mapThemes[3];
                Set(game, "genX", 100f);
                Call(game, "ItemPattern", potion.kind);
                Near(Get<float>(game, "genX"), 108, "SO pattern length");
                Check(Entities(game).Count == 5, "SO positions and guide jelly counts");
                for (int i = 0; i < 3; i++)
                    Check(((RunnerEntity)Entities(game)[i]).transform.localPosition == new Vector3(100 + potion.positions[i].x, RunnerGame.GroundY + potion.positions[i].y), "SO pattern coordinates " + i);
                Check(game.ThemeAt(100) == game.mapThemes[0], "preview theme does not change map rotation");
                Clear(game);
                foreach (var kind in new[] { EntityKind.Magnet, EntityKind.Boost, EntityKind.Dash }) data[kind].spawnWeight = 0;
                object[] args = { EntityKind.Jelly };
                Check(!(bool)Call(game, "TryRandomItem", args), "zero weights disable random items");
                data[EntityKind.Dash].spawnWeight = 1;
                for (int i = 0; i < 100; i++)
                {
                    args[0] = EntityKind.Jelly;
                    Check((bool)Call(game, "TryRandomItem", args) && (EntityKind)args[0] == EntityKind.Dash, "zero-weight kinds excluded");
                }

                var copy = Object.Instantiate(potion);
                game.items = new[] { potion, potion, data[EntityKind.Jelly] };
                var window = ScriptableObject.CreateInstance<ItemEditorWindow>();
                Set(window, "selected", copy);
                var serialized = new SerializedObject(game);
                Call(window, "ConnectSelected", serialized); serialized.ApplyModifiedPropertiesWithoutUndo();
                Check(game.items.Length == 2 && game.items.Count(i => i != null && i.kind == potion.kind) == 1 && game.items.Contains(copy), "connecting replaces duplicate kind slots");
                Object.DestroyImmediate(window);
                game.items = Array.Empty<ItemData>();
                Call(game, "Awake");
                Check(game.ItemFor(EntityKind.Potion) == null && game.ResolveEntityPrefab(EntityKind.Potion, 0) == potion.prefab, "legacy prefab fallback");
                Set(game, "hp", 40f); Call(game, "ApplyLegacyItem", potion.prefab);
                Near(Get<float>(game, "hp"), 40 + game.potionHeal, "legacy effects fallback");

                const string result = "ITEM_DATA_VALIDATION_PASS: six SO assets, original balance, all collision effects, scroll speed, immunity, prefab pooling/image reset, editable layout, preview theme isolation, spawn weights, catalog replacement, legacy fallback";
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/ItemDataValidation.result.txt", result);
                Debug.Log(result); if (exitAfter) EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/ItemDataValidation.result.txt", ex.ToString());
                Debug.LogException(ex); if (exitAfter) EditorApplication.Exit(1);
                else throw;
            }
        }

        static IList Entities(RunnerGame game) => Get<IList>(game, "entities");
        static void Clear(RunnerGame game) { for (int i = Entities(game).Count - 1; i >= 0; i--) Call(game, "Release", i); }
        static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        static void Near(float actual, float expected, string description) => Check(Mathf.Abs(actual - expected) < 0.001f, description + " (" + actual + " / " + expected + ")");
        static void Check(bool value, string description) { if (!value) throw new InvalidOperationException("Item test failed: " + description); }
    }
}
