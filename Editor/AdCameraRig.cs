using UnityEngine;

namespace Geuneda.AdCapture
{
    internal sealed class AdCameraRig
    {
        private Camera _camera;
        private float _baseSize;
        private Vector3 _basePosition;
        private float _lastSize;
        private Vector3 _lastPosition;
        private bool _written;

        public float Zoom { get; set; } = 1f;
        public Vector2 Focus { get; set; } = new Vector2(0.5f, 0.5f);
        public Vector2 Shake { get; set; }

        public bool Ensure()
        {
            var current = Camera.main;
            if (current != _camera)
            {
                Restore();
                _camera = current;
            }

            return _camera != null;
        }

        public void Apply()
        {
            if (!Ensure())
            {
                return;
            }

            var size = _camera.orthographic ? _camera.orthographicSize : _camera.fieldOfView;
            if (!_written || !Mathf.Approximately(size, _lastSize))
            {
                _baseSize = size;
            }

            if (!_written || _camera.transform.position != _lastPosition)
            {
                _basePosition = _camera.transform.position;
            }

            var halfHeight = _camera.orthographic ? _baseSize : Mathf.Tan(_baseSize * 0.5f * Mathf.Deg2Rad) * Mathf.Abs(_basePosition.z);
            var view = new Vector2(halfHeight * 2f * _camera.aspect, halfHeight * 2f);
            var toFocus = Vector2.Scale(Focus - new Vector2(0.5f, 0.5f), view) * (1f - 1f / Zoom);
            var offset = toFocus + Shake * view.y;
            var position = _basePosition + _camera.transform.right * offset.x + _camera.transform.up * offset.y;

            if (_camera.orthographic)
            {
                _camera.orthographicSize = _baseSize / Zoom;
            }
            else
            {
                _camera.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(_baseSize * 0.5f * Mathf.Deg2Rad) / Zoom) * Mathf.Rad2Deg;
            }

            _camera.transform.position = position;
            _lastSize = _camera.orthographic ? _camera.orthographicSize : _camera.fieldOfView;
            _lastPosition = position;
            _written = true;
        }

        public void Restore()
        {
            if (_camera != null && _written)
            {
                if (_camera.orthographic)
                {
                    _camera.orthographicSize = _baseSize;
                }
                else
                {
                    _camera.fieldOfView = _baseSize;
                }

                _camera.transform.position = _basePosition;
            }

            _written = false;
        }
    }
}
