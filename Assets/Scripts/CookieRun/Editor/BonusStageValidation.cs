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
    public static class BonusStageValidation
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project copy.");
            try
            {
                ValidateBonus();
                MapThemeValidation.RunValidation();
                ItemDataValidation.RunValidation();
                const string result = "BONUS_STAGE_VALIDATION_PASS: N-pickup trigger and score, configurable duration, bonus-only theme, dense safe track, immunity/HP freeze, pause/countdown, no recursive trigger, timed return across 500m, retrigger/retry reset, fallback theme; map/item regression suites passed";
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/BonusStageValidation.result.txt", result);
                Debug.Log(result); EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/BonusStageValidation.result.txt", ex.ToString());
                Debug.LogException(ex); EditorApplication.Exit(1);
            }
        }

        static void ValidateBonus()
        {
            EditorSceneManager.OpenScene(MapThemeMigration.ScenePath);
            var game = Object.FindFirstObjectByType<RunnerGame>();
            Check(game != null && game.bonusMap != null && game.bonusMap.bonusOnly, "BonusMap SO connected");
            Check(game.mapThemes.Length == 5 && !game.mapThemes.Contains(game.bonusMap), "five ordinary rotation themes preserved");
            Check(game.bonusMap.background != null && game.bonusMap.ground != null && game.bonusMap.background != game.mapThemes[0].background, "distinct illustrated bonus background and ground");
            var source = game.items.Single(i => i.kind == EntityKind.BigJelly);
            Check(source.effect == ItemEffect.BonusStage && source.bonusTheme == game.bonusMap, "BigJelly effect/theme connected");
            Check(source.bonusRequiredCount == 5 && source.bonusDuration == 5, "5 pickups / 5 seconds defaults");
            var item = Object.Instantiate(source);
            item.bonusRequiredCount = 3;
            item.bonusDuration = 1.25f;
            game.items = game.items.Select(i => i == source ? item : i).ToArray();
            // An accidentally added bonus-only SO must not enter ordinary distance rotation.
            game.mapThemes = game.mapThemes.Concat(new[] { game.bonusMap }).ToArray();
            Call(game, "Awake"); Call(game.player, "Awake"); Call(game, "SetupThemes"); Call(game, "ResetRun");
            Check(game.ThemeAt(2500) == game.mapThemes[0] && game.ThemeAt(500) == game.mapThemes[1], "bonus-only SO filtered from ordinary rotation");
            Clear(game);
            Set(game, "scroll", 490f); game.world.localPosition = new Vector3(-490, 0, 0);
            Call(game, "RebuildTrack", false); Clear(game);
            Set(game, "hp", 50f); Set(game, "bonusScore", 0);
            for (int i = 1; i <= 2; i++)
            {
                Pickup(game, EntityKind.BigJelly);
                Check(!game.InBonusStage && game.BonusCollected == i && game.BonusRequired == 3, "partial collection " + i);
                Check(Get<int>(game, "bonusScore") == source.score * i, "BigJelly still grants score");
            }
            // Multiple overlapping items exercise the deferred track switch safely.
            for (int i = 0; i < 10; i++) Call(game, "Spawn", EntityKind.BigJelly, PlayerCookie.X + Get<float>(game, "scroll"), game.player.HitCenter.y);
            Call(game, "HandleEntities", 0f, 9f);
            Check(!game.InBonusStage && Get<ItemData>(game, "pendingBonus") == item, "threshold queues transition after collision iteration");
            Call(game, "TickPlaying", 0f, false, false);
            Check(game.InBonusStage && game.BonusCollected == 0, "entry resets collection progress");
            Near(game.BonusTimeRemaining, 1.25f, "SO bonus duration");
            Check(game.sky.sprite == game.bonusMap.background, "bonus background appears immediately");
            Check(game.hud.buffText.text.Contains("보너스") && game.hud.buffText.text.Contains("무적"), "bonus HUD status");
            Check(Entities(game).Count > 60 && Entities(game).Cast<RunnerEntity>().All(e => e.kind == EntityKind.Jelly), "dense reward rows without obstacles or recursive bonus items");
            foreach (var ground in Get<IList>(game, "grounds"))
                Check(((GameObject)ground.GetType().GetField("go").GetValue(ground)).GetComponent<SpriteRenderer>().sprite == game.bonusMap.ground, "bonus ground uses SO");
            float start = Get<float>(game, "scroll");
            for (float x = start - 7; x <= start + 32; x++) Check((bool)Call(game, "HasGround", x), "continuous bonus floor");

            Clear(game);
            Call(game, "Spawn", EntityKind.ObstacleLow, PlayerCookie.X + start, RunnerGame.GroundY);
            Set(game, "invincibleT", 0f); Set(game, "boostT", 0f); Set(game, "dashT", 0f);
            Call(game, "HandleEntities", 0f, 9f);
            Near(Get<float>(game, "hp"), 50, "bonus collision immunity");
            Clear(game);
            Pickup(game, EntityKind.BigJelly); Pickup(game, EntityKind.BigJelly); Pickup(game, EntityKind.BigJelly);
            Check(game.BonusCollected == 0 && Get<ItemData>(game, "pendingBonus") == null, "bonus pickups cannot recursively trigger another stage");
            Near(game.BonusTimeRemaining, 1.25f, "bonus pickup cannot extend timer");

            game.Pause();
            Check(Get<object>(game, "state").ToString() == "Paused", "pause state");
            Call(game, "Update"); Near(game.BonusTimeRemaining, 1.25f, "bonus timer pauses");
            Call(game, "BeginResume");
            Check(Get<object>(game, "state").ToString() == "Countdown", "resume countdown state");
            Call(game, "Update"); Near(game.BonusTimeRemaining, 1.25f, "bonus timer pauses during countdown");
            var state = game.GetType().GetField("state", Private);
            state.SetValue(game, Enum.Parse(state.FieldType, "Playing"));
            game.hud.settingsPanel.gameObject.SetActive(false);
            int ticks = 0;
            while (game.InBonusStage && ticks < 40)
            {
                Call(game, "TickPlaying", 0.05f, false, false);
                Near(Get<float>(game, "hp"), 50, "bonus freezes HP drain");
                ticks++;
            }
            Check(!game.InBonusStage && ticks >= 25 && ticks <= 26, "bonus exits at configured time");
            Check(Get<float>(game, "scroll") > 500 && game.sky.sprite == game.mapThemes[1].background, "return uses current distance across a 500m boundary");
            Check((bool)Call(game, "HasGround", PlayerCookie.X + Get<float>(game, "scroll")), "safe floor on return");
            Check(Get<float>(game, "invincibleT") >= 0.5f, "short return protection");
            Check(game.hud.buffText.text.Contains("0/3"), "HUD returns to collection progress");
            Call(game, "TickPlaying", 0.05f, false, false);
            Check(Get<float>(game, "hp") < 50, "normal HP drain resumes");
            Clear(game);
            item.bonusRequiredCount = 1; item.bonusDuration = 0.3f; item.bonusTheme = null;
            Pickup(game, EntityKind.BigJelly); Call(game, "TickPlaying", 0f, false, false);
            Check(game.InBonusStage && game.sky.sprite == game.bonusMap.background, "one-pickup retrigger and MainGame theme fallback");
            Near(game.BonusTimeRemaining, 0.3f, "updated duration");
            Call(game, "ResetRun");
            Check(!game.InBonusStage && game.BonusCollected == 0 && Get<ItemData>(game, "pendingBonus") == null, "retry clears active bonus and counters");
            Check(game.sky.sprite == game.mapThemes[0].background, "retry returns to first ordinary map");
            Object.DestroyImmediate(item);
        }

        static IList Entities(RunnerGame game) => Get<IList>(game, "entities");
        static void Clear(RunnerGame game) { for (int i = Entities(game).Count - 1; i >= 0; i--) Call(game, "Release", i); }
        static void Pickup(RunnerGame game, EntityKind kind)
        {
            Call(game, "Spawn", kind, PlayerCookie.X + Get<float>(game, "scroll"), game.player.HitCenter.y);
            Call(game, "HandleEntities", 0f, 9f);
        }
        static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        static void Near(float actual, float expected, string description) => Check(Mathf.Abs(actual - expected) < 0.001f, description + " (" + actual + " / " + expected + ")");
        static void Check(bool value, string description) { if (!value) throw new InvalidOperationException("Bonus stage test failed: " + description); }
    }
}
