using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Sf6Rain
{
    public class MainForm : Form
    {
        Settings _cfg;
        readonly MusicLibrary _lib = new MusicLibrary();
        readonly AudioPlayer _audio;
        readonly OverlayForm _overlay;
        MonitorEngine _engine;
        bool _monitoring;
        bool _loadingUi;
        float _uiScale = 1f;
        IntPtr _gameHandle;
        string _lastWindowLog = "";

        readonly Timer _saveTimer = new Timer();
        readonly Timer _windowTimer = new Timer();
        readonly List<Control> _configControls = new List<Control>();

        Pill _pill;
        NavButton _navMonitor, _navMusic, _navRules;
        Panel _monitorPage, _musicPage, _rulesPage;

        PreviewPanel _preview;
        SelectBox _windowCombo;
        FlatButton _refreshWindows, _activateWindow;
        Label _stateLabel, _detailLabel, _sideLabel, _triggersLabel, _trackLabel;
        Label _meterValue, _startHint, _statusLeft, _saveState, _version;
        Meter _meter;
        FlatButton _startBtn;
        DarkList _log;

        DarkList _tracks;
        Label _trackCount, _nowPlaying;
        SlimSlider _volume;
        Toggle _loopToggle, _preferToggle;
        SelectBox _orderCombo;
        FlatButton _testBtn, _stopTestBtn, _addMusicBtn, _openMusicBtn, _refreshMusicBtn;

        NumberBox _holdNum, _clearNum, _cooldownNum, _maxNum;
        Toggle _stopClearToggle, _fadeToggle, _shotsToggle, _minimizeToggle, _overlayToggle;
        SelectBox _sideCombo, _fpsCombo;
        FlatButton _selectBarBtn, _clearBarBtn, _resetBtn;
        Label _manualNote, _selfTestNote;

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        public MainForm()
        {
            _uiScale = ComputeUiScale();
            _cfg = Settings.Load(AppPaths.ConfigFile);
            _audio = new AudioPlayer();
            _audio.Volume = _cfg.Volume;
            _audio.Ended += OnAudioEnded;
            _audio.Failed += OnAudioFailed;
            _overlay = new OverlayForm();

            Text = "街霸六的雨";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(S(1100), S(700));
            BackColor = Theme.Bg;
            Font = Theme.F(9f);
            DoubleBuffered = true;
            KeyPreview = true;

            BuildUi();

            _saveTimer.Interval = 450;
            _saveTimer.Tick += delegate
            {
                _saveTimer.Stop();
                SaveNow(true);
            };
            _windowTimer.Interval = 4000;
            _windowTimer.Tick += delegate
            {
                if (_monitoring) return;
                if (_windowCombo.Items.Count == 0) RefreshWindows(false);
            };

            AppLog.Line += OnLogLine;
            foreach (string line in AppLog.RecentLines()) AppendLog(line);

            ApplySettingsToUi();
            ReloadMusic();
            RefreshWindows(false);
            ShowPage(0);
            UpdateStartUi(false);
            _pill.Set("待机", Theme.Muted);
            _statusLeft.Text = "待机 · 选好游戏窗口，再点「开始监看」";
            AppLog.Info("界面就绪 · " + AppPaths.BaseDir);
        }

        // ==================================================================
        // Layout
        // ==================================================================
        void BuildUi()
        {
            var logo = new LogoBox();
            logo.Bounds = SRect(24, 12, 40, 40);
            Controls.Add(logo);

            Label title = L(this, "街霸六的雨", 76, 8, 320, 28, 15f, Theme.Text, FontStyle.Bold,
                ContentAlignment.MiddleLeft);
            Label sub = L(this, "我方血条见黄，雨就落下来", 77, 34, 360, 20, 8.5f, Theme.Muted,
                FontStyle.Regular, ContentAlignment.MiddleLeft);

            _pill = new Pill();
            _pill.Bounds = SRect(762, 18, 230, 28);
            _pill.BackColor = Theme.Bg;
            Controls.Add(_pill);

            var min = new FlatButton();
            min.Text = "—";
            min.Quiet = true;
            min.BackColor = Theme.Bg;
            min.Font = Theme.F(11f, FontStyle.Bold);
            min.Bounds = SRect(1012, 16, 34, 30);
            min.Click += delegate { WindowState = FormWindowState.Minimized; };
            Controls.Add(min);

            var close = new FlatButton();
            close.Text = "✕";
            close.Quiet = true;
            close.BackColor = Theme.Bg;
            close.Font = Theme.F(10f, FontStyle.Bold);
            close.Bounds = SRect(1052, 16, 34, 30);
            close.Click += delegate { Close(); };
            Controls.Add(close);

            Controls.Add(Line(0, 61, 1100, 1));
            Controls.Add(Line(0, 111, 1100, 1));

            _navMonitor = Nav(24, "监看", 0);
            _navMusic = Nav(116, "音乐", 1);
            _navRules = Nav(208, "规则", 2);
            _navMonitor.Active = true;

            L(this, "F8 开始/停止 · F9 急停", 820, 70, 256, 26, 8.5f, Theme.Faint,
                FontStyle.Regular, ContentAlignment.MiddleRight);

            _monitorPage = Page();
            _musicPage = Page();
            _rulesPage = Page();
            _monitorPage.Visible = true;
            _musicPage.Visible = false;
            _rulesPage.Visible = false;

            BuildMonitorPage();
            BuildMusicPage();
            BuildRulesPage();

            Controls.Add(Line(0, 664, 1100, 1));
            _statusLeft = L(this, "待机", 24, 670, 620, 22, 8.5f, Theme.Muted,
                FontStyle.Regular, ContentAlignment.MiddleLeft);
            _saveState = L(this, "自动保存", 660, 670, 200, 22, 8f, Theme.Faint,
                FontStyle.Regular, ContentAlignment.MiddleRight);
            _version = L(this, "v1.0 · 单文件 · 不读游戏内存", 872, 670, 204, 22, 8f, Theme.Faint,
                FontStyle.Regular, ContentAlignment.MiddleRight);

            HeaderDrag(this);
            HeaderDrag(logo);
            HeaderDrag(title);
            HeaderDrag(sub);
            HeaderDrag(_pill);
        }

        Panel Page()
        {
            var p = new Panel();
            p.Bounds = SRect(24, 112, 1052, 552);
            p.BackColor = Theme.Bg;
            Controls.Add(p);
            return p;
        }

        NavButton Nav(int x, string text, int page)
        {
            var n = new NavButton();
            n.Text = text;
            n.Bounds = SRect(x, 67, 84, 34);
            n.Click += delegate { ShowPage(page); };
            Controls.Add(n);
            return n;
        }

        Panel Line(int x, int y, int w, int h)
        {
            var p = new Panel();
            p.Bounds = SRect(x, y, w, h);
            p.BackColor = Theme.BorderSoft;
            return p;
        }

        void BuildMonitorPage()
        {
            var card = CardAt(_monitorPage, 0, 0, 648, 404, "实时画面", "GDI 截屏 · 不读内存");
            _preview = new PreviewPanel();
            _preview.Bounds = SRect(14, 38, 620, 322);
            _preview.SelectionMade += OnSelectionMade;
            card.Controls.Add(_preview);

            L(card, "游戏窗口", 16, 370, 68, 26, 8.5f, Theme.Muted, FontStyle.Regular,
                ContentAlignment.MiddleLeft);
            _windowCombo = new SelectBox();
            _windowCombo.Bounds = SRect(88, 368, 330, 30);
            _windowCombo.SelectedIndexChanged += OnWindowChanged;
            card.Controls.Add(_windowCombo);
            AddConfig(_windowCombo);

            _refreshWindows = Btn(card, "刷新", 426, 368, 76, 30, false,
                delegate { RefreshWindows(true); });
            _activateWindow = Btn(card, "激活", 510, 368, 76, 30, false,
                delegate { ActivateWindow(); });
            AddConfig(_refreshWindows);

            card = CardAt(_monitorPage, 668, 0, 384, 404, "状态", "");
            _stateLabel = L(card, "未开始", 16, 38, 352, 32, 15f, Theme.Yellow, FontStyle.Bold,
                ContentAlignment.MiddleLeft);
            _detailLabel = L(card, "选好游戏窗口，点下面的按钮开始。", 16, 72, 352, 46, 8.5f,
                Theme.Muted, FontStyle.Regular, ContentAlignment.TopLeft);
            L(card, "方位", 16, 126, 40, 24, 8.5f, Theme.Faint, FontStyle.Regular,
                ContentAlignment.MiddleLeft);
            _sideLabel = L(card, "—", 62, 124, 150, 26, 9.5f, Theme.Text, FontStyle.Bold,
                ContentAlignment.MiddleLeft);
            _triggersLabel = L(card, "触发 0 次", 218, 126, 150, 24, 8.5f, Theme.Muted,
                FontStyle.Regular, ContentAlignment.MiddleRight);

            L(card, "我方黄色占比", 16, 158, 200, 22, 8.5f, Theme.Muted, FontStyle.Regular,
                ContentAlignment.MiddleLeft);
            _meterValue = L(card, "0%", 270, 158, 98, 22, 9.5f, Theme.Yellow, FontStyle.Bold,
                ContentAlignment.MiddleRight);
            _meter = new Meter();
            _meter.Bounds = SRect(16, 182, 352, 12);
            _meter.BackColor = Theme.Panel;
            card.Controls.Add(_meter);
            L(card, "黄色段占整条血条的比例", 16, 198, 352, 18, 8f, Theme.Faint,
                FontStyle.Regular, ContentAlignment.MiddleLeft);
            var sep = new Panel();
            sep.Bounds = SRect(16, 222, 352, 1);
            sep.BackColor = Theme.BorderSoft;
            card.Controls.Add(sep);

            L(card, "正在播放", 16, 232, 80, 22, 8.5f, Theme.Faint, FontStyle.Regular,
                ContentAlignment.MiddleLeft);
            _trackLabel = L(card, "—", 16, 252, 352, 24, 9.5f, Theme.Text, FontStyle.Regular,
                ContentAlignment.MiddleLeft);

            _startBtn = new FlatButton();
            _startBtn.Text = "开始监看";
            _startBtn.Primary = true;
            _startBtn.AccentColor = Theme.Green;
            _startBtn.Font = Theme.F(11.5f, FontStyle.Bold);
            _startBtn.BackColor = Theme.Panel;
            _startBtn.Bounds = SRect(16, 300, 352, 52);
            _startBtn.Click += delegate { ToggleMonitoring(); };
            card.Controls.Add(_startBtn);
            _startHint = L(card, "F8 也可以", 16, 360, 352, 20, 8f, Theme.Faint,
                FontStyle.Regular, ContentAlignment.MiddleCenter);

            card = CardAt(_monitorPage, 0, 420, 1052, 132, "日志", "日志.txt");
            _log = new DarkList();
            _log.Bounds = SRect(14, 34, 1024, 88);
            card.Controls.Add(_log);
        }

        void BuildMusicPage()
        {
            var card = CardAt(_musicPage, 0, 0, 648, 552, "播放列表",
                "music 文件夹 · 递归查找");
            _tracks = new DarkList();
            _tracks.Bounds = SRect(14, 38, 620, 452);
            _tracks.Numbered = true;
            _tracks.ShowSeparators = true;
            _tracks.DoubleClick += delegate { TestSelected(); };
            card.Controls.Add(_tracks);
            AddConfig(_tracks);

            _addMusicBtn = Btn(card, "添加音频", 14, 500, 100, 34, false,
                delegate { AddMusic(); });
            _openMusicBtn = Btn(card, "打开文件夹", 122, 500, 110, 34, false,
                delegate { OpenFolder(AppPaths.MusicDir); });
            _refreshMusicBtn = Btn(card, "刷新", 240, 500, 74, 34, false,
                delegate { ReloadMusic(); });
            AddConfig(_addMusicBtn);
            AddConfig(_openMusicBtn);
            AddConfig(_refreshMusicBtn);
            _trackCount = L(card, "0 首", 330, 500, 304, 34, 8.5f, Theme.Faint,
                FontStyle.Regular, ContentAlignment.MiddleRight);

            card = CardAt(_musicPage, 668, 0, 384, 268, "播放", "");
            L(card, "当前曲目", 16, 36, 80, 20, 8f, Theme.Faint, FontStyle.Regular,
                ContentAlignment.MiddleLeft);
            _nowPlaying = L(card, "—", 16, 56, 352, 28, 10.5f, Theme.Text, FontStyle.Bold,
                ContentAlignment.MiddleLeft);

            L(card, "音量", 16, 90, 40, 24, 8.5f, Theme.Muted, FontStyle.Regular,
                ContentAlignment.MiddleLeft);
            _volume = new SlimSlider();
            _volume.Bounds = SRect(58, 90, 310, 24);
            _volume.BackColor = Theme.Panel;
            _volume.ShowValue = true;
            _volume.Value = _cfg.Volume;
            _volume.ValueChanged += delegate
            {
                if (_loadingUi) return;
                _cfg.Volume = _volume.Value;
                _audio.Volume = _cfg.Volume;
                QueueSave();
            };
            card.Controls.Add(_volume);

            _loopToggle = Tg(card, "一首结束继续下一首", 16, 124, 352, _cfg.Loop,
                delegate
                {
                    if (_loadingUi) return;
                    _cfg.Loop = _loopToggle.Checked;
                    QueueSave();
                });
            _preferToggle = Tg(card, "优先播放「雨爱」", 16, 154, 352, _cfg.PreferRainLove,
                delegate
                {
                    if (_loadingUi) return;
                    _cfg.PreferRainLove = _preferToggle.Checked;
                    ReloadMusic();
                    QueueSave();
                });
            AddConfig(_loopToggle);
            AddConfig(_preferToggle);

            L(card, "顺序", 16, 190, 70, 26, 8.5f, Theme.Muted, FontStyle.Regular,
                ContentAlignment.MiddleLeft);
            _orderCombo = Combo(card, 92, 188, 130, 30, new[] { "随机", "顺序" },
                _cfg.PlayOrder == "ordered" ? 1 : 0, delegate
                {
                    if (_loadingUi) return;
                    _cfg.PlayOrder = _orderCombo.SelectedIndex == 1 ? "ordered" : "shuffle";
                    ReloadMusic();
                    QueueSave();
                });
            AddConfig(_orderCombo);

            _testBtn = Btn(card, "试听选中的歌", 16, 226, 168, 34, false,
                delegate { TestSelected(); });
            _stopTestBtn = Btn(card, "停止试听", 200, 226, 168, 34, false,
                delegate { _audio.FadeStop(250); });
            AddConfig(_testBtn);
            AddConfig(_stopTestBtn);

            card = CardAt(_musicPage, 668, 282, 384, 270, "关于音乐", "");
            L(card,
                "歌曲由你自己提供，程序不附带、也不从网上下载；本文件夹里的《雨爱》只存在于你的本机。\n\n" +
                "把带「雨爱」的歌放进 music 文件夹并打开「优先播放 雨爱」，血条一黄就先放它。\n\n" +
                "没有雨爱时，按随机或顺序播放文件夹里的其他歌。支持 mp3 / wav / m4a / wma / aac / flac。\n\n" +
                "播放使用 Windows 自带媒体引擎，不需要额外解码器。",
                16, 38, 352, 216, 8.5f, Theme.Muted, FontStyle.Regular,
                ContentAlignment.TopLeft);
        }

        void BuildRulesPage()
        {
            var card = CardAt(_rulesPage, 0, 0, 512, 552, "触发判定", "");
            L(card, "黄色持续（秒）", 16, 44, 220, 26, 9f, Theme.Text, FontStyle.Regular,
                ContentAlignment.MiddleLeft);
            _holdNum = Num(card, 342, 42, 150, 0, 5, 0.05, 2,
                _cfg.YellowHoldSeconds, OnRuleChanged);
            L(card, "见黄后连续保持这么久才开始播放；0.35 秒能滤掉受击闪烁。", 16, 70, 476, 20,
                8f, Theme.Faint, FontStyle.Regular, ContentAlignment.MiddleLeft);

            L(card, "恢复判定（秒）", 16, 104, 220, 26, 9f, Theme.Text, FontStyle.Regular,
                ContentAlignment.MiddleLeft);
            _clearNum = Num(card, 342, 102, 150, 0.2, 10, 0.1, 2,
                _cfg.ClearSeconds, OnRuleChanged);
            L(card, "离开黄色超过这么久，判定为血条已恢复，音乐停止。", 16, 130, 476, 20,
                8f, Theme.Faint, FontStyle.Regular, ContentAlignment.MiddleLeft);

            L(card, "冷却时间（秒）", 16, 164, 220, 26, 9f, Theme.Text, FontStyle.Regular,
                ContentAlignment.MiddleLeft);
            _cooldownNum = Num(card, 342, 162, 150, 0, 120, 1, 0,
                _cfg.CooldownSeconds, OnRuleChanged);
            L(card, "停止播放后至少等这么久，才会再次因为见黄而播放。", 16, 190, 476, 20,
                8f, Theme.Faint, FontStyle.Regular, ContentAlignment.MiddleLeft);

            L(card, "黄色上限（%）", 16, 224, 220, 26, 9f, Theme.Text, FontStyle.Regular,
                ContentAlignment.MiddleLeft);
            _maxNum = Num(card, 342, 222, 150, 5, 100, 5, 0,
                _cfg.YellowMaxFraction * 100, OnRuleChanged);
            L(card, "黄色段超过血条的这个比例，视为自定义 HUD，不触发。", 16, 250, 476, 20,
                8f, Theme.Faint, FontStyle.Regular, ContentAlignment.MiddleLeft);

            _stopClearToggle = Tg(card, "血条恢复后自动停雨", 16, 292, 476, _cfg.StopOnClear,
                delegate
                {
                    if (_loadingUi) return;
                    _cfg.StopOnClear = _stopClearToggle.Checked;
                    QueueSave();
                });
            _fadeToggle = Tg(card, "触发时淡入", 16, 324, 476, _cfg.FadeIn,
                delegate
                {
                    if (_loadingUi) return;
                    _cfg.FadeIn = _fadeToggle.Checked;
                    QueueSave();
                });
            _shotsToggle = Tg(card, "保存触发瞬间的截图", 16, 356, 476, _cfg.SaveTriggerShots,
                delegate
                {
                    if (_loadingUi) return;
                    _cfg.SaveTriggerShots = _shotsToggle.Checked;
                    QueueSave();
                });
            AddConfig(_stopClearToggle);
            AddConfig(_fadeToggle);
            AddConfig(_shotsToggle);

            var sep = new Panel();
            sep.Bounds = SRect(16, 396, 476, 1);
            sep.BackColor = Theme.BorderSoft;
            card.Controls.Add(sep);
            L(card, "判定顺序", 16, 408, 476, 20, 8.5f, Theme.Text, FontStyle.Bold,
                ContentAlignment.MiddleLeft);
            L(card,
                "自动找血条 → 认「我」字在哪边 → 连续黄 0.35 秒 → 播放 → 血条恢复 1.2 秒后停 → 重新待命。",
                16, 430, 476, 64, 8.5f, Theme.Muted, FontStyle.Regular,
                ContentAlignment.TopLeft);
            L(card, "所有修改都会自动保存。", 16, 514, 476, 20, 8f, Theme.Faint,
                FontStyle.Regular, ContentAlignment.MiddleLeft);

            card = CardAt(_rulesPage, 532, 0, 520, 268, "识别方式", "");
            L(card, "方位判定", 16, 44, 76, 26, 9f, Theme.Text, FontStyle.Regular,
                ContentAlignment.MiddleLeft);
            _sideCombo = Combo(card, 96, 40, 260, 30,
                new[] { "自动认「我」字", "强制：我方在左", "强制：我方在右", "手动框选" },
                SideIndex(_cfg.SideMode), OnSideChanged);
            AddConfig(_sideCombo);
            L(card, "自动最稳", 366, 40, 138, 26, 8f, Theme.Faint, FontStyle.Regular,
                ContentAlignment.MiddleRight);

            _selectBarBtn = Btn(card, "框选我方血条", 16, 84, 150, 34, false,
                delegate { BeginBarSelection(); });
            _clearBarBtn = Btn(card, "清除框选", 176, 84, 110, 34, false,
                delegate { ClearManualBar(); });
            AddConfig(_selectBarBtn);
            AddConfig(_clearBarBtn);
            _manualNote = L(card, "自动模式不需要框选；只有自定义 HUD 才用。", 16, 126, 488, 42,
                8.5f, Theme.Faint, FontStyle.Regular, ContentAlignment.TopLeft);

            L(card, "检测帧率", 16, 180, 80, 26, 9f, Theme.Text, FontStyle.Regular,
                ContentAlignment.MiddleLeft);
            _fpsCombo = Combo(card, 100, 176, 90, 30, new[] { "15", "20", "30" },
                _cfg.TargetFps == 30 ? 2 : _cfg.TargetFps == 15 ? 0 : 1, OnFpsChanged);
            AddConfig(_fpsCombo);
            L(card, "20 帧足够；调高更耗 CPU", 202, 180, 302, 26, 8f, Theme.Faint,
                FontStyle.Regular, ContentAlignment.MiddleLeft);
            L(card, "游戏建议设为无边框窗口模式，并保持在前台。", 16, 216, 488, 20, 8f,
                Theme.Faint, FontStyle.Regular, ContentAlignment.MiddleLeft);

            card = CardAt(_rulesPage, 532, 282, 520, 270, "程序", "");
            _minimizeToggle = Tg(card, "触发时最小化本程序", 16, 40, 488, _cfg.MinimizeOnListen,
                delegate
                {
                    if (_loadingUi) return;
                    _cfg.MinimizeOnListen = _minimizeToggle.Checked;
                    QueueSave();
                });
            _overlayToggle = Tg(card, "在游戏画面上显示小提示条", 16, 72, 488, _cfg.ShowOverlay,
                delegate
                {
                    if (_loadingUi) return;
                    _cfg.ShowOverlay = _overlayToggle.Checked;
                    QueueSave();
                });
            AddConfig(_minimizeToggle);
            AddConfig(_overlayToggle);

            Btn(card, "自检", 16, 112, 96, 34, false, delegate { RunSelfTest(); });
            Btn(card, "打开日志", 120, 112, 108, 34, false,
                delegate { OpenFile(AppPaths.LogFile); });
            Btn(card, "数据文件夹", 236, 112, 128, 34, false,
                delegate { OpenFolder(AppPaths.BaseDir); });
            _resetBtn = Btn(card, "恢复默认", 372, 112, 110, 34, false,
                delegate { ResetDefaults(); });
            AddConfig(_resetBtn);

            L(card, "F8 开始 / 停止监看 · F9 立即停雨", 16, 158, 488, 22, 8.5f,
                Theme.Muted, FontStyle.Regular, ContentAlignment.MiddleLeft);
            _selfTestNote = L(card,
                "自检会用程序自己画的模拟血条，验证左侧/右侧、黄血、镜像、整条黄色与手动框选，不需要启动游戏。",
                16, 184, 488, 72, 8f, Theme.Faint, FontStyle.Regular,
                ContentAlignment.TopLeft);
        }

        // ==================================================================
        // Small builders
        // ==================================================================
        static float ComputeUiScale()
        {
            float s = WinApi.SystemDpi() / 96f;
            try
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                float fitW = (wa.Width - 48) / 1100f;
                float fitH = (wa.Height - 48) / 700f;
                if (fitW < s) s = fitW;
                if (fitH < s) s = fitH;
            }
            catch { }
            if (s < 0.80f) s = 0.80f;
            if (s > 4f) s = 4f;
            return s;
        }

        int S(int v)
        {
            return (int)Math.Round(v * _uiScale);
        }

        Rectangle SRect(int x, int y, int w, int h)
        {
            return new Rectangle(S(x), S(y), S(w), S(h));
        }

        Label L(Control parent, string text, int x, int y, int w, int h, float size,
            Color color, FontStyle style, ContentAlignment align)
        {
            var label = new Label();
            label.Text = text;
            label.Bounds = SRect(x, y, w, h);
            label.Font = Theme.F(size, style);
            label.ForeColor = color;
            label.BackColor = parent is Card ? Theme.Panel : Theme.Bg;
            label.AutoSize = false;
            label.TextAlign = align;
            label.UseMnemonic = false;
            parent.Controls.Add(label);
            return label;
        }

        Card CardAt(Control parent, int x, int y, int w, int h, string title, string hint)
        {
            var card = new Card();
            card.Bounds = SRect(x, y, w, h);
            card.Title = title;
            card.Hint = hint;
            parent.Controls.Add(card);
            return card;
        }

        FlatButton Btn(Control parent, string text, int x, int y, int w, int h,
            bool primary, EventHandler click)
        {
            var b = new FlatButton();
            b.Text = text;
            b.Bounds = SRect(x, y, w, h);
            b.Primary = primary;
            b.BackColor = parent is Card ? Theme.Panel : Theme.Bg;
            b.Click += click;
            parent.Controls.Add(b);
            return b;
        }

        Toggle Tg(Control parent, string text, int x, int y, int w, bool value,
            EventHandler changed)
        {
            var t = new Toggle();
            t.Text = text;
            t.Bounds = SRect(x, y, w, 30);
            t.BackColor = parent is Card ? Theme.Panel : Theme.Bg;
            t.Checked = value;
            t.CheckedChanged += changed;
            parent.Controls.Add(t);
            return t;
        }

        NumberBox Num(Control parent, int x, int y, int w, double min, double max,
            double increment, int decimals, double value, EventHandler changed)
        {
            var n = new NumberBox();
            n.Bounds = SRect(x, y, w, 26);
            n.Minimum = min;
            n.Maximum = max;
            n.Increment = increment;
            n.DecimalPlaces = decimals;
            n.Value = value;
            n.ValueChanged += changed;
            parent.Controls.Add(n);
            return n;
        }

        SelectBox Combo(Control parent, int x, int y, int w, int h, string[] items,
            int index, EventHandler changed)
        {
            var c = new SelectBox();
            c.Bounds = SRect(x, y, w, h);
            c.Items.AddRange(items);
            if (items.Length > 0)
                c.SelectedIndex = Math.Max(0, Math.Min(items.Length - 1, index));
            c.SelectedIndexChanged += changed;
            parent.Controls.Add(c);
            return c;
        }

        void AddConfig(Control c)
        {
            _configControls.Add(c);
        }

        void HeaderDrag(Control c)
        {
            c.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 2, 0);
            };
        }

        // ==================================================================
        // Navigation / window chrome
        // ==================================================================
        void ShowPage(int page)
        {
            _navMonitor.Active = page == 0;
            _navMusic.Active = page == 1;
            _navRules.Active = page == 2;
            _navMonitor.Invalidate();
            _navMusic.Invalidate();
            _navRules.Invalidate();
            _monitorPage.Visible = page == 0;
            _musicPage.Visible = page == 1;
            _rulesPage.Visible = page == 2;
            if (page == 0) _preview.Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!WinApi.RegisterHotKey(Handle, WinApi.HotkeyToggle, 0, WinApi.VK_F8))
                AppLog.Error("F8 热键注册失败（可能被其他程序占用）");
            if (!WinApi.RegisterHotKey(Handle, WinApi.HotkeyStop, 0, WinApi.VK_F9))
                AppLog.Error("F9 热键注册失败（可能被其他程序占用）");
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _windowTimer.Start();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WinApi.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == WinApi.HotkeyToggle) ToggleMonitoring();
                else if (id == WinApi.HotkeyStop) EmergencyStop();
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_monitoring) StopMonitoring();
            try { WinApi.UnregisterHotKey(Handle, WinApi.HotkeyToggle); } catch { }
            try { WinApi.UnregisterHotKey(Handle, WinApi.HotkeyStop); } catch { }
            _windowTimer.Stop();
            _saveTimer.Stop();
            SaveNow(false);
            AppLog.Line -= OnLogLine;
            _audio.StopNow();
            _audio.Dispose();
            _overlay.HideOverlay();
            _overlay.Close();
            _overlay.Dispose();
            base.OnFormClosing(e);
        }

        // ==================================================================
        // Settings
        // ==================================================================
        void QueueSave()
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        void SaveNow(bool announce)
        {
            _cfg.Normalize();
            _cfg.Save(AppPaths.ConfigFile);
            if (announce)
                _saveState.Text = "已保存 " + DateTime.Now.ToString("HH:mm:ss");
        }

        void ApplySettingsToUi()
        {
            _loadingUi = true;
            try
            {
                _volume.Value = _cfg.Volume;
                _loopToggle.Checked = _cfg.Loop;
                _preferToggle.Checked = _cfg.PreferRainLove;
                _orderCombo.SelectedIndex = _cfg.PlayOrder == "ordered" ? 1 : 0;
                _sideCombo.SelectedIndex = SideIndex(_cfg.SideMode);
                _fpsCombo.SelectedIndex = _cfg.TargetFps == 30 ? 2 : _cfg.TargetFps == 15 ? 0 : 1;
                _holdNum.Value = _cfg.YellowHoldSeconds;
                _clearNum.Value = _cfg.ClearSeconds;
                _cooldownNum.Value = _cfg.CooldownSeconds;
                _maxNum.Value = Math.Round(_cfg.YellowMaxFraction * 100);
                _stopClearToggle.Checked = _cfg.StopOnClear;
                _fadeToggle.Checked = _cfg.FadeIn;
                _shotsToggle.Checked = _cfg.SaveTriggerShots;
                _minimizeToggle.Checked = _cfg.MinimizeOnListen;
                _overlayToggle.Checked = _cfg.ShowOverlay;
                _preview.SetManualSelection(RectFromCfg(), _cfg.ManualBar != null);
                UpdateManualNote();
            }
            finally
            {
                _loadingUi = false;
            }
            _audio.Volume = _cfg.Volume;
        }

        static int SideIndex(string mode)
        {
            if (mode == "left") return 1;
            if (mode == "right") return 2;
            if (mode == "manual") return 3;
            return 0;
        }

        void UpdateManualNote()
        {
            if (_manualNote == null) return;
            if (_cfg.ManualBar == null)
                _manualNote.Text = "自动模式不需要框选；只有自定义 HUD 才用。";
            else
                _manualNote.Text = string.Format(
                    "已保存框选：横向 {0:0.0}% · 纵向 {1:0.0}%。方位选「手动框选」时生效。",
                    _cfg.ManualBar[2] * 100, _cfg.ManualBar[3] * 100);
        }

        RectangleF RectFromCfg()
        {
            if (_cfg.ManualBar == null || _cfg.ManualBar.Length != 4) return RectangleF.Empty;
            return new RectangleF((float)_cfg.ManualBar[0], (float)_cfg.ManualBar[1],
                (float)_cfg.ManualBar[2], (float)_cfg.ManualBar[3]);
        }

        void OnRuleChanged(object sender, EventArgs e)
        {
            if (_loadingUi) return;
            _cfg.YellowHoldSeconds = _holdNum.Value;
            _cfg.ClearSeconds = _clearNum.Value;
            _cfg.CooldownSeconds = _cooldownNum.Value;
            _cfg.YellowMaxFraction = _maxNum.Value / 100.0;
            QueueSave();
        }

        void OnSideChanged(object sender, EventArgs e)
        {
            if (_loadingUi) return;
            string mode = _sideCombo.SelectedIndex == 1 ? "left"
                : _sideCombo.SelectedIndex == 2 ? "right"
                : _sideCombo.SelectedIndex == 3 ? "manual" : "auto";
            if (mode == "manual" && _cfg.ManualBar == null)
            {
                _manualNote.Text = "还没有框选。点「框选我方血条」，在画面上拖一个矩形。";
                _manualNote.ForeColor = Theme.Yellow;
            }
            else
            {
                _manualNote.ForeColor = Theme.Faint;
                UpdateManualNote();
            }
            _cfg.SideMode = mode;
            QueueSave();
            AppLog.Info("方位判定：" + _sideCombo.SelectedItem);
        }

        void OnFpsChanged(object sender, EventArgs e)
        {
            if (_loadingUi) return;
            _cfg.TargetFps = _fpsCombo.SelectedIndex == 2 ? 30 : _fpsCombo.SelectedIndex == 0 ? 15 : 20;
            QueueSave();
        }

        // ==================================================================
        // Window list
        // ==================================================================
        void RefreshWindows(bool manual)
        {
            if (_monitoring) return;
            List<WindowInfo> list = WinApi.ListWindows(640, 360);
            string wanted = _cfg.WindowTitle;
            int index = -1;
            _loadingUi = true;
            try
            {
                _windowCombo.BeginUpdate();
                _windowCombo.Items.Clear();
                foreach (WindowInfo w in list) _windowCombo.Items.Add(w);
                if (wanted.Length > 0)
                {
                    for (int i = 0; i < list.Count; i++)
                        if (string.Equals(list[i].Title, wanted, StringComparison.OrdinalIgnoreCase))
                        { index = i; break; }
                    if (index < 0)
                        for (int i = 0; i < list.Count; i++)
                            if (list[i].Title.IndexOf(wanted, StringComparison.CurrentCultureIgnoreCase) >= 0)
                            { index = i; break; }
                }
                if (index < 0)
                {
                    for (int i = 0; i < list.Count; i++)
                        if (WinApi.IsGameLike(list[i])) { index = i; break; }
                }
                _windowCombo.SelectedIndex = index;
                _windowCombo.EndUpdate();
            }
            finally
            {
                _loadingUi = false;
            }

            if (index >= 0)
            {
                var w = list[index];
                if (_cfg.WindowTitle != w.Title)
                {
                    _cfg.WindowTitle = w.Title;
                    QueueSave();
                }
                _statusLeft.Text = "已选窗口：" + w.Title + "  (" + w.Width + "×" + w.Height + ")";
                if (manual) AppLog.Info("刷新窗口列表：" + list.Count + " 个，已选 " + w.Title);
            }
            else
            {
                _statusLeft.Text = "没有找到游戏窗口 · 先启动游戏（建议无边框窗口）";
                if (manual || _lastWindowLog != DateTime.Now.ToString("HHmm"))
                {
                    _lastWindowLog = DateTime.Now.ToString("HHmm");
                    AppLog.Error("没有找到候选游戏窗口。请先启动游戏，并确认不是全屏独占。");
                }
            }
        }

        void OnWindowChanged(object sender, EventArgs e)
        {
            if (_loadingUi) return;
            var w = _windowCombo.SelectedItem as WindowInfo;
            if (w == null) return;
            _cfg.WindowTitle = w.Title;
            QueueSave();
            _statusLeft.Text = "已选窗口：" + w.Title + "  (" + w.Width + "×" + w.Height + ")";
        }

        WindowInfo CurrentWindow()
        {
            var w = _windowCombo.SelectedItem as WindowInfo;
            if (w != null && WinApi.IsWindow(w.Handle)) return w;
            w = WinApi.FindByTitle(_cfg.WindowTitle, 640, 360);
            return w;
        }

        void ActivateWindow()
        {
            WindowInfo w = CurrentWindow();
            if (w == null)
            {
                MessageBox.Show(this, "还没有找到游戏窗口。\n请先启动《街头霸王6》，再点「刷新」。",
                    "街霸六的雨", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            WinApi.Activate(w.Handle);
        }

        // ==================================================================
        // Monitoring
        // ==================================================================
        void ToggleMonitoring()
        {
            if (_monitoring) StopMonitoring();
            else StartMonitoring();
        }

        void StartMonitoring()
        {
            if (_monitoring) return;
            WindowInfo w = CurrentWindow();
            if (w == null)
            {
                RefreshWindows(false);
                w = CurrentWindow();
            }
            if (w == null)
            {
                MessageBox.Show(this,
                    "没有找到游戏窗口。\n\n请先启动《街头霸王6》，把画面设为无边框窗口或无边框全屏，再点「刷新」。",
                    "街霸六的雨", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _cfg.Normalize();
            if (_cfg.SideMode == "manual" && _cfg.ManualBar == null)
            {
                MessageBox.Show(this, "方位选了「手动框选」，但还没框过血条。\n先点「框选我方血条」。",
                    "街霸六的雨", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ShowPage(2);
                return;
            }

            _lib.Reload(_cfg.MusicFolder, _cfg.PreferRainLove, _cfg.PlayOrder);
            if (_lib.Count == 0)
            {
                DialogResult r = MessageBox.Show(this,
                    "music 文件夹里没有可播放的音频。\n血条见黄时不会有声音。仍然开始吗？",
                    "街霸六的雨", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return;
            }

            _audio.StopNow();
            _overlay.HideOverlay();
            SaveNow(false);

            _gameHandle = w.Handle;
            _preview.ClearFrame();
            _preview.SelectionMode = false;
            _preview.SetManualSelection(RectFromCfg(), _cfg.ManualBar != null);

            _engine = new MonitorEngine(_cfg.Clone(), _lib);
            _engine.Frame += OnFrame;
            _engine.StatusChanged += OnStatus;
            _engine.Triggered += OnTriggered;
            _engine.StopAudioRequested += OnStopAudioRequested;

            _monitoring = true;
            try
            {
                _engine.Start(w.Handle, w.Title);
            }
            catch (Exception ex)
            {
                _monitoring = false;
                _engine = null;
                MessageBox.Show(this, "启动失败：" + ex.Message, "街霸六的雨",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SetConfigEnabled(false);
            UpdateStartUi(true);
            _stateLabel.Text = "正在连接";
            _stateLabel.ForeColor = Theme.Blue;
            _detailLabel.Text = "正在读取游戏画面，约一秒内出结果。";
            _sideLabel.Text = "—";
            _pill.Set("连接中", Theme.Blue);
            _statusLeft.Text = "监看中 · " + w.Title;
            AppLog.Info("开始监看：" + w.Title + " " + w.Width + "×" + w.Height);
        }

        void StopMonitoring()
        {
            StopMonitoring(null, null);
        }

        void StopMonitoring(string title, string detail)
        {
            if (!_monitoring && _engine == null) return;
            _monitoring = false;
            MonitorEngine engine = _engine;
            _engine = null;
            if (engine != null)
            {
                Dictionary<string, double[]> learned = engine.LayoutsCopy();
                engine.Stop();
                foreach (var kv in learned)
                    _cfg.Layouts[kv.Key] = kv.Value;
            }
            _audio.StopNow();
            _overlay.HideOverlay();
            SaveNow(true);
            SetConfigEnabled(true);
            UpdateStartUi(false);
            _stateLabel.Text = title != null ? title : "已停止";
            _stateLabel.ForeColor = title != null ? Theme.Red : Theme.Muted;
            _detailLabel.Text = detail != null ? detail : "选好游戏窗口，点「开始监看」。";
            _sideLabel.Text = "—";
            _meter.Value = 0;
            _meterValue.Text = "0%";
            _pill.Set(title != null ? "已断开" : "待机", title != null ? Theme.Red : Theme.Muted);
            _statusLeft.Text = title != null
                ? "已停止 · " + title
                : "待机 · 选好游戏窗口，再点「开始监看」";
            _preview.SelectionMode = false;
        }

        void EmergencyStop()
        {
            if (_engine != null) _engine.EmergencyStop();
            _audio.FadeStop(220);
            _overlay.HideOverlay();
            _statusLeft.Text = "已急停 · 血条恢复后自动重新待命";
            AppLog.Info("F9 急停");
        }

        void UpdateStartUi(bool running)
        {
            _startBtn.Text = running ? "停止监看" : "开始监看";
            _startBtn.Primary = true;
            _startBtn.Danger = running;
            _startBtn.AccentColor = Theme.Green;
            _startHint.Text = running ? "F8 停止 · F9 急停" : "F8 也可以";
            _startBtn.Invalidate();
        }

        void SetConfigEnabled(bool enabled)
        {
            foreach (Control c in _configControls) c.Enabled = enabled;
            _testBtn.Enabled = enabled;
            _stopTestBtn.Enabled = enabled;
            _windowCombo.Enabled = enabled;
        }

        // ==================================================================
        // Engine callbacks
        // ==================================================================
        void OnFrame(Detection det, Bitmap preview)
        {
            if (!_monitoring)
            {
                preview.Dispose();
                return;
            }
            _preview.SetFrame(preview, det);
        }

        void OnStatus(EngineStatus st)
        {
            if (!_monitoring) return;
            _stateLabel.Text = st.Title;
            _stateLabel.ForeColor = StateColor(st.State);
            _detailLabel.Text = st.Detail;
            _sideLabel.Text = SideText(st.Side);
            _sideLabel.ForeColor = st.Side.Length > 0 ? Theme.Yellow : Theme.Muted;
            _triggersLabel.Text = "触发 " + st.Triggers + " 次";
            if (st.Track.Length > 0) _trackLabel.Text = st.Track;
            _meter.Value = st.Fraction;
            _meterValue.Text = (int)Math.Round(st.Fraction * 100) + "%";
            if (st.Side.Length == 0) _meterValue.ForeColor = Theme.Muted;
            else _meterValue.ForeColor = st.Yellow ? Theme.Yellow : Theme.Blue;

            _pill.Set(st.Title, StateColor(st.State));
            if (st.State == "error")
                _statusLeft.Text = "需要处理 · " + st.Title;
            else if (st.State == "playing")
                _statusLeft.Text = "监看中 · 雨正在下" + (st.Track.Length > 0 ? " · " + st.Track : "");
            else
                _statusLeft.Text = "监看中 · " + st.Window;

            if (st.State == "stopped")
            {
                if (st.Title.Length > 0 && st.Title != "已停止监看")
                    StopMonitoring(st.Title, st.Detail);
                else
                    StopMonitoring();
            }
        }

        static Color StateColor(string state)
        {
            if (state == "playing" || state == "hold") return Theme.Yellow;
            if (state == "watching") return Theme.Green;
            if (state == "error") return Theme.Red;
            if (state == "ended") return Theme.Muted;
            if (state == "waiting") return Theme.Muted;
            return Theme.Muted;
        }

        static string SideText(string side)
        {
            if (side == "left") return "左侧";
            if (side == "right") return "右侧";
            if (side == "manual") return "手动";
            return "—";
        }

        void OnTriggered(Track track)
        {
            if (!_monitoring) return;
            _audio.Play(track.Path, _cfg.FadeIn);
            _nowPlaying.Text = track.Name;
            _trackLabel.Text = track.Name;
            AppLog.Info("播放：" + track.Name);
            if (_cfg.MinimizeOnListen && WindowState == FormWindowState.Normal)
                WindowState = FormWindowState.Minimized;
            if (_cfg.ShowOverlay)
                _overlay.ShowFor(_gameHandle, track.Name);
        }

        void OnStopAudioRequested()
        {
            _audio.FadeStop(350);
            _overlay.HideOverlay();
        }

        void OnAudioEnded()
        {
            if (_monitoring && _engine != null)
            {
                _engine.NotifyAudioEnded();
                if (!_audio.IsPlaying) _overlay.HideOverlay();
            }
            else
            {
                _nowPlaying.Text = "—";
            }
        }

        void OnAudioFailed(string message)
        {
            _overlay.HideOverlay();
            if (_monitoring && _engine != null)
            {
                _engine.NotifyAudioFailed(message);
            }
            else
            {
                MessageBox.Show(this, "无法播放这个文件：\n" + message + "\n\n请换一个 mp3 / wav / m4a 试试。",
                    "街霸六的雨", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ==================================================================
        // Music
        // ==================================================================
        void ReloadMusic()
        {
            _lib.Reload(_cfg.MusicFolder, _cfg.PreferRainLove, _cfg.PlayOrder);
            _tracks.BeginUpdate();
            try
            {
                _tracks.Items.Clear();
                foreach (Track t in _lib.Tracks) _tracks.Items.Add(t);
            }
            finally
            {
                _tracks.EndUpdate();
            }
            _trackCount.Text = _lib.Count + " 首";
            if (_lib.LastError.Length > 0) AppLog.Error(_lib.LastError);
            else if (_lib.Count == 0) AppLog.Error("music 文件夹里没有音频：" + _lib.Folder);
            else AppLog.Info("音乐库：" + _lib.Count + " 首 · " + _lib.Folder);
        }

        void TestSelected()
        {
            if (_monitoring)
            {
                MessageBox.Show(this, "监看中不能试听。先点「停止监看」，或者按 F8。",
                    "街霸六的雨", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Track t = _tracks.SelectedItem as Track;
            if (t == null) t = _lib.First();
            if (t == null)
            {
                MessageBox.Show(this, "music 文件夹里没有音频。\n把 雨爱.mp3 放进去再点「刷新」。",
                    "街霸六的雨", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _audio.Play(t.Path, false);
            _nowPlaying.Text = t.Name;
            AppLog.Info("试听：" + t.Name);
        }

        void AddMusic()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "选择要加入 music 文件夹的音频";
                dlg.Multiselect = true;
                dlg.Filter = "音频|*.mp3;*.wav;*.m4a;*.wma;*.aac;*.flac|所有文件|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                Directory.CreateDirectory(AppPaths.MusicDir);
                int copied = 0;
                foreach (string source in dlg.FileNames)
                {
                    try
                    {
                        string full = Path.GetFullPath(source);
                        if (full.StartsWith(AppPaths.MusicDir, StringComparison.OrdinalIgnoreCase))
                            continue;
                        string dest = UniqueDest(Path.GetFileNameWithoutExtension(source),
                            Path.GetExtension(source));
                        File.Copy(source, dest, false);
                        copied++;
                    }
                    catch (Exception ex)
                    {
                        AppLog.Error("添加音频失败：" + source + " · " + ex.Message);
                    }
                }
                ReloadMusic();
                if (copied == 0)
                    MessageBox.Show(this, "没有复制任何文件。它们可能已经在 music 文件夹里了。",
                        "街霸六的雨", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        static string UniqueDest(string name, string ext)
        {
            string dest = Path.Combine(AppPaths.MusicDir, name + ext);
            int n = 1;
            while (File.Exists(dest))
            {
                dest = Path.Combine(AppPaths.MusicDir, name + " (" + n + ")" + ext);
                n++;
            }
            return dest;
        }

        void OpenFolder(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                var psi = new ProcessStartInfo();
                psi.FileName = "explorer.exe";
                psi.Arguments = "\"" + folder + "\"";
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "打不开文件夹：" + ex.Message, "街霸六的雨",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void OpenFile(string file)
        {
            if (!File.Exists(file))
            {
                MessageBox.Show(this, "还没有日志文件。开始监看后就会生成。", "街霸六的雨",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try { Process.Start(file); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "打不开文件：" + ex.Message, "街霸六的雨",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ==================================================================
        // Manual bar selection
        // ==================================================================
        void BeginBarSelection()
        {
            if (_monitoring) StopMonitoring();
            WindowInfo w = CurrentWindow();
            if (w == null)
            {
                MessageBox.Show(this, "先选好游戏窗口，才能截一张图来框选。", "街霸六的雨",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (CaptureResult cap = ScreenCapture.Capture(w.Handle))
            {
                if (cap.Image == null)
                {
                    MessageBox.Show(this, cap.Error, "街霸六的雨",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Bitmap still = (Bitmap)cap.Image.Clone();
                var det = Detector.Analyze(still, _cfg, new DetectorState(), Environment.TickCount);
                _preview.SetFrame(still, det);
            }
            ShowPage(0);
            _preview.SelectionMode = true;
            _preview.SetManualSelection(RectFromCfg(), _cfg.ManualBar != null);
            _stateLabel.Text = "框选我方血条";
            _stateLabel.ForeColor = Theme.Yellow;
            _detailLabel.Text = "在左边画面上按住左键拖一个框，框住我方血条本身（含黄色段），松开就保存。";
            _pill.Set("框选中", Theme.Yellow);
            _statusLeft.Text = "框选模式 · 拖动鼠标框住我方血条";
            AppLog.Info("进入手动框选");
        }

        void OnSelectionMade(RectangleF n)
        {
            _cfg.ManualBar = new double[] { n.X, n.Y, n.Width, n.Height };
            _cfg.SideMode = "manual";
            _cfg.Normalize();
            SaveNow(true);
            _loadingUi = true;
            _sideCombo.SelectedIndex = 3;
            _loadingUi = false;
            UpdateManualNote();
            _manualNote.ForeColor = Theme.Faint;

            double fraction = 0;
            Bitmap still = _preview.FrameClone();
            if (still != null)
            {
                var det = Detector.Analyze(still, _cfg, new DetectorState(), Environment.TickCount);
                _preview.SetFrame(still, det);
                fraction = det.ActiveFraction;
                if (det.Yellow)
                {
                    _stateLabel.Text = "已框选 · 见黄";
                    _stateLabel.ForeColor = Theme.Yellow;
                    _detailLabel.Text = string.Format(
                        "当前这块区域的黄色占比约 {0:0.0}%。开始监看后，血条见黄就会播放。",
                        fraction * 100);
                    _pill.Set("已框选", Theme.Green);
                }
                else
                {
                    _stateLabel.Text = "已框选";
                    _stateLabel.ForeColor = Theme.Green;
                    _detailLabel.Text = "框选已保存。当前这块区域不是黄色——正常；真正黄血时才会触发。";
                    _pill.Set("已框选", Theme.Green);
                }
            }
            _statusLeft.Text = "已保存手动框选 · 可以开始监看了";
            AppLog.Info(string.Format("手动框选 x={0:0.000} y={1:0.000} w={2:0.000} h={3:0.000}",
                n.X, n.Y, n.Width, n.Height));
        }

        void ClearManualBar()
        {
            if (_cfg.ManualBar == null) return;
            _cfg.ManualBar = null;
            if (_cfg.SideMode == "manual") _cfg.SideMode = "auto";
            SaveNow(true);
            _loadingUi = true;
            _sideCombo.SelectedIndex = SideIndex(_cfg.SideMode);
            _loadingUi = false;
            _preview.SetManualSelection(RectangleF.Empty, false);
            UpdateManualNote();
            _manualNote.ForeColor = Theme.Faint;
            _stateLabel.Text = "已清除框选";
            _stateLabel.ForeColor = Theme.Muted;
            _detailLabel.Text = "回到自动识别「我」字。";
            AppLog.Info("清除手动框选");
        }

        // ==================================================================
        // Self test / reset
        // ==================================================================
        void RunSelfTest()
        {
            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;
            try
            {
                SelfTestResult r = SelfTest.Run();
                foreach (string line in r.Lines) AppendLog(line);
                if (r.Pass)
                {
                    _selfTestNote.Text = "上次自检：全部通过（" + DateTime.Now.ToString("HH:mm") + "）";
                    _selfTestNote.ForeColor = Theme.Green;
                    MessageBox.Show(this,
                        "自检全部通过。\n\n覆盖：左侧/右侧、黄血 17%、镜像、720p、整条黄色防护、无「我」字防护、手动框选。\n\n详细结果已写入 自检.txt。",
                        "街霸六的雨 · 自检", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    _selfTestNote.Text = "上次自检：有失败项，见 captures 与 自检.txt";
                    _selfTestNote.ForeColor = Theme.Red;
                    MessageBox.Show(this,
                        "自检有项目没通过。\n\n失败时的模拟画面已保存到 captures 文件夹，数据在 自检.txt。",
                        "街霸六的雨 · 自检", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                Cursor = previous;
            }
        }

        void ResetDefaults()
        {
            if (_monitoring) return;
            DialogResult r = MessageBox.Show(this,
                "把触发、识别、界面和音乐设置恢复默认？\n（不会删除 music 里的文件）",
                "街霸六的雨", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;
            string title = _cfg.WindowTitle;
            _cfg = new Settings();
            _cfg.WindowTitle = title;
            ApplySettingsToUi();
            ReloadMusic();
            RefreshWindows(false);
            SaveNow(true);
            AppLog.Info("设置已恢复默认");
        }

        // ==================================================================
        // Log
        // ==================================================================
        void OnLogLine(string line)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<string>(OnLogLine), line); } catch { }
                return;
            }
            AppendLog(line);
        }

        void AppendLog(string line)
        {
            if (_log == null) return;
            _log.Items.Add(line);
            while (_log.Items.Count > 200) _log.Items.RemoveAt(0);
            _log.TopIndex = Math.Max(0, _log.Items.Count - 4);
            _log.EndUpdate();
        }
    }
}
