using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CookieRun.Editor
{
    public static class MapThemeValidation
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        // Run in a disposable project copy so tests never change the user's open scene.
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
                var game = UnityEngine.Object.FindFirstObjectByType<RunnerGame>();
                Check(game != null, "MainGame RunnerGame");
                Check(game.mapThemes.Length == 5, "five SO themes");
                foreach (var data in game.mapThemes)
                {
                    Check(data != null && data.background != null && data.ground != null, "background/ground references");
                    foreach (EntityKind kind in new[] { EntityKind.ObstacleHang, EntityKind.ObstacleLow, EntityKind.ObstacleTall })
                        Check(data.ObstacleFor(kind) != null && data.ObstacleFor(kind).kind == kind, "obstacle references/kinds");
                }
                Call(game, "Awake");
                Call(game.player, "Awake");
                Call(game, "SetupThemes");
                Call(game, "ResetRun");
                Check(game.ThemeAt(-16) == game.mapThemes[0], "negative start distance");
                Check(game.ThemeAt(499.99f) == game.mapThemes[0], "before boundary");
                for (int i = 0; i < 10; i++)
                {
                    float distance = i * 500f;
                    var expected = game.mapThemes[i % 5];
                    Check(game.ThemeAt(distance) == expected, "500m rotation " + i);
                    foreach (EntityKind kind in new[] { EntityKind.ObstacleHang, EntityKind.ObstacleLow, EntityKind.ObstacleTall })
                        Check(game.ResolveEntityPrefab(kind, distance) == expected.ObstacleFor(kind), "themed prefab selection");
                }
                var list = (IList)typeof(RunnerGame).GetField("entities", Private).GetValue(game);
                for (int i = list.Count - 1; i >= 0; i--) Call(game, "Release", i);
                ValidateObstacleOffsets(game, list);
                foreach (EntityKind kind in new[] { EntityKind.ObstacleHang, EntityKind.ObstacleLow, EntityKind.ObstacleTall })
                {
                    Call(game, "Spawn", kind, 10f, RunnerGame.GroundY);
                    var first = (RunnerEntity)list[0];
                    Call(game, "Release", 0);
                    Call(game, "Spawn", kind, 510f, RunnerGame.GroundY);
                    var second = (RunnerEntity)list[0];
                    Check(first != second, "different themes do not share pooled obstacles");
                    Call(game, "Release", 0);
                    Call(game, "Spawn", kind, 10f, RunnerGame.GroundY);
                    Check((RunnerEntity)list[0] == first, "same-theme obstacle is reused");
                    Call(game, "Release", 0);
                }
                typeof(RunnerGame).GetField("genX", Private).SetValue(game, 499f);
                Call(game, "AddGround", 3f);
                var grounds = (IList)typeof(RunnerGame).GetField("grounds", Private).GetValue(game);
                var last = grounds[grounds.Count - 1];
                var previous = grounds[grounds.Count - 2];
                var goField = last.GetType().GetField("go");
                Check(((GameObject)goField.GetValue(previous)).GetComponent<SpriteRenderer>().sprite == game.mapThemes[0].ground, "ground before 500m");
                Check(((GameObject)goField.GetValue(last)).GetComponent<SpriteRenderer>().sprite == game.mapThemes[1].ground, "ground after 500m");
                typeof(RunnerGame).GetField("scroll", Private).SetValue(game, 500f);
                Call(game, "UpdateTheme", 0f);
                Call(game, "UpdateTheme", 2f);
                Check(game.sky.sprite == game.mapThemes[1].background, "background fade uses SO");
                game.themeDistance = 0;
                Check(game.ThemeAt(1f) != null, "invalid interval is safely clamped");
                const string result = "MAP_THEME_VALIDATION_PASS: five SO assets, all obstacle slots, 500m boundaries, two rotations, per-prefab pooling/reuse, per-theme obstacle Y offsets at generation boundaries and pooled reuse, fallback offsets, unaffected pickups, zero-offset compatibility, split ground, background fade, zero-interval fallback";
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/MapThemeValidation.result.txt", result);
                Debug.Log(result);
                if (exitAfter) EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/MapThemeValidation.result.txt", ex.ToString());
                Debug.LogException(ex);
                if (exitAfter) EditorApplication.Exit(1);
                else throw;
            }
        }

        static void ValidateObstacleOffsets(RunnerGame game, IList list)
        {
            var field = typeof(RunnerGame).GetField("activeThemes", Private);
            var original = field.GetValue(game);
            var first = UnityEngine.Object.Instantiate(game.mapThemes[0]);
            var second = UnityEngine.Object.Instantiate(game.mapThemes[1]);
            try
            {
                first.upperObstacleYOffset = 0.35f; first.lowerObstacleYOffset = -0.2f; first.tallObstacleYOffset = -0.4f;
                second.upperObstacleYOffset = -0.15f; second.lowerObstacleYOffset = 0.45f; second.tallObstacleYOffset = 0.1f;
                // The same prefab/pool must still use the theme at the spawn distance.
                second.upperObstacle = first.upperObstacle;
                second.lowerObstacle = first.lowerObstacle;
                second.tallObstacle = first.tallObstacle;
                field.SetValue(game, new[] { first, second });
                foreach (var pair in new[] { (EntityKind.ObstacleHang, 0.35f, -0.15f), (EntityKind.ObstacleLow, -0.2f, 0.45f), (EntityKind.ObstacleTall, -0.4f, 0.1f) })
                {
                    float baseY = RunnerGame.GroundY + (pair.Item1 == EntityKind.ObstacleHang ? game.hangClearance : 0f);
                    RunnerEntity reused = null;
                    foreach (var sample in new[] { (499.99f, pair.Item2), (500f, pair.Item3), (499.99f, pair.Item2) })
                    {
                        Call(game, "Spawn", pair.Item1, sample.Item1, baseY);
                        var entity = (RunnerEntity)list[0];
                        Check(Mathf.Abs(entity.transform.localPosition.y - baseY - sample.Item2) < 0.001f, "Y offset at generation boundary " + pair.Item1);
                        Check(Mathf.Abs(entity.transform.localPosition.x - sample.Item1) < 0.001f, "Y offset preserves spawn X");
                        Check(entity.hitOffset == first.ObstacleFor(pair.Item1).hitOffset && entity.hitHalf == first.ObstacleFor(pair.Item1).hitHalf, "offset preserves relative hitbox");
                        if (reused != null) Check(reused == entity, "shared pooled obstacle gets new theme offset without accumulation");
                        reused = entity;
                        Call(game, "Release", 0);
                    }
                }
                first.lowerObstacle = null;
                Call(game, "Spawn", EntityKind.ObstacleLow, 10f, RunnerGame.GroundY);
                Check(Mathf.Abs(((RunnerEntity)list[0]).transform.localPosition.y - (RunnerGame.GroundY - 0.2f)) < 0.001f, "empty slot retains theme offset on fallback prefab");
                Call(game, "Release", 0);
                Call(game, "Spawn", EntityKind.Jelly, 10f, RunnerGame.GroundY + 1.4f);
                Check(Mathf.Abs(((RunnerEntity)list[0]).transform.localPosition.y - (RunnerGame.GroundY + 1.4f)) < 0.001f, "pickup Y is unaffected");
                Call(game, "Release", 0);
                first.lowerObstacleYOffset = 0f;
                Call(game, "Spawn", EntityKind.ObstacleLow, 10f, RunnerGame.GroundY);
                Check(Mathf.Abs(((RunnerEntity)list[0]).transform.localPosition.y - RunnerGame.GroundY) < 0.001f, "zero offset preserves existing height");
                Call(game, "Release", 0);
                field.SetValue(game, new MapThemeData[0]);
                Call(game, "Spawn", EntityKind.ObstacleLow, 10f, RunnerGame.GroundY);
                Check(Mathf.Abs(((RunnerEntity)list[0]).transform.localPosition.y - RunnerGame.GroundY) < 0.001f, "no theme preserves existing height");
            }
            finally
            {
                for (int i = list.Count - 1; i >= 0; i--) Call(game, "Release", i);
                field.SetValue(game, original);
                UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second);
            }
        }

        static void Check(bool value, string description)
        { if (!value) throw new InvalidOperationException("Map theme test failed: " + description); }
        static void Call(object target, string method, params object[] args)
        { target.GetType().GetMethod(method, Private).Invoke(target, args); }
    }
}
