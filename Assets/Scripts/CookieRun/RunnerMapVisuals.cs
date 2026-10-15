using UnityEngine;

namespace CookieRun
{
    // Runtime and the SO editors share these layout rules.
    public static class RunnerMapVisuals
    {
        public const float BackgroundOverscan = 1.04f;
        public static readonly Color BonusTint = new Color(1f, 0.93f, 0.6f);

        public static float CameraSize(float aspect) => Mathf.Max(5f, 8.5f / Mathf.Max(0.01f, aspect));

        public static void FitBackground(SpriteRenderer renderer, Camera camera)
        {
            if (renderer == null || renderer.sprite == null || camera == null) return;
            renderer.transform.localScale = Vector3.one;
            Vector2 size = renderer.bounds.size;
            if (size.x <= 0 || size.y <= 0) return;
            float scale = Mathf.Max(camera.orthographicSize * 2f * camera.aspect / size.x,
                camera.orthographicSize * 2f / size.y) * BackgroundOverscan;
            renderer.transform.localScale = new Vector3(scale, scale, 1);
            Vector3 offset = camera.transform.position - renderer.bounds.center;
            offset.z = 0;
            renderer.transform.position += offset;
        }

        public static void ConfigureGround(SpriteRenderer renderer, Sprite sprite, float start, float length, float groundY)
        {
            if (sprite != null) renderer.sprite = sprite;
            if (renderer.sprite == null) return;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            // Match one complete vertical tile. A fixed prefab height repeats replacement art vertically.
            renderer.size = new Vector2(length / Mathf.Max(0.0001f, Mathf.Abs(renderer.transform.localScale.x)),
                renderer.sprite.bounds.size.y);
            renderer.transform.localPosition = new Vector3(start, groundY, 0);
            Vector3 topLeft = renderer.transform.parent != null
                ? renderer.transform.parent.TransformPoint(new Vector3(start, groundY, 0))
                : new Vector3(start, groundY, 0);
            var bounds = renderer.bounds;
            renderer.transform.position += new Vector3(topLeft.x - bounds.min.x, topLeft.y - bounds.max.y, 0);
        }

        public static float ObstacleY(MapThemeData theme, EntityKind kind, float baseY) =>
            baseY + (theme != null ? theme.ObstacleYOffset(kind) : 0f);

        public static RunnerEntity ObstaclePrefab(MapThemeData theme, EntityKind kind, RunnerEntity fallback)
        {
            var themed = theme != null ? theme.ObstacleFor(kind) : null;
            return themed != null && themed.kind == kind ? themed : fallback;
        }
    }
}
