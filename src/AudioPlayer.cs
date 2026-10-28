using System;
using System.IO;
using System.Windows.Forms;
using System.Windows.Media;

namespace Sf6Rain
{
    /// <summary>
    /// WPF's MediaPlayer is the only media engine available without shipping a
    /// decoder, and it handles mp3/wav/m4a on Windows 10/11. It must be created
    /// and driven on the UI thread so its media events are dispatched.
    /// </summary>
    public sealed class AudioPlayer : IDisposable
    {
        readonly MediaPlayer _media = new MediaPlayer();
        readonly Timer _fade = new Timer();
        double _target = 0.8;
        double _fadeFrom, _fadeTo;
        int _fadeElapsed, _fadeMs;
        bool _stopAfterFade;
        bool _disposed;

        public event Action Ended;
        public event Action<string> Failed;

        public bool IsPlaying { get; private set; }
        public string CurrentPath { get; private set; }
        public string CurrentName { get; private set; }

        public double Volume
        {
            get { return _target; }
            set
            {
                double v = value;
                if (v < 0) v = 0;
                if (v > 1) v = 1;
                _target = v;
                if (!_fade.Enabled) _media.Volume = v;
            }
        }

        public AudioPlayer()
        {
            _media.Volume = _target;
            _media.MediaEnded += OnMediaEnded;
            _media.MediaFailed += OnMediaFailed;
            _fade.Interval = 40;
            _fade.Tick += OnFadeTick;
        }

        public void Play(string path, bool fadeIn)
        {
            if (_disposed) return;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                RaiseFailed("文件不存在：" + path);
                return;
            }
            try
            {
                _fade.Stop();
                _media.Stop();
                _media.Close();
                CurrentPath = path;
                CurrentName = Path.GetFileNameWithoutExtension(path);
                IsPlaying = true;
                _media.Open(new Uri(Path.GetFullPath(path)));
                _media.Volume = fadeIn ? 0.0 : _target;
                _media.Play();
                if (fadeIn) StartFade(0.0, _target, 650, false);
            }
            catch (Exception ex)
            {
                IsPlaying = false;
                RaiseFailed(ex.Message);
            }
        }

        /// <summary>Fade to silence, then stop. Does not raise Ended.</summary>
        public void FadeStop(int milliseconds)
        {
            if (_disposed) return;
            IsPlaying = false;
            if (milliseconds <= 0)
            {
                _fade.Stop();
                try { _media.Stop(); } catch { }
                _media.Volume = _target;
                return;
            }
            if (!_media.HasAudio && !_media.HasVideo) return;
            StartFade(_media.Volume, 0.0, milliseconds, true);
        }

        public void StopNow()
        {
            if (_disposed) return;
            _fade.Stop();
            IsPlaying = false;
            try { _media.Stop(); } catch { }
            _media.Volume = _target;
        }

        void StartFade(double from, double to, int milliseconds, bool stopAfter)
        {
            _fadeFrom = from;
            _fadeTo = to;
            _fadeElapsed = 0;
            _fadeMs = Math.Max(40, milliseconds);
            _stopAfterFade = stopAfter;
            _media.Volume = from;
            _fade.Start();
        }

        void OnFadeTick(object sender, EventArgs e)
        {
            _fadeElapsed += _fade.Interval;
            double k = Math.Min(1.0, _fadeElapsed / (double)_fadeMs);
            k = k * k * (3 - 2 * k);
            try { _media.Volume = _fadeFrom + (_fadeTo - _fadeFrom) * k; }
            catch { }
            if (k >= 1.0)
            {
                _fade.Stop();
                if (_stopAfterFade)
                {
                    _stopAfterFade = false;
                    try { _media.Stop(); } catch { }
                    _media.Volume = _target;
                }
            }
        }

        void OnMediaEnded(object sender, EventArgs e)
        {
            if (_disposed) return;
            if (!IsPlaying) return;
            IsPlaying = false;
            _fade.Stop();
            var h = Ended;
            if (h != null)
            {
                try { h(); } catch { }
            }
        }

        void OnMediaFailed(object sender, System.Windows.Media.ExceptionEventArgs e)
        {
            if (_disposed) return;
            // Stopping/closing a clip can make the engine report a failure
            // after the fact (seen once as 0xC00D11B1 right after a fade-out).
            // Only a failure while we believe audio is playing is real.
            if (!IsPlaying) return;
            IsPlaying = false;
            _fade.Stop();
            string message = e != null && e.ErrorException != null
                ? e.ErrorException.Message : "无法播放这个文件";
            RaiseFailed(message);
        }

        void RaiseFailed(string message)
        {
            var h = Failed;
            if (h != null)
            {
                try { h(message); } catch { }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _fade.Stop();
            _fade.Dispose();
            try { _media.Stop(); } catch { }
            try { _media.Close(); } catch { }
        }
    }
}
