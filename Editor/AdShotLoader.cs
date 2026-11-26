using System;
using System.IO;
using UnityEngine;

namespace Geuneda.AdCapture
{
    internal static class AdShotLoader
    {
        public static AdShot Load(string path, Func<string, bool> hasAction)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                throw new ArgumentException($"촬영 스크립트가 없습니다: {path}");
            }

            var shot = JsonUtility.FromJson<AdShot>(File.ReadAllText(path));
            if (shot == null || shot.steps == null || shot.steps.Length == 0)
            {
                throw new ArgumentException("steps 가 비어 있습니다.");
            }

            shot.width = shot.width == 0 ? 1080 : shot.width;
            shot.height = shot.height == 0 ? 1920 : shot.height;
            shot.fps = shot.fps == 0 ? 60 : shot.fps;
            shot.handSize = shot.handSize == 0 ? 0.12f : shot.handSize;
            shot.handTip = shot.handTip == null || shot.handTip.Length == 0 ? new[] { 0.2f, 0.85f } : shot.handTip;

            Require(shot.width > 0 && shot.width <= 4096 && shot.width % 2 == 0, "width 는 4096 이하의 양의 짝수여야 합니다.");
            Require(shot.height > 0 && shot.height <= 4096 && shot.height % 2 == 0, "height 는 4096 이하의 양의 짝수여야 합니다.");
            Require(shot.fps >= 1 && shot.fps <= 120, "fps 는 1~120 이어야 합니다.");
            Require(shot.handSize > 0 && shot.handSize <= 1, "handSize 는 0 초과 1 이하여야 합니다.");
            Require(IsPoint(shot.handTip), "handTip 은 0~1 범위의 [x, y] 여야 합니다.");

            var recording = false;
            var recorded = false;
            for (var i = 0; i < shot.steps.Length; i++)
            {
                var step = shot.steps[i];
                var at = $"steps[{i}] ({step?.op})";
                Require(step != null && !string.IsNullOrEmpty(step.op), $"{at}: op 가 없습니다.");
                Require(Finite(step.seconds) && step.seconds >= 0, $"{at}: seconds 는 0 이상이어야 합니다.");
                Require(step.focus == null || step.focus.Length == 0 || IsPoint(step.focus), $"{at}: focus 는 0~1 범위의 [x, y] 여야 합니다.");
                switch (step.op)
                {
                    case "wait":
                        break;
                    case "waitfor":
                        Require(!string.IsNullOrEmpty(step.target), $"{at}: target 이 필요합니다.");
                        break;
                    case "record":
                        Require(!recording, $"{at}: 이미 녹화 중입니다.");
                        Require(!recorded, $"{at}: 한 스크립트는 한 파일만 녹화합니다.");
                        recording = true;
                        recorded = true;
                        break;
                    case "stop":
                        Require(recording, $"{at}: 녹화 중이 아닙니다.");
                        recording = false;
                        break;
                    case "zoom":
                        Require(Finite(step.scale) && step.scale > 0 && step.scale <= 20, $"{at}: scale 은 0 초과 20 이하여야 합니다.");
                        break;
                    case "shake":
                        Require(Finite(step.amplitude) && step.amplitude > 0 && step.amplitude <= 1, $"{at}: amplitude 는 0 초과 1 이하여야 합니다.");
                        Require(step.seconds > 0, $"{at}: seconds 가 필요합니다.");
                        break;
                    case "timescale":
                        Require(Finite(step.scale) && step.scale >= 0 && step.scale <= 100, $"{at}: scale 은 0~100 이어야 합니다.");
                        break;
                    case "hide":
                    case "show":
                        Require(step.targets != null && step.targets.Length > 0, $"{at}: targets 가 필요합니다.");
                        break;
                    case "tap":
                        Require(!string.IsNullOrEmpty(step.target) || IsPoint(step.focus), $"{at}: target 또는 focus 가 필요합니다.");
                        break;
                    case "action":
                        Require(!string.IsNullOrEmpty(step.name) && hasAction(step.name), $"{at}: 등록되지 않은 action '{step.name}' 입니다.");
                        break;
                    default:
                        throw new ArgumentException($"{at}: 알 수 없는 op 입니다.");
                }
            }

            if (recorded)
            {
                Require(!string.IsNullOrEmpty(shot.output) && Path.IsPathRooted(shot.output), "output 은 절대 경로여야 합니다.");
                Require(shot.output.IndexOfAny(new[] { '<', '>' }) < 0, "output 경로에 < > 를 쓰지 않습니다.");
                Require(Path.DirectorySeparatorChar == '\\' || shot.output.IndexOf('\\') < 0, "output 경로에 \\ 를 쓰지 않습니다.");
                shot.output = Path.GetFullPath(Path.ChangeExtension(shot.output, ".mp4"));
                Require(Directory.Exists(Path.GetDirectoryName(shot.output)), "output 폴더가 없습니다.");
                Require(!File.Exists(shot.output), $"이미 있는 파일은 덮어쓰지 않습니다: {shot.output}");
            }

            return shot;
        }

        private static bool IsPoint(float[] p)
        {
            return p != null && p.Length == 2 && Finite(p[0]) && Finite(p[1]) && p[0] >= 0 && p[0] <= 1 && p[1] >= 0 && p[1] <= 1;
        }

        private static bool Finite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }

        private static void Require(bool ok, string message)
        {
            if (!ok)
            {
                throw new ArgumentException(message);
            }
        }
    }
}
