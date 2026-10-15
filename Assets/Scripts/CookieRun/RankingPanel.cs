using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace CookieRun
{
    /// <summary>누적순위 창: 이 기기에 쌓인 상위 점수 목록 (2.Prefab/UI/RankingPanel).</summary>
    public class RankingPanel : MonoBehaviour
    {
        public Text listText;
        public Button closeButton;
        public string lineFormat = "{0}위      {1:N0}";
        public string emptyText = "아직 기록이 없습니다";

        public bool IsOpen => gameObject.activeSelf;

        void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        void OnEnable()
        {
            if (listText == null) return;
            var scores = ScoreBoard.Load();
            if (scores.Count == 0)
            {
                listText.text = emptyText;
                return;
            }
            var sb = new StringBuilder();
            for (int i = 0; i < scores.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.AppendFormat(lineFormat, i + 1, scores[i]);
            }
            listText.text = sb.ToString();
        }

        public void Open()
        {
            GameAudio.Play(Sfx.Button);
            gameObject.SetActive(true);
        }

        public void Close()
        {
            GameAudio.Play(Sfx.Button);
            gameObject.SetActive(false);
        }
    }
}
