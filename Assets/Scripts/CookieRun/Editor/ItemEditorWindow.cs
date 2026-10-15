using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CookieRun.Editor
{
    public class ItemEditorWindow : EditorWindow
    {
        ItemData[] assets = Array.Empty<ItemData>();
        ItemData selected;
        RunnerGame game;
        Vector2 scroll, listScroll;
        bool showObstacles = true, showGrid = true, applicationOpen;
        int pointIndex = -1;
        bool guidePoint;
        bool focusName;
        Vector2 dragOffset;
        RunnerMapPreview scenePreview;
        float previewPan;
        static bool Locked => EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Tools/CookieRun/아이템 편집기")]
        public static void Open()
        {
            var window = GetWindow<ItemEditorWindow>("아이템 편집기");
            window.minSize = new Vector2(960, 720);
            window.Show();
        }

        public static void Open(ItemData data)
        {
            Open();
            GetWindow<ItemEditorWindow>().Select(data);
        }

        [OnOpenAsset]
        static bool OpenItem(int instanceId, int line)
        {
            var data = EditorUtility.InstanceIDToObject(instanceId) as ItemData;
            if (data == null) return false;
            Open(data);
            return true;
        }

        void OnEnable()
        {
            Reload();
            if (game != null && !Locked && (game.items == null || game.items.Length == 0))
                ItemDataMigration.EnsureForGame(game);
            Undo.undoRedoPerformed += OnUndo;
        }
        void OnDisable() { Undo.undoRedoPerformed -= OnUndo; scenePreview?.Dispose(); scenePreview = null; }
        void OnUndo() { pointIndex = -1; Repaint(); }
        void OnProjectChange() { scenePreview?.Invalidate(); Reload(); Repaint(); }

        void Reload()
        {
            assets = AssetDatabase.FindAssets("t:ItemData").Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<ItemData>).Where(d => d != null).ToArray();
            if (selected == null && assets.Length > 0) Select(assets[0]);
            if (game == null) game = RunnerMapPreview.FindLoadedGame();
        }

        void Select(ItemData data) { selected = data; pointIndex = -1; previewPan = 0; Repaint(); }

        void OnGUI()
        {
            if (!Locked && selected != null && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F2)
            { focusName = true; Event.current.Use(); }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("아이템 SO", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                showObstacles = GUILayout.Toggle(showObstacles, "장애물 보기", EditorStyles.toolbarButton);
                showGrid = GUILayout.Toggle(showGrid, "격자·배치 표시", EditorStyles.toolbarButton);
                using (new EditorGUI.DisabledScope(Locked))
                {
                    if (GUILayout.Button("기존 아이템 가져오기", EditorStyles.toolbarButton)) { ItemDataMigration.MigrateScene(); Reload(); }
                    if (GUILayout.Button("새 아이템", EditorStyles.toolbarButton)) Create(false);
                    using (new EditorGUI.DisabledScope(selected == null))
                        if (GUILayout.Button("복제", EditorStyles.toolbarButton)) Create(true);
                    if (GUILayout.Button("저장", EditorStyles.toolbarButton))
                    {
                        AssetDatabase.SaveAssets();
                        if (game != null && game.gameObject.scene.isDirty) EditorSceneManager.SaveScene(game.gameObject.scene);
                        ShowNotification(new GUIContent("아이템 데이터를 저장했습니다."));
                    }
                }
            }
            RunnerMapPreview.DrawToolbar();
            if (selected != null)
                previewPan = EditorGUILayout.Slider("미리보기 가로 이동 (m)", previewPan, -4, Mathf.Max(4, selected.Length - 4));
            scroll = EditorGUILayout.BeginScrollView(scroll);
            Rect preview = GUILayoutUtility.GetRect(Mathf.Max(900, position.width - 34), Mathf.Clamp(position.height * 0.47f, 280, 440));
            DrawPreview(preview);
            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawList();
                using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(650)))
                {
                    if (selected == null) EditorGUILayout.HelpBox("기존 아이템 가져오기를 눌러 현재 아이템을 SO로 변환하세요.", MessageType.Info);
                    else using (new EditorGUI.DisabledScope(Locked))
                    {
                        var serialized = new SerializedObject(selected);
                        serialized.Update();
                        DrawDetails(serialized);
                        serialized.ApplyModifiedProperties();
                    }
                }
            }
            EditorGUILayout.Space(6);
            DrawApplication();
            if (Locked) EditorGUILayout.HelpBox("플레이 중에는 미리보기만 가능합니다. 수정하려면 플레이 모드를 종료하세요.", MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        void DrawList()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(190), GUILayout.MinHeight(320)))
            {
                GUILayout.Label("아이템 리스트", EditorStyles.boldLabel);
                listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Height(280));
                foreach (var data in assets)
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Toggle(selected == data, data.displayName, "Button", GUILayout.Height(35)) && selected != data) Select(data);
                        using (new EditorGUI.DisabledScope(Locked))
                            if (GUILayout.Button("이름", GUILayout.Width(38), GUILayout.Height(35))) { Select(data); focusName = true; }
                    }
                EditorGUILayout.EndScrollView();
                if (selected != null && GUILayout.Button("SO 에셋 위치 보기")) EditorGUIUtility.PingObject(selected);
            }
        }

        void DrawDetails(SerializedObject serialized)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("아이템 이름 · 이미지", EditorStyles.boldLabel);
                GUI.SetNextControlName("ItemName");
                EditorGUILayout.PropertyField(serialized.FindProperty("displayName"), new GUIContent("아이템 이름"));
                if (focusName) { EditorGUI.FocusTextInControl("ItemName"); focusName = false; }
                var kind = serialized.FindProperty("kind");
                int index = Array.IndexOf(ItemDataMigration.Kinds, (EntityKind)kind.intValue);
                EditorGUI.BeginChangeCheck();
                int next = EditorGUILayout.Popup("아이템 종류", Mathf.Max(0, index), ItemDataMigration.Labels);
                if (EditorGUI.EndChangeCheck()) kind.intValue = (int)ItemDataMigration.Kinds[next];
                var prefab = serialized.FindProperty("prefab");
                EditorGUI.BeginChangeCheck();
                var value = (RunnerEntity)EditorGUILayout.ObjectField("아이템 프리팹", prefab.objectReferenceValue, typeof(RunnerEntity), false);
                if (EditorGUI.EndChangeCheck())
                {
                    if (value == null || (value.kind == (EntityKind)kind.intValue && PrefabUtility.IsPartOfPrefabAsset(value))) prefab.objectReferenceValue = value;
                    else ShowNotification(new GUIContent("선택한 아이템 종류와 같은 프리팹을 넣으세요."));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(serialized.FindProperty("sprite"), new GUIContent("교체 이미지", "비우면 프리팹 이미지 사용"));
                    if (prefab.objectReferenceValue != null && GUILayout.Button("프리팹 열기", GUILayout.Width(94)))
                        AssetDatabase.OpenAsset(((RunnerEntity)prefab.objectReferenceValue).gameObject);
                }
                if (value == null || value.kind != (EntityKind)kind.intValue)
                    EditorGUILayout.HelpBox("아이템 종류에 맞는 프리팹을 연결하세요.", MessageType.Warning);
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("아이템 효과", EditorStyles.boldLabel);
                ItemDataEditor.DrawEffect(serialized);
                var slot = (EntityKind)serialized.FindProperty("kind").intValue;
                if (slot == EntityKind.Magnet || slot == EntityKind.Boost || slot == EntityKind.Dash)
                    EditorGUILayout.PropertyField(serialized.FindProperty("spawnWeight"), new GUIContent("랜덤 등장 가중치", "다른 특수 아이템과 비교한 등장 비율. 0이면 제외"));
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("아이템 배치", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serialized.FindProperty("patternLength"), new GUIContent("구간 길이 (m)"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ 아이템")) { AddPoint(serialized, "positions", new Vector2(selected.Length / 2, 2.3f)); guidePoint = false; }
                    using (new EditorGUI.DisabledScope(selected.kind == EntityKind.Jelly))
                        if (GUILayout.Button("+ 안내 젤리")) { AddPoint(serialized, "guideJellies", new Vector2(selected.Length / 2, 0.6f)); guidePoint = true; }
                    var array = serialized.FindProperty(guidePoint ? "guideJellies" : "positions");
                    using (new EditorGUI.DisabledScope(pointIndex < 0 || pointIndex >= array.arraySize))
                        if (GUILayout.Button("선택한 배치 삭제")) { array.DeleteArrayElementAtIndex(pointIndex); pointIndex = -1; }
                }
                var points = serialized.FindProperty(guidePoint ? "guideJellies" : "positions");
                if (pointIndex >= 0 && pointIndex < points.arraySize)
                {
                    var point = points.GetArrayElementAtIndex(pointIndex);
                    EditorGUI.BeginChangeCheck();
                    var position = EditorGUILayout.Vector2Field((guidePoint ? "안내 젤리 " : "아이템 ") + (pointIndex + 1) + " 위치 (m)", point.vector2Value);
                    if (EditorGUI.EndChangeCheck()) point.vector2Value = selected.PositionAt(position);
                }
                EditorGUILayout.PropertyField(serialized.FindProperty("positions"), new GUIContent("아이템 위치 · X: 구간 시작 / Y: 바닥"), true);
                EditorGUILayout.PropertyField(serialized.FindProperty("guideJellies"), new GUIContent("안내 젤리 위치"), true);
                string hint = selected.kind == EntityKind.BigJelly
                    ? "큰 별 젤리의 배치는 위에서 시험할 수 있습니다. 실제 큰 별 젤리는 점프 구간의 정점에 자동 배치됩니다."
                    : "위 미리보기의 아이템을 드래그해 배치하세요. 저장한 배치는 평지 젤리 또는 해당 아이템 구간에 적용됩니다.";
                EditorGUILayout.HelpBox(hint, MessageType.None);
            }
        }

        void AddPoint(SerializedObject serialized, string name, Vector2 point)
        {
            var array = serialized.FindProperty(name);
            pointIndex = array.arraySize++;
            array.GetArrayElementAtIndex(pointIndex).vector2Value = point;
        }

        void DrawApplication()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                game = (RunnerGame)EditorGUILayout.ObjectField("적용 대상 · MainGame", game, typeof(RunnerGame), true);
                if (game == null) { GUILayout.Label("MainGame 씬을 열면 게임에 사용할 아이템 SO를 연결할 수 있습니다."); return; }
                using (new EditorGUI.DisabledScope(Locked))
                {
                    var serialized = new SerializedObject(game);
                    serialized.Update();
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        applicationOpen = EditorGUILayout.Foldout(applicationOpen, "게임에서 사용하는 아이템 SO", true);
                        using (new EditorGUI.DisabledScope(selected == null || !selected.IsValid))
                            if (GUILayout.Button("선택한 SO를 게임에 연결", GUILayout.Width(185))) ConnectSelected(serialized);
                    }
                    if (applicationOpen)
                    {
                        EditorGUILayout.PropertyField(serialized.FindProperty("items"), new GUIContent("아이템 목록 · 종류별 하나"), true);
                        EditorGUILayout.PropertyField(serialized.FindProperty("itemFrequency"), new GUIContent("전체 등장 빈도 배율"));
                        EditorGUILayout.PropertyField(serialized.FindProperty("itemPatternChance"), new GUIContent("특수 아이템 구간 확률"));
                        EditorGUILayout.PropertyField(serialized.FindProperty("potionInterval"), new GUIContent("체력 아이템 기준 간격 (m)"));
                    }
                    serialized.ApplyModifiedProperties();
                }
                var entries = (game.items ?? Array.Empty<ItemData>()).Where(i => i != null).ToArray();
                if (entries.Any(i => !i.IsValid) || entries.GroupBy(i => i.kind).Any(g => g.Count() > 1))
                    EditorGUILayout.HelpBox("잘못된 프리팹이나 중복된 아이템 종류가 있습니다. 같은 종류는 첫 번째 유효한 SO를 사용합니다.", MessageType.Warning);
                if (selected != null && !entries.Contains(selected))
                    EditorGUILayout.HelpBox("이 SO를 게임에 사용하려면 '선택한 SO를 게임에 연결'을 누르세요.", MessageType.Info);
            }
        }

        void ConnectSelected(SerializedObject serialized)
        {
            var array = serialized.FindProperty("items");
            int index = -1;
            for (int i = array.arraySize - 1; i >= 0; i--)
            {
                var entry = array.GetArrayElementAtIndex(i).objectReferenceValue as ItemData;
                if (entry == null || entry.kind != selected.kind) continue;
                if (index >= 0)
                {
                    array.GetArrayElementAtIndex(index).objectReferenceValue = null;
                    array.DeleteArrayElementAtIndex(index);
                }
                index = i;
            }
            if (index < 0) index = array.arraySize++;
            array.GetArrayElementAtIndex(index).objectReferenceValue = selected;
        }

        void Create(bool duplicate)
        {
            PrefabCatalog.Ensure(ItemDataMigration.DataFolder);
            var data = duplicate ? Instantiate(selected) : CreateInstance<ItemData>();
            data.displayName = duplicate ? selected.displayName + " 복사" : "새 아이템";
            if (!duplicate)
            {
                var source = selected != null ? selected : assets.FirstOrDefault();
                if (source != null) { data.kind = source.kind; data.prefab = source.prefab; data.effect = source.effect; data.previewTheme = source.previewTheme; }
            }
            AssetDatabase.CreateAsset(data, AssetDatabase.GenerateUniqueAssetPath(ItemDataMigration.DataFolder + "/NewItem.asset"));
            Undo.RegisterCreatedObjectUndo(data, "아이템 SO 만들기");
            AssetDatabase.SaveAssets();
            Select(data);
            Reload();
        }

        void DrawPreview(Rect outer)
        {
            EditorGUI.DrawRect(outer, new Color(0.09f, 0.11f, 0.14f));
            if (selected == null) { GUI.Label(outer, "아이템을 선택하세요", EditorStyles.centeredGreyMiniLabel); return; }
            Rect inner = new Rect(outer.x + 8, outer.y + 8, outer.width - 16, outer.height - 16);
            var map = RunnerMapPreview.FitRect(inner, RunnerMapPreview.Aspect);
            float width = map.width, height = map.height;
            var theme = selected.previewTheme;
            if (scenePreview == null) scenePreview = new RunnerMapPreview();
            scenePreview.Draw(map, theme, game, selected, showObstacles, previewPan);
            GUI.BeginClip(map);
            float unit = RunnerMapPreview.Unit(map, RunnerMapPreview.Aspect);
            float baseline = RunnerMapPreview.Project(map, RunnerMapPreview.Aspect, new Vector2(0, RunnerGame.GroundY), previewPan).y;
            float start = RunnerMapPreview.Project(map, RunnerMapPreview.Aspect, new Vector2(RunnerMapPreview.PatternStart, 0), previewPan).x;
            if (showGrid)
            {
                Handles.BeginGUI();
                Handles.color = new Color(1, 1, 1, 0.2f);
                for (float x = start; x <= start + selected.Length * unit + 0.1f; x += unit)
                    Handles.DrawLine(new Vector3(x, 0), new Vector3(x, baseline));
                for (float y = baseline; y >= 0; y -= unit)
                    Handles.DrawLine(new Vector3(start, y), new Vector3(start + selected.Length * unit, y));
                Handles.color = Color.white;
                Handles.EndGUI();
            }
            var jelly = game != null && game.items != null ? game.items.FirstOrDefault(d => d != null && d.kind == EntityKind.Jelly) : assets.FirstOrDefault(d => d.kind == EntityKind.Jelly);
            DrawPoints(selected.guideJellies, jelly != null ? jelly.prefab : null, jelly != null ? jelly.sprite : null, start, baseline, unit, true);
            DrawPoints(selected.positions, selected.prefab, selected.sprite, start, baseline, unit, false);
            HandleDrag(start, baseline, unit, new Rect(0, 0, width, height),
                new Rect(outer.x + 12 - map.x, outer.y + 12 - map.y, 215, 76));
            GUI.Label(new Rect(10, height - 27, width - 20, 24), selected.displayName + " · " + (theme != null ? theme.displayName : "맵 테마 SO를 넣으세요"), EditorStyles.whiteBoldLabel);
            GUI.EndClip();

            GUILayout.BeginArea(new Rect(outer.x + 12, outer.y + 12, 215, 76), EditorStyles.helpBox);
            GUILayout.Label("Map Theme · 맵 테마 SO", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(Locked))
            {
                var serialized = new SerializedObject(selected);
                serialized.Update();
                EditorGUILayout.PropertyField(serialized.FindProperty("previewTheme"), GUIContent.none);
                serialized.ApplyModifiedProperties();
            }
            GUILayout.EndArea();
        }

        void DrawPoints(Vector2[] points, RunnerEntity prefab, Sprite sprite, float start, float baseline, float unit, bool guides)
        {
            if (points == null) return;
            for (int i = 0; i < points.Length; i++)
            {
                var point = selected.PositionAt(points[i]);
                Vector2 screen = new Vector2(start + point.x * unit, baseline - point.y * unit);
                if (!showGrid && !(guidePoint == guides && pointIndex == i)) continue;
                Handles.BeginGUI();
                Handles.color = guidePoint == guides && pointIndex == i ? Color.yellow : guides ? new Color(0.5f, 0.9f, 1f) : Color.white;
                Handles.DrawWireDisc(screen, Vector3.forward, Mathf.Max(11, unit * 0.48f));
                Handles.color = Color.white;
                Handles.EndGUI();
                GUI.Label(new Rect(screen.x - 12, screen.y + unit * 0.5f, 45, 18), (guides ? "J" : "") + (i + 1), EditorStyles.whiteMiniLabel);
            }
        }

        void HandleDrag(float start, float baseline, float unit, Rect bounds, Rect themeSlot)
        {
            int control = GUIUtility.GetControlID("ItemPlacementDrag".GetHashCode(), FocusType.Passive);
            var e = Event.current;
            if (Locked)
            {
                if (GUIUtility.hotControl == control) GUIUtility.hotControl = 0;
                return;
            }
            if (e.type == EventType.MouseDown && e.button == 0 && bounds.Contains(e.mousePosition) && !themeSlot.Contains(e.mousePosition))
            {
                // Main item handles take precedence over guide jellies.
                for (int group = 0; group < 2; group++)
                {
                    var points = group == 0 ? selected.positions : selected.guideJellies;
                    if (points == null) continue;
                    for (int i = points.Length - 1; i >= 0; i--)
                    {
                        var point = selected.PositionAt(points[i]);
                        var screen = new Vector2(start + point.x * unit, baseline - point.y * unit);
                        if ((e.mousePosition - screen).sqrMagnitude > Mathf.Pow(Mathf.Max(13, unit * 0.65f), 2)) continue;
                        Undo.RegisterCompleteObjectUndo(selected, "아이템 배치 이동");
                        pointIndex = i; guidePoint = group == 1;
                        dragOffset = e.mousePosition - screen;
                        GUIUtility.hotControl = control;
                        GUIUtility.keyboardControl = 0;
                        e.Use(); Repaint(); return;
                    }
                }
            }
            else if (GUIUtility.hotControl == control)
            {
                if (e.type == EventType.MouseDrag)
                {
                    var points = guidePoint ? selected.guideJellies : selected.positions;
                    if (points != null && pointIndex >= 0 && pointIndex < points.Length)
                    {
                        var mouse = e.mousePosition - dragOffset;
                        points[pointIndex] = selected.PositionAt(new Vector2((mouse.x - start) / unit, (baseline - mouse.y) / unit));
                        EditorUtility.SetDirty(selected);
                    }
                    e.Use(); Repaint();
                }
                else if (e.type == EventType.MouseUp) { GUIUtility.hotControl = 0; e.Use(); }
            }
        }
    }

    [CustomEditor(typeof(ItemData))]
    public class ItemDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("아이템 편집기 열기", GUILayout.Height(28))) ItemEditorWindow.Open((ItemData)target);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode)) DrawDefaultInspector();
        }

        internal static void DrawEffect(SerializedObject serialized)
        {
            EditorGUILayout.PropertyField(serialized.FindProperty("effect"), new GUIContent("효과 종류"));
            var effect = (ItemEffect)serialized.FindProperty("effect").intValue;
            if (effect == ItemEffect.Score) EditorGUILayout.PropertyField(serialized.FindProperty("score"), new GUIContent("획득 점수"));
            else if (effect == ItemEffect.Heal) EditorGUILayout.PropertyField(serialized.FindProperty("healAmount"), new GUIContent("체력 회복량"));
            else if (effect == ItemEffect.BonusStage)
            {
                EditorGUILayout.PropertyField(serialized.FindProperty("score"), new GUIContent("획득 점수"));
                EditorGUILayout.PropertyField(serialized.FindProperty("bonusRequiredCount"), new GUIContent("필요 획득 개수 (N개)"));
                EditorGUILayout.PropertyField(serialized.FindProperty("bonusDuration"), new GUIContent("보너스 지속 시간 (초)"));
                EditorGUILayout.PropertyField(serialized.FindProperty("bonusTheme"), new GUIContent("보너스 맵 테마 SO", "비우면 MainGame의 BonusMap 사용"));
                EditorGUILayout.HelpBox("설정한 개수를 모으면 무적 보너스 맵으로 전환됩니다. 체력 감소가 멈추고 젤리가 많은 구간이 나오며, 시간이 끝나면 일반 맵으로 돌아옵니다.", MessageType.None);
            }
            else
            {
                EditorGUILayout.PropertyField(serialized.FindProperty("duration"), new GUIContent("지속 시간 (초)"));
                if (effect == ItemEffect.Magnet)
                {
                    EditorGUILayout.PropertyField(serialized.FindProperty("magnetRadius"), new GUIContent("끌어오는 범위 (m)"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("magnetPullSpeed"), new GUIContent("끌어오는 추가 속도"));
                }
                else EditorGUILayout.PropertyField(serialized.FindProperty("speedMultiplier"), new GUIContent("맵 속도 배율"));
            }
        }
    }
}
