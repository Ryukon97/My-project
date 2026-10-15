using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace CookieRun
{
    /// <summary>
    /// 쿠키런 방식 러너의 본체 (MainGame 씬의 Game 오브젝트).
    /// 젤리/장애물/바닥은 연결된 프리팹을 world 아래에 생성하고, world 를 왼쪽으로 밀어서 스크롤한다.
    /// </summary>
    /// <summary>맵 테마 하나. 비워 둔 스프라이트는 바꾸지 않고 이전 것을 그대로 쓴다.</summary>
    [Serializable]
    public class MapTheme
    {
        public string name = "테마";
        public Sprite sky;
        [Tooltip("Tiled 로 반복되는 바닥 타일 (Mesh Type: Full Rect, 피벗 위쪽 가운데)")] public Sprite ground;
        public Sprite hill;
        public Color hillColorA = Color.white;
        public Color hillColorB = Color.white;
        public Color cloudColor = Color.white;
    }

    public class RunnerGame : MonoBehaviour
    {
        public const float GroundY = -2.5f;
        public const string BestKey = "CookieRun.Best";
        const float DespawnX = -14f;

        [Header("씬 오브젝트")]
        public Camera cam;
        public Transform world;
        public PlayerCookie player;
        public GameHud hud;
        [Tooltip("자식들이 느리게 흘러가며 반복된다")] public Transform cloudsRoot;
        public Transform hillsRoot;
        [Tooltip("비워 두면 Background/Sky 를 찾는다")] public SpriteRenderer sky;

        [Header("맵 테마 (순서대로 반복)")]
        [Tooltip("Tools > CookieRun > 맵 테마 편집기에서 수정할 수 있습니다")]
        public MapThemeData[] mapThemes;
        [Tooltip("큰 별 젤리의 보너스 테마가 비어 있을 때 사용하는 보너스 전용 맵")]
        public MapThemeData bonusMap;
        [HideInInspector] public MapTheme[] themes; // Compatibility for scenes made before SO migration.
        [Min(1f), Tooltip("이 거리(m)마다 다음 테마로 넘어간다")] public float themeDistance = 500f;
        public float themeFadeTime = 1.2f;
        [Tooltip("일러스트 배경을 카메라 화면 비율에 맞춰 채운다")]
        public bool fitIllustratedBackground;

        [Header("프리팹")]
        [Tooltip("Tiled 모드 SpriteRenderer, 피벗은 위쪽 가운데")] public SpriteRenderer groundPrefab;
        [Tooltip("종류(kind)별로 하나씩")] public RunnerEntity[] entityPrefabs;

        [Header("아이템 SO")]
        [Tooltip("Tools > CookieRun > 아이템 편집기에서 이미지·효과·배치를 수정합니다. 종류별로 하나씩 연결하세요")]
        public ItemData[] items;

        [Header("씬 이름")]
        public string titleSceneName = "Title";

        [Header("밸런스")]
        [Tooltip("맵(바닥·장애물·배경)이 흘러가는 속도. 캐릭터 동작 속도는 바뀌지 않는다")] public float startSpeed = 9f;
        public float maxSpeed = 16f;
        [Tooltip("한 번에 올라가는 속도")] public float speedStep = 0.1f;
        [Tooltip("속도가 한 번 올라가는 간격(초)")] public float speedStepInterval = 1.5f;
        public float maxHp = 100f;
        public float hpDrainStart = 2.5f;
        public float hpDrainMax = 7f;
        public float hitDamage = 20f;
        [HideInInspector] public float potionHeal = 20f;
        public float potionInterval = 80f;
        [Tooltip("아이템 등장 빈도 배율. 자석/부스터/질주 구간이 나올 확률과 하트가 나오는 간격에 함께 적용된다")]
        public float itemFrequency = 1.2f;
        [Tooltip("배율 1 일 때 자석/부스터/질주 구간이 나올 확률")] public float itemPatternChance = 0.06f;
        public float invincibleTime = 1.5f;
        [HideInInspector] public float magnetTime = 6f;
        [HideInInspector] public float boostTime = 3f;
        [HideInInspector] public float boostSpeedScale = 1.8f;
        [HideInInspector] public float dashTime = 1f;
        [HideInInspector] public float dashSpeedScale = 2f;
        [Tooltip("일시정지를 풀 때 세는 시간(초)")] public float resumeCountdown = 3f;
        [Tooltip("천장 장애물 아래 끝의 바닥으로부터 높이")] public float hangClearance = 1.45f;
        [Tooltip("슬라이드 장애물 한 개의 가로 폭")] public float hangObstacleWidth = 1.4f;
        [Tooltip("가장 빠른 기본 속도에서도 확보할 슬라이드 구간 시간")] public float minSlideDuration = 0.55f;

        enum State { Playing, Paused, Countdown, Dying, GameOver }
        int finalScore;

        class Ground
        {
            public float x0, x1;
            public GameObject go;
        }

        readonly Dictionary<EntityKind, RunnerEntity> prefabs = new Dictionary<EntityKind, RunnerEntity>();
        readonly Dictionary<EntityKind, ItemData> itemCatalog = new Dictionary<EntityKind, ItemData>();
        readonly Dictionary<ItemData, int> bonusCollections = new Dictionary<ItemData, int>();
        readonly Dictionary<RunnerEntity, Stack<RunnerEntity>> pools = new Dictionary<RunnerEntity, Stack<RunnerEntity>>();
        readonly Dictionary<RunnerEntity, RunnerEntity> entitySources = new Dictionary<RunnerEntity, RunnerEntity>();
        readonly List<RunnerEntity> entities = new List<RunnerEntity>();
        readonly List<Ground> grounds = new List<Ground>();

        State state;
        float stateTime;
        float scroll, genX, lastPotionX;
        float mapSpeed, speedTimer;
        float countdownT;
        int countdownShown;
        float hp, invincibleT, magnetT, boostT, dashT, shake;
        float activeBoostScale, activeDashScale, activeMagnetRadius, activeMagnetPullSpeed;
        float bonusT;
        MapThemeData activeBonusTheme;
        ItemData pendingBonus;

        public bool InBonusStage => bonusT > 0f;
        public float BonusTimeRemaining => bonusT;
        public int BonusRequired => ItemFor(EntityKind.BigJelly) is ItemData data && data.effect == ItemEffect.BonusStage
            ? Mathf.Max(1, data.bonusRequiredCount) : 0;
        public int BonusCollected => ItemFor(EntityKind.BigJelly) is ItemData data && bonusCollections.TryGetValue(data, out var count) ? count : 0;

        SpriteRenderer skyFade;
        SpriteRenderer[] hillRenderers, cloudRenderers;
        MapThemeData theme, prevTheme;
        MapThemeData[] activeThemes;
        int themeIndex = -1;
        float themeFade = 1f;
        bool hillsSwapped;
        int bonusScore, best;

        // ---------- 초기화 ----------

        void Awake()
        {
            Application.targetFrameRate = 60;
            best = PlayerPrefs.GetInt(BestKey, 0);
            if (cam == null) cam = Camera.main;

            foreach (var p in entityPrefabs ?? Array.Empty<RunnerEntity>())
            {
                if (p == null) continue;
                prefabs[p.kind] = p;
            }
            itemCatalog.Clear();
            foreach (var data in items ?? Array.Empty<ItemData>())
            {
                if (data == null) continue;
                if (!data.IsValid)
                {
                    Debug.LogWarning("아이템 SO의 종류와 프리팹을 확인하세요: " + data.name, data);
                    continue;
                }
                if (itemCatalog.ContainsKey(data.kind))
                {
                    Debug.LogWarning("아이템 종류가 중복되어 첫 번째 SO를 사용합니다: " + data.kind, data);
                    continue;
                }
                itemCatalog[data.kind] = data;
            }
        }

        void Start()
        {
            hud.retryButton.onClick.AddListener(Retry);
            hud.titleButton.onClick.AddListener(GoToTitle);
            if (hud.pauseButton != null) hud.pauseButton.onClick.AddListener(Pause);
            if (hud.settingsPanel != null)
            {
                hud.settingsPanel.gameObject.SetActive(false);
                hud.settingsPanel.Closed += BeginResume;
                hud.settingsPanel.showTitleButton = true;
                hud.settingsPanel.TitleRequested += GoToTitle;
            }
            SetupThemes();
            ResetRun();
        }

        void ResetRun()
        {
            for (int i = entities.Count - 1; i >= 0; i--) Release(i);
            foreach (var g in grounds) Destroy(g.go);
            grounds.Clear();

            scroll = 0;
            world.localPosition = Vector3.zero;
            hp = maxHp;
            invincibleT = magnetT = boostT = dashT = shake = 0;
            bonusT = 0;
            pendingBonus = null;
            activeBonusTheme = null;
            bonusCollections.Clear();
            activeBoostScale = boostSpeedScale;
            activeDashScale = dashSpeedScale;
            activeMagnetRadius = 6f;
            activeMagnetPullSpeed = 14f;
            bonusScore = 0;
            themeIndex = -1;
            UpdateTheme(0);

            genX = -16f;
            AddGround(30);
            lastPotionX = genX;
            mapSpeed = startSpeed;
            speedTimer = 0;
            Generate(mapSpeed);

            player.ResetState();
            hud.SetHp(1);
            hud.SetScore(0, best);
            hud.SetBuffs(0, 0, 0, 0, 0, BonusRequired);
            hud.SetHint(true);
            hud.HideGameOver();
            hud.SetCountdown(0);

            state = State.Playing;
            stateTime = 0;
        }

        public void Retry()
        {
            GameAudio.Play(Sfx.Button);
            ClearSelection();
            ResetRun();
        }

        public void GoToTitle()
        {
            GameAudio.Play(Sfx.Button);
            SceneManager.LoadScene(titleSceneName);
        }

        // ---------- 메인 루프 ----------

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            ReadInput(out bool jump, out bool slide);
            UpdateCamera(dt);
            stateTime += dt;

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                // 플레이 중: 일시정지 / 일시정지 중: 창 닫기(카운트다운 시작) / 게임 오버: 타이틀로
                if (state == State.Paused) hud.settingsPanel.Close();
                else if (state == State.GameOver) GoToTitle();
                else Pause();
                return;
            }

            switch (state)
            {
                case State.Playing:
                    TickPlaying(dt, jump, slide);
                    break;
                case State.Countdown:
                    TickCountdown();
                    break;
                case State.Dying:
                    player.TickExhaustion(dt);
                    if (player.ExhaustionComplete) GameOver(finalScore);
                    break;
                case State.GameOver:
                    if (stateTime > 0.7f && kb != null &&
                        (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) Retry();
                    break;
            }
        }

        // ---------- 일시정지 ----------

        /// <summary>게임을 멈추고 설정 창을 연다. 카운트다운 중에 다시 눌러도 된다.</summary>
        public void Pause()
        {
            if (state != State.Playing && state != State.Countdown) return;
            if (hud.settingsPanel == null)
            {
                GoToTitle(); // 설정 창이 없는 HUD 에서는 예전처럼 타이틀로
                return;
            }
            state = State.Paused;
            hud.SetCountdown(0);
            hud.settingsPanel.Open();
        }

        /// <summary>설정 창이 닫히면 카운트다운을 세고 나서 게임을 이어 간다.</summary>
        void BeginResume()
        {
            if (state != State.Paused) return;
            ClearSelection();
            state = State.Countdown;
            countdownT = Mathf.Max(0f, resumeCountdown);
            countdownShown = 0;
        }

        // 방금 누른 UI 버튼이 선택된 채로 남으면 Space(점프)가 그 버튼을 다시 누르게 된다.
        static void ClearSelection()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        void TickCountdown()
        {
            countdownT -= Time.unscaledDeltaTime;
            if (countdownT <= 0f)
            {
                state = State.Playing;
                hud.SetCountdown(0);
                return;
            }
            int number = Mathf.CeilToInt(countdownT);
            if (number != countdownShown)
            {
                countdownShown = number;
                hud.SetCountdown(number);
                GameAudio.Play(Sfx.Button);
            }
        }

        /// <summary>일정 간격마다 맵 속도를 speedStep 씩 올린다.</summary>
        void UpdateMapSpeed(float dt)
        {
            if (mapSpeed >= maxSpeed) return;
            speedTimer += dt;
            float interval = Mathf.Max(0.01f, speedStepInterval);
            while (speedTimer >= interval && mapSpeed < maxSpeed)
            {
                speedTimer -= interval;
                mapSpeed = Mathf.Min(maxSpeed, mapSpeed + speedStep);
            }
        }

        void TickPlaying(float dt, bool jump, bool slide)
        {
            bool wasBonus = InBonusStage;
            UpdateMapSpeed(dt);
            float difficulty = maxSpeed > startSpeed ? Mathf.InverseLerp(startSpeed, maxSpeed, mapSpeed) : 0f;
            bool boosting = boostT > 0, dashing = dashT > 0;
            bool rushing = boosting || dashing;
            float speedScale = Mathf.Max(boosting ? activeBoostScale : 1f, dashing ? activeDashScale : 1f);
            float speed = mapSpeed * speedScale;

            scroll += speed * dt;
            world.localPosition = new Vector3(-scroll, 0, 0);
            // 지금 만드는 구간에 도착할 즈음엔 조금 더 빨라져 있으므로 간격에 여유를 둔다.
            Generate(mapSpeed + 0.5f);

            // 부스터/질주 중에는 구덩이 위도 달린다.
            float px = PlayerCookie.X + scroll;
            bool onGround = HasGround(px);
            player.Tick(dt, jump, slide, rushing || InBonusStage || onGround);

            invincibleT -= dt;
            magnetT -= dt;
            if (rushing)
            {
                boostT -= dt;
                dashT -= dt;
                if (boostT <= 0 && dashT <= 0)
                {
                    // 끝난 직후 장애물 한가운데일 수 있으므로 잠깐 무적을 준다.
                    if (onGround) invincibleT = Mathf.Max(invincibleT, boosting ? invincibleTime : 0.5f);
                    else if (boosting) boostT = 0.05f; // 구덩이 위에서 끝나지 않게 연장
                    else dashT = 0.05f;
                }
            }
            else if (!InBonusStage)
            {
                hp -= Mathf.Lerp(hpDrainStart, hpDrainMax, difficulty) * dt;
            }

            HandleEntities(dt, speed);
            if (wasBonus)
            {
                bonusT = Mathf.Max(0, bonusT - dt);
                if (bonusT <= 0) EndBonusStage();
            }
            if (pendingBonus != null) BeginBonusStage();
            CleanupGround();
            ScrollLayer(cloudsRoot, speed * dt * 0.12f, 28f);
            ScrollLayer(hillsRoot, speed * dt * 0.3f, 35f);
            UpdatePlayerTint();
            UpdateTheme(dt);

            int total = bonusScore + (int)scroll;
            hud.SetHp(hp / maxHp);
            hud.SetScore(total, Mathf.Max(best, total));
            hud.SetBuffs(magnetT, boostT, dashT, bonusT, BonusCollected, BonusRequired);
            hud.SetHint(scroll < 40f);

            if (!InBonusStage && (hp <= 0 || player.transform.position.y < GroundY - 5f)) BeginDeath(total);
        }

        void BeginDeath(int total)
        {
            state = State.Dying;
            stateTime = 0;
            finalScore = total;
            hp = 0;
            hud.SetHp(0);
            hud.SetBuffs(0, 0, 0);
            hud.SetHint(false);
            shake = 0;
            player.BeginExhaustion();
            GameAudio.Play(Sfx.GameOver);
        }

        void GameOver(int total)
        {
            state = State.GameOver;
            stateTime = 0;
            hp = 0;
            hud.SetHp(0);
            hud.SetBuffs(0, 0, 0);
            hud.SetHint(false);
            bool record = total > best;
            if (record)
            {
                best = total;
                PlayerPrefs.SetInt(BestKey, best);
                PlayerPrefs.Save();
            }
            ScoreBoard.Add(total);
            hud.SetScore(total, best);
            hud.ShowGameOver(total, record);
        }

        void UpdatePlayerTint()
        {
            if (InBonusStage) player.SetTint(RunnerMapVisuals.BonusTint);
            else if (dashT > 0) player.SetTint(new Color(0.55f, 1f, 0.95f));
            else if (boostT > 0) player.SetTint(new Color(1f, 0.95f, 0.45f));
            else if (invincibleT > 0)
                player.SetTint(Mathf.Repeat(invincibleT, 0.2f) < 0.1f ? new Color(1, 1, 1, 0.35f) : Color.white);
            else player.SetTint(Color.white);
        }

        // ---------- 입력 ----------

        void ReadInput(out bool jump, out bool slide)
        {
            jump = slide = false;

            var kb = Keyboard.current;
            if (kb != null)
            {
                jump = kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
                slide = kb.downArrowKey.isPressed || kb.sKey.isPressed || kb.leftCtrlKey.isPressed;
            }

            // 화면의 Jump / Slide 버튼
            if (hud.jumpButton != null && hud.slideButton != null)
            {
                jump |= hud.jumpButton.ConsumePressed();
                slide |= hud.slideButton.IsHeld;
                return;
            }

            // 버튼이 없는 HUD 라면: 화면 왼쪽 절반 = 점프, 오른쪽 절반 = 슬라이드
            float half = Screen.width * 0.5f;
            bool touched = false;
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var touch in ts.touches)
                {
                    if (!touch.press.isPressed) continue;
                    touched = true;
                    if (touch.position.ReadValue().x < half) jump |= touch.press.wasPressedThisFrame;
                    else slide = true;
                }
            }

            var mouse = Mouse.current;
            if (mouse != null && !touched && mouse.leftButton.isPressed)
            {
                if (mouse.position.ReadValue().x < half) jump |= mouse.leftButton.wasPressedThisFrame;
                else slide = true;
            }
        }

        // ---------- 카메라 / 배경 ----------

        void UpdateCamera(float dt)
        {
            // 세로로 긴 화면에서도 플레이어 앞쪽이 충분히 보이도록 한다.
            cam.orthographicSize = RunnerMapVisuals.CameraSize(cam.aspect);
            if (fitIllustratedBackground)
            {
                FitBackground(sky);
                FitBackground(skyFade);
            }
            shake = Mathf.Max(0, shake - dt * 2.5f);
            Vector2 offset = shake > 0 ? Random.insideUnitCircle * shake * 0.35f : Vector2.zero;
            cam.transform.position = new Vector3(offset.x, offset.y, -10);
        }

        void FitBackground(SpriteRenderer renderer)
        {
            RunnerMapVisuals.FitBackground(renderer, cam);
        }

        static void ScrollLayer(Transform root, float dx, float wrapWidth)
        {
            if (root == null) return;
            foreach (Transform child in root)
            {
                var p = child.position;
                p.x -= dx;
                if (p.x < -wrapWidth * 0.5f) p.x += wrapWidth;
                child.position = p;
            }
        }

        // ---------- 맵 테마 ----------

        void SetupThemes()
        {
            var configured = new List<MapThemeData>();
            if (mapThemes != null)
                foreach (var data in mapThemes) if (data != null && !data.bonusOnly) configured.Add(data);
            if (configured.Count == 0 && themes != null)
                foreach (var old in themes)
                {
                    if (old == null) continue;
                    var data = ScriptableObject.CreateInstance<MapThemeData>();
                    data.hideFlags = HideFlags.HideAndDontSave;
                    data.displayName = old.name;
                    data.background = old.sky;
                    data.ground = old.ground;
                    data.hill = old.hill;
                    data.hillColorA = old.hillColorA;
                    data.hillColorB = old.hillColorB;
                    data.cloudColor = old.cloudColor;
                    configured.Add(data);
                }
            activeThemes = configured.ToArray();
            if (activeThemes.Length == 0 && bonusMap == null) return;
            if (sky == null && cloudsRoot != null && cloudsRoot.parent != null)
            {
                var found = cloudsRoot.parent.Find("Sky");
                if (found != null) sky = found.GetComponent<SpriteRenderer>();
            }
            if (sky != null)
            {
                // 하늘은 위에 한 장을 더 겹쳐서 서서히 바꾼다.
                skyFade = Instantiate(sky, sky.transform.parent);
                skyFade.name = "SkyFade";
                skyFade.sortingOrder = sky.sortingOrder + 1;
                skyFade.color = new Color(1, 1, 1, 0);
            }
            hillRenderers = hillsRoot != null ? hillsRoot.GetComponentsInChildren<SpriteRenderer>() : new SpriteRenderer[0];
            cloudRenderers = cloudsRoot != null ? cloudsRoot.GetComponentsInChildren<SpriteRenderer>() : new SpriteRenderer[0];
        }

        public MapThemeData ThemeAt(float distance)
        {
            var data = activeThemes ?? mapThemes;
            if (data == null || data.Length == 0) return null;
            int count = 0;
            foreach (var entry in data) if (entry != null && !entry.bonusOnly) count++;
            if (count == 0) return null;
            int index = Mathf.Max(0, Mathf.FloorToInt(distance / Mathf.Max(1f, themeDistance))) % count;
            foreach (var entry in data)
                if (entry != null && !entry.bonusOnly && index-- == 0) return entry;
            return null;
        }

        MapThemeData GenerationThemeAt(float distance) => InBonusStage ? activeBonusTheme : ThemeAt(distance);

        void UpdateTheme(float dt)
        {
            if (hillRenderers == null) return; // 테마 없음

            var next = GenerationThemeAt(scroll);
            if (next == null) return;
            int index = InBonusStage ? -2 : Mathf.Max(0, Mathf.FloorToInt(scroll / Mathf.Max(1f, themeDistance)));
            if (index != themeIndex || next != theme)
            {
                bool instant = theme == null || themeIndex < 0 || InBonusStage;
                themeIndex = index;
                prevTheme = instant ? next : theme;
                theme = next;
                themeFade = instant ? 1f : 0f;
                hillsSwapped = false;
                if (skyFade != null && theme.background != null) skyFade.sprite = theme.background;
                hud.ShowTheme(theme.displayName);
                ApplyThemeFade();
            }
            else if (themeFade < 1f)
            {
                themeFade = Mathf.Min(1f, themeFade + dt / Mathf.Max(0.01f, themeFadeTime));
                ApplyThemeFade();
            }
        }

        void ApplyThemeFade()
        {
            float t = themeFade;

            if (sky != null && skyFade != null)
            {
                if (t >= 1f)
                {
                    if (theme.background != null) sky.sprite = theme.background;
                    skyFade.color = new Color(1, 1, 1, 0);
                }
                else skyFade.color = new Color(1, 1, 1, t);
            }

            // 언덕은 절반까지 사라졌다가, 그림을 바꾸고 다시 나타난다.
            bool second = t >= 0.5f;
            var shown = second ? theme : prevTheme;
            float alpha = second ? t * 2f - 1f : 1f - t * 2f;
            for (int i = 0; i < hillRenderers.Length; i++)
            {
                var hr = hillRenderers[i];
                if (second && !hillsSwapped && theme.hill != null) hr.sprite = theme.hill;
                var c = i % 2 == 0 ? shown.hillColorA : shown.hillColorB;
                c.a *= alpha;
                hr.color = c;
            }
            if (second) hillsSwapped = true;

            var cloud = Color.Lerp(prevTheme.cloudColor, theme.cloudColor, t);
            foreach (var cr in cloudRenderers) cr.color = cloud;
        }

        // ---------- 바닥 ----------

        void AddGround(float length)
        {
            while (length > 0.001f)
            {
                float interval = Mathf.Max(1f, themeDistance);
                float boundary = (Mathf.Max(0, Mathf.FloorToInt(genX / interval)) + 1) * interval;
                float part = Mathf.Min(length, boundary - genX);
                var sr = Instantiate(groundPrefab, world);
                var groundTheme = GenerationThemeAt(genX);
                RunnerMapVisuals.ConfigureGround(sr, groundTheme != null ? groundTheme.ground : null, genX, part, GroundY);
                grounds.Add(new Ground { x0 = genX, x1 = genX + part, go = sr.gameObject });
                genX += part;
                length -= part;
            }
        }

        void OnDestroy()
        {
            if (activeThemes == null) return;
            foreach (var data in activeThemes)
                if (data != null && data.hideFlags == HideFlags.HideAndDontSave) Destroy(data);
        }

        bool HasGround(float x)
        {
            foreach (var g in grounds)
                if (g.x0 <= x + 0.2f && g.x1 >= x - 0.2f) return true;
            return false;
        }

        void CleanupGround()
        {
            for (int i = grounds.Count - 1; i >= 0; i--)
            {
                if (grounds[i].x1 - scroll > DespawnX) continue;
                Destroy(grounds[i].go);
                grounds.RemoveAt(i);
            }
        }

        // ---------- 엔티티 ----------

        void Spawn(EntityKind kind, float x, float y)
        {
            var prefab = ResolveEntityPrefab(kind, x);
            if (prefab == null) return;

            if (!pools.TryGetValue(prefab, out var pool))
                pools[prefab] = pool = new Stack<RunnerEntity>();
            RunnerEntity e;
            if (pool.Count > 0)
            {
                e = pool.Pop();
                e.gameObject.SetActive(true);
            }
            else
            {
                e = Instantiate(prefab, world);
                entitySources[e] = prefab;
            }

            e.transform.localPosition = new Vector3(x, RunnerMapVisuals.ObstacleY(GenerationThemeAt(x), kind, y), 0);
            e.OnSpawn(ItemFor(kind));
            entities.Add(e);
        }

        public RunnerEntity ResolveEntityPrefab(EntityKind kind, float distance)
        {
            var item = ItemFor(kind);
            if (item != null) return item.prefab;
            var data = GenerationThemeAt(distance);
            prefabs.TryGetValue(kind, out var fallback);
            return RunnerMapVisuals.ObstaclePrefab(data, kind, fallback);
        }

        public ItemData ItemFor(EntityKind kind)
        {
            itemCatalog.TryGetValue(kind, out var data);
            return data;
        }

        void Release(int index)
        {
            var e = entities[index];
            e.gameObject.SetActive(false);
            pools[entitySources[e]].Push(e);
            entities[index] = entities[entities.Count - 1];
            entities.RemoveAt(entities.Count - 1);
        }

        void Collect(int index, Sfx sound)
        {
            GameAudio.Play(sound);
            Release(index);
        }

        void HandleEntities(float dt, float speed)
        {
            Vector2 pc = player.HitCenter + new Vector2(scroll, 0);
            Vector2 ph = player.HitHalf;

            for (int i = entities.Count - 1; i >= 0; i--)
            {
                var e = entities[i];
                Vector2 p = e.transform.localPosition;

                if (magnetT > 0 && e.IsPickup && (pc - p).sqrMagnitude < activeMagnetRadius * activeMagnetRadius)
                {
                    p = Vector2.MoveTowards(p, pc, (speed + activeMagnetPullSpeed) * dt);
                    e.transform.localPosition = p;
                }

                if (p.x - scroll < DespawnX) { Release(i); continue; }

                Vector2 c = p + e.hitOffset;
                if (Mathf.Abs(c.x - pc.x) > e.hitHalf.x + ph.x || Mathf.Abs(c.y - pc.y) > e.hitHalf.y + ph.y) continue;

                if (e.IsPickup)
                {
                    if (e.Item != null) ApplyItem(e.Item);
                    else ApplyLegacyItem(e);
                    bool scoring = e.Item != null ? e.Item.effect == ItemEffect.Score :
                        e.kind == EntityKind.Jelly || e.kind == EntityKind.BigJelly;
                    scoring |= e.Item != null && e.Item.effect == ItemEffect.BonusStage;
                    Collect(i, scoring ? Sfx.Jelly : Sfx.Item);
                    // Switching tracks is deferred until collection finishes using this list.
                    if (pendingBonus != null) break;
                }
                else if (boostT > 0)
                {
                    bonusScore += e.score;
                    shake = Mathf.Max(shake, 0.4f);
                    Collect(i, Sfx.Hit);
                }
                else if (!InBonusStage && dashT <= 0 && invincibleT <= 0)
                {
                    hp -= hitDamage;
                    invincibleT = invincibleTime;
                    shake = 1f;
                    GameAudio.Play(Sfx.Hit);
                }
            }
        }

        void ApplyItem(ItemData data)
        {
            switch (data.effect)
            {
                case ItemEffect.Score: bonusScore += Mathf.Max(0, data.score); break;
                case ItemEffect.Heal: hp = Mathf.Min(maxHp, hp + Mathf.Max(0, data.healAmount)); break;
                case ItemEffect.Magnet:
                    magnetT = Mathf.Max(0.01f, data.duration);
                    activeMagnetRadius = Mathf.Max(0, data.magnetRadius);
                    activeMagnetPullSpeed = Mathf.Max(0, data.magnetPullSpeed);
                    break;
                case ItemEffect.Boost:
                    boostT = Mathf.Max(0.01f, data.duration);
                    activeBoostScale = Mathf.Max(1f, data.speedMultiplier);
                    break;
                case ItemEffect.Dash:
                    dashT = Mathf.Max(0.01f, data.duration);
                    activeDashScale = Mathf.Max(1f, data.speedMultiplier);
                    break;
                case ItemEffect.BonusStage:
                    bonusScore += Mathf.Max(0, data.score);
                    if (InBonusStage || pendingBonus != null) break;
                    bonusCollections.TryGetValue(data, out var count);
                    count++;
                    bonusCollections[data] = count;
                    if (count >= Mathf.Max(1, data.bonusRequiredCount))
                    {
                        if (data.bonusTheme != null || bonusMap != null) pendingBonus = data;
                        else Debug.LogWarning("보너스 스테이지 테마 SO를 연결하세요.", data);
                    }
                    break;
            }
        }

        void BeginBonusStage()
        {
            var data = pendingBonus;
            pendingBonus = null;
            if (data == null) return;
            activeBonusTheme = data.bonusTheme != null ? data.bonusTheme : bonusMap;
            if (activeBonusTheme == null) return;
            bonusCollections[data] = 0;
            bonusT = Mathf.Max(0.1f, data.bonusDuration);
            RebuildTrack(true);
            UpdateTheme(0);
            GameAudio.Play(Sfx.Item);
        }

        void EndBonusStage()
        {
            bonusT = 0;
            activeBonusTheme = null;
            RebuildTrack(false);
            invincibleT = Mathf.Max(invincibleT, 0.5f);
            UpdateTheme(0);
        }

        void RebuildTrack(bool bonus)
        {
            for (int i = entities.Count - 1; i >= 0; i--) Release(i);
            foreach (var ground in grounds)
            {
                ground.go.SetActive(false);
                Destroy(ground.go);
            }
            grounds.Clear();
            // Put a safe continuous floor under the player before generating the next section.
            genX = scroll - 16f;
            AddGround(bonus ? 12f : 30f);
            lastPotionX = genX;
            if (player.transform.position.y < GroundY) player.ResetState();
            Generate(mapSpeed + 0.5f);
        }

        void ApplyLegacyItem(RunnerEntity entity)
        {
            switch (entity.kind)
            {
                case EntityKind.Jelly:
                case EntityKind.BigJelly: bonusScore += entity.score; break;
                case EntityKind.Potion: hp = Mathf.Min(maxHp, hp + potionHeal); break;
                case EntityKind.Magnet:
                    magnetT = magnetTime; activeMagnetRadius = 6f; activeMagnetPullSpeed = 14f; break;
                case EntityKind.Boost: boostT = boostTime; activeBoostScale = boostSpeedScale; break;
                case EntityKind.Dash: dashT = dashTime; activeDashScale = dashSpeedScale; break;
            }
        }

        // ---------- 맵 생성 ----------

        /// <summary>화면 오른쪽 바깥까지 패턴을 이어 붙인다. speed 는 간격 계산용 기본 속도.</summary>
        void Generate(float speed)
        {
            if (InBonusStage)
            {
                while (genX < scroll + 32f) BonusPattern();
                return;
            }
            // 점프 한 번으로 이동하는 거리. 속도가 오를수록 패턴 간격도 넓혀야 공정하다.
            float jumpDist = speed * 0.65f;

            while (genX < scroll + 32f)
            {
                float frequency = Mathf.Max(0.01f, itemFrequency);
                if (genX - lastPotionX > potionInterval / frequency)
                {
                    ItemPattern(EntityKind.Potion);
                    lastPotionX = genX;
                    continue;
                }

                if (genX >= 20f && Random.value < itemPatternChance * frequency && TryRandomItem(out var randomItem))
                {
                    ItemPattern(randomItem);
                    continue;
                }

                // 나머지 구간은 예전과 같은 비율로 나눈다.
                float r = Random.value * 0.94f;
                if (genX < 20f || r < 0.15f) FlatPattern();
                else if (r < 0.40f) LowPattern(jumpDist);
                else if (r < 0.58f) TallPattern(jumpDist);
                else if (r < 0.78f) HangPattern(jumpDist, speed);
                else PitPattern(jumpDist);
            }
        }

        void BonusPattern()
        {
            float x = genX;
            AddGround(6f);
            for (int i = 0; i < 8; i++)
                for (int row = 0; row < 3; row++)
                    Spawn(EntityKind.Jelly, x + 0.4f + i * 0.75f, GroundY + 0.7f + row * 1.1f);
        }

        bool TryRandomItem(out EntityKind kind)
        {
            float magnet = ItemFor(EntityKind.Magnet) != null ? Mathf.Max(0, ItemFor(EntityKind.Magnet).spawnWeight) : 0.35f;
            float boost = ItemFor(EntityKind.Boost) != null ? Mathf.Max(0, ItemFor(EntityKind.Boost).spawnWeight) : 0.3f;
            float dash = ItemFor(EntityKind.Dash) != null ? Mathf.Max(0, ItemFor(EntityKind.Dash).spawnWeight) : 0.35f;
            float total = magnet + boost + dash;
            float r = Random.value * total;
            kind = r < magnet ? EntityKind.Magnet : r < magnet + boost ? EntityKind.Boost : EntityKind.Dash;
            return total > 0;
        }

        void FlatPattern()
        {
            var data = ItemFor(EntityKind.Jelly);
            if (data != null) { SpawnItemPattern(data); return; }
            float x = genX;
            AddGround(6);
            for (int i = 0; i < 6; i++) Spawn(EntityKind.Jelly, x + 0.5f + i, GroundY + 0.6f);
        }

        void ItemPattern(EntityKind item)
        {
            var data = ItemFor(item);
            if (data != null) { SpawnItemPattern(data); return; }
            float x = genX;
            AddGround(6);
            Spawn(item, x + 3f, GroundY + 2.3f);
            Spawn(EntityKind.Jelly, x + 1.2f, GroundY + 0.9f);
            Spawn(EntityKind.Jelly, x + 2.1f, GroundY + 1.7f);
            Spawn(EntityKind.Jelly, x + 3.9f, GroundY + 1.7f);
            Spawn(EntityKind.Jelly, x + 4.8f, GroundY + 0.9f);
        }

        void SpawnItemPattern(ItemData data)
        {
            float x = genX;
            AddGround(data.Length);
            foreach (var point in data.positions ?? Array.Empty<Vector2>())
            {
                var offset = data.PositionAt(point);
                Spawn(data.kind, x + offset.x, GroundY + offset.y);
            }
            foreach (var point in data.guideJellies ?? Array.Empty<Vector2>())
            {
                var offset = data.PositionAt(point);
                Spawn(EntityKind.Jelly, x + offset.x, GroundY + offset.y);
            }
        }

        void LowPattern(float jumpDist)
        {
            int side = Mathf.CeilToInt(jumpDist * 0.8f) + 1;
            float mid = genX + side;
            AddGround(side * 2);
            if (Random.value < 0.35f)
            {
                Spawn(EntityKind.ObstacleLow, mid - 0.55f, GroundY);
                Spawn(EntityKind.ObstacleLow, mid + 0.55f, GroundY);
            }
            else Spawn(EntityKind.ObstacleLow, mid, GroundY);
            JellyArc(mid, jumpDist * 0.5f, 2.1f, 5, false);
        }

        void TallPattern(float jumpDist)
        {
            int side = Mathf.CeilToInt(jumpDist * 1.1f) + 1;
            float mid = genX + side;
            AddGround(side * 2);
            Spawn(EntityKind.ObstacleTall, mid, GroundY);
            JellyArc(mid, jumpDist * 0.8f, 3.7f, 7, true);
        }

        void HangPattern(float jumpDist, float speed)
        {
            int side = Mathf.CeilToInt(jumpDist * 0.8f) + 1;
            float spacing = Mathf.Max(1.4f, hangObstacleWidth);
            int count = Mathf.Max(Random.Range(3, 6), Mathf.CeilToInt(speed * minSlideDuration / spacing));
            float length = count * spacing;
            float start = genX + side;
            AddGround(side * 2 + length);
            for (int i = 0; i < count; i++)
            {
                Spawn(EntityKind.ObstacleHang, start + spacing * (0.5f + i), GroundY + hangClearance);
                Spawn(EntityKind.Jelly, start + spacing * (0.5f + i), GroundY + 0.45f);
            }
            Spawn(EntityKind.Jelly, start - 0.5f, GroundY + 0.45f);
            Spawn(EntityKind.Jelly, start + length + 0.5f, GroundY + 0.45f);
        }

        void PitPattern(float jumpDist)
        {
            int side = Mathf.CeilToInt(jumpDist * 0.8f) + 1;
            int width = jumpDist > 7.5f && Random.value < 0.5f ? 4 : 3;
            AddGround(side);
            float mid = genX + width * 0.5f;
            genX += width;
            AddGround(side);
            JellyArc(mid, width * 0.5f + 1f, 2.1f, 5, Random.value < 0.3f);
        }

        void JellyArc(float centerX, float halfWidth, float peak, int count, bool bigOnTop)
        {
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0 : i / (count - 1f) * 2f - 1f;
                bool top = bigOnTop && i == count / 2;
                Spawn(top ? EntityKind.BigJelly : EntityKind.Jelly, centerX + t * halfWidth,
                    GroundY + 0.7f + peak * (1f - t * t));
            }
        }
    }
}
