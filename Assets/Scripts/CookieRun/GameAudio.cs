using UnityEngine;

namespace CookieRun
{
    public enum Sfx { Jump, Jelly, Item, Hit, GameOver, Button }

    /// <summary>
    /// BGM 과 효과음. 씬이 바뀌어도 하나만 유지된다 (2.Prefab/GameAudio).
    /// 소리 파일은 이 프리팹의 칸에 끌어다 놓으면 바뀌고, 볼륨은 설정 창에서 조절되어 저장된다.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        const string BgmKey = "CookieRun.BgmVolume";
        const string SfxKey = "CookieRun.SfxVolume";

        [Header("BGM")]
        public AudioClip bgm;

        [Header("효과음")]
        public AudioClip jump;
        public AudioClip jelly;
        public AudioClip item;
        public AudioClip hit;
        public AudioClip gameOver;
        public AudioClip button;

        static GameAudio instance;
        AudioSource bgmSource, sfxSource;

        public static float BgmVolume
        {
            get => PlayerPrefs.GetFloat(BgmKey, 0.6f);
            set
            {
                PlayerPrefs.SetFloat(BgmKey, Mathf.Clamp01(value));
                if (instance != null) instance.bgmSource.volume = Mathf.Clamp01(value);
            }
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxKey, 0.8f);
            set
            {
                PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value));
                if (instance != null) instance.sfxSource.volume = Mathf.Clamp01(value);
            }
        }

        void Awake()
        {
            // 다른 씬에서 넘어온 것이 이미 있으면 그쪽을 계속 쓴다 (BGM 이 끊기지 않게).
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.volume = BgmVolume;
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.volume = SfxVolume;

            if (bgm != null)
            {
                bgmSource.clip = bgm;
                bgmSource.Play();
            }
        }

        public static void Play(Sfx id)
        {
            if (instance == null) return;
            AudioClip clip = id switch
            {
                Sfx.Jump => instance.jump,
                Sfx.Jelly => instance.jelly,
                Sfx.Item => instance.item,
                Sfx.Hit => instance.hit,
                Sfx.GameOver => instance.gameOver,
                _ => instance.button
            };
            if (clip != null) instance.sfxSource.PlayOneShot(clip);
        }
    }
}
