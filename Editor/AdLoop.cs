using System;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Geuneda.AdCapture
{
    internal static class AdLoop
    {
        private struct AdCaptureTick
        {
        }

        public static void Add(PlayerLoopSystem.UpdateFunction tick)
        {
            Edit(list =>
            {
                var system = new PlayerLoopSystem { type = typeof(AdCaptureTick), updateDelegate = tick };
                var copy = new PlayerLoopSystem[list.Length + 1];
                copy[0] = system;
                Array.Copy(list, 0, copy, 1, list.Length);
                return copy;
            });
        }

        public static void Remove(PlayerLoopSystem.UpdateFunction tick)
        {
            Edit(list => Array.FindAll(list, s => s.type != typeof(AdCaptureTick) || s.updateDelegate != tick));
        }

        private static void Edit(Func<PlayerLoopSystem[], PlayerLoopSystem[]> change)
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            for (var i = 0; i < loop.subSystemList.Length; i++)
            {
                if (loop.subSystemList[i].type == typeof(PostLateUpdate))
                {
                    loop.subSystemList[i].subSystemList = change(loop.subSystemList[i].subSystemList ?? Array.Empty<PlayerLoopSystem>());
                    PlayerLoop.SetPlayerLoop(loop);
                    return;
                }
            }

            throw new InvalidOperationException("PostLateUpdate 단계를 찾지 못했습니다.");
        }
    }
}
