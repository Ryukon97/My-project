using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CookieRun
{
    /// <summary>
    /// 메뉴 CookieRun > Build Scenes And Prefabs.
    /// 4.Sound(WAV), 3.Sprite(PNG), 2.Prefab(프리팹), 1.Scene(Title/MainGame) 을 만든다.
    /// 이미 있는 파일은 건드리지 않으므로, 직접 고친 이미지/프리팹/씬은 다시 실행해도 유지된다.
    /// 기본 상태로 되돌리고 싶은 파일만 지우고 다시 실행하면 된다.
    /// 예외: 나중에 추가된 기능(맵 테마, 질주 아이템, 테마 이름 표시)이 기존 MainGame 씬/GameHUD 프리팹에
    /// 아직 연결되어 있지 않으면 비어 있는 칸만 채워 넣는다.
    /// </summary>
    public static class CookieRunSetup
    {
        const string SceneDir = "Assets/1.Scene";
        const string PrefabDir = "Assets/2.Prefab";
        const string UiDir = PrefabDir + "/UI";
        const string SpriteDir = "Assets/3.Sprite";
        const string SoundDir = "Assets/4.Sound";
        const string TitleScene = SceneDir + "/Title.unity";
        const string GameScene = SceneDir + "/MainGame.unity";

        static readonly Color Brown = new Color(0.25f, 0.12f, 0.08f);
        static readonly Color Cream = new Color(1f, 0.95f, 0.84f);
        static readonly Color Orange = new Color(1f, 0.6f, 0.2f);

        static readonly string[] EntityNames =
        {
            "Jelly", "BigJelly", "Potion", "Magnet", "Boost", "ObstacleLow", "ObstacleTall", "ObstacleHang", "Dash"
        };

        static Font font;
        static Sprite panel, circle;

        [MenuItem("CookieRun/Build Scenes And Prefabs")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[CookieRun] 플레이 모드를 끈 뒤 실행하세요.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            foreach (var dir in new[] { SceneDir, PrefabDir, UiDir, SpriteDir, SoundDir }) Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            // 새로 만드는 글자는 프로젝트 폰트(여기어때 잘난체 고딕)를 쓰고, 없으면 Unity 기본 폰트로 대신한다.
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/JalnanGothic.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panel = ExportSprite("UI_Panel", SpriteFactory.Panel, new Vector4(24, 24, 24, 24));
            circle = ExportSprite("UI_Circle", SpriteFactory.Circle);

            // ----- 이미지 -----
            ExportSprite("Sky", SpriteFactory.Sky);
            ExportThemeSprites();
            var cloudSprite = ExportSprite("Cloud", SpriteFactory.Cloud);
            var hillSprite = ExportSprite("Hill", SpriteFactory.Hill);
            var jellySprite = ExportSprite("Jelly", SpriteFactory.Jelly);

            // ----- 프리팹 -----
            Prefab(PrefabDir + "/Cookie.prefab", () => BuildCookie(
                ExportSprite("CookieBody", SpriteFactory.CookieBody), ExportSprite("CookieLeg", SpriteFactory.CookieLeg)));

            Prefab(PrefabDir + "/Ground.prefab", () =>
            {
                var sr = SpriteObject("Ground", ExportSprite("Ground", SpriteFactory.Ground), 0);
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(6, 3);
                return sr.gameObject;
            });

            Prefab(PrefabDir + "/Cloud.prefab", () => SpriteObject("Cloud", cloudSprite, -90).gameObject);
            Prefab(PrefabDir + "/Hill.prefab", () => SpriteObject("Hill", hillSprite, -80).gameObject);

            Entity("Jelly", EntityKind.Jelly, jellySprite, 0.35f, 0.35f, 0, 10, e => e.randomTints = new[]
            {
                new Color(1f, 0.85f, 0.2f), new Color(1f, 0.5f, 0.7f),
                new Color(0.4f, 0.85f, 1f), new Color(0.55f, 0.95f, 0.45f)
            });
            Entity("BigJelly", EntityKind.BigJelly, jellySprite, 0.5f, 0.5f, 0, 100, e =>
            {
                e.transform.localScale = Vector3.one * 1.6f;
                e.GetComponent<SpriteRenderer>().color = new Color(0.75f, 0.5f, 1f);
            });
            Entity("Potion", EntityKind.Potion, ExportSprite("Heart", SpriteFactory.Heart), 0.4f, 0.4f, 0, 0);
            Entity("Magnet", EntityKind.Magnet, ExportSprite("Magnet", SpriteFactory.Magnet), 0.4f, 0.4f, 0, 0);
            Entity("Boost", EntityKind.Boost, ExportSprite("Boost", SpriteFactory.Bolt), 0.4f, 0.4f, 0, 0);
            Entity("ObstacleLow", EntityKind.ObstacleLow, ExportSprite("ObstacleLow", SpriteFactory.ObstacleLow), 0.28f, 0.42f, 0.45f, 30);
            Entity("ObstacleTall", EntityKind.ObstacleTall, ExportSprite("ObstacleTall", SpriteFactory.ObstacleTall), 0.22f, 1.1f, 1.15f, 30);
            Entity("ObstacleHang", EntityKind.ObstacleHang, ExportSprite("ObstacleHang", SpriteFactory.ObstacleHang), 0.45f, 3.2f, 3.3f, 30);

            Entity("Dash", EntityKind.Dash, ExportSprite("Dash", SpriteFactory.Dash), 0.4f, 0.4f, 0, 0);

            Prefab(PrefabDir + "/GameAudio.prefab", () =>
            {
                var audio = new GameObject("GameAudio").AddComponent<GameAudio>();
                audio.bgm = ExportSound("BGM", SoundFactory.Bgm);
                audio.jump = ExportSound("SFX_Jump", SoundFactory.Jump);
                audio.jelly = ExportSound("SFX_Jelly", SoundFactory.Jelly);
                audio.item = ExportSound("SFX_Item", SoundFactory.Item);
                audio.hit = ExportSound("SFX_Hit", SoundFactory.Hit);
                audio.gameOver = ExportSound("SFX_GameOver", SoundFactory.GameOver);
                audio.button = ExportSound("SFX_Button", SoundFactory.Button);
                return audio.gameObject;
            });

            Prefab(UiDir + "/SettingsPanel.prefab", BuildSettingsPanel);
            UpgradeSettingsPanel();
            Prefab(UiDir + "/GameHUD.prefab", BuildHud);
            UpgradeHud();
            Prefab(UiDir + "/RankingPanel.prefab", BuildRankingPanel);
            Prefab(UiDir + "/TitleUI.prefab", BuildTitleUi);
            UpgradeTitleUi();
            AssetDatabase.SaveAssets();

            // ----- 씬 -----
            bool newTitleScene = !File.Exists(TitleScene);
            var titleScene = newTitleScene
                ? EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)
                : EditorSceneManager.OpenScene(TitleScene);
            if (newTitleScene)
            {
                MakeCamera();
                MakeBackground(out _, out _);
                MakeGroundStrip();
                Instance("Cookie").transform.position = new Vector3(-4.5f, RunnerGame.GroundY, 0);
                Instance("Jelly").transform.position = new Vector3(-2.8f, RunnerGame.GroundY + 0.9f, 0);
                Instance("UI/TitleUI");
                MakeEventSystem();
            }
            if (EnsureAudio(titleScene) || newTitleScene)
            {
                EditorSceneManager.MarkSceneDirty(titleScene);
                EditorSceneManager.SaveScene(titleScene, TitleScene);
            }

            bool newGameScene = !File.Exists(GameScene);
            var gameScene = newGameScene
                ? EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)
                : EditorSceneManager.OpenScene(GameScene);
            RunnerGame game = null;
            if (newGameScene)
            {
                game = new GameObject("Game").AddComponent<RunnerGame>();
                game.cam = MakeCamera();
                MakeBackground(out game.cloudsRoot, out game.hillsRoot);
                game.world = new GameObject("World").transform;
                var player = Instance("Cookie");
                player.transform.position = new Vector3(PlayerCookie.X, RunnerGame.GroundY, 0);
                game.player = player.GetComponent<PlayerCookie>();
                game.hud = Instance("UI/GameHUD").GetComponent<GameHud>();
                game.groundPrefab = LoadPrefab("Ground").GetComponent<SpriteRenderer>();
                MakeEventSystem();
            }
            else
            {
                foreach (var root in gameScene.GetRootGameObjects())
                {
                    game = root.GetComponentInChildren<RunnerGame>(true);
                    if (game != null) break;
                }
            }
            bool gameChanged = game != null && UpgradeGame(game);
            bool audioAdded = EnsureAudio(gameScene);
            if (gameChanged || audioAdded || newGameScene)
            {
                EditorSceneManager.MarkSceneDirty(gameScene);
                EditorSceneManager.SaveScene(gameScene, GameScene);
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScene, true),
                new EditorBuildSettingsScene(GameScene, true)
            };

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(TitleScene);
            Debug.Log("[CookieRun] 완료: 1.Scene (Title, MainGame), 2.Prefab, 3.Sprite, 4.Sound");
        }

        // ---------- 맵 테마 ----------

        static Color Rgb(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

        static void ExportThemeSprites()
        {
            ExportSprite("Sky_Forest", () => SpriteFactory.Sky(Rgb(0.82f, 0.95f, 0.75f), Rgb(0.35f, 0.7f, 0.6f)));
            ExportSprite("Sky_Rock", () => SpriteFactory.Sky(Rgb(0.88f, 0.9f, 0.94f), Rgb(0.45f, 0.56f, 0.72f)));
            ExportSprite("Sky_Lava", () => SpriteFactory.Sky(Rgb(1f, 0.55f, 0.2f), Rgb(0.25f, 0.05f, 0.08f)));
            ExportSprite("Sky_Cave", () => SpriteFactory.Sky(Rgb(0.2f, 0.18f, 0.3f), Rgb(0.05f, 0.05f, 0.1f)));

            ExportSprite("Ground_Forest", () => SpriteFactory.Ground(Rgb(0.45f, 0.78f, 0.3f), Rgb(0.45f, 0.3f, 0.18f), Rgb(0.36f, 0.23f, 0.13f)));
            ExportSprite("Ground_Rock", () => SpriteFactory.Ground(Rgb(0.8f, 0.82f, 0.85f), Rgb(0.5f, 0.52f, 0.56f), Rgb(0.4f, 0.42f, 0.46f)));
            ExportSprite("Ground_Lava", () => SpriteFactory.Ground(Rgb(1f, 0.5f, 0.1f), Rgb(0.2f, 0.12f, 0.12f), Rgb(0.6f, 0.15f, 0.05f)));
            ExportSprite("Ground_Cave", () => SpriteFactory.Ground(Rgb(0.5f, 0.45f, 0.7f), Rgb(0.2f, 0.18f, 0.28f), Rgb(0.14f, 0.12f, 0.2f)));

            ExportSprite("Hill_Forest", SpriteFactory.HillForest);
            ExportSprite("Hill_Rock", SpriteFactory.HillRock);
            ExportSprite("Hill_Lava", SpriteFactory.HillLava);
            ExportSprite("Hill_Cave", SpriteFactory.HillCave);
        }

        static Sprite LoadSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{name}.png");
        }

        static MapTheme Theme(string name, string suffix, Color hillA, Color hillB, Color cloud)
        {
            return new MapTheme
            {
                name = name,
                sky = LoadSprite("Sky" + suffix),
                ground = LoadSprite("Ground" + suffix),
                hill = LoadSprite("Hill" + suffix),
                hillColorA = hillA,
                hillColorB = hillB,
                cloudColor = cloud
            };
        }

        static MapTheme[] DefaultThemes()
        {
            var white = Color.white;
            var dim = Rgb(0.85f, 0.85f, 0.85f);
            return new[]
            {
                Theme("기본", "", Rgb(1f, 0.72f, 0.75f), Rgb(0.98f, 0.8f, 0.6f), white),
                Theme("숲", "_Forest", white, dim, white),
                Theme("돌산", "_Rock", white, dim, Rgb(0.85f, 0.87f, 0.9f)),
                Theme("용암", "_Lava", white, dim, Rgb(0.3f, 0.2f, 0.2f, 0.7f)),
                Theme("동굴", "_Cave", white, dim, Rgb(1f, 1f, 1f, 0f)),
            };
        }

        /// <summary>기존 MainGame 씬에 새 기능의 빈 칸만 채운다. 바뀐 것이 있으면 true.</summary>
        static bool UpgradeGame(RunnerGame game)
        {
            bool changed = false;

            if (game.sky == null && game.cloudsRoot != null && game.cloudsRoot.parent != null)
            {
                var found = game.cloudsRoot.parent.Find("Sky");
                if (found != null)
                {
                    game.sky = found.GetComponent<SpriteRenderer>();
                    changed = true;
                }
            }

            if (game.themes == null || game.themes.Length == 0)
            {
                game.themes = DefaultThemes();
                changed = true;
            }

            var list = new List<RunnerEntity>(game.entityPrefabs ?? new RunnerEntity[0]);
            foreach (var name in EntityNames)
            {
                var prefab = LoadPrefab(name);
                var entity = prefab != null ? prefab.GetComponent<RunnerEntity>() : null;
                if (entity == null || list.Exists(x => x != null && x.kind == entity.kind)) continue;
                list.Add(entity);
                changed = true;
            }
            game.entityPrefabs = list.ToArray();

            if (CookieRun.Editor.MapThemeMigration.EnsureForGame(game)) changed = true;
            if (CookieRun.Editor.ItemDataMigration.EnsureForGame(game)) changed = true;

            if (changed) EditorUtility.SetDirty(game);
            return changed;
        }

        const string OldHint = "점프: Space / 화면 왼쪽 (공중에서 한 번 더!)      슬라이드: ↓ / 화면 오른쪽 누르고 있기";
        const string NewHint = "점프: Space / Jump (공중에서 한 번 더!)      슬라이드: ↓ / Slide (공중에서는 급강하)";

        /// <summary>기존 GameHUD 프리팹에 테마 이름, Jump/Slide 버튼, 일시정지 버튼·설정 창·카운트다운이 없으면 추가한다.</summary>
        static void UpgradeHud()
        {
            string path = UiDir + "/GameHUD.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var hud = root.GetComponent<GameHud>();
            bool changed = false;
            if (hud != null)
            {
                // 게임 오버 창보다 뒤에 그려지도록 그 앞 순서에 둔다.
                void PlaceBehindGameOver(Transform t)
                {
                    if (hud.gameOverPanel != null) t.SetSiblingIndex(hud.gameOverPanel.transform.GetSiblingIndex());
                    changed = true;
                }

                if (hud.themeText == null)
                {
                    hud.themeText = MakeText("ThemeName", root.transform, new Vector2(0.5f, 1), new Vector2(0, -190), 80,
                        TextAnchor.UpperCenter, Color.white, "기본");
                    PlaceBehindGameOver(hud.themeText.transform);
                }
                if (hud.jumpButton == null)
                {
                    hud.jumpButton = MakeHoldButton("JumpButton", root.transform, new Vector2(0, 0), new Vector2(50, 40), "Jump");
                    PlaceBehindGameOver(hud.jumpButton.transform);
                }
                if (hud.slideButton == null)
                {
                    hud.slideButton = MakeHoldButton("SlideButton", root.transform, new Vector2(1, 0), new Vector2(-50, 40), "Slide");
                    PlaceBehindGameOver(hud.slideButton.transform);
                }
                if (hud.pauseButton == null)
                {
                    hud.pauseButton = MakeButton("PauseButton", root.transform, new Vector2(-40, -40), new Vector2(110, 110),
                        new Color(0.55f, 0.75f, 0.95f), "II", new Vector2(1, 1), 56);
                    PlaceBehindGameOver(hud.pauseButton.transform);
                    // 점수 글자가 기본 위치 그대로라면 버튼 자리만큼 왼쪽으로 옮긴다.
                    MoveIfAt(hud.scoreText, new Vector2(-50, -30), new Vector2(-180, -30));
                    MoveIfAt(hud.bestText, new Vector2(-50, -110), new Vector2(-180, -110));
                }
                if (hud.countdownText == null)
                {
                    hud.countdownText = MakeText("Countdown", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 60), 300,
                        TextAnchor.MiddleCenter, Color.white, "3");
                    PlaceBehindGameOver(hud.countdownText.transform);
                    hud.countdownText.gameObject.SetActive(false);
                }
                if (hud.settingsPanel == null)
                {
                    // 맨 뒤 순서 = 가장 위에 그려진다
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab("UI/SettingsPanel"), root.transform);
                    inst.SetActive(false);
                    hud.settingsPanel = inst.GetComponent<SettingsPanel>();
                    changed = true;
                }
                var hintText = hud.hint != null ? hud.hint.GetComponent<Text>() : null;
                if (hintText != null && hintText.text == OldHint)
                {
                    hintText.text = NewHint;
                    changed = true;
                }
            }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void MoveIfAt(Text text, Vector2 from, Vector2 to)
        {
            if (text != null && (text.rectTransform.anchoredPosition - from).sqrMagnitude < 0.01f)
                text.rectTransform.anchoredPosition = to;
        }

        /// <summary>버튼이 기본 위치·크기 그대로일 때만 새 자리로 옮긴다 (직접 고친 배치는 건드리지 않는다).</summary>
        static void RelayoutButton(Button button, Vector2 fromPos, Vector2 fromSize, Vector2 toPos, Vector2 toSize, int fontSize)
        {
            if (button == null) return;
            var rt = (RectTransform)button.transform;
            if ((rt.anchoredPosition - fromPos).sqrMagnitude > 0.01f || (rt.sizeDelta - fromSize).sqrMagnitude > 0.01f) return;
            rt.anchoredPosition = toPos;
            rt.sizeDelta = toSize;
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.rectTransform.sizeDelta = toSize;
                label.fontSize = fontSize;
            }
        }

        /// <summary>기존 SettingsPanel 프리팹에 X 버튼, 타이틀로 버튼, 나가기 확인 창이 없으면 추가한다.</summary>
        static void UpgradeSettingsPanel()
        {
            string path = UiDir + "/SettingsPanel.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var settings = root.GetComponent<SettingsPanel>();
            var window = root.transform.Find("Window");
            bool changed = false;
            if (settings != null && window != null)
            {
                var red = new Color(0.9f, 0.35f, 0.3f);
                var blue = new Color(0.55f, 0.75f, 0.95f);
                var center = new Vector2(0.5f, 0.5f);

                if (settings.xButton == null)
                {
                    settings.xButton = MakeButton("XButton", window, new Vector2(-20, -20), new Vector2(90, 90),
                        red, "X", new Vector2(1, 1), 50);
                    changed = true;
                }
                if (settings.titleButton == null)
                {
                    // 아래 줄을 [게임 나가기] [타이틀로] [닫기] 세 칸으로 나눈다.
                    var size = new Vector2(270, 110);
                    RelayoutButton(settings.quitButton, new Vector2(-200, -200), new Vector2(340, 110), new Vector2(-295, -200), size, 40);
                    RelayoutButton(settings.closeButton, new Vector2(200, -200), new Vector2(340, 110), new Vector2(295, -200), size, 44);
                    settings.titleButton = MakeButton("TitleButton", window, new Vector2(0, -200), size, Orange, "타이틀로", null, 44);
                    changed = true;
                }
                if (settings.confirmPanel == null)
                {
                    var dim = MakeImage("ConfirmPanel", root.transform, Vector2.zero, Vector2.zero, Vector2.zero,
                        new Color(0, 0, 0, 0.5f), false);
                    Stretch(dim.rectTransform, 0);
                    var box = MakeImage("Window", dim.transform, center, Vector2.zero, new Vector2(820, 420), Cream).transform;
                    MakeText("Message", box, center, new Vector2(0, 80), 46, TextAnchor.MiddleCenter, Color.white,
                        "타이틀로 나가시겠습니까?\n진행 중인 게임은 사라집니다.");
                    settings.confirmYesButton = MakeButton("YesButton", box, new Vector2(-190, -110), new Vector2(320, 110), red, "나가기");
                    settings.confirmNoButton = MakeButton("NoButton", box, new Vector2(190, -110), new Vector2(320, 110), blue, "취소");
                    settings.confirmPanel = dim.gameObject;
                    dim.gameObject.SetActive(false);
                    changed = true;
                }
            }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        /// <summary>기존 TitleUI 프리팹에 설정/누적순위 버튼과 창, 빈 곳 터치 영역이 없으면 추가한다.</summary>
        static void UpgradeTitleUi()
        {
            string path = UiDir + "/TitleUI.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var title = root.GetComponent<TitleScreen>();
            bool changed = false;
            if (title != null)
            {
                var blue = new Color(0.55f, 0.75f, 0.95f);
                if (title.tapArea == null)
                {
                    var area = MakeImage("TapArea", root.transform, Vector2.zero, Vector2.zero, Vector2.zero,
                        new Color(0, 0, 0, 0), false);
                    Stretch(area.rectTransform, 0);
                    area.transform.SetAsFirstSibling(); // 다른 버튼들 뒤에 깔린다
                    title.tapArea = area.gameObject.AddComponent<Button>();
                    title.tapArea.transition = Selectable.Transition.None;
                    changed = true;
                }
                if (title.settingsButton == null)
                {
                    title.settingsButton = MakeButton("SettingsButton", root.transform, new Vector2(40, -40),
                        new Vector2(220, 110), blue, "설정", new Vector2(0, 1), 44);
                    changed = true;
                }
                if (title.rankingButton == null)
                {
                    title.rankingButton = MakeButton("RankingButton", root.transform, new Vector2(-40, -40),
                        new Vector2(300, 110), blue, "누적순위", new Vector2(1, 1), 44);
                    changed = true;
                }
                if (title.settingsPanel == null)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab("UI/SettingsPanel"), root.transform);
                    inst.SetActive(false);
                    title.settingsPanel = inst.GetComponent<SettingsPanel>();
                    changed = true;
                }
                if (title.rankingPanel == null)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab("UI/RankingPanel"), root.transform);
                    inst.SetActive(false);
                    title.rankingPanel = inst.GetComponent<RankingPanel>();
                    changed = true;
                }
            }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        /// <summary>씬에 GameAudio 가 없으면 프리팹을 하나 놓는다. 추가했으면 true.</summary>
        static bool EnsureAudio(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<GameAudio>(true) != null) return false;
            Instance("GameAudio");
            return true;
        }

        // ---------- 소리 ----------

        static AudioClip ExportSound(string name, Func<float[]> make)
        {
            string path = $"{SoundDir}/{name}.wav";
            if (!File.Exists(path))
            {
                File.WriteAllBytes(path, SoundFactory.ToWav(make()));
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // ---------- 이미지 ----------

        static Sprite ExportSprite(string name, Func<SpriteSpec> make, Vector4 border = default)
        {
            string path = $"{SpriteDir}/{name}.png";
            if (!File.Exists(path))
            {
                var spec = make();
                File.WriteAllBytes(path, spec.tex.EncodeToPNG());
                Object.DestroyImmediate(spec.tex);
                AssetDatabase.ImportAsset(path);

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = spec.ppu;
                importer.spriteBorder = border;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = spec.fullRect ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = spec.pivot;
                settings.spriteMeshType = spec.fullRect ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ---------- 프리팹 ----------

        static GameObject Prefab(string path, Func<GameObject> build)
        {
            path = PrefabCatalog.Resolve(path);
            PrefabCatalog.Ensure(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var go = build();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // 새 씬을 열면 이전에 잡아 둔 에셋 참조가 무효가 되므로, 쓸 때마다 경로로 다시 불러온다.
        static GameObject LoadPrefab(string name)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabCatalog.Resolve($"{PrefabDir}/{name}.prefab"));
        }

        static GameObject Instance(string name)
        {
            return (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(name));
        }

        static SpriteRenderer SpriteObject(string name, Sprite sprite, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        static RunnerEntity Entity(string name, EntityKind kind, Sprite sprite, float halfX, float halfY,
            float offsetY, int score, Action<RunnerEntity> extra = null)
        {
            return Prefab($"{PrefabDir}/{name}.prefab", () =>
            {
                var sr = SpriteObject(name, sprite, kind <= EntityKind.Boost ? 6 : 5);
                var e = sr.gameObject.AddComponent<RunnerEntity>();
                e.kind = kind;
                e.hitHalf = new Vector2(halfX, halfY);
                e.hitOffset = new Vector2(0, offsetY);
                e.score = score;
                extra?.Invoke(e);
                return sr.gameObject;
            }).GetComponent<RunnerEntity>();
        }

        static GameObject BuildCookie(Sprite bodySprite, Sprite legSprite)
        {
            var root = new GameObject("Cookie");
            var player = root.AddComponent<PlayerCookie>();
            player.legBack = SpriteObject("LegBack", legSprite, 9, root.transform).transform;
            player.legFront = SpriteObject("LegFront", legSprite, 9, root.transform).transform;
            player.body = SpriteObject("Body", bodySprite, 10, root.transform).transform;
            player.legBack.localPosition = new Vector3(-0.14f, 0.48f, 0);
            player.legFront.localPosition = new Vector3(0.14f, 0.48f, 0);
            player.body.localPosition = new Vector3(0, 0.9f, 0);
            return root;
        }

        // ---------- 씬 구성 ----------

        static Camera MakeCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0, 0, -10);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.8f, 0.98f);
            go.AddComponent<AudioListener>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            return cam;
        }

        static void MakeBackground(out Transform clouds, out Transform hills)
        {
            var bg = new GameObject("Background").transform;
            SpriteObject("Sky", AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/Sky.png"), -100, bg).transform.localScale = new Vector3(120, 30, 1);

            clouds = new GameObject("Clouds").transform;
            clouds.SetParent(bg, false);
            float[] cloudY = { 3.4f, 1.6f, 4.0f, 2.4f, 3.0f, 1.2f, 3.7f };
            float[] cloudScale = { 1.2f, 0.8f, 1.0f, 1.4f, 0.7f, 1.1f, 0.9f };
            for (int i = 0; i < cloudY.Length; i++)
            {
                var c = Instance("Cloud").transform;
                c.SetParent(clouds, false);
                c.position = new Vector3(-13f + i * 4f, cloudY[i], 0);
                c.localScale = Vector3.one * cloudScale[i];
            }

            hills = new GameObject("Hills").transform;
            hills.SetParent(bg, false);
            float[] hillScale = { 1.0f, 0.75f, 1.25f, 0.9f, 1.1f, 0.7f, 1.3f };
            for (int i = 0; i < hillScale.Length; i++)
            {
                var h = Instance("Hill").transform;
                h.SetParent(hills, false);
                h.localScale = Vector3.one * hillScale[i];
                h.position = new Vector3(-17f + i * 5f, RunnerGame.GroundY - 0.2f + 1.5f * hillScale[i], 0);
                h.GetComponent<SpriteRenderer>().color =
                    i % 2 == 0 ? new Color(1f, 0.72f, 0.75f) : new Color(0.98f, 0.8f, 0.6f);
            }
        }

        static void MakeGroundStrip()
        {
            var g = Instance("Ground");
            g.transform.position = new Vector3(0, RunnerGame.GroundY, 0);
            var sr = g.GetComponent<SpriteRenderer>();
            sr.size = new Vector2(60, sr.size.y);
        }

        static void MakeEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        // ---------- UI ----------

        static GameObject BuildHud()
        {
            var root = MakeCanvas("GameHUD");
            var hud = root.AddComponent<GameHud>();
            var topLeft = new Vector2(0, 1);
            var topRight = new Vector2(1, 1);

            var hpBack = MakeImage("HpBar", root.transform, topLeft, new Vector2(50, -40), new Vector2(900, 60),
                new Color(0.2f, 0.1f, 0.08f, 0.85f));
            var inner = MakeRect("Inner", hpBack.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(inner, 7);
            var fill = MakeImage("Fill", inner, Vector2.zero, Vector2.zero, Vector2.zero, Color.white);
            Stretch(fill.rectTransform, 0);
            hud.hpFill = fill.rectTransform;
            hud.hpFillImage = fill;

            hud.buffText = MakeText("Buffs", root.transform, topLeft, new Vector2(50, -110), 34, TextAnchor.UpperLeft,
                new Color(1f, 0.95f, 0.5f), "");
            hud.scoreText = MakeText("Score", root.transform, topRight, new Vector2(-50, -30), 64, TextAnchor.UpperRight,
                Color.white, "0");
            hud.bestText = MakeText("Best", root.transform, topRight, new Vector2(-50, -110), 32, TextAnchor.UpperRight,
                new Color(1f, 0.9f, 0.6f), "최고 0");

            var hint = MakeText("Hint", root.transform, new Vector2(0.5f, 0), new Vector2(0, 40), 32, TextAnchor.LowerCenter,
                Color.white, "점프: Space / 화면 왼쪽 (공중에서 한 번 더!)      슬라이드: ↓ / 화면 오른쪽 누르고 있기");
            hud.hint = hint.gameObject;

            // 게임 오버 창
            var dim = MakeImage("GameOverPanel", root.transform, Vector2.zero, Vector2.zero, Vector2.zero,
                new Color(0, 0, 0, 0.5f), false);
            Stretch(dim.rectTransform, 0);
            var center = new Vector2(0.5f, 0.5f);
            var window = MakeImage("Window", dim.transform, center, Vector2.zero, new Vector2(900, 560), Cream);
            hud.gameOverTitle = MakeText("Title", window.transform, center, new Vector2(0, 160), 100, TextAnchor.MiddleCenter,
                Orange, "게임 오버");
            hud.gameOverScore = MakeText("Score", window.transform, center, new Vector2(0, 30), 56, TextAnchor.MiddleCenter,
                Color.white, "점수 0");
            hud.retryButton = MakeButton("RetryButton", window.transform, new Vector2(-200, -150), new Vector2(340, 120),
                Orange, "다시 하기");
            hud.titleButton = MakeButton("TitleButton", window.transform, new Vector2(200, -150), new Vector2(340, 120),
                new Color(0.55f, 0.75f, 0.95f), "타이틀로");
            hud.gameOverPanel = dim.gameObject;
            dim.gameObject.SetActive(false);

            return root;
        }

        static GameObject BuildTitleUi()
        {
            var root = MakeCanvas("TitleUI");
            var title = root.AddComponent<TitleScreen>();
            var center = new Vector2(0.5f, 0.5f);

            MakeText("Title", root.transform, center, new Vector2(0, 250), 190, TextAnchor.MiddleCenter, Orange, "Juwel RUN");
            title.bestText = MakeText("Best", root.transform, center, new Vector2(0, 90), 48, TextAnchor.MiddleCenter,
                new Color(1f, 0.9f, 0.6f), "최고 기록 0");
            title.startButton = MakeButton("StartButton", root.transform, new Vector2(0, -60), new Vector2(460, 140),
                Orange, "게임 시작");
            title.blink = MakeText("PressAnyKey", root.transform, new Vector2(0.5f, 0), new Vector2(0, 60), 36,
                TextAnchor.LowerCenter, Color.white, "아무 키나 누르거나 화면을 터치하세요");
            return root;
        }

        static GameObject BuildSettingsPanel()
        {
            var dim = MakeImage("SettingsPanel", null, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.5f), false);
            Stretch(dim.rectTransform, 0);
            var settings = dim.gameObject.AddComponent<SettingsPanel>();
            var center = new Vector2(0.5f, 0.5f);
            var window = MakeImage("Window", dim.transform, center, Vector2.zero, new Vector2(900, 640), Cream).transform;

            MakeText("Title", window, center, new Vector2(0, 220), 80, TextAnchor.MiddleCenter, Orange, "설정");
            MakeText("BgmLabel", window, center, new Vector2(-310, 80), 52, TextAnchor.MiddleCenter, Color.white, "BGM");
            settings.bgmSlider = MakeSlider("BgmSlider", window, new Vector2(90, 80), new Vector2(520, 56));
            MakeText("SfxLabel", window, center, new Vector2(-310, -30), 52, TextAnchor.MiddleCenter, Color.white, "SFX");
            settings.sfxSlider = MakeSlider("SfxSlider", window, new Vector2(90, -30), new Vector2(520, 56));
            settings.quitButton = MakeButton("QuitButton", window, new Vector2(-200, -200), new Vector2(340, 110),
                new Color(0.9f, 0.35f, 0.3f), "게임 나가기", null, 46);
            settings.closeButton = MakeButton("CloseButton", window, new Vector2(200, -200), new Vector2(340, 110),
                new Color(0.55f, 0.75f, 0.95f), "닫기");
            return dim.gameObject;
        }

        static GameObject BuildRankingPanel()
        {
            var dim = MakeImage("RankingPanel", null, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.5f), false);
            Stretch(dim.rectTransform, 0);
            var ranking = dim.gameObject.AddComponent<RankingPanel>();
            var center = new Vector2(0.5f, 0.5f);
            var window = MakeImage("Window", dim.transform, center, Vector2.zero, new Vector2(800, 940), Cream).transform;

            MakeText("Title", window, center, new Vector2(0, 380), 80, TextAnchor.MiddleCenter, Orange, "누적순위");
            ranking.listText = MakeText("List", window, center, new Vector2(0, 20), 44, TextAnchor.MiddleCenter, Color.white,
                "1위      0");
            ranking.closeButton = MakeButton("CloseButton", window, new Vector2(0, -380), new Vector2(340, 110),
                new Color(0.55f, 0.75f, 0.95f), "닫기");
            return dim.gameObject;
        }

        static Slider MakeSlider(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var center = new Vector2(0.5f, 0.5f);
            var root = MakeRect(name, parent, center, pos, size);
            var slider = root.gameObject.AddComponent<Slider>();

            // 가운데 절반 높이의 막대
            void Bar(RectTransform rt, float inset)
            {
                rt.anchorMin = new Vector2(0, 0.25f);
                rt.anchorMax = new Vector2(1, 0.75f);
                rt.pivot = center;
                rt.offsetMin = new Vector2(inset, 0);
                rt.offsetMax = new Vector2(-inset, 0);
            }

            var back = MakeImage("Background", root, center, Vector2.zero, Vector2.zero, new Color(0.3f, 0.18f, 0.12f, 0.9f));
            Bar(back.rectTransform, 0);
            var fillArea = MakeRect("Fill Area", root, center, Vector2.zero, Vector2.zero);
            Bar(fillArea, 6);
            var fill = MakeImage("Fill", fillArea, center, Vector2.zero, Vector2.zero, Orange);
            Stretch(fill.rectTransform, 0);

            var handleArea = MakeRect("Handle Slide Area", root, center, Vector2.zero, Vector2.zero);
            Stretch(handleArea, 0);
            handleArea.offsetMin = new Vector2(size.y * 0.5f, 0);
            handleArea.offsetMax = new Vector2(-size.y * 0.5f, 0);
            var handle = MakeImage("Handle", handleArea, center, Vector2.zero, Vector2.zero, Color.white, false);
            handle.sprite = circle;
            handle.rectTransform.anchorMin = new Vector2(0, 0);
            handle.rectTransform.anchorMax = new Vector2(0, 1);
            handle.rectTransform.sizeDelta = new Vector2(size.y, 0);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = 0.7f;
            return slider;
        }

        static HoldButton MakeHoldButton(string name, Transform parent, Vector2 anchor, Vector2 pos, string label)
        {
            var image = MakeImage(name, parent, anchor, pos, new Vector2(260, 260), new Color(1, 1, 1, 0.35f), false);
            image.sprite = circle;
            MakeText("Label", image.transform, new Vector2(0.5f, 0.5f), Vector2.zero, 60, TextAnchor.MiddleCenter,
                Color.white, label);
            return image.gameObject.AddComponent<HoldButton>();
        }

        static GameObject MakeCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        static RectTransform MakeRect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        static Image MakeImage(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color,
            bool rounded = true)
        {
            var image = MakeRect(name, parent, anchor, pos, size).gameObject.AddComponent<Image>();
            if (rounded)
            {
                image.sprite = panel;
                image.type = Image.Type.Sliced;
            }
            image.color = color;
            return image;
        }

        static Text MakeText(string name, Transform parent, Vector2 anchor, Vector2 pos, int size, TextAnchor align,
            Color color, string content)
        {
            var rt = MakeRect(name, parent, anchor, pos, new Vector2(1600, size * 1.4f));
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = align;
            t.color = color;
            t.text = content;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Brown.r, Brown.g, Brown.b, 0.9f);
            outline.effectDistance = new Vector2(3, -3);
            return t;
        }

        static Button MakeButton(string name, Transform parent, Vector2 pos, Vector2 size, Color color, string label,
            Vector2? anchor = null, int fontSize = 52)
        {
            var image = MakeImage(name, parent, anchor ?? new Vector2(0.5f, 0.5f), pos, size, color);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = MakeText("Label", image.transform, new Vector2(0.5f, 0.5f), Vector2.zero, fontSize, TextAnchor.MiddleCenter,
                Color.white, label);
            text.rectTransform.sizeDelta = size;
            return button;
        }
    }
}
