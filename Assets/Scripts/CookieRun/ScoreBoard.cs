using System.Collections.Generic;
using UnityEngine;

namespace CookieRun
{
    /// <summary>이 기기에 쌓이는 점수 순위 (상위 10개). PlayerPrefs 에 저장한다.</summary>
    public static class ScoreBoard
    {
        public const int MaxEntries = 10;
        const string Key = "CookieRun.Ranks";

        public static List<int> Load()
        {
            var list = new List<int>();
            foreach (var part in PlayerPrefs.GetString(Key, "").Split(','))
                if (int.TryParse(part, out int value) && value > 0) list.Add(value);
            list.Sort((a, b) => b.CompareTo(a));
            return list;
        }

        public static void Add(int score)
        {
            if (score <= 0) return;
            var list = Load();
            list.Add(score);
            list.Sort((a, b) => b.CompareTo(a));
            if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);
            PlayerPrefs.SetString(Key, string.Join(",", list));
            PlayerPrefs.Save();
        }
    }
}
