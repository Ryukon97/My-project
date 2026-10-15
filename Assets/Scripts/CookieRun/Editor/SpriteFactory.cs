using System;
using UnityEngine;

namespace CookieRun
{
    /// <summary>PNG 로 내보낼 텍스처와 스프라이트 임포트 설정.</summary>
    public struct SpriteSpec
    {
        public Texture2D tex;
        public float ppu;
        public Vector2 pivot;
        public bool fullRect;
    }

    /// <summary>
    /// 기본 이미지를 코드로 그린다. CookieRunSetup 이 Assets/3.Sprite 에 PNG 로 내보낼 때만 쓴다.
    /// 각 그림은 "유닛 좌표 -> 색" 함수로 정의하고 슈퍼샘플링으로 가장자리를 부드럽게 처리한다.
    /// </summary>
    public static class SpriteFactory
    {
        const int Samples = 3;
        static readonly Color None = new Color(0, 0, 0, 0);

        public static SpriteSpec Make(float w, float h, Func<Vector2, Color> shade, float ppu = 64f,
            Vector2? pivot = null, bool fullRect = false)
        {
            int tw = Mathf.Max(1, Mathf.RoundToInt(w * ppu));
            int th = Mathf.Max(1, Mathf.RoundToInt(h * ppu));
            var pixels = new Color[tw * th];

            for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                for (int sy = 0; sy < Samples; sy++)
                for (int sx = 0; sx < Samples; sx++)
                {
                    var p = new Vector2(
                        ((x + (sx + 0.5f) / Samples) / tw - 0.5f) * w,
                        ((y + (sy + 0.5f) / Samples) / th - 0.5f) * h);
                    Color c = shade(p);
                    r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                }
                pixels[y * tw + x] = a > 0 ? new Color(r / a, g / a, b / a, a / (Samples * Samples)) : None;
            }

            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            return new SpriteSpec { tex = tex, ppu = ppu, pivot = pivot ?? new Vector2(0.5f, 0.5f), fullRect = fullRect };
        }

        // ---------- 도형 ----------

        static bool Circle(Vector2 p, float cx, float cy, float r)
        {
            float dx = p.x - cx, dy = p.y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        static bool Ellipse(Vector2 p, float cx, float cy, float rx, float ry)
        {
            float dx = (p.x - cx) / rx, dy = (p.y - cy) / ry;
            return dx * dx + dy * dy <= 1f;
        }

        static bool Box(Vector2 p, float cx, float cy, float hw, float hh, float r = 0f)
        {
            float dx = Mathf.Abs(p.x - cx) - (hw - r), dy = Mathf.Abs(p.y - cy) - (hh - r);
            float outside = new Vector2(Mathf.Max(dx, 0), Mathf.Max(dy, 0)).magnitude;
            return Mathf.Min(Mathf.Max(dx, dy), 0) + outside <= r;
        }

        static bool Tri(Vector2 p, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = (p.x - bx) * (ay - by) - (ax - bx) * (p.y - by);
            float d2 = (p.x - cx) * (by - cy) - (bx - cx) * (p.y - cy);
            float d3 = (p.x - ax) * (cy - ay) - (cx - ax) * (p.y - ay);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0;
            bool pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        // ---------- 캐릭터 ----------

        public static SpriteSpec CookieBody()
        {
            var outline = new Color(0.36f, 0.20f, 0.09f);
            var dough = new Color(0.87f, 0.62f, 0.33f);
            var chip = new Color(0.38f, 0.21f, 0.10f);
            return Make(1.2f, 1.2f, p =>
            {
                if (Circle(p, 0.21f, 0.10f, 0.055f) || Circle(p, 0.42f, 0.10f, 0.045f)) return Color.black;
                if (Circle(p, 0.16f, 0.10f, 0.12f) || Circle(p, 0.39f, 0.10f, 0.095f)) return Color.white;
                if (Circle(p, 0.28f, -0.14f, 0.10f) && p.y < -0.14f) return new Color(0.6f, 0.12f, 0.12f);
                if (Circle(p, -0.02f, -0.12f, 0.07f)) return new Color(0.96f, 0.55f, 0.5f);
                if (Circle(p, -0.28f, 0.25f, 0.07f) || Circle(p, 0.02f, 0.37f, 0.06f) ||
                    Circle(p, -0.32f, -0.2f, 0.07f) || Circle(p, 0.08f, -0.36f, 0.06f) ||
                    Circle(p, -0.4f, 0.03f, 0.05f)) return chip;
                if (Circle(p, 0, 0, 0.52f)) return dough;
                if (Circle(p, 0, 0, 0.58f)) return outline;
                return None;
            });
        }

        public static SpriteSpec CookieLeg()
        {
            var c = new Color(0.36f, 0.20f, 0.09f);
            return Make(0.22f, 0.5f, p => Box(p, 0, 0, 0.09f, 0.23f, 0.09f) ? c : None,
                pivot: new Vector2(0.5f, 0.9f));
        }

        // ---------- 수집품 ----------

        /// <summary>회색조 곰 젤리. SpriteRenderer.color 로 색을 입힌다.</summary>
        public static SpriteSpec Jelly()
        {
            return Make(0.6f, 0.6f, p =>
            {
                if (Circle(p, -0.08f, 0.0f, 0.035f) || Circle(p, 0.08f, 0.0f, 0.035f)) return new Color(0.15f, 0.1f, 0.1f);
                if (Ellipse(p, 0, -0.09f, 0.05f, 0.03f)) return new Color(0.15f, 0.1f, 0.1f);
                if (Circle(p, -0.11f, 0.11f, 0.045f)) return Color.white;
                if (Circle(p, 0, -0.03f, 0.23f) || Circle(p, -0.17f, 0.17f, 0.09f) || Circle(p, 0.17f, 0.17f, 0.09f))
                    return new Color(0.86f, 0.86f, 0.86f);
                return None;
            });
        }

        public static SpriteSpec Heart()
        {
            return Make(0.7f, 0.7f, p =>
            {
                float x = p.x / 0.27f, y = (p.y + 0.04f) / 0.27f;
                float q = x * x + y * y - 1f;
                if (q * q * q - x * x * y * y * y > 0) return None;
                if (Circle(p, -0.12f, 0.12f, 0.05f)) return new Color(1f, 0.8f, 0.8f);
                return new Color(0.93f, 0.18f, 0.25f);
            });
        }

        public static SpriteSpec Magnet()
        {
            return Make(0.7f, 0.7f, p =>
            {
                float d = new Vector2(p.x, p.y + 0.04f).magnitude;
                bool arc = p.y <= -0.04f && d >= 0.13f && d <= 0.3f;
                bool legs = p.y > -0.04f && p.y < 0.3f && Mathf.Abs(p.x) >= 0.13f && Mathf.Abs(p.x) <= 0.3f;
                if (!arc && !legs) return None;
                return p.y > 0.16f ? new Color(0.92f, 0.92f, 0.95f) : new Color(0.9f, 0.15f, 0.2f);
            });
        }

        public static SpriteSpec Bolt()
        {
            return Make(0.76f, 0.76f, p =>
            {
                if (Tri(p, 0.1f, 0.3f, -0.2f, -0.04f, 0.06f, -0.04f) ||
                    Tri(p, -0.1f, -0.3f, 0.2f, 0.04f, -0.06f, 0.04f)) return new Color(1f, 0.9f, 0.2f);
                if (Circle(p, 0, 0, 0.32f)) return new Color(0.2f, 0.45f, 0.95f);
                if (Circle(p, 0, 0, 0.37f)) return Color.white;
                return None;
            });
        }

        /// <summary>질주 아이템: 초록 배지 위의 이중 화살표.</summary>
        public static SpriteSpec Dash()
        {
            return Make(0.76f, 0.76f, p =>
            {
                if (Tri(p, -0.2f, 0.2f, -0.2f, -0.2f, 0.02f, 0f) ||
                    Tri(p, 0.02f, 0.2f, 0.02f, -0.2f, 0.24f, 0f)) return Color.white;
                if (Circle(p, 0, 0, 0.32f)) return new Color(0.12f, 0.72f, 0.45f);
                if (Circle(p, 0, 0, 0.37f)) return Color.white;
                return None;
            });
        }

        // ---------- 장애물 ----------

        public static SpriteSpec ObstacleLow()
        {
            var dark = new Color(0.3f, 0.12f, 0.4f);
            var light = new Color(0.72f, 0.4f, 0.85f);
            return Make(0.9f, 1.1f, p =>
            {
                bool hit = Tri(p, -0.45f, -0.55f, 0.05f, -0.55f, -0.22f, 0.2f) ||
                           Tri(p, -0.2f, -0.55f, 0.45f, -0.55f, 0.12f, 0.55f);
                return hit ? Color.Lerp(dark, light, (p.y + 0.55f) / 1.1f) : None;
            }, pivot: new Vector2(0.5f, 0f));
        }

        public static SpriteSpec ObstacleTall()
        {
            var dark = new Color(0.45f, 0.1f, 0.15f);
            var light = new Color(0.95f, 0.4f, 0.35f);
            return Make(1.0f, 2.5f, p =>
            {
                bool hit = Box(p, 0, -0.4f, 0.27f, 0.85f, 0.08f) ||
                           Tri(p, -0.42f, 0.35f, 0.42f, 0.35f, 0, 1.25f) ||
                           Tri(p, -0.5f, -1.25f, -0.2f, -1.25f, -0.3f, -0.5f) ||
                           Tri(p, 0.5f, -1.25f, 0.2f, -1.25f, 0.3f, -0.5f);
                return hit ? Color.Lerp(dark, light, (p.y + 1.25f) / 2.5f) : None;
            }, pivot: new Vector2(0.5f, 0f));
        }

        /// <summary>천장에서 내려오는 장애물. 폭 1, 높이 6.5 (아래쪽 1유닛이 톱니).</summary>
        public static SpriteSpec ObstacleHang()
        {
            var a = new Color(0.33f, 0.38f, 0.5f);
            var b = new Color(0.25f, 0.29f, 0.4f);
            var tooth = new Color(0.8f, 0.83f, 0.9f);
            return Make(1.0f, 6.5f, p =>
            {
                if (p.y >= -2.25f) return Mathf.Repeat(p.y, 0.5f) < 0.08f ? b : a;
                if (Tri(p, -0.5f, -2.25f, 0f, -2.25f, -0.25f, -3.25f) ||
                    Tri(p, 0f, -2.25f, 0.5f, -2.25f, 0.25f, -3.25f)) return tooth;
                return None;
            }, pivot: new Vector2(0.5f, 0f));
        }

        // ---------- 배경 ----------

        /// <summary>폭 1유닛마다 반복되는 바닥 타일 (Tiled 모드용).</summary>
        public static SpriteSpec Ground()
        {
            return Ground(new Color(1f, 0.94f, 0.8f), new Color(0.47f, 0.28f, 0.16f), new Color(0.38f, 0.22f, 0.12f));
        }

        /// <summary>테마별 바닥: 윗면(cream) / 몸통(choco) / 얼룩(spot) 색만 다르다.</summary>
        public static SpriteSpec Ground(Color cream, Color choco, Color spot)
        {
            return Make(1f, 3f, p =>
            {
                float drip = 1.15f - 0.12f * (Mathf.Sin(p.x * Mathf.PI * 4f) * 0.5f + 0.5f);
                if (p.y > drip) return cream;
                if (Circle(p, -0.2f, 0.6f, 0.09f) || Circle(p, 0.25f, 0.1f, 0.07f) ||
                    Circle(p, -0.1f, -0.5f, 0.1f) || Circle(p, 0.2f, -1.0f, 0.08f)) return spot;
                return choco;
            }, 32f, new Vector2(0.5f, 1f), true);
        }

        public static SpriteSpec Sky()
        {
            return Sky(new Color(1f, 0.87f, 0.78f), new Color(0.55f, 0.8f, 0.98f));
        }

        public static SpriteSpec Sky(Color bottom, Color top)
        {
            return Make(1f, 1f, p => Color.Lerp(bottom, top, p.y + 0.5f), 32f);
        }

        public static SpriteSpec Cloud()
        {
            var c = new Color(1f, 1f, 1f, 0.9f);
            return Make(2.4f, 1.0f, p =>
                Circle(p, -0.6f, -0.1f, 0.33f) || Circle(p, -0.1f, 0.1f, 0.4f) ||
                Circle(p, 0.5f, -0.05f, 0.35f) || Box(p, 0, -0.25f, 0.9f, 0.2f, 0.2f) ? c : None, 32f);
        }

        /// <summary>9-slice 용 둥근 흰색 패널 (UI 버튼/창/체력바).</summary>
        public static SpriteSpec Panel()
        {
            return Make(1f, 1f, p => Box(p, 0, 0, 0.5f, 0.5f, 0.3f) ? Color.white : None, 64f);
        }

        /// <summary>둥근 흰색 원 (Jump/Slide 버튼, 슬라이더 손잡이).</summary>
        public static SpriteSpec Circle()
        {
            return Make(2f, 2f, p => Circle(p, 0, 0, 0.98f) ? Color.white : None, 64f);
        }

        public static SpriteSpec Hill()
        {
            return Make(6f, 3f, p => Ellipse(p, 0, -1.5f, 3f, 3f) ? Color.white : None, 32f);
        }

        // ---------- 테마별 원경 (모두 6 x 3, 아래쪽이 바닥선) ----------

        static bool Pine(Vector2 p, float cx, float h, out bool trunk)
        {
            const float b = -1.1f;
            trunk = Box(p, cx, -1.3f, 0.12f, 0.2f);
            return trunk ||
                   Tri(p, cx - 0.8f, b, cx + 0.8f, b, cx, b + h * 0.55f) ||
                   Tri(p, cx - 0.6f, b + h * 0.3f, cx + 0.6f, b + h * 0.3f, cx, b + h * 0.8f) ||
                   Tri(p, cx - 0.4f, b + h * 0.6f, cx + 0.4f, b + h * 0.6f, cx, b + h);
        }

        public static SpriteSpec HillForest()
        {
            var leaf = new Color(0.2f, 0.5f, 0.3f);
            var leafLight = new Color(0.3f, 0.62f, 0.36f);
            var wood = new Color(0.4f, 0.27f, 0.17f);
            return Make(6f, 3f, p =>
            {
                if (Pine(p, 0f, 2.6f, out bool t1)) return t1 ? wood : leafLight;
                if (Pine(p, -1.9f, 2.0f, out bool t2)) return t2 ? wood : leaf;
                if (Pine(p, 1.9f, 1.8f, out bool t3)) return t3 ? wood : leaf;
                return None;
            }, 32f);
        }

        public static SpriteSpec HillRock()
        {
            var rock = new Color(0.55f, 0.57f, 0.62f);
            var rockDark = new Color(0.44f, 0.46f, 0.52f);
            var snow = new Color(0.96f, 0.97f, 1f);
            return Make(6f, 3f, p =>
            {
                if (Tri(p, -3f, -1.5f, 0.6f, -1.5f, -1.0f, 1.4f)) return p.y > 0.8f ? snow : rock;
                if (Tri(p, -0.6f, -1.5f, 3f, -1.5f, 1.3f, 0.7f)) return p.y > 0.25f ? snow : rockDark;
                return None;
            }, 32f);
        }

        public static SpriteSpec HillLava()
        {
            var basalt = new Color(0.24f, 0.13f, 0.12f);
            var glow = new Color(1f, 0.55f, 0.1f);
            return Make(6f, 3f, p =>
            {
                bool cone = Tri(p, -3f, -1.5f, 3f, -1.5f, -0.9f, 1.0f) || Tri(p, 3f, -1.5f, 0.9f, 1.0f, -0.9f, 1.0f);
                if (!cone) return None;
                // 분화구와 흘러내리는 용암 줄기
                if (p.y > 0.75f || Box(p, -0.2f, 0.2f, 0.1f, 0.7f, 0.1f) || Box(p, 0.35f, 0.45f, 0.08f, 0.45f, 0.08f)) return glow;
                return basalt;
            }, 32f);
        }

        public static SpriteSpec HillCave()
        {
            var a = new Color(0.33f, 0.3f, 0.45f);
            var b = new Color(0.25f, 0.23f, 0.36f);
            return Make(6f, 3f, p =>
            {
                if (Tri(p, -1.4f, -1.5f, 0f, -1.5f, -0.7f, 1.4f) || Tri(p, 1.4f, -1.5f, 2.9f, -1.5f, 2.1f, 1.0f)) return a;
                if (Tri(p, -2.8f, -1.5f, -1.6f, -1.5f, -2.2f, 0.6f) || Tri(p, 0.2f, -1.5f, 1.2f, -1.5f, 0.7f, 0.2f)) return b;
                return None;
            }, 32f);
        }
    }
}
