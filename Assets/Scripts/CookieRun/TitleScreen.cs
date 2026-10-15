using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CookieRun
{
    /// <summary>
    /// 타이틀 화면. 시작 버튼, 빈 곳 터치(tapArea), 아무 키 중 하나로 메인 게임 씬을 연다.
    /// 설정/누적순위 창이 열려 있는 동안에는 시작되지 않는다.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        public string gameSceneName = "MainGame";
        public Button startButton;
        [Tooltip("다른 버튼 뒤에 깔린 전체 화면 버튼. 빈 곳을 누르면 시작한다")] public Button tapArea;
        public Text bestText;
        public string bestFormat = "최고 기록 {0:N0}";
        [Tooltip("깜빡이는 안내 문구 (없어도 됨)")] public Graphic blink;

        [Header("설정 / 누적순위")]
        public Button settingsButton;
        public Button rankingButton;
        public SettingsPanel settingsPanel;
        public RankingPanel rankingPanel;

        bool loading;

        bool PanelOpen => (settingsPanel != null && settingsPanel.IsOpen) || (rankingPanel != null && rankingPanel.IsOpen);

        void Start()
        {
            if (bestText != null) bestText.text = string.Format(bestFormat, PlayerPrefs.GetInt(RunnerGame.BestKey, 0));
            if (startButton != null) startButton.onClick.AddListener(StartGame);
            if (tapArea != null) tapArea.onClick.AddListener(StartGame);
            if (settingsButton != null && settingsPanel != null) settingsButton.onClick.AddListener(settingsPanel.Open);
            if (rankingButton != null && rankingPanel != null) rankingButton.onClick.AddListener(rankingPanel.Open);
            if (settingsPanel != null) settingsPanel.gameObject.SetActive(false);
            if (rankingPanel != null) rankingPanel.gameObject.SetActive(false);
        }

        void Update()
        {
            if (blink != null)
            {
                var c = blink.color;
                c.a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
                blink.color = c;
            }

            var kb = Keyboard.current;
            if (kb == null) return;

            if (PanelOpen)
            {
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    if (settingsPanel != null && settingsPanel.IsOpen) settingsPanel.Close();
                    if (rankingPanel != null && rankingPanel.IsOpen) rankingPanel.Close();
                }
            }
            else if (kb.anyKey.wasPressedThisFrame && !kb.escapeKey.wasPressedThisFrame) StartGame();
        }

        public void StartGame()
        {
            if (loading || PanelOpen) return;
            loading = true;
            GameAudio.Play(Sfx.Button);
            SceneManager.LoadScene(gameSceneName);
        }
    }
}
