using UnityEngine;

namespace CookieRun
{
    [CreateAssetMenu(fileName = "MapTheme", menuName = "CookieRun/Map Theme")]
    public class MapThemeData : ScriptableObject
    {
        [Tooltip("게임에서 표시하는 테마 이름")] public string displayName = "새 테마";
        [Tooltip("보너스 전용 테마는 일반 거리별 로테이션에서 제외됩니다")]
        public bool bonusOnly;
        [Tooltip("슬라이드로 피하는 상단 장애물 프리팹 (ObstacleHang)")] public RunnerEntity upperObstacle;
        [Tooltip("점프로 피하는 하단 장애물 프리팹 (ObstacleLow)")] public RunnerEntity lowerObstacle;
        [Tooltip("2단 점프로 피하는 높은 하단 장애물 프리팹 (ObstacleTall)")] public RunnerEntity tallObstacle;
        [Tooltip("상단 장애물의 기존 생성 높이에서 이동할 거리(m). 음수는 아래, 양수는 위입니다")]
        public float upperObstacleYOffset;
        [Tooltip("하단 장애물의 바닥 기준 생성 높이에서 이동할 거리(m). 음수는 아래, 양수는 위입니다")]
        public float lowerObstacleYOffset;
        [Tooltip("높은 하단 장애물의 바닥 기준 생성 높이에서 이동할 거리(m). 음수는 아래, 양수는 위입니다")]
        public float tallObstacleYOffset;
        [Tooltip("반복해서 이어지는 바닥 이미지")] public Sprite ground;
        [Tooltip("화면을 채우는 뒷배경 이미지")] public Sprite background;

        // Preserve the original parallax settings when migrating older scenes.
        [HideInInspector] public Sprite hill;
        [HideInInspector] public Color hillColorA = Color.white;
        [HideInInspector] public Color hillColorB = Color.white;
        [HideInInspector] public Color cloudColor = Color.white;

        public RunnerEntity ObstacleFor(EntityKind kind)
        {
            switch (kind)
            {
                case EntityKind.ObstacleHang: return upperObstacle;
                case EntityKind.ObstacleLow: return lowerObstacle;
                case EntityKind.ObstacleTall: return tallObstacle;
                default: return null;
            }
        }

        public float ObstacleYOffset(EntityKind kind)
        {
            switch (kind)
            {
                case EntityKind.ObstacleHang: return upperObstacleYOffset;
                case EntityKind.ObstacleLow: return lowerObstacleYOffset;
                case EntityKind.ObstacleTall: return tallObstacleYOffset;
                default: return 0f;
            }
        }
    }
}
