using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CookieRun.Editor
{
    public static class ItemDataMigration
    {
        public const string DataFolder = "Assets/5.Data/Items";
        public static readonly EntityKind[] Kinds = { EntityKind.Jelly, EntityKind.BigJelly, EntityKind.Potion,
            EntityKind.Magnet, EntityKind.Boost, EntityKind.Dash };
        public static readonly string[] Labels = { "별 젤리", "큰 별 젤리", "체력 회복", "자석", "부스터", "무적 질주" };

        [MenuItem("Tools/CookieRun/기존 아이템을 SO로 변환")]
        public static void MigrateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetSceneByPath(MapThemeMigration.ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(MapThemeMigration.ScenePath, OpenSceneMode.Additive);
            try
            {
                var game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RunnerGame>(true)).FirstOrDefault();
                if (game == null) throw new InvalidOperationException("MainGame 씬에 RunnerGame이 없습니다.");
                if (EnsureForGame(game)) EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("ITEM_DATA_MIGRATION_PASS: " + game.items.Length + " SO items connected");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        public static bool EnsureForGame(RunnerGame game)
        {
            if (game.items != null && game.items.Length > 0) return false;
            PrefabCatalog.Ensure(DataFolder);
            var data = new ItemData[Kinds.Length];
            var theme = (game.mapThemes ?? Array.Empty<MapThemeData>()).FirstOrDefault(t => t != null);
            for (int i = 0; i < Kinds.Length; i++)
            {
                string path = DataFolder + "/" + (i + 1).ToString("00") + "_" + Kinds[i] + ".asset";
                data[i] = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (data[i] != null) continue;
                var item = ScriptableObject.CreateInstance<ItemData>();
                item.kind = Kinds[i];
                item.displayName = Labels[i];
                item.prefab = (game.entityPrefabs ?? Array.Empty<RunnerEntity>()).FirstOrDefault(p => p != null && p.kind == item.kind);
                item.previewTheme = theme;
                item.effect = i < 2 ? ItemEffect.Score : (ItemEffect)(i - 1);
                if (item.kind == EntityKind.BigJelly)
                {
                    item.effect = ItemEffect.BonusStage;
                    item.bonusTheme = game.bonusMap;
                }
                item.score = item.prefab != null ? item.prefab.score : (i == 0 ? 10 : i == 1 ? 100 : 0);
                item.healAmount = game.potionHeal;
                item.duration = item.kind == EntityKind.Magnet ? game.magnetTime : item.kind == EntityKind.Boost ? game.boostTime : game.dashTime;
                item.speedMultiplier = item.kind == EntityKind.Boost ? game.boostSpeedScale : game.dashSpeedScale;
                item.spawnWeight = item.kind == EntityKind.Magnet ? 0.35f : item.kind == EntityKind.Boost ? 0.3f : item.kind == EntityKind.Dash ? 0.35f : 0;
                if (item.kind == EntityKind.Jelly)
                    item.positions = Enumerable.Range(0, 6).Select(n => new Vector2(0.5f + n, 0.6f)).ToArray();
                else if (item.kind != EntityKind.BigJelly)
                    item.guideJellies = new[] { new Vector2(1.2f, 0.9f), new Vector2(2.1f, 1.7f),
                        new Vector2(3.9f, 1.7f), new Vector2(4.8f, 0.9f) };
                AssetDatabase.CreateAsset(item, path);
                data[i] = item;
            }
            Undo.RecordObject(game, "아이템 SO 연결");
            game.items = data;
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            return true;
        }
    }
}
