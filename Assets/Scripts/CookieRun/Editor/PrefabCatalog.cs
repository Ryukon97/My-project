using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CookieRun
{
    public static class PrefabCatalog
    {
        const string Root = "Assets/2.Prefab/";
        public static string Resolve(string path)
        {
            if (!path.StartsWith(Root) || path.Substring(Root.Length).Contains("/")) return path;
            string name = Path.GetFileNameWithoutExtension(path);
            string category = name == "Cookie" ? "Characters" : name == "GameAudio" ? "Systems" :
                name.StartsWith("Obstacle") ? "Obstacles" :
                name == "Ground" || name == "Hill" || name == "Cloud" ? "Environment" : "Items";
            return Root + category + "/" + name + ".prefab";
        }

        [MenuItem("CookieRun/Organize All Prefabs")]
        public static void Organize()
        {
            foreach (string category in new[] { "Characters", "Items", "Obstacles", "Environment", "Systems", "UI/Controls", "UI/Popups", "UI/Title", "Environment/Themes" }) Ensure(Root + category);
            foreach (string path in Directory.GetFiles(Root, "*.prefab"))
            {
                string source = path.Replace('\\', '/');
                string destination = Resolve(source);
                string error = AssetDatabase.MoveAsset(source, destination);
                if (!string.IsNullOrEmpty(error)) throw new Exception(error);
            }
            Extract("SettingsPanel", "BgmSlider", "Controls/VolumeSlider");
            Extract("GameHUD", "JumpButton", "Controls/JumpButton");
            Extract("GameHUD", "SlideButton", "Controls/SlideButton");
            Extract("GameHUD", "PauseButton", "Controls/PauseButton");
            Extract("GameHUD", "ScorePanel", "Controls/ScorePanel");
            Extract("GameHUD", "GameOverPanel", "Popups/GameOverPanel");
            Extract("SettingsPanel", "ConfirmPanel", "Popups/ConfirmTitlePanel");
            Extract("TitleUI", "CookieAdventureLogo", "Title/CookieAdventureLogo");
            Extract("TitleUI", "StorybookBackdrop", "Title/StorybookBackdrop");
            Extract("TitleUI", "StartButton", "Controls/StartButton");
            foreach (string theme in new[] { "Basic", "Forest", "Rock", "Lava", "Cave", "Title" })
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/3.Sprite/Storybook/Background_" + (theme == "Title" ? "MinecartTitle" : theme) + ".png");
                if (!sprite) continue;
                var go = new GameObject("Background_" + theme, typeof(SpriteRenderer));
                go.GetComponent<SpriteRenderer>().sprite = sprite;
                go.GetComponent<SpriteRenderer>().sortingOrder = -100;
                PrefabUtility.SaveAsPrefabAsset(go, Root + "Environment/Themes/Background_" + theme + ".prefab");
                Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("PREFAB_CATALOG_COMPLETE");
        }

        static void Extract(string owner, string child, string destination)
        {
            var root = PrefabUtility.LoadPrefabContents(Root + "UI/" + owner + ".prefab");
            try
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == child)
                    {
                        var copy = Object.Instantiate(t.gameObject);
                        copy.name = child;
                        copy.SetActive(true);
                        PrefabUtility.SaveAsPrefabAsset(copy, Root + "UI/" + destination + ".prefab");
                        Object.DestroyImmediate(copy);
                        return;
                    }
                Debug.LogWarning("Prefab extraction skipped: " + child);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void Ensure(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
