using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CookieRun.Editor
{
    /// <summary>Render the real game prefabs in an isolated scene, with the runtime camera/layout rules.</summary>
    public sealed class RunnerMapPreview : IDisposable
    {
        const string AspectKey = "CookieRun.ArtPreview.Aspect";
        static readonly string[] AspectLabels = { "게임 뷰와 동일", "16:9", "16:10", "4:3", "9:16" };
        readonly PreviewRenderUtility utility = new PreviewRenderUtility();
        readonly List<GameObject> objects = new List<GameObject>();
        Scene sourceScene;
        RunnerGame savedGame;

        public const float PatternStart = PlayerCookie.X + 2f;

        public static RunnerGame FindLoadedGame() => Object.FindObjectsByType<RunnerGame>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(g => !EditorSceneManager.IsPreviewScene(g.gameObject.scene));

        public static void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("미리보기 화면", GUILayout.Width(90));
                int current = Mathf.Clamp(EditorPrefs.GetInt(AspectKey, 0), 0, AspectLabels.Length - 1);
                int next = EditorGUILayout.Popup(current, AspectLabels, GUILayout.Width(150));
                if (next != current) EditorPrefs.SetInt(AspectKey, next);
                GUILayout.Label("실제 SpriteRenderer · 게임 카메라 배율 · 배치 예시", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }
        }

        public static float Aspect
        {
            get
            {
                switch (EditorPrefs.GetInt(AspectKey, 0))
                {
                    case 1: return 16f / 9f;
                    case 2: return 1.6f;
                    case 3: return 4f / 3f;
                    case 4: return 9f / 16f;
                    default:
                        var size = Handles.GetMainGameViewSize();
                        return size.x > 0 && size.y > 0 ? size.x / size.y : 16f / 9f;
                }
            }
        }

        public static Rect FitRect(Rect outer, float aspect)
        {
            float height = Mathf.Min(outer.height, outer.width / aspect);
            return new Rect(outer.center.x - height * aspect / 2, outer.center.y - height / 2, height * aspect, height);
        }

        public static float Unit(Rect frame, float aspect) => frame.height / (2 * RunnerMapVisuals.CameraSize(aspect));
        public static Vector2 Project(Rect frame, float aspect, Vector2 world, float pan = 0) =>
            new Vector2(frame.width / 2 + (world.x - pan) * Unit(frame, aspect), frame.height / 2 - world.y * Unit(frame, aspect));

        public void Draw(Rect rect, MapThemeData theme, RunnerGame game, ItemData item = null, bool obstacles = true, float pan = 0)
        {
            if (Event.current.type != EventType.Repaint || rect.width < 1 || rect.height < 1) return;
            utility.BeginPreview(rect, GUIStyle.none);
            Build(theme, game, rect.width / rect.height, item, obstacles, pan);
            utility.Render(true);
            GUI.DrawTexture(rect, utility.EndPreview(), ScaleMode.StretchToFill, false);
        }

        // Also used by the graphics validation, without an IMGUI event loop.
        public Texture2D Capture(int width, int height, MapThemeData theme, RunnerGame game, ItemData item = null, bool obstacles = true)
        {
            utility.BeginStaticPreview(new Rect(0, 0, width, height));
            Build(theme, game, width / (float)height, item, obstacles, 0);
            utility.Render(true);
            return utility.EndStaticPreview();
        }

        public RunnerGame Source(RunnerGame game)
        {
            if (game != null) return game;
            if (savedGame != null) return savedGame;
            sourceScene = EditorSceneManager.OpenPreviewScene(MapThemeMigration.ScenePath);
            savedGame = sourceScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RunnerGame>(true)).FirstOrDefault();
            return savedGame;
        }

        public void Invalidate()
        {
            Clear();
            savedGame = null;
            if (sourceScene.IsValid()) EditorSceneManager.ClosePreviewScene(sourceScene);
            sourceScene = default;
        }

        public void Dispose() { Invalidate(); utility.Cleanup(); }

        void Clear()
        {
            foreach (var go in objects) if (go != null) Object.DestroyImmediate(go);
            objects.Clear();
        }

        T Clone<T>(T source) where T : Component
        {
            if (source == null) return null;
            var go = Object.Instantiate(source.gameObject);
            go.hideFlags = HideFlags.HideAndDontSave;
            foreach (var script in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (script.GetType().Namespace == "CookieRun") script.enabled = false;
            utility.AddSingleGO(go);
            objects.Add(go);
            return go.GetComponent<T>();
        }

        void Build(MapThemeData theme, RunnerGame source, float aspect, ItemData item, bool obstacles, float pan)
        {
            Clear();
            var game = Source(source);
            var camera = utility.camera;
            camera.orthographic = true;
            camera.orthographicSize = RunnerMapVisuals.CameraSize(aspect);
            camera.aspect = aspect;
            camera.transform.SetPositionAndRotation(new Vector3(pan, 0, -10), Quaternion.identity);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = game != null && game.cam != null ? game.cam.backgroundColor : new Color(0.55f, 0.8f, 0.98f);
            camera.allowHDR = game != null && game.cam != null && game.cam.allowHDR;
            camera.allowMSAA = game != null && game.cam != null && game.cam.allowMSAA;
            if (game == null) return;

            var sky = Clone(game.sky);
            if (sky != null)
            {
                if (theme != null && theme.background != null) sky.sprite = theme.background;
                sky.gameObject.SetActive(true);
                sky.enabled = true;
                if (game.fitIllustratedBackground) RunnerMapVisuals.FitBackground(sky, camera);
            }
            Parallax(game.hillsRoot, theme, true);
            Parallax(game.cloudsRoot, theme, false);

            // Same first floor segment as ResetRun; extra segments allow item-layout panning.
            float left = Mathf.Min(-16, pan - camera.orthographicSize * aspect - 2);
            var ground = Clone(game.groundPrefab);
            if (ground != null) RunnerMapVisuals.ConfigureGround(ground, theme != null ? theme.ground : null,
                left, Mathf.Max(30, pan + camera.orthographicSize * aspect + 2 - left), RunnerGame.GroundY);

            var player = Clone(game.player);
            if (player != null)
            {
                player.transform.position = new Vector3(PlayerCookie.X, RunnerGame.GroundY, 0);
                if (player.ridingFrames != null && player.ridingFrames.Length > 0 && player.ridingFrames[0] != null)
                {
                    player.body.GetComponent<SpriteRenderer>().sprite = player.ridingFrames[0];
                    player.body.localPosition = Vector3.zero;
                    player.body.localScale = Vector3.one;
                    player.body.localRotation = Quaternion.identity;
                    player.legBack.GetComponent<SpriteRenderer>().enabled = false;
                    player.legFront.GetComponent<SpriteRenderer>().enabled = false;
                }
                foreach (var renderer in player.GetComponentsInChildren<SpriteRenderer>())
                    renderer.color = theme != null && theme.bonusOnly ? RunnerMapVisuals.BonusTint : Color.white;
            }
            if (obstacles && (theme == null || !theme.bonusOnly))
            {
                AddObstacle(game, theme, EntityKind.ObstacleLow, 0, RunnerGame.GroundY);
                AddObstacle(game, theme, EntityKind.ObstacleTall, camera.orthographicSize * aspect - 1.7f, RunnerGame.GroundY);
                float spacing = Mathf.Max(1.4f, game.hangObstacleWidth);
                for (int i = 0; i < 3; i++) AddObstacle(game, theme, EntityKind.ObstacleHang, 2.2f + i * spacing, RunnerGame.GroundY + game.hangClearance);
            }
            var jelly = (game.items ?? Array.Empty<ItemData>()).FirstOrDefault(d => d != null && d.IsValid && d.kind == EntityKind.Jelly);
            if (item != null)
            {
                foreach (var point in item.positions ?? Array.Empty<Vector2>())
                    AddEntity(item.prefab, new Vector2(PatternStart, RunnerGame.GroundY) + item.PositionAt(point), item.sprite);
                foreach (var point in item.guideJellies ?? Array.Empty<Vector2>())
                    AddEntity(jelly != null ? jelly.prefab : null, new Vector2(PatternStart, RunnerGame.GroundY) + item.PositionAt(point), jelly != null ? jelly.sprite : null);
            }
            else if (theme != null && theme.bonusOnly)
                for (int i = 0; i < 8; i++)
                    for (int row = 0; row < 3; row++)
                        AddEntity(jelly != null ? jelly.prefab : null, new Vector2(PatternStart + 0.4f + i * 0.75f, RunnerGame.GroundY + 0.7f + row * 1.1f), jelly != null ? jelly.sprite : null);
        }

        void Parallax(Transform source, MapThemeData theme, bool hills)
        {
            if (source == null || !source.gameObject.activeInHierarchy) return;
            var root = Clone(source);
            var renderers = root.GetComponentsInChildren<SpriteRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (theme == null) continue;
                if (hills && theme.hill != null) renderers[i].sprite = theme.hill;
                renderers[i].color = hills ? i % 2 == 0 ? theme.hillColorA : theme.hillColorB : theme.cloudColor;
            }
        }

        static RunnerEntity Obstacle(RunnerGame game, MapThemeData theme, EntityKind kind) =>
            RunnerMapVisuals.ObstaclePrefab(theme, kind, (game.entityPrefabs ?? Array.Empty<RunnerEntity>()).FirstOrDefault(p => p != null && p.kind == kind));

        void AddObstacle(RunnerGame game, MapThemeData theme, EntityKind kind, float x, float baseY) =>
            AddEntity(Obstacle(game, theme, kind), new Vector2(x, RunnerMapVisuals.ObstacleY(theme, kind, baseY)));

        void AddEntity(RunnerEntity source, Vector2 position, Sprite sprite = null)
        {
            var entity = Clone(source);
            if (entity == null) return;
            entity.transform.position = position;
            if (sprite != null) entity.GetComponent<SpriteRenderer>().sprite = sprite;
        }
    }
}
