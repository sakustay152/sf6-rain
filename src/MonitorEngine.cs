using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

namespace Sf6Rain
{
    public sealed class EngineStatus
    {
        public string State = "idle";
        public string Title = "未开始";
        public string Detail = "";
        public string Side = "";
        public bool Yellow;
        public double Fraction;
        public int Triggers;
        public string Track = "";
        public string Window = "";
    }

    /// <summary>
    /// Capture thread + trigger state machine. Nothing here touches the game
    /// process: frames arrive through GDI, and audio/UI work is posted back to
    /// the UI thread because MediaPlayer must live there.
    /// </summary>
    public sealed class MonitorEngine
    {
        readonly Settings _cfg;
        readonly MusicLibrary _lib;
        readonly SynchronizationContext _ui;
        readonly DetectorState _detState = new DetectorState();

        Thread _thread;
        IntPtr _hwnd;
        string _windowTitle = "";
        volatile bool _run;
        volatile bool _playing;
        bool _armed = true;

        int _yellowSince = -1;
        int _lastYellow = -1;
        int _nextTriggerAt;
        int _triggers;
        string _trackName = "";
        int _lastPreviewAt;
        int _lastStatusAt;
        string _lastStatusSig = "";
        int _audioFailures;
        string _startedSide = "";

        public event Action<Detection, Bitmap> Frame;
        public event Action<EngineStatus> StatusChanged;
        public event Action<Track> Triggered;
        public event Action StopAudioRequested;

        public bool Running { get { return _run; } }

        public MonitorEngine(Settings cfg, MusicLibrary lib)
        {
            _cfg = cfg;
            _lib = lib;
            _ui = SynchronizationContext.Current;
        }

        public void Start(IntPtr hwnd, string windowTitle)
        {
            if (_run) return;
            _hwnd = hwnd;
            _windowTitle = windowTitle;
            _playing = false;
            _armed = true;
            _yellowSince = -1;
            _lastYellow = Environment.TickCount;
            _nextTriggerAt = 0;
            _triggers = 0;
            _trackName = "";
            _audioFailures = 0;
            _startedSide = "";
            _detState.Side = "";
            _detState.HasCache = false;
            _run = true;
            _thread = new Thread(Loop);
            _thread.IsBackground = true;
            _thread.Name = "sf6-capture";
            _thread.Start();
            AppLog.Info(string.Format("开始监看：{0} ({1}×{2})", windowTitle,
                WinApi.ClientOnScreen(hwnd).Width, WinApi.ClientOnScreen(hwnd).Height));
        }

        public void Stop()
        {
            bool was = _run;
            _run = false;
            Thread t = _thread;
            _thread = null;
            if (t != null && t.IsAlive) t.Join(1500);
            _playing = false;
            if (was) AppLog.Info("停止监看");
            PostStopAudio();
            PostStatus("stopped", "已停止监看", "", "", 0, false);
        }

        /// <summary>F9: cut the audio now and wait for the bar to leave yellow.</summary>
        public void EmergencyStop()
        {
            if (!_run) return;
            _playing = false;
            _armed = false;
            _lastYellow = Environment.TickCount;
            AppLog.Info("手动急停");
            PostStopAudio();
            PostStatus("ended", "已急停", "血条恢复后会自动重新待命", "", 0, false);
        }

        public void NotifyAudioEnded()
        {            if (!_run) return;
            if (!_playing) return;
            int now = Environment.TickCount;
            if (_cfg.Loop && now - _lastYellow < 900)
            {
                _lib.Reload(_cfg.MusicFolder, _cfg.PreferRainLove, _cfg.PlayOrder);
                Track next = _lib.Next();
                if (next != null)
                {
                    _trackName = next.Name;
                    AppLog.Info("继续播放：" + next.Name);
                    Track chosen = next;
                    Post(delegate
                    {
                        var h = Triggered;
                        if (h != null)
                        {
                            try { h(chosen); } catch { }
                        }
                    });
                    PostStatus("playing", "雨还在下", next.Name, _startedSide, 0, true);
                    return;
                }
            }
            _playing = false;
            _armed = false;
            _nextTriggerAt = now + (int)(_cfg.CooldownSeconds * 1000);
            AppLog.Info("播放结束：" + _trackName);
            PostStatus("ended", "播放结束，等血条恢复后重新待命", "", "", 0, false);
        }

        public void NotifyAudioFailed(string message)
        {
            if (!_run) return;
            _playing = false;
            _audioFailures++;
            AppLog.Error("播放失败：" + message);
            if (_audioFailures <= 3)
            {
                _armed = true;
                _nextTriggerAt = Environment.TickCount + 2500;
                PostStatus("error", "这首歌没能播放，稍后换下一首", message, "", 0, false);
            }
            else
            {
                _armed = false;
                PostStatus("error", "连续播放失败，请检查 music 文件夹", message, "", 0, false);
            }
        }

        // ------------------------------------------------------------------
        void Loop()
        {
            int frameMs = Math.Max(25, 1000 / Math.Max(5, _cfg.TargetFps));
            var clock = new System.Diagnostics.Stopwatch();
            try
            {
                while (_run)
                {
                    clock.Restart();
                    if (!WinApi.IsWindow(_hwnd))
                    {
                        AppLog.Error("游戏窗口已关闭");
                        _run = false;
                        _playing = false;
                        PostStopAudio();
                        PostStatus("stopped", "游戏窗口已关闭", "请重新选择窗口后再开始", "", 0, false);
                        break;
                    }

                    Detection det = null;
                    using (CaptureResult cap = ScreenCapture.Capture(_hwnd))
                    {
                        if (cap.Image == null || cap.Error.Length > 0 && cap.Black)
                        {
                            PostStatus("error", "抓不到有效画面", cap.Error, "", 0, false);
                        }
                        else
                        {
                            det = Detector.Analyze(cap.Image, _cfg, _detState, Environment.TickCount);
                            try { Step(det, cap); }
                            catch (Exception ex)
                            {
                                AppLog.Error("状态机异常：" + ex.Message);
                            }
                            PostPreview(cap.Image, det);
                        }
                    }

                    int spent = (int)clock.ElapsedMilliseconds;
                    if (spent < frameMs) Thread.Sleep(frameMs - spent);
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("监视线程异常：" + ex);
                PostStatus("error", "监视线程出错", ex.Message, "", 0, false);
            }
            finally
            {
                _run = false;
                PostStatus("stopped", "已停止", "", "", 0, false);
            }
        }

        void Step(Detection det, CaptureResult cap)
        {
            int now = Environment.TickCount;
            int clearMs = (int)(_cfg.ClearSeconds * 1000);
            int holdMs = (int)(_cfg.YellowHoldSeconds * 1000);
            bool myYellow = det.HudFound && det.SideKnown && det.Yellow && !det.YellowTooWide;

            if (det.SideKnown && det.Side.Length > 0) _startedSide = det.Side;

            if (myYellow)
            {
                _lastYellow = now;
                if (_yellowSince < 0) _yellowSince = now;
                if (!_playing && _armed && now >= _nextTriggerAt)
                {
                    if (now - _yellowSince >= holdMs) Trigger(det, cap, now);
                    else
                    {
                        double left = (holdMs - (now - _yellowSince)) / 1000.0;
                        PostStatus("hold", "血量见黄，马上播放",
                            string.Format("再确认 {0:0.0} 秒；{1}", Math.Max(0, left), SideText(det.Side)),
                            det.Side, det.ActiveFraction, true);
                    }
                }
                else
                {
                    PostStatus(_playing ? "playing" : (_armed ? "watching" : "ended"),
                        _playing ? "雨正在下" : (_armed ? "监看中" : "播放结束"),
                        _playing ? _trackName : (_armed ? SideText(det.Side) : "等血条恢复后重新待命"),
                        det.Side, det.ActiveFraction, true);
                }
                return;
            }

            _yellowSince = -1;

            if (_playing && _cfg.StopOnClear && now - _lastYellow >= clearMs)
            {
                _playing = false;
                _armed = true;
                _nextTriggerAt = now + (int)(_cfg.CooldownSeconds * 1000);
                AppLog.Info("血条恢复，停止播放");
                PostStopAudio();
                PostStatus("watching", "血条恢复，雨停了", "再次见黄会重新播放",
                    det.Side, 0, false);
                return;
            }

            if (!_playing && !_armed && now - _lastYellow >= clearMs)
            {
                _armed = true;
                PostStatus("watching", "重新待命", "血条已经恢复，下一次见黄会再播放",
                    det.Side, 0, false);
                return;
            }

            if (det.HudFound)
            {
                PostStatus(_playing ? "playing" : (_armed ? "watching" : "ended"),
                    _playing ? "雨正在下" : (_armed ? "监看中" : "播放结束"),
                    _playing ? _trackName : (det.SideKnown ? SideText(det.Side) : det.Note),
                    det.Side, det.ActiveFraction, false);
            }
            else
            {
                string detail = det.Note.Length > 0 ? det.Note : det.Info;
                if (det.BlackFrame)
                    PostStatus("error", "画面全黑", "请把游戏改成无边框窗口模式，并让画面保持在前台",
                        "", 0, false);
                else if (_playing)
                    PostStatus("playing", "雨正在下", _trackName + "（血条暂时不可见）", "", 0, false);
                else
                    PostStatus("waiting", "正在找血条", detail, "", 0, false);
            }
        }

        void Trigger(Detection det, CaptureResult cap, int now)
        {
            _lib.Reload(_cfg.MusicFolder, _cfg.PreferRainLove, _cfg.PlayOrder);
            Track track = _lib.Next();
            if (track == null)
            {
                _nextTriggerAt = now + 3000;
                AppLog.Error("music 文件夹里没有可播放的音频");
                PostStatus("error", "没有可播放的音乐",
                    "把 雨爱.mp3 或其他 mp3/wav/m4a 放进 music 文件夹", det.Side,
                    det.ActiveFraction, true);
                return;
            }

            _playing = true;
            _armed = false;
            _triggers++;
            _trackName = track.Name;
            _audioFailures = 0;
            string sideName = det.Side == "left" ? "左侧" : det.Side == "right" ? "右侧" : "手动框选";
            AppLog.Info(string.Format("触发 #{0}：我方（{1}）血条黄色占比 {2:0.0}%，播放《{3}》",
                _triggers, sideName, det.ActiveFraction * 100, track.Name));

            if (_cfg.SaveTriggerShots) SaveShot(cap.Image);

            Track chosen = track;
            Post(delegate
            {
                var h = Triggered;
                if (h != null)
                {
                    try { h(chosen); } catch (Exception ex) { AppLog.Error("播放回调异常：" + ex.Message); }
                }
            });

            PostStatus("playing", "雨正在下", track.Name, det.Side, det.ActiveFraction, true);
        }

        void SaveShot(Bitmap image)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.CaptureDir);
                string name = "触发-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".png";
                image.Save(Path.Combine(AppPaths.CaptureDir, name), ImageFormat.Png);
            }
            catch (Exception ex)
            {
                AppLog.Error("保存触发截图失败：" + ex.Message);
            }
        }

        string SideText(string side)
        {
            if (side == "left") return "我方在左侧血条";
            if (side == "right") return "我方在右侧血条";
            if (side == "manual") return "按手动框选的血条";
            return "还没认出哪条是我方";
        }

        // ------------------------------------------------------------------
        void PostPreview(Bitmap frame, Detection det)
        {
            int now = Environment.TickCount;
            if (now - _lastPreviewAt < 130) return;
            _lastPreviewAt = now;
            Bitmap preview = ScreenCapture.Preview(frame, 960);
            if (preview == null) return;
            Detection snapshot = det;
            Post(delegate
            {
                var h = Frame;
                if (h != null)
                {
                    try { h(snapshot, preview); }
                    catch (Exception ex) { AppLog.Error("预览回调异常：" + ex.Message); preview.Dispose(); }
                }
                else preview.Dispose();
            });
        }

        void PostStatus(string state, string title, string detail, string side,
            double fraction, bool yellow)
        {
            int now = Environment.TickCount;
            var st = new EngineStatus();
            st.State = state;
            st.Title = title;
            st.Detail = detail;
            st.Side = side;
            st.Fraction = fraction;
            st.Yellow = yellow;
            st.Triggers = _triggers;
            st.Track = _trackName;
            st.Window = _windowTitle;

            string sig = state + "|" + title + "|" + detail + "|" + side + "|" +
                (int)(fraction * 100) + "|" + _triggers;
            if (sig == _lastStatusSig && now - _lastStatusAt < 1200) return;
            _lastStatusSig = sig;
            _lastStatusAt = now;

            Post(delegate
            {
                var h = StatusChanged;
                if (h != null)
                {
                    try { h(st); } catch { }
                }
            });
        }

        void PostStopAudio()
        {
            Post(delegate
            {
                var h = StopAudioRequested;
                if (h != null)
                {
                    try { h(); } catch { }
                }
            });
        }

        void Post(SendOrPostCallback callback)
        {
            if (_ui != null) _ui.Post(callback, null);
            else callback(null);
        }

        /// <summary>Learned geometry, merged back into config after Stop().</summary>
        public Dictionary<string, double[]> LayoutsCopy()
        {
            var copy = new Dictionary<string, double[]>();
            foreach (var kv in _cfg.Layouts)
                copy[kv.Key] = (double[])kv.Value.Clone();
            return copy;
        }
    }
}
