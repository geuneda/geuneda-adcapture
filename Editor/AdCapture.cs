using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Geuneda.AdCapture
{
    public static class AdCapture
    {
        private static readonly Dictionary<string, Func<string[], string>> Actions = new Dictionary<string, Func<string[], string>>();
        private static AdRunner _runner;

        public static string State { get; internal set; } = "idle";

        public static void Register(string name, Func<string[], string> action)
        {
            if (string.IsNullOrEmpty(name) || action == null)
            {
                throw new ArgumentException("이름과 동작이 모두 필요합니다.");
            }

            Actions[name] = action;
        }

        public static string Run(string shotPath)
        {
            if (!EditorApplication.isPlaying)
            {
                return "실패: 플레이 모드에서만 실행합니다.";
            }

            if (_runner != null)
            {
                return $"실패: 이미 실행 중입니다 ({State}).";
            }

            AdShot shot;
            Sprite hand;
            try
            {
                shot = AdShotLoader.Load(shotPath, Actions.ContainsKey);
                hand = LoadHand(shot.hand);
            }
            catch (ArgumentException e)
            {
                return "실패: " + e.Message;
            }

            _runner = new AdRunner();
            State = "시작";
            _runner.Begin(shot, hand, Actions);
            return State;
        }

        public static string Status()
        {
            return State;
        }

        public static string Abort()
        {
            if (_runner != null)
            {
                _runner.Finish("중단: 사용자가 멈춤");
            }

            return State;
        }

        internal static void Finished(AdRunner runner)
        {
            if (_runner == runner)
            {
                _runner = null;
            }
        }

        private static Sprite LoadHand(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                throw new ArgumentException($"손 그림을 찾지 못했습니다: {path}");
            }

            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
    }
}
