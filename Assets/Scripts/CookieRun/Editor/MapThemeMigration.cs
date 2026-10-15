using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CookieRun.Editor
{
    public static class MapThemeMigration
    {
        public const string DataFolder = "Assets/5.Data/MapThemes";
        public const string ScenePath = "Assets/1.Scene/MainGame.unity";
        static readonly string[] Keys = { "Basic", "Forest", "Rock", "Lava", "Cave" };
        static readonly string[] Labels = { "기본", "숲", "돌산", "용암", "동굴" };

        [MenuItem("Tools/CookieRun/기존 맵 테마를 SO로 변환")]
        public static void MigrateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("맵 테마 변환은 플레이 모드를 종료한 뒤 실행하세요.");
                return;
            }
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                RunnerGame game = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    game = root.GetComponentInChildren<RunnerGame>(true);
                    if (game != null) break;
                }
                if (game == null) throw new InvalidOperationException("MainGame 씬에 RunnerGame이 없습니다.");
                if (EnsureForGame(game)) EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("MAP_THEME_MIGRATION_PASS: " + game.mapThemes.Length + " SO themes connected");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        public static bool EnsureForGame(RunnerGame game)
        {
            if (game.mapThemes != null && game.mapThemes.Length > 0) return EnsureBonusForGame(game);
            PrefabCatalog.Ensure(DataFolder);
            int count = game.themes != null && game.themes.Length > 0 ? game.themes.Length : Keys.Length;
            var data = new MapThemeData[count];
            for (int i = 0; i < count; i++)
            {
                string key = i < Keys.Length ? Keys[i] : "Theme" + i;
                string path = DataFolder + "/" + (i + 1).ToString("00") + "_" + key + ".asset";
                data[i] = AssetDatabase.LoadAssetAtPath<MapThemeData>(path);
                if (data[i] != null) continue;
                var theme = ScriptableObject.CreateInstance<MapThemeData>();
                var old = game.themes != null && i < game.themes.Length ? game.themes[i] : null;
                theme.displayName = old != null ? old.name : Labels[Mathf.Min(i, Labels.Length - 1)];
                theme.background = old != null ? old.sky : Sprite("Background_" + key);
                theme.ground = old != null ? old.ground : Sprite("Ground_" + key);
                if (old != null)
                {
                    theme.hill = old.hill;
                    theme.hillColorA = old.hillColorA;
                    theme.hillColorB = old.hillColorB;
                    theme.cloudColor = old.cloudColor;
                }
                theme.upperObstacle = MakeObstacle(game, EntityKind.ObstacleHang, key);
                theme.lowerObstacle = MakeObstacle(game, EntityKind.ObstacleLow, key);
                theme.tallObstacle = MakeObstacle(game, EntityKind.ObstacleTall, key);
                AssetDatabase.CreateAsset(theme, path);
                data[i] = theme;
            }
            Undo.RecordObject(game, "맵 테마 SO 연결");
            game.mapThemes = data;
            EnsureBonusForGame(game);
            // Keep legacy scene values as a compatibility fallback, hidden in the Inspector.
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            return true;
        }

        public static bool EnsureBonusForGame(RunnerGame game)
        {
            if (game.bonusMap != null) return false;
            PrefabCatalog.Ensure(DataFolder);
            string path = DataFolder + "/06_BonusMap.asset";
            var data = AssetDatabase.LoadAssetAtPath<MapThemeData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<MapThemeData>();
                data.displayName = "BonusMap";
                data.bonusOnly = true;
                data.background = Sprite("Background_BonusMap");
                data.ground = Sprite("Ground_Basic");
                data.hillColorA = data.hillColorB = Color.clear;
                data.cloudColor = new Color(1, 1, 1, 0);
                AssetDatabase.CreateAsset(data, path);
            }
            Undo.RecordObject(game, "BonusMap SO 연결");
            game.bonusMap = data;
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            return true;
        }

        static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/3.Sprite/Storybook/" + name + ".png");

        static RunnerEntity MakeObstacle(RunnerGame game, EntityKind kind, string key)
        {
            string folder = "Assets/2.Prefab/Obstacles/Themes/" + key;
            PrefabCatalog.Ensure(folder);
            string path = folder + "/" + kind + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<RunnerEntity>();
            if (game.entityPrefabs == null) return null;
            foreach (var source in game.entityPrefabs)
            {
                if (source == null || source.kind != kind) continue;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source.gameObject);
                try { return PrefabUtility.SaveAsPrefabAsset(instance, path).GetComponent<RunnerEntity>(); }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            return null;
        }
    }
}
