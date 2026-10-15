using System;
using System.IO;
using UnityEngine;

namespace CookieRun
{
    /// <summary>
    /// 기본 BGM/효과음을 코드로 합성한다. CookieRunSetup 이 Assets/4.Sound 에 WAV 로 내보낼 때만 쓴다.
    /// 임시 소리이므로 같은 이름의 파일로 덮어쓰거나 GameAudio 프리팹의 칸을 바꿔서 교체하면 된다.
    /// </summary>
    public static class SoundFactory
    {
        const int Rate = 22050;

        public static byte[] ToWav(float[] samples)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                int bytes = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes);
                w.Write(new[] { 'W', 'A', 'V', 'E' });
                w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16);
                w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2);
                w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
                foreach (float s in samples) w.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32000f));
                w.Flush();
                return ms.ToArray();
            }
        }

        static float Square(float phase) => Mathf.Repeat(phase, 1f) < 0.5f ? 1f : -1f;
        static float Triangle(float phase) => 4f * Mathf.Abs(Mathf.Repeat(phase, 1f) - 0.5f) - 1f;

        /// <summary>from 에서 to 로 음높이가 변하는 짧은 소리를 buffer 의 start 초 위치에 섞는다.</summary>
        static void Tone(float[] buffer, float start, float length, float from, float to, float volume,
            Func<float, float> wave)
        {
            int begin = (int)(start * Rate), count = (int)(length * Rate);
            float phase = 0;
            for (int i = 0; i < count && begin + i < buffer.Length; i++)
            {
                float t = i / (float)count;
                phase += Mathf.Lerp(from, to, t) / Rate;
                float envelope = Mathf.Min(1f, i / (0.005f * Rate)) * (1f - t);
                buffer[begin + i] += wave(phase) * envelope * volume;
            }
        }

        static float[] Buffer(float seconds) => new float[(int)(seconds * Rate)];

        public static float[] Jump()
        {
            var b = Buffer(0.18f);
            Tone(b, 0, 0.18f, 380, 820, 0.5f, Triangle);
            return b;
        }

        public static float[] Jelly()
        {
            var b = Buffer(0.08f);
            Tone(b, 0, 0.08f, 1250, 1700, 0.35f, Triangle);
            return b;
        }

        public static float[] Item()
        {
            var b = Buffer(0.36f);
            Tone(b, 0.00f, 0.12f, 660, 660, 0.4f, Square);
            Tone(b, 0.09f, 0.12f, 880, 880, 0.4f, Square);
            Tone(b, 0.18f, 0.18f, 1320, 1320, 0.4f, Square);
            return b;
        }

        public static float[] Hit()
        {
            var b = Buffer(0.28f);
            Tone(b, 0, 0.28f, 170, 55, 0.6f, Square);
            var random = new System.Random(7);
            for (int i = 0; i < b.Length; i++)
                b[i] += ((float)random.NextDouble() * 2f - 1f) * 0.35f * (1f - i / (float)b.Length);
            return b;
        }

        public static float[] GameOver()
        {
            var b = Buffer(1.0f);
            float[] notes = { 523.25f, 440f, 349.23f, 261.63f };
            for (int i = 0; i < notes.Length; i++)
                Tone(b, i * 0.2f, i == notes.Length - 1 ? 0.4f : 0.22f, notes[i], notes[i], 0.45f, Triangle);
            return b;
        }

        public static float[] Button()
        {
            var b = Buffer(0.06f);
            Tone(b, 0, 0.06f, 900, 700, 0.4f, Triangle);
            return b;
        }

        /// <summary>8마디짜리 반복 BGM (C–Am–F–G 진행 두 번).</summary>
        public static float[] Bgm()
        {
            const float step = 60f / 140f / 2f; // 8분음표
            // 반음 단위(C4 = 0), -99 는 쉼표
            int[] melody =
            {
                4, 7, 12, 7, 4, 7, 12, 14,      9, 12, 16, 12, 9, 12, 16, -99,
                5, 9, 12, 9, 5, 9, 12, 17,      7, 11, 14, 11, 7, 11, 14, -99,
                16, 14, 12, 7, 12, 14, 16, -99, 16, 12, 9, 12, 16, 19, 16, -99,
                17, 16, 14, 12, 9, 12, 14, 17,  19, 17, 14, 11, 7, 11, 14, -99
            };
            int[] bass = { -12, -15, -19, -17, -12, -15, -19, -17 };

            var b = Buffer(melody.Length * step);
            for (int i = 0; i < melody.Length; i++)
            {
                float time = i * step;
                if (melody[i] > -99)
                {
                    float f = 261.63f * Mathf.Pow(2f, melody[i] / 12f);
                    Tone(b, time, step * 0.95f, f, f, 0.16f, Square);
                }
                float root = 261.63f * Mathf.Pow(2f, bass[i / 8] / 12f) * (i % 2 == 0 ? 1f : 2f);
                Tone(b, time, step * 0.9f, root, root, 0.22f, Triangle);
            }
            return b;
        }
    }
}
