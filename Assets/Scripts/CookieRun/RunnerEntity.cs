using UnityEngine;

namespace CookieRun
{
    public enum EntityKind { Jelly, BigJelly, Potion, Magnet, Boost, ObstacleLow, ObstacleTall, ObstacleHang, Dash }
    // 프리팹에 숫자로 저장되므로 새 종류는 항상 맨 뒤에 추가한다.

    /// <summary>
    /// 젤리/아이템/장애물 프리팹에 붙는 데이터. 이미지는 SpriteRenderer 에서, 판정 범위는 여기서 고친다.
    /// 판정 박스는 프리팹 스케일과 무관한 월드 단위이며, 선택하면 씬 뷰에 초록 상자로 보인다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class RunnerEntity : MonoBehaviour
    {
        public EntityKind kind;
        [Tooltip("판정 박스 절반 크기")] public Vector2 hitHalf = new Vector2(0.35f, 0.35f);
        [Tooltip("피벗 기준 판정 박스 중심")] public Vector2 hitOffset;
        [Tooltip("젤리/큰 젤리/부수는 장애물의 점수")] public int score;
        [Tooltip("비어 있지 않으면 생성될 때마다 이 중 한 색을 입힌다")] public Color[] randomTints;

        SpriteRenderer sr;
        Sprite originalSprite;
        public ItemData Item { get; private set; }

        public bool IsPickup => kind < EntityKind.ObstacleLow || kind > EntityKind.ObstacleHang;

        public void OnSpawn(ItemData item = null)
        {
            Item = item;
            if (sr == null)
            {
                sr = GetComponent<SpriteRenderer>();
                originalSprite = sr.sprite;
            }
            sr.sprite = item != null && item.sprite != null ? item.sprite : originalSprite;
            if (randomTints == null || randomTints.Length == 0) return;
            sr.color = randomTints[Random.Range(0, randomTints.Length)];
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position + (Vector3)hitOffset, hitHalf * 2f);
        }
    }
}
