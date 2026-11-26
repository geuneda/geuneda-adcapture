using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Geuneda.AdCapture
{
    internal sealed class AdRunner
    {
        private const int RecorderWarmupFrames = 3;
        private const float FileWaitSeconds = 10f;

        private readonly AdRecorder _recorder = new AdRecorder();
        private readonly AdHider _hider = new AdHider();
        private readonly AdCameraRig _rig = new AdCameraRig();
        private readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();
        private AdShot _shot;
        private IReadOnlyDictionary<string, Func<string[], string>> _actions;
        private AdTap _tap;
        private GameObject _root;
        private float _timeScale;
        private float _shakeAmplitude;
        private float _shakeLeft;
        private float _shakeLength;
        private bool _recording;
        private int _recordedFrames;
        private string _error;
        private string _saved;
        private bool _hooked;
        private bool _done;

        private static float Delta => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;

        public void Begin(AdShot shot, Sprite hand, IReadOnlyDictionary<string, Func<string[], string>> actions)
        {
            try
            {
                _shot = shot;
                _actions = actions;
                _timeScale = Time.timeScale;
                _root = new GameObject("AdCapture") { hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave };
                UnityEngine.Object.DontDestroyOnLoad(_root);
                _tap = new AdTap(hand, new Vector2(shot.handTip[0], shot.handTip[1]), shot.handSize, _root.transform);
                _stack.Push(Play());
                AdLoop.Add(Tick);
                _hooked = true;
                EditorApplication.playModeStateChanged += OnPlayMode;
            }
            catch (Exception e)
            {
                Finish("실패: 시작하지 못했습니다: " + e.GetBaseException().Message);
            }
        }

        private void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                Finish("중단: 플레이 모드가 끝났습니다.");
            }
        }

        private void Tick()
        {
            if (_done)
            {
                return;
            }

            if (_recording)
            {
                _recordedFrames++;
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }

            try
            {
                while (_stack.Count > 0)
                {
                    var top = _stack.Peek();
                    if (!top.MoveNext())
                    {
                        _stack.Pop();
                        continue;
                    }

                    if (top.Current is IEnumerator nested)
                    {
                        _stack.Push(nested);
                        continue;
                    }

                    break;
                }

                if (!_done)
                {
                    ApplyCamera();
                }
            }
            catch (Exception e)
            {
                Finish("실패: " + e.GetBaseException().Message);
            }
        }

        private IEnumerator Play()
        {
            for (var i = 0; i < _shot.steps.Length; i++)
            {
                var step = _shot.steps[i];
                AdCapture.State = $"진행: {i + 1}/{_shot.steps.Length} {step.op}";
                _error = null;
                yield return Step(step);
                if (_error != null)
                {
                    Finish($"실패: steps[{i}] {step.op}: {_error}");
                    yield break;
                }
            }

            if (_recorder.IsActive)
            {
                yield return StopRecording();
                if (_error != null)
                {
                    Finish("실패: stop: " + _error);
                    yield break;
                }
            }

            Finish("완료");
        }

        private IEnumerator Step(AdStep step)
        {
            switch (step.op)
            {
                case "wait":
                    yield return Wait(step.seconds);
                    break;
                case "waitfor":
                    yield return WaitFor(step.target, step.seconds > 0 ? step.seconds : 10f);
                    break;
                case "record":
                    yield return StartRecording();
                    break;
                case "stop":
                    yield return StopRecording();
                    break;
                case "zoom":
                    yield return Zoom(step);
                    break;
                case "shake":
                    if (RequireCamera())
                    {
                        _shakeAmplitude = step.amplitude;
                        _shakeLength = _shakeLeft = step.seconds;
                    }

                    break;
                case "timescale":
                    yield return Ease(step.seconds, Time.timeScale, step.scale, v => Time.timeScale = v);
                    break;
                case "hide":
                    foreach (var path in step.targets)
                    {
                        _error ??= _hider.Hide(path, _root.scene);
                    }

                    break;
                case "show":
                    foreach (var path in step.targets)
                    {
                        _hider.Show(path);
                    }

                    break;
                case "tap":
                    yield return Tap(step);
                    break;
                case "action":
                    _error = Invoke(step);
                    break;
            }
        }

        private IEnumerator StartRecording()
        {
            _error = _recorder.Start(_shot.output, _shot.width, _shot.height, _shot.fps, _shot.mute);
            if (_error != null)
            {
                yield break;
            }

            _recordedFrames = 0;
            _recording = true;
            for (var i = 0; i < RecorderWarmupFrames; i++)
            {
                yield return null;
            }
        }

        private IEnumerator StopRecording()
        {
            _recording = false;
            _recorder.Stop();
            var temp = _recorder.TempFile;
            if (_recordedFrames == 0)
            {
                _error = "녹화된 프레임이 없습니다.";
                yield break;
            }

            var until = Time.realtimeSinceStartup + FileWaitSeconds;
            while (!AdRecorder.HasVideo(temp) && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            if (!AdRecorder.HasVideo(temp))
            {
                _error = $"영상 트랙이 있는 녹화 파일이 만들어지지 않았습니다: {temp}";
                yield break;
            }

            if (File.Exists(_shot.output))
            {
                _error = $"같은 이름의 파일이 생겨 옮기지 않았습니다. 녹화본은 {temp} 에 있습니다.";
                yield break;
            }

            File.Move(temp, _shot.output);
            _saved = _shot.output;
        }

        private IEnumerator Wait(float seconds)
        {
            for (var t = 0f; t < seconds; t += Delta)
            {
                yield return null;
            }
        }

        private IEnumerator WaitFor(string path, float timeout)
        {
            for (var t = 0f; t < timeout; t += Delta)
            {
                if (AdFind.IsVisible(AdFind.Path(path, _root.scene)))
                {
                    yield break;
                }

                yield return null;
            }

            _error = $"{timeout}초 안에 '{path}' 가 화면에 보이지 않았습니다.";
        }

        private bool RequireCamera()
        {
            if (_rig.Ensure())
            {
                return true;
            }

            _error = "연출할 카메라(Camera.main)가 없습니다.";
            return false;
        }

        private IEnumerator Zoom(AdStep step)
        {
            if (!RequireCamera())
            {
                yield break;
            }

            var fromFocus = _rig.Focus;
            var toFocus = step.focus != null && step.focus.Length == 2 ? new Vector2(step.focus[0], step.focus[1]) : fromFocus;
            var fromZoom = _rig.Zoom;
            for (var t = 0f; ; t += Delta)
            {
                if (!RequireCamera())
                {
                    yield break;
                }

                var k = t >= step.seconds ? 1f : Mathf.SmoothStep(0f, 1f, t / step.seconds);
                _rig.Zoom = Mathf.Lerp(fromZoom, step.scale, k);
                _rig.Focus = Vector2.Lerp(fromFocus, toFocus, k);
                if (k >= 1f)
                {
                    yield break;
                }

                yield return null;
            }
        }

        private IEnumerator Ease(float seconds, float from, float to, Action<float> set)
        {
            for (var t = 0f; t < seconds; t += Delta)
            {
                set(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds)));
                yield return null;
            }

            set(to);
        }

        private IEnumerator Tap(AdStep step)
        {
            var byPath = !string.IsNullOrEmpty(step.target);
            var point = Vector2.zero;
            if (byPath)
            {
                var target = AdFind.Path(step.target, _root.scene);
                if (!AdFind.IsVisible(target) || !AdTap.TryScreenPoint(target, out point))
                {
                    _error = $"'{step.target}' 가 화면에 없거나 UI 가 아닙니다.";
                    yield break;
                }
            }
            else
            {
                point = new Vector2(step.focus[0] * Screen.width, step.focus[1] * Screen.height);
            }

            var seconds = step.seconds > 0 ? step.seconds : 0.6f;
            var start = point + new Vector2(Screen.width * 0.12f, -Screen.height * 0.1f);
            var clicked = false;
            for (var t = 0f; t < seconds && _error == null; t += Delta)
            {
                var k = t / seconds;
                var move = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.55f));
                var press = k < 0.55f ? 0f : Mathf.Sin(Mathf.Clamp01((k - 0.55f) / 0.25f) * Mathf.PI);
                _tap.Show(Vector2.Lerp(start, point, move), press);
                if (!clicked && k >= 0.65f)
                {
                    clicked = true;
                    _error = Press(step, byPath, point);
                }

                yield return null;
            }

            if (!clicked && _error == null)
            {
                _error = Press(step, byPath, point);
            }

            _tap.Hide();
        }

        private string Press(AdStep step, bool byPath, Vector2 point)
        {
            if (!byPath)
            {
                return AdTap.Click(null, point);
            }

            var target = AdFind.Path(step.target, _root.scene);
            if (!AdFind.IsVisible(target) || !AdTap.TryScreenPoint(target, out var now))
            {
                return $"누르는 순간 '{step.target}' 가 화면에 없습니다.";
            }

            return AdTap.Click(target, now);
        }

        private string Invoke(AdStep step)
        {
            try
            {
                return _actions[step.name](step.args ?? Array.Empty<string>());
            }
            catch (Exception e)
            {
                return e.GetBaseException().Message;
            }
        }

        private void ApplyCamera()
        {
            if (_shakeLeft > 0f)
            {
                if (!_rig.Ensure())
                {
                    Finish("실패: 흔들림 도중 카메라(Camera.main)가 사라졌습니다.");
                    return;
                }

                _shakeLeft -= Delta;
                var decay = Mathf.Clamp01(_shakeLeft / _shakeLength);
                var t = Time.frameCount * 0.37f;
                _rig.Shake = new Vector2(Mathf.PerlinNoise(t, 0.1f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f) * (2f * _shakeAmplitude * decay);
            }
            else
            {
                _rig.Shake = Vector2.zero;
            }

            _rig.Apply();
        }

        public void Finish(string message)
        {
            if (_done)
            {
                return;
            }

            _done = true;
            var notes = new List<string>();
            Try(notes, () => _stack.Clear());
            Try(notes, () =>
            {
                if (_recorder.IsActive)
                {
                    _recorder.Stop();
                    notes.Add(AdRecorder.HasVideo(_recorder.TempFile) ? $"부분 녹화: {_recorder.TempFile}" : "녹화를 멈췄지만 저장된 영상이 아직 없습니다");
                }
            });
            Try(notes, _rig.Restore);
            Try(notes, () => Time.timeScale = _timeScale);
            Try(notes, _hider.RestoreAll);
            Try(notes, () => _tap?.Dispose());
            Try(notes, () =>
            {
                if (_hooked)
                {
                    AdLoop.Remove(Tick);
                }
            });
            Try(notes, () => EditorApplication.playModeStateChanged -= OnPlayMode);
            Try(notes, () =>
            {
                if (_root != null)
                {
                    UnityEngine.Object.Destroy(_root);
                }
            });

            if (_saved != null)
            {
                notes.Insert(0, $"녹화 저장: {_saved}");
            }

            AdCapture.State = notes.Count > 0 ? $"{message} ({string.Join(" / ", notes)})" : message;
            AdCapture.Finished(this);
        }

        private static void Try(List<string> notes, Action cleanup)
        {
            try
            {
                cleanup();
            }
            catch (Exception e)
            {
                notes.Add("정리 실패: " + e.GetBaseException().Message);
            }
        }
    }
}
