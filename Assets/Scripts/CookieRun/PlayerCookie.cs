using UnityEngine;

namespace CookieRun
{
    /// <summary>
    /// 플레이어. 화면의 고정된 X 위치에서 점프/2단 점프/슬라이드만 한다 (월드가 왼쪽으로 흐른다).
    /// transform.position 은 바닥 기준 위치. 완성형 캐릭터 이미지는 탑승 프레임에서 교체한다.
    /// </summary>
    public class PlayerCookie : MonoBehaviour
    {
        public const float X = -5f;

        [Header("부위")]
        public Transform body;
        public Transform legBack;
        public Transform legFront;

        [Header("움직임")]
        public float gravity = -48f;
        public float jumpVelocity = 15.5f;
        public float doubleJumpVelocity = 14f;
        [Tooltip("다리 움직임 빠르기. 맵이 빨라져도 캐릭터 동작은 이 속도로 일정하다")] public float runAnimSpeed = 14.4f;
        [Tooltip("공중에서 슬라이드를 누르면 이 속도로 급강하한다")] public float fastFallSpeed = 30f;

        [Header("슬라이드 전용 이미지")]
        public Sprite slideSprite;
        public Vector2 slideSpritePosition = new Vector2(-0.05f, 0.67f);

        [Header("프레임 애니메이션 (순서대로 교체 가능)")]
        [Tooltip("탑승 캐릭터 전체 이미지. 비워 두면 기존 Body/Leg 표현을 사용한다")]
        public Sprite[] ridingFrames;
        [Min(0.1f)] public float ridingFramesPerSecond = 8f;
        public Sprite[] doubleJumpFrames;
        public Sprite[] slideFrames;
        public Sprite[] exhaustionFrames;
        public float exhaustionDuration = 1.8f;
        public bool ExhaustionComplete => deathTime >= Mathf.Max(0.5f, exhaustionDuration);

        [Header("판정 박스 (발 기준)")]
        public Vector2 standHitCenter = new Vector2(0, 0.8f);
        public Vector2 standHitHalf = new Vector2(0.33f, 0.76f);
        public Vector2 slideHitCenter = new Vector2(0, 0.64f);
        public Vector2 slideHitHalf = new Vector2(0.95f, 0.6f);

        public bool Grounded { get; private set; }
        public bool Sliding { get; private set; }

        public Vector2 HitCenter => (Vector2)transform.position + (Sliding ? slideHitCenter : standHitCenter);
        public Vector2 HitHalf => Sliding ? slideHitHalf : standHitHalf;

        SpriteRenderer[] renderers;
        float velY, runPhase, spin, ridingTime;
        SpriteRenderer bodyRenderer, backRenderer, frontRenderer;
        Sprite runningBodySprite;
        int jumps;
        float slideTime, deathTime;
        bool dying;

        void Awake()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>();
            bodyRenderer = body.GetComponent<SpriteRenderer>();
            backRenderer = legBack.GetComponent<SpriteRenderer>();
            frontRenderer = legFront.GetComponent<SpriteRenderer>();
            runningBodySprite = bodyRenderer.sprite;
        }

        public void ResetState()
        {
            transform.position = new Vector3(X, RunnerGame.GroundY, 0);
            velY = 0; jumps = 0; spin = 0; runPhase = 0; ridingTime = 0;
            Grounded = true; Sliding = false;
            dying = false; deathTime = slideTime = 0;
            SetTint(Color.white);
            Pose();
        }

        public void SetTint(Color c)
        {
            foreach (var r in renderers) r.color = c;
        }

        public void Tick(float dt, bool jump, bool slide, bool hasGround)
        {
            float y = transform.position.y;
            float prevY = y;

            if (jump && jumps < 2)
            {
                velY = jumps == 0 ? jumpVelocity : doubleJumpVelocity;
                if (jumps == 1) spin = 1f;
                jumps++;
                Grounded = false;
                GameAudio.Play(Sfx.Jump);
            }

            if (!Grounded || !hasGround)
            {
                Grounded = false;
                velY += gravity * dt;
                // 공중에서 슬라이드 = 급강하. 점프를 누른 프레임은 점프가 우선.
                if (slide && !jump) velY = Mathf.Min(velY, -fastFallSpeed);
                y += velY * dt;
                // 이미 구덩이 아래로 떨어진 경우에는 바닥이 다시 받아주지 않는다.
                if (hasGround && velY <= 0 && y <= RunnerGame.GroundY && prevY >= RunnerGame.GroundY - 0.4f)
                {
                    y = RunnerGame.GroundY;
                    velY = 0;
                    jumps = 0;
                    Grounded = true;
                }
            }

            Sliding = slide && Grounded;
            slideTime = Sliding ? slideTime + dt : 0;
            transform.position = new Vector3(X, y, 0);

            runPhase += runAnimSpeed * dt;
            ridingTime += dt;
            spin = Mathf.Max(0, spin - dt / 0.4f);
            Pose();
        }

        void Pose()
        {
            if (dying) return;
            if (spin > 0 && doubleJumpFrames != null && doubleJumpFrames.Length > 0)
            {
                ShowFrame(doubleJumpFrames, 1f - spin);
                return;
            }
            if (Sliding && slideFrames != null && slideFrames.Length > 0)
            {
                // Keep one crouched pose throughout the slide.
                int frame = Mathf.Min(1, slideFrames.Length - 1);
                ShowFrame(slideFrames, frame / (float)slideFrames.Length);
                return;
            }
            if (ridingFrames != null && ridingFrames.Length > 0)
            {
                if (!Grounded && doubleJumpFrames != null && doubleJumpFrames.Length > 0)
                {
                    int frame = velY > 3f ? 0 : velY >= -3f ? Mathf.Min(1, doubleJumpFrames.Length - 1) : doubleJumpFrames.Length - 1;
                    ShowFrame(doubleJumpFrames, frame / (float)doubleJumpFrames.Length);
                }
                else ShowFrame(ridingFrames, Mathf.Repeat(ridingTime * Mathf.Max(0.1f, ridingFramesPerSecond), ridingFrames.Length) / ridingFrames.Length);
                return;
            }
            // The complete slide sprite includes its own legs. Never squash either sprite.
            bool useSlideSprite = Sliding && slideSprite != null;
            bodyRenderer.sprite = useSlideSprite ? slideSprite : runningBodySprite;
            backRenderer.enabled = frontRenderer.enabled = !useSlideSprite;
            body.localScale = Vector3.one;
            if (useSlideSprite)
            {
                float skidBob = Mathf.Sin(runPhase * 1.5f) * 0.008f;
                body.localPosition = new Vector3(slideSpritePosition.x, slideSpritePosition.y + skidBob, 0);
                body.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(runPhase) * 1.2f);
                return;
            }

            float bob = Grounded ? Mathf.Abs(Mathf.Sin(runPhase)) * 0.06f : 0f;
            float swing = Grounded ? Mathf.Sin(runPhase) * 45f : 25f;
            body.localPosition = new Vector3(0, 0.9f + bob, 0);
            body.localScale = Vector3.one;
            body.localRotation = Quaternion.Euler(0, 0, spin > 0 ? -360f * (1f - spin) : 0f);
            SetLeg(legBack, -0.14f, 0.48f, swing);
            SetLeg(legFront, 0.14f, 0.48f, -swing);

        }

        void ShowFrame(Sprite[] frames, float progress)
        {
            int index = Mathf.Clamp((int)(progress * frames.Length), 0, frames.Length - 1);
            Sprite sprite = frames[index];
            if (sprite == null) return;
            bodyRenderer.sprite = sprite;
            backRenderer.enabled = frontRenderer.enabled = false;
            body.localScale = Vector3.one;
            body.localRotation = Quaternion.identity;
            // Frames share a bottom-centred pivot and pixels-per-unit.
            body.localPosition = Vector3.zero;
        }

        public void BeginExhaustion()
        {
            dying = true; deathTime = 0; Sliding = false; spin = 0;
            SetTint(Color.white);
            if (transform.position.y < RunnerGame.GroundY)
                transform.position = new Vector3(X, RunnerGame.GroundY, 0);
            velY = Mathf.Min(velY, 0);
            TickExhaustion(0);
        }

        public void TickExhaustion(float dt)
        {
            deathTime += dt;
            float y = transform.position.y;
            velY += gravity * dt;
            y = Mathf.Max(RunnerGame.GroundY, y + velY * dt);
            transform.position = new Vector3(X, y, 0);
            float progress = Mathf.Clamp01(deathTime / Mathf.Max(0.5f, exhaustionDuration));
            if (exhaustionFrames != null && exhaustionFrames.Length > 0)
                ShowFrame(exhaustionFrames, Mathf.Clamp01(progress / 0.7f));
            else
            {
                body.localRotation = Quaternion.Euler(0, 0, -80f * progress);
                body.localPosition = new Vector3(0, Mathf.Lerp(0.9f, 0.35f, progress), 0);
            }
        }

        static void SetLeg(Transform leg, float x, float y, float angle)
        {
            leg.localPosition = new Vector3(x, y, 0);
            leg.localRotation = Quaternion.Euler(0, 0, angle);
        }
    }
}
