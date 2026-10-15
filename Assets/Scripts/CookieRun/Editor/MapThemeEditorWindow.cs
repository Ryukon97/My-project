using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace CookieRun.Editor
{
    public class MapThemeEditorWindow : EditorWindow
    {
        MapThemeData[] assets = new MapThemeData[0];
        MapThemeData selected;
        RunnerGame game;
        Vector2 themeScroll, scroll;
        bool rotationOpen;
        MapThemeData renaming;
        string renameText;
        bool focusRename;
        RunnerMapPreview scenePreview;

        [MenuItem("Tools/CookieRun/맵 테마 편집기")]
        public static void Open()
        {
            var window = GetWindow<MapThemeEditorWindow>("맵 테마 편집기");
            window.minSize = new Vector2(960, 680);
            window.Show();
        }

        [OnOpenAsset]
        static bool OpenTheme(int instanceId, int line)
        {
            var theme = EditorUtility.InstanceIDToObject(instanceId) as MapThemeData;
            if (theme == null) return false;
            Open();
            GetWindow<MapThemeEditorWindow>().selected = theme;
            return true;
        }

        void OnEnable()
        {
            Reload();
            if (game != null && !EditorApplication.isPlayingOrWillChangePlaymode)
                MapThemeMigration.EnsureForGame(game);
            Undo.undoRedoPerformed += Repaint;
        }
        void OnDisable() { Undo.undoRedoPerformed -= Repaint; scenePreview?.Dispose(); scenePreview = null; }
        void OnProjectChange() { scenePreview?.Invalidate(); Reload(); Repaint(); }

        void Reload()
        {
            assets = AssetDatabase.FindAssets("t:MapThemeData").Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p).Select(AssetDatabase.LoadAssetAtPath<MapThemeData>).ToArray();
            if (selected == null && assets.Length > 0) selected = assets[0];
            if (game == null) game = RunnerMapPreview.FindLoadedGame();
        }

        void OnGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) renaming = null;
            if (!EditorApplication.isPlayingOrWillChangePlaymode && selected != null &&
                Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F2)
            {
                BeginRename(selected);
                Event.current.Use();
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("맵 테마", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button("기존 테마 가져오기", EditorStyles.toolbarButton))
                    { MapThemeMigration.MigrateScene(); Reload(); }
                    if (GUILayout.Button("새 테마", EditorStyles.toolbarButton)) Create(false);
                    if (GUILayout.Button("복제", EditorStyles.toolbarButton) && selected != null) Create(true);
                    if (GUILayout.Button("저장", EditorStyles.toolbarButton))
                    {
                        AssetDatabase.SaveAssets();
                        if (game != null && game.gameObject.scene.IsValid() && game.gameObject.scene.isDirty)
                            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(game.gameObject.scene);
                    }
                }
            }
            RunnerMapPreview.DrawToolbar();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            float available = Mathf.Max(900, position.width - 34);
            Rect preview = GUILayoutUtility.GetRect(available, Mathf.Clamp(position.height * 0.46f, 250, 430));
            DrawPreview(preview);
            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawThemeList();
                if (selected != null)
                {
                    var serialized = new SerializedObject(selected);
                    serialized.Update();
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    {
                        using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(480)))
                        {
                            EditorGUILayout.PropertyField(serialized.FindProperty("displayName"), new GUIContent("테마 이름"));
                            EditorGUILayout.PropertyField(serialized.FindProperty("bonusOnly"), new GUIContent("보너스 전용 테마"));
                            DrawSlot(serialized, "upperObstacle", "High Object · 상단 장애물", EntityKind.ObstacleHang);
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                DrawSlot(serialized, "lowerObstacle", "Down Object · 하단 장애물", EntityKind.ObstacleLow);
                                DrawSlot(serialized, "tallObstacle", "높은 하단 장애물 · 2단 점프", EntityKind.ObstacleTall);
                            }
                            DrawSlot(serialized, "ground", "Ground · 바닥", null);
                        }
                        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(220)))
                        {
                            GUILayout.Label("Back Ground · 뒷배경", EditorStyles.boldLabel);
                            EditorGUILayout.PropertyField(serialized.FindProperty("background"), GUIContent.none);
                            DrawSprite(GUILayoutUtility.GetRect(200, 205), selected.background, false);
                        }
                        if (serialized.ApplyModifiedProperties()) Repaint();
                    }
                }
                else EditorGUILayout.HelpBox("기존 테마 가져오기를 눌러 현재 맵을 SO 데이터로 변환하세요.", MessageType.Info);
            }
            EditorGUILayout.Space(8);
            DrawRotation();
            EditorGUILayout.HelpBox("장애물의 Y 위치 보정(m)은 음수면 아래로, 양수면 위로 이동합니다. 미리보기에는 즉시 반영되며, 저장 후 다음 게임 실행부터 이미지와 충돌 판정에 함께 적용됩니다. 장애물 이미지·크기·판정 크기는 해당 프리팹에서 편집합니다.", MessageType.None);
            if (EditorApplication.isPlaying) EditorGUILayout.HelpBox("미리보기는 가능하며, 데이터 수정은 플레이 모드를 종료한 뒤 가능합니다.", MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        void DrawThemeList()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(160), GUILayout.MinHeight(285)))
            {
                GUILayout.Label("맵 테마", EditorStyles.boldLabel);
                themeScroll = EditorGUILayout.BeginScrollView(themeScroll, GUILayout.Height(240));
                foreach (var data in assets)
                {
                    if (data == null) continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (renaming == data)
                        {
                            GUI.SetNextControlName("MapThemeRename");
                            renameText = EditorGUILayout.TextField(renameText, GUILayout.Height(34));
                            if (focusRename)
                            {
                                EditorGUI.FocusTextInControl("MapThemeRename");
                                focusRename = false;
                            }
                            bool accept = GUILayout.Button("확인", GUILayout.Width(38), GUILayout.Height(34));
                            if (Event.current.type == EventType.KeyDown)
                            {
                                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                                { accept = true; Event.current.Use(); }
                                else if (Event.current.keyCode == KeyCode.Escape)
                                { renaming = null; GUI.FocusControl(null); Event.current.Use(); Repaint(); }
                            }
                            if (accept) CommitRename();
                        }
                        else
                        {
                            if (GUILayout.Toggle(selected == data, data.displayName, "Button", GUILayout.Height(34))) selected = data;
                            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                                if (GUILayout.Button(new GUIContent("이름", "테마 이름 변경 (F2)"), GUILayout.Width(38), GUILayout.Height(34)))
                                    BeginRename(data);
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
                GUILayout.Label("이름 버튼 또는 F2로 변경", EditorStyles.miniLabel);
                if (selected != null && GUILayout.Button("SO 에셋 위치 보기")) EditorGUIUtility.PingObject(selected);
            }
        }

        void BeginRename(MapThemeData data)
        {
            selected = renaming = data;
            renameText = data.displayName;
            focusRename = true;
            Repaint();
        }

        void CommitRename()
        {
            if (renaming == null) return;
            string value = (renameText ?? "").Trim();
            if (value.Length == 0)
            {
                ShowNotification(new GUIContent("테마 이름을 입력하세요."));
                focusRename = true;
                return;
            }
            var serialized = new SerializedObject(renaming);
            serialized.FindProperty("displayName").stringValue = value;
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(renaming);
            renaming = null;
            GUI.FocusControl(null);
            Repaint();
        }

        void DrawSlot(SerializedObject serialized, string property, string label, EntityKind? kind)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(220)))
            {
                GUILayout.Label(label, EditorStyles.boldLabel);
                var field = serialized.FindProperty(property);
                var type = kind.HasValue ? typeof(RunnerEntity) : typeof(Sprite);
                EditorGUI.BeginChangeCheck();
                var value = EditorGUILayout.ObjectField(field.objectReferenceValue, type, false);
                if (EditorGUI.EndChangeCheck())
                {
                    var entity = value as RunnerEntity;
                    if (entity == null || (entity.kind == kind && PrefabUtility.IsPartOfPrefabAsset(entity)))
                        field.objectReferenceValue = value;
                    else ShowNotification(new GUIContent("장애물 종류가 맞는 프리팹을 선택하세요: " + kind));
                }
                if (kind.HasValue)
                    EditorGUILayout.PropertyField(serialized.FindProperty(property + "YOffset"),
                        new GUIContent("Y 위치 보정 (m)", "기존 생성 위치 기준입니다. 0은 기존 높이, 음수는 아래로, 양수는 위로 이동합니다. 충돌 판정도 함께 이동합니다."));
                using (new EditorGUILayout.HorizontalScope())
                {
                    var sprite = SpriteOf(field.objectReferenceValue);
                    DrawSprite(GUILayoutUtility.GetRect(80, 45, GUILayout.ExpandWidth(true)), sprite, false);
                    if (field.objectReferenceValue != null && GUILayout.Button("열기", GUILayout.Width(48)))
                        AssetDatabase.OpenAsset(kind.HasValue ? ((RunnerEntity)field.objectReferenceValue).gameObject : field.objectReferenceValue);
                }
            }
        }

        void DrawRotation()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                game = (RunnerGame)EditorGUILayout.ObjectField("적용 대상 · MainGame", game, typeof(RunnerGame), true);
                if (game == null)
                {
                    EditorGUILayout.LabelField("SO 데이터는 연결된 MainGame에서 사용합니다. 씬을 열면 적용 순서를 수정할 수 있습니다.");
                    return;
                }
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    var serialized = new SerializedObject(game);
                    serialized.Update();
                    EditorGUILayout.PropertyField(serialized.FindProperty("themeDistance"), new GUIContent("테마 전환 거리 (m)"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("bonusMap"), new GUIContent("BonusMap · 기본 보너스 테마"));
                    rotationOpen = EditorGUILayout.Foldout(rotationOpen, "게임 적용 순서 · 마지막 테마 다음에는 처음으로 반복", true);
                    if (rotationOpen) EditorGUILayout.PropertyField(serialized.FindProperty("mapThemes"), new GUIContent("맵 테마 순서"), true);
                    serialized.ApplyModifiedProperties();
                }
            }
        }

        void Create(bool duplicate)
        {
            PrefabCatalog.Ensure(MapThemeMigration.DataFolder);
            var data = duplicate ? Instantiate(selected) : CreateInstance<MapThemeData>();
            data.displayName = duplicate ? selected.displayName + " 복사" : "새 테마";
            string path = AssetDatabase.GenerateUniqueAssetPath(MapThemeMigration.DataFolder + "/NewTheme.asset");
            AssetDatabase.CreateAsset(data, path);
            Undo.RegisterCreatedObjectUndo(data, "맵 테마 만들기");
            AssetDatabase.SaveAssets();
            selected = data;
            Reload();
        }

        void DrawPreview(Rect outer)
        {
            EditorGUI.DrawRect(outer, new Color(0.09f, 0.11f, 0.14f));
            Rect rect = new Rect(outer.x + 8, outer.y + 8, outer.width - 16, outer.height - 16);
            if (selected == null)
            {
                GUI.Label(rect, "맵 테마를 선택하세요", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            var map = RunnerMapPreview.FitRect(rect, RunnerMapPreview.Aspect);
            if (scenePreview == null) scenePreview = new RunnerMapPreview();
            scenePreview.Draw(map, selected, game);
            GUI.Label(new Rect(map.x + 10, map.y + 8, map.width - 20, 24), selected.displayName + " · 게임 렌더링 미리보기", EditorStyles.whiteBoldLabel);
        }

        internal static void DrawCookie(float x, float baseline, float unit)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2.Prefab/Characters/Cookie.prefab");
            if (prefab == null) return;
            var cookie = prefab.GetComponent<PlayerCookie>();
            if (cookie == null) return;
            foreach (var part in new[] { cookie.legBack, cookie.body, cookie.legFront })
            {
                if (part == null) continue;
                var sr = part.GetComponent<SpriteRenderer>();
                if (sr == null || !sr.enabled || !part.gameObject.activeSelf || sr.sprite == null) continue;
                Vector3 size = sr.sprite.bounds.size;
                Vector3 pivot = sr.sprite.bounds.center;
                var center = part.localPosition + Vector3.Scale(pivot, part.localScale);
                DrawSprite(new Rect(x + (center.x - size.x * part.localScale.x / 2) * unit,
                    baseline - (center.y + size.y * part.localScale.y / 2) * unit,
                    size.x * part.localScale.x * unit, size.y * part.localScale.y * unit), sr.sprite, false);
            }
        }

        internal static void DrawEntity(RunnerEntity entity, float x, float baseline, float unit, Sprite overrideSprite = null)
        {
            if (entity == null) return;
            foreach (var sr in entity.GetComponentsInChildren<SpriteRenderer>())
            {
                var sprite = overrideSprite != null && sr.transform == entity.transform ? overrideSprite : sr.sprite;
                if (sprite == null) continue;
                Vector3 scale = sr.transform.lossyScale;
                Vector3 size = Vector3.Scale(sprite.bounds.size, scale);
                Vector3 center = sr.transform.position - entity.transform.position + Vector3.Scale(sprite.bounds.center, scale);
                DrawSprite(new Rect(x + (center.x - size.x / 2) * unit, baseline - (center.y + size.y / 2) * unit,
                    Mathf.Abs(size.x) * unit, Mathf.Abs(size.y) * unit), sprite, false);
            }
        }

        static Sprite SpriteOf(Object value)
        {
            if (value is Sprite sprite) return sprite;
            var entity = value as RunnerEntity;
            var renderer = entity != null ? entity.GetComponentInChildren<SpriteRenderer>() : null;
            return renderer != null ? renderer.sprite : null;
        }

        internal static void DrawSprite(Rect rect, Sprite sprite, bool cover)
        {
            if (sprite == null || rect.width <= 0 || rect.height <= 0) return;
            Rect tex = sprite.textureRect;
            float ratio = tex.width / tex.height;
            float height = cover ? Mathf.Max(rect.height, rect.width / ratio) : Mathf.Min(rect.height, rect.width / ratio);
            var draw = new Rect(rect.center.x - height * ratio / 2, rect.center.y - height / 2, height * ratio, height);
            GUI.BeginClip(rect);
            draw.position -= rect.position;
            GUI.DrawTextureWithTexCoords(draw, sprite.texture,
                new Rect(tex.x / sprite.texture.width, tex.y / sprite.texture.height, tex.width / sprite.texture.width, tex.height / sprite.texture.height), true);
            GUI.EndClip();
        }
    }
}
