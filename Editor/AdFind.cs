using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Geuneda.AdCapture
{
    internal static class AdFind
    {
        public static GameObject Path(string path, Scene persistent)
        {
            var parts = path.Trim('/').Split('/');
            GameObject inactive = null;
            foreach (var root in Roots(persistent))
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!Matches(t, parts))
                    {
                        continue;
                    }

                    if (t.gameObject.activeInHierarchy)
                    {
                        return t.gameObject;
                    }

                    inactive ??= t.gameObject;
                }
            }

            return inactive;
        }

        public static bool IsVisible(GameObject go)
        {
            if (go == null || !go.activeInHierarchy)
            {
                return false;
            }

            if (!(go.transform is RectTransform))
            {
                return true;
            }

            var canvas = go.GetComponentInParent<Canvas>();
            if (canvas == null || !canvas.isActiveAndEnabled)
            {
                return false;
            }

            var alpha = 1f;
            for (var t = go.transform; t != null; t = t.parent)
            {
                if (t.TryGetComponent<CanvasGroup>(out var group) && group.enabled)
                {
                    alpha *= group.alpha;
                    if (group.ignoreParentGroups)
                    {
                        break;
                    }
                }
            }

            return alpha > 0.01f;
        }

        private static IEnumerable<GameObject> Roots(Scene persistent)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    foreach (var go in scene.GetRootGameObjects())
                    {
                        yield return go;
                    }
                }
            }

            if (persistent.IsValid())
            {
                foreach (var go in persistent.GetRootGameObjects())
                {
                    yield return go;
                }
            }
        }

        private static bool Matches(Transform t, string[] parts)
        {
            for (var i = parts.Length - 1; i >= 0; i--)
            {
                if (t == null || !MatchesPart(t, parts[i]))
                {
                    return false;
                }

                t = t.parent;
            }

            return true;
        }

        private static bool MatchesPart(Transform t, string part)
        {
            var open = part.LastIndexOf('[');
            if (open > 0 && part.EndsWith("]") && int.TryParse(part.Substring(open + 1, part.Length - open - 2), out var index))
            {
                return t.name == part.Substring(0, open) && t.GetSiblingIndex() == index;
            }

            return t.name == part;
        }
    }
}
