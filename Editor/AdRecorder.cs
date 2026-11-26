using System;
using System.IO;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Geuneda.AdCapture
{
    internal sealed class AdRecorder
    {
        private RecorderController _controller;
        private RecorderControllerSettings _settings;
        private MovieRecorderSettings _movie;

        public string TempFile { get; private set; }

        public bool IsActive => _controller != null;

        public string Start(string output, int width, int height, int fps, bool mute)
        {
            var temp = Path.Combine(Path.GetDirectoryName(output), ".adcapture-" + Guid.NewGuid().ToString("N"));
            _settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            _movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            _movie.name = "AdCapture";
            _movie.Enabled = true;
            _movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
            };
            _movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = width, OutputHeight = height };
            _movie.AudioInputSettings.PreserveAudio = !mute;
            _movie.OutputFile = temp;
            TempFile = temp + ".mp4";
            string resolved;
            try
            {
                resolved = Path.GetFullPath(_movie.FileNameGenerator.BuildAbsolutePath(null));
            }
            catch (Exception e)
            {
                resolved = e.GetBaseException().Message;
            }

            if (resolved != TempFile)
            {
                Release();
                return $"Recorder 가 다른 경로에 쓰려 합니다: {resolved}";
            }

            _settings.AddRecorderSettings(_movie);
            _settings.SetRecordModeToManual();
            _settings.FrameRatePlayback = FrameRatePlayback.Constant;
            _settings.FrameRate = fps;
            _settings.CapFrameRate = true;
            _controller = new RecorderController(_settings);
            _controller.PrepareRecording();
            if (_controller.StartRecording())
            {
                return null;
            }

            Stop();
            return "Recorder 가 녹화를 시작하지 못했습니다.";
        }

        public void Stop()
        {
            if (_controller == null)
            {
                return;
            }

            try
            {
                if (_controller.IsRecording())
                {
                    _controller.StopRecording();
                }
            }
            finally
            {
                _controller = null;
                Release();
            }
        }

        public static bool HasVideo(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length < 1024)
            {
                return false;
            }

            var bytes = File.ReadAllBytes(path);
            return Contains(bytes, "moov") && Contains(bytes, "vide");
        }

        private static bool Contains(byte[] bytes, string tag)
        {
            for (var i = 0; i + 4 <= bytes.Length; i++)
            {
                if (bytes[i] == tag[0] && bytes[i + 1] == tag[1] && bytes[i + 2] == tag[2] && bytes[i + 3] == tag[3])
                {
                    return true;
                }
            }

            return false;
        }

        private void Release()
        {
            if (_settings != null)
            {
                Object.DestroyImmediate(_settings);
            }

            if (_movie != null)
            {
                Object.DestroyImmediate(_movie);
            }

            _settings = null;
            _movie = null;
        }
    }
}
