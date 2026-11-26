using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Geuneda.AdCapture
{
    internal sealed class AdTap
    {
        private readonly RectTransform _hand;
        private readonly GameObject _root;

        public AdTap(Sprite sprite, Vector2 tip, float size, Transform parent)
        {
            if (sprite == null)
            {
                return;
            }

            _root = new GameObject("AdCaptureHand", typeof(Canvas));
            _root.transform.SetParent(parent, false);
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var image = new GameObject("Hand", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(_root.transform, false);
            image.sprite = sprite;
            image.raycastTarget = false;
            _hand = image.rectTransform;
            _hand.anchorMin = _hand.anchorMax = Vector2.zero;
            _hand.pivot = tip;
            var height = Screen.height * size;
            _hand.sizeDelta = new Vector2(height * sprite.rect.width / sprite.rect.height, height);
            _root.SetActive(false);
        }

        public static bool TryScreenPoint(GameObject target, out Vector2 point)
        {
            point = default;
            var rect = target != null ? target.transform as RectTransform : null;
            if (rect == null)
            {
                return false;
            }

            var canvas = rect.GetComponentInParent<Canvas>();
            var camera = canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            return true;
        }

        public void Show(Vector2 screen, float press)
        {
            if (_hand == null)
            {
                return;
            }

            _root.SetActive(true);
            _hand.anchoredPosition = screen * (_root.transform as RectTransform).rect.size / new Vector2(Screen.width, Screen.height);
            _hand.localScale = Vector3.one * Mathf.Lerp(1f, 0.85f, press);
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        public void Dispose()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
            }
        }

        public static string Click(GameObject target, Vector2 screen)
        {
            var system = EventSystem.current;
            if (system == null)
            {
                return "EventSystem 이 없어 탭을 보낼 수 없습니다.";
            }

            var data = new PointerEventData(system) { position = screen, pressPosition = screen, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            system.RaycastAll(data, hits);
            if (hits.Count == 0)
            {
                return $"{screen} 위치에 탭을 받을 UI 가 없습니다.";
            }

            var hit = hits[0];
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
            if (target != null)
            {
                var wanted = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
                if (wanted == null)
                {
                    return $"'{target.name}' 에 클릭을 받을 컴포넌트가 없습니다.";
                }

                if (handler != wanted)
                {
                    return $"'{target.name}' 가 '{hit.gameObject.name}' 에 가려 눌리지 않습니다.";
                }
            }

            if (handler == null)
            {
                return $"'{hit.gameObject.name}' 에 클릭을 받을 컴포넌트가 없습니다.";
            }

            var selectable = handler.GetComponent<Selectable>();
            if (selectable != null && !selectable.IsInteractable())
            {
                return $"'{handler.name}' 가 비활성이라 눌리지 않습니다.";
            }

            data.pointerCurrentRaycast = hit;
            data.pointerPressRaycast = hit;
            data.pointerEnter = hit.gameObject;
            data.rawPointerPress = hit.gameObject;
            data.eligibleForClick = true;
            data.pointerPress = ExecuteEvents.ExecuteHierarchy(hit.gameObject, data, ExecuteEvents.pointerDownHandler) ?? handler;
            data.pointerClick = handler;
            ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
            if (!data.eligibleForClick || handler == null || !handler.activeInHierarchy || (selectable != null && !selectable.IsInteractable()))
            {
                return $"'{target?.name ?? hit.gameObject.name}' 를 누르는 동안 클릭이 취소됐습니다.";
            }

            return ExecuteEvents.Execute(handler, data, ExecuteEvents.pointerClickHandler) ? null : $"'{handler.name}' 가 클릭을 받지 않았습니다.";
        }
    }
}
