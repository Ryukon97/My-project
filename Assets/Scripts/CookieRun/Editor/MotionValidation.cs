using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CookieRun.Editor
{
    public static class MotionValidation
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [MenuItem("CookieRun/Validate Motion Sequence")]
        static void Validate()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<RunnerGame>();
            if (!Application.isPlaying || game == null) throw new Exception("Run MainGame first");
            bool wasEnabled = game.enabled;
            game.enabled = false;
            var player = game.player;
            try
            {
                Check(player.doubleJumpFrames.Length == 4 && player.slideFrames.Length == 4 && player.exhaustionFrames.Length == 4, "frame arrays");
                player.ResetState();
                player.Tick(0.02f, true, false, true);
                player.Tick(0.02f, true, false, true);
                Check(player.body.GetComponent<SpriteRenderer>().sprite == player.doubleJumpFrames[0], "double jump frame");
                player.ResetState();
                player.Tick(0.12f, false, true, true);
                Check(player.Sliding && player.body.localScale == Vector3.one, "slide without deformation");
                var slidePose = player.body.GetComponent<SpriteRenderer>().sprite;
                for (int i = 0; i < 20; i++)
                {
                    player.Tick(0.05f, false, true, true);
                    Check(player.body.GetComponent<SpriteRenderer>().sprite == slidePose, "slide pose stays fixed");
                }
                player.Tick(0.02f, true, false, true);
                Check(!player.Sliding, "jump cancels slide");
                typeof(RunnerGame).GetMethod("ResetRun", Private).Invoke(game, null);
                typeof(RunnerGame).GetMethod("BeginDeath", Private).Invoke(game, new object[] { 0 });
                Check(!game.hud.gameOverPanel.activeSelf, "results hidden on death start");
                player.TickExhaustion(player.exhaustionDuration - 0.1f);
                Check(!player.ExhaustionComplete && !game.hud.gameOverPanel.activeSelf, "results wait for animation");
                player.TickExhaustion(0.11f);
                Check(player.ExhaustionComplete, "death completes");
                typeof(RunnerGame).GetMethod("Update", Private).Invoke(game, null);
                Check(game.hud.gameOverPanel.activeSelf, "results appear after animation");
                game.Retry();
                Check(!game.hud.gameOverPanel.activeSelf && player.Grounded && !player.Sliding, "retry resets pose and results");
                Debug.Log("MOTION_VALIDATION_PASS: double jump, slide, death delay, results, retry");
            }
            finally { game.enabled = wasEnabled; }
        }
        static void Check(bool value, string message)
        {
            if (!value) throw new Exception("Motion validation failed: " + message);
        }
    }
}
