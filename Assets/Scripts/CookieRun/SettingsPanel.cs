using System;
using UnityEngine;
using UnityEngine.UI;

namespace CookieRun
{
    /// <summary>
    /// 설정 창: BGM / SFX 볼륨과 게임 나가기 (2.Prefab/UI/SettingsPanel).
    /// 게임 중 일시정지 창으로 쓸 때는 "타이틀로" 버튼도 보이며, 누르면 한 번 더 확인한다.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        public Slider bgmSlider;
        public Slider sfxSlider;
        public Button quitButton;
        public Button closeButton;
        [Tooltip("창 모서리의 X 버튼. 닫기 버튼과 같은 동작")] public Button xButton;

        [Header("타이틀로 (게임 중에만 보인다)")]
        public Button titleButton;
        [Tooltip("정말 나갈지 묻는 확인 창")] public GameObject confirmPanel;
        public Button confirmYesButton;
        public Button confirmNoButton;
        [Tooltip("게임 씬에서 코드가 켠다. 타이틀 화면에서는 꺼져 있어 버튼이 숨겨진다")] public bool showTitleButton;

        /// <summary>확인 창에서 나가기를 고르면 알린다.</summary>
        public event Action TitleRequested;

        bool ConfirmOpen => confirmPanel != null && confirmPanel.activeSelf;

        /// <summary>창이 닫힐 때 알린다 (게임 중 일시정지 해제에 쓴다).</summary>
        public event Action Closed;

        public bool IsOpen => gameObject.activeSelf;

        void Awake()
        {
            if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(v => GameAudio.BgmVolume = v);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(v => GameAudio.SfxVolume = v);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (xButton != null) xButton.onClick.AddListener(Close);
            if (titleButton != null) titleButton.onClick.AddListener(() => SetConfirm(true));
            if (confirmNoButton != null) confirmNoButton.onClick.AddListener(() => SetConfirm(false));
            if (confirmYesButton != null) confirmYesButton.onClick.AddListener(ConfirmTitle);
        }

        void OnEnable()
        {
            if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(GameAudio.BgmVolume);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(GameAudio.SfxVolume);
            if (titleButton != null) titleButton.gameObject.SetActive(showTitleButton);
            if (confirmPanel != null) confirmPanel.SetActive(false);
        }

        void SetConfirm(bool open)
        {
            GameAudio.Play(Sfx.Button);
            if (confirmPanel != null) confirmPanel.SetActive(open);
            else if (open) TitleRequested?.Invoke(); // 확인 창이 없는 프리팹이면 바로 나간다
        }

        void ConfirmTitle()
        {
            PlayerPrefs.Save();
            TitleRequested?.Invoke();
        }

        public void Open()
        {
            GameAudio.Play(Sfx.Button);
            gameObject.SetActive(true);
        }

        public void Close()
        {
            // 확인 창이 떠 있으면 그것만 닫는다 (ESC / X).
            if (ConfirmOpen)
            {
                SetConfirm(false);
                return;
            }
            GameAudio.Play(Sfx.Button);
            PlayerPrefs.Save();
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        public void Quit()
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
