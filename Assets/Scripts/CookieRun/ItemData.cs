using UnityEngine;

namespace CookieRun
{
    public enum ItemEffect
    {
        [InspectorName("점수 획득")] Score,
        [InspectorName("체력 회복")] Heal,
        [InspectorName("자석")] Magnet,
        [InspectorName("무적 부스터 · 장애물 파괴")] Boost,
        [InspectorName("무적 질주")] Dash,
        [InspectorName("모아서 보너스 스테이지")] BonusStage
    }

    [CreateAssetMenu(fileName = "Item", menuName = "CookieRun/Item")]
    public class ItemData : ScriptableObject
    {
        public string displayName = "새 아이템";
        [Tooltip("게임의 자동 배치 패턴에서 사용할 아이템 슬롯")]
        public EntityKind kind;
        public RunnerEntity prefab;
        [Tooltip("비워 두면 프리팹의 이미지를 사용합니다")]
        public Sprite sprite;
        public ItemEffect effect;
        [Min(0)] public int score = 10;
        [Min(0)] public float healAmount = 20f;
        [Min(0.01f)] public float duration = 1f;
        [Min(1)] public float speedMultiplier = 2f;
        [Min(0)] public float magnetRadius = 6f;
        [Min(0)] public float magnetPullSpeed = 14f;
        [Min(0), Tooltip("자석/부스터/질주 슬롯이 선택되는 상대 확률. 0이면 랜덤 등장 제외")]
        public float spawnWeight = 1f;

        [Header("보너스 스테이지 효과")]
        [Min(1), Tooltip("이 아이템을 몇 개 모으면 보너스 스테이지가 열리는지")]
        public int bonusRequiredCount = 5;
        [Min(0.1f), Tooltip("무적 보너스 스테이지 지속 시간(초)")]
        public float bonusDuration = 5f;
        [Tooltip("비워 두면 MainGame의 BonusMap을 사용합니다")]
        public MapThemeData bonusTheme;

        [Header("맵 미리보기")]
        [Tooltip("미리보기의 배경·바닥·장애물. 게임의 맵 로테이션과는 별도로 선택합니다")]
        public MapThemeData previewTheme;
        [Min(1), Tooltip("평지 젤리 또는 특수 아이템 구간 길이(m)")]
        public float patternLength = 6f;
        [Tooltip("구간 시작 기준 X, 바닥 기준 Y. 편집기 미리보기에서 드래그할 수 있습니다")]
        public Vector2[] positions = { new Vector2(3f, 2.3f) };
        [Tooltip("아이템으로 안내하는 일반 젤리의 위치")]
        public Vector2[] guideJellies = new Vector2[0];

        public static bool IsPickupKind(EntityKind value) => value == EntityKind.Jelly ||
            value == EntityKind.BigJelly || value == EntityKind.Potion || value == EntityKind.Magnet ||
            value == EntityKind.Boost || value == EntityKind.Dash;

        public bool IsValid => IsPickupKind(kind) && prefab != null && prefab.kind == kind;
        public float Length => Mathf.Max(1f, patternLength);
        public Vector2 PositionAt(Vector2 point) => new Vector2(Mathf.Clamp(point.x, 0, Length), Mathf.Max(0, point.y));

        void OnValidate()
        {
            score = Mathf.Max(0, score);
            healAmount = Mathf.Max(0, healAmount);
            duration = Mathf.Max(0.01f, duration);
            speedMultiplier = Mathf.Max(1f, speedMultiplier);
            magnetRadius = Mathf.Max(0, magnetRadius);
            magnetPullSpeed = Mathf.Max(0, magnetPullSpeed);
            spawnWeight = Mathf.Max(0, spawnWeight);
            patternLength = Mathf.Max(1f, patternLength);
            bonusRequiredCount = Mathf.Max(1, bonusRequiredCount);
            bonusDuration = Mathf.Max(0.1f, bonusDuration);
        }
    }
}
