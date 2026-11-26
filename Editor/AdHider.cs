using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Geuneda.AdCapture
{
    internal sealed class AdHider
    {
        private sealed class Entry
        {
            public int Count;
            public Action Restore;
        }

        private readonly Dictionary<Object, Entry> _entries = new Dictionary<Object, Entry>();
        private readonly Dictionary<string, List<Object>> _paths = new Dictionary<string, List<Object>>();
        private readonly HashSet<CanvasGroup> _added = new HashSet<CanvasGroup>();

        public string Hide(string path, Scene persistent)
        {
            if (_paths.ContainsKey(path))
            {
                return null;
            }

            var go = AdFind.Path(path, persistent);
            if (go == null)
            {
                return $"'{path}' 를 찾지 못했습니다.";
            }

            var held = new List<Object>();
            if (go.transform is RectTransform)
            {
                var group = go.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = go.AddComponent<CanvasGroup>();
                    _added.Add(group);
                }

                if (!_entries.ContainsKey(group))
                {
                    var alpha = group.alpha;
                    var target = group;
                    _entries[group] = new Entry { Restore = () => { if (target != null) target.alpha = alpha; } };
                }

                Retain(group, held);
                group.alpha = 0f;
            }
            else
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (!_entries.ContainsKey(r))
                    {
                        if (!r.enabled)
                        {
                            continue;
                        }

                        var target = r;
                        _entries[r] = new Entry { Restore = () => { if (target != null) target.enabled = true; } };
                    }

                    Retain(r, held);
                    r.enabled = false;
                }
            }

            _paths[path] = held;
            return null;
        }

        public void Show(string path)
        {
            if (!_paths.TryGetValue(path, out var held))
            {
                return;
            }

            _paths.Remove(path);
            foreach (var target in held)
            {
                var entry = _entries[target];
                if (--entry.Count == 0)
                {
                    _entries.Remove(target);
                    entry.Restore();
                }
            }
        }

        public void RestoreAll()
        {
            foreach (var entry in _entries.Values)
            {
                entry.Restore();
            }

            foreach (var group in _added)
            {
                if (group != null)
                {
                    Object.Destroy(group);
                }
            }

            _entries.Clear();
            _paths.Clear();
            _added.Clear();
        }

        private void Retain(Object target, List<Object> held)
        {
            _entries[target].Count++;
            held.Add(target);
        }
    }
}
