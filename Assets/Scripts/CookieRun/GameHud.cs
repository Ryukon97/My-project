using UnityEngine;
using UnityEngine.UI;

namespace CookieRun
{
    /// <summary>
    /// 메인 게임 HUD. 배치와 모양은 2.Prefab/UI/GameHUD 프리팹에서 고치고,
    /// 이 컴포넌트는 연결된 요소에 값만 채운다.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        [Header("체력바")]
        public RectTransform hpFill;
        public Image hpFillImage;
        public Text hpText;
        public Color hpLowColor = new Color(0.95f, 0.25f, 0.2f);
        public Color hpHighColor = new Color(0.45f, 0.9f, 0.35f);

        [Header("점수")]
        public Text scoreText;
        public Text bestText;
        public Text buffText;
        public string bestFormat = "최고 {0:N0}";
        public string scoreFormat = "점수 {0:N0}";

        [Header("안내")]
        public GameObject hint;
        [Tooltip("맵 테마가 바뀔 때 잠깐 뜨는 이름 (없어도 됨)")] public Text themeText;
        public float themeShowTime = 2.5f;

        [Header("조작 버튼 (없으면 화면 왼쪽/오른쪽 터치로 조작)")]
        public HoldButton jumpButton;
        public HoldButton slideButton;

        [Header("일시정지")]
        public Button pauseButton;
        [Tooltip("일시정지하면 열리는 설정 창")] public SettingsPanel settingsPanel;
        [Tooltip("게임으로 돌아가기 전 3, 2, 1 을 보여 주는 글자")] public Text countdownText;

        [Header("게임 오버")]
        public GameObject gameOverPanel;
        public Text gameOverTitle;
        public Text gameOverScore;
        public Button retryButton;
        public Button titleButton;
        public string gameOverLabel = "게임 오버";
        public string newRecordLabel = "신기록!";
        public string resultFormat = "점수 {0:N0}";

        public void SetHp(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            if (hpFillImage.type == Image.Type.Filled) hpFillImage.fillAmount = ratio;
            else hpFill.anchorMax = new Vector2(ratio, 1);
            hpFillImage.color = Color.Lerp(hpLowColor, hpHighColor, ratio);
            if (hpText != null) hpText.text = $"{Mathf.CeilToInt(ratio * 100f)} / 100";
        }

        public void SetScore(int value, int bestValue)
        {
            scoreText.text = string.Format(scoreFormat, value);
            bestText.text = string.Format(bestFormat, bestValue);
        }

        float themeTimer;

        void Update()
        {
            if (themeText == null || themeTimer <= 0) return;
            themeTimer -= Time.deltaTime;
            var c = themeText.color;
            c.a = Mathf.Clamp01(Mathf.Min(themeTimer, themeShowTime - themeTimer) * 3f);
            themeText.color = c;
        }

        public void ShowTheme(string themeName)
        {
            if (themeText == null) return;
            themeText.text = themeName;
            themeTimer = themeShowTime;
        }

        public void SetBuffs(float magnet, float boost, float dash, float bonus = 0, int collected = 0, int required = 0)
        {
            string s = "";
            if (bonus > 0) s += $"보너스 · 무적 {bonus:0.0}초   ";
            else if (required > 0) s += $"큰 별 젤리 {collected}/{required}   ";
            if (dash > 0) s += $"질주 {dash:0.0}초   ";
            if (boost > 0) s += $"부스터 {boost:0.0}초   ";
            if (magnet > 0) s += $"자석 {magnet:0.0}초";
            buffText.text = s;
        }

        /// <summary>0 이하이면 숨긴다.</summary>
        public void SetCountdown(int number)
        {
            if (countdownText == null) return;
            bool show = number > 0;
            if (countdownText.gameObject.activeSelf != show) countdownText.gameObject.SetActive(show);
            if (show) countdownText.text = number.ToString();
        }

        public void SetHint(bool visible)
        {
            if (hint.activeSelf != visible) hint.SetActive(visible);
        }

        public void ShowGameOver(int total, bool newRecord)
        {
            gameOverTitle.text = newRecord ? newRecordLabel : gameOverLabel;
            gameOverScore.text = string.Format(resultFormat, total);
            gameOverPanel.SetActive(true);
        }

        public void HideGameOver()
        {
            gameOverPanel.SetActive(false);
        }
    }
}
