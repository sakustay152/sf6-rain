using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Sf6Rain
{
    public sealed class Settings
    {
        public const int Schema = 1;

        public string WindowTitle = "Street Fighter 6";
        // auto / left / right / manual
        public string SideMode = "auto";
        // normalized [x, y, width, height], only used by manual mode
        public double[] ManualBar;
        public double YellowHoldSeconds = 0.35;
        public double ClearSeconds = 1.20;
        public bool StopOnClear = true;
        public double YellowMaxFraction = 0.60;
        public double CooldownSeconds = 0.0;
        public int TargetFps = 20;
        public string MusicFolder = "music";
        public bool Loop = true;
        public bool PreferRainLove = true;
        public string PlayOrder = "shuffle";     // shuffle / ordered
        public double Volume = 0.80;
        public bool MinimizeOnListen = true;
        public bool ShowOverlay = false;
        public bool SaveTriggerShots = false;
        public bool FadeIn = true;
        // aspect ("1.60") -> [fullW/H, gap/H, y0/H, y1/H]
        public Dictionary<string, double[]> Layouts = new Dictionary<string, double[]>();

        // The default HUD measured from 2560x1600: bar 900x42, inner gap 143,
        // top 166. All divided by frame height so the layout follows resolution.
        public static readonly double[] DefaultLayout = { 0.5625, 0.089375, 0.10375, 0.13000 };

        public Settings Clone()
        {
            var s = (Settings)MemberwiseClone();
            s.ManualBar = ManualBar == null ? null : (double[])ManualBar.Clone();
            s.Layouts = new Dictionary<string, double[]>();
            foreach (var kv in Layouts)
                s.Layouts[kv.Key] = (double[])kv.Value.Clone();
            return s;
        }

        public void Normalize()
        {
            if (SideMode != "left" && SideMode != "right" && SideMode != "manual")
                SideMode = "auto";
            if (SideMode == "manual" && ManualBar == null) SideMode = "auto";
            if (PlayOrder != "ordered") PlayOrder = "shuffle";
            YellowHoldSeconds = Clamp(YellowHoldSeconds, 0.0, 5.0);
            ClearSeconds = Clamp(ClearSeconds, 0.2, 10.0);
            YellowMaxFraction = Clamp(YellowMaxFraction, 0.05, 1.0);
            CooldownSeconds = Clamp(CooldownSeconds, 0.0, 120.0);
            Volume = Clamp(Volume, 0.0, 1.0);
            if (TargetFps != 15 && TargetFps != 20 && TargetFps != 30) TargetFps = 20;
            if (ManualBar != null)
            {
                if (ManualBar.Length != 4) ManualBar = null;
                else
                {
                    for (int i = 0; i < 4; i++) ManualBar[i] = Clamp(ManualBar[i], 0.0, 1.0);
                    if (ManualBar[2] < 0.02 || ManualBar[3] < 0.005) ManualBar = null;
                }
            }
            if (SideMode == "manual" && ManualBar == null) SideMode = "auto";
            if (string.IsNullOrEmpty(WindowTitle)) WindowTitle = "Street Fighter 6";
            if (string.IsNullOrEmpty(MusicFolder)) MusicFolder = "music";
            foreach (var key in new List<string>(Layouts.Keys))
            {
                double[] v = Layouts[key];
                if (v == null || v.Length != 4) { Layouts.Remove(key); continue; }
                for (int i = 0; i < 4; i++) if (double.IsNaN(v[i]) || double.IsInfinity(v[i])) { Layouts.Remove(key); break; }
            }
        }

        static double Clamp(double v, double lo, double hi)
        {
            if (double.IsNaN(v)) return lo;
            return v < lo ? lo : v > hi ? hi : v;
        }

        public string AspectKey(int width, int height)
        {
            if (height <= 0) return "1.60";
            return Math.Round(width / (double)height, 2).ToString("0.00", CultureInfo.InvariantCulture);
        }

        public double[] LayoutFor(int width, int height)
        {
            string key = AspectKey(width, height);
            double[] v;
            if (Layouts.TryGetValue(key, out v) && v != null && v.Length == 4) return v;
            return DefaultLayout;
        }

        public void RememberLayout(int width, int height, double fullW, double gap, double y0, double y1)
        {
            if (height <= 0) return;
            double[] v = { fullW / height, gap / height, y0 / height, y1 / height };
            Layouts[AspectKey(width, height)] = v;
        }

        public Dictionary<string, object> ToDict()
        {
            var o = new Dictionary<string, object>();
            o["schema"] = Schema;
            o["window_title"] = WindowTitle;
            o["side_mode"] = SideMode;
            o["manual_bar"] = ManualBar;
            o["yellow_hold_seconds"] = YellowHoldSeconds;
            o["clear_seconds"] = ClearSeconds;
            o["stop_on_clear"] = StopOnClear;
            o["yellow_max_fraction"] = YellowMaxFraction;
            o["cooldown_seconds"] = CooldownSeconds;
            o["target_fps"] = TargetFps;
            o["music_folder"] = MusicFolder;
            o["loop"] = Loop;
            o["prefer_rain_love"] = PreferRainLove;
            o["play_order"] = PlayOrder;
            o["volume"] = Volume;
            o["minimize_on_listen"] = MinimizeOnListen;
            o["show_overlay"] = ShowOverlay;
            o["save_trigger_shots"] = SaveTriggerShots;
            o["fade_in"] = FadeIn;
            var layouts = new Dictionary<string, object>();
            foreach (var kv in Layouts) layouts[kv.Key] = kv.Value;
            o["layouts"] = layouts;
            return o;
        }

        public static Settings FromDict(Dictionary<string, object> o)
        {
            var s = new Settings();
            s.WindowTitle = Json.Str(o, "window_title", s.WindowTitle);
            s.SideMode = Json.Str(o, "side_mode", s.SideMode);
            var mb = Json.AsArray(o.ContainsKey("manual_bar") ? o["manual_bar"] : null);
            if (mb.Count == 4)
            {
                s.ManualBar = new double[4];
                for (int i = 0; i < 4; i++)
                    s.ManualBar[i] = Convert.ToDouble(mb[i], CultureInfo.InvariantCulture);
            }
            s.YellowHoldSeconds = Json.Num(o, "yellow_hold_seconds", s.YellowHoldSeconds);
            s.ClearSeconds = Json.Num(o, "clear_seconds", s.ClearSeconds);
            s.StopOnClear = Json.Bool(o, "stop_on_clear", s.StopOnClear);
            s.YellowMaxFraction = Json.Num(o, "yellow_max_fraction", s.YellowMaxFraction);
            s.CooldownSeconds = Json.Num(o, "cooldown_seconds", s.CooldownSeconds);
            s.TargetFps = Json.Int(o, "target_fps", s.TargetFps);
            s.MusicFolder = Json.Str(o, "music_folder", s.MusicFolder);
            s.Loop = Json.Bool(o, "loop", s.Loop);
            s.PreferRainLove = Json.Bool(o, "prefer_rain_love", s.PreferRainLove);
            s.PlayOrder = Json.Str(o, "play_order", s.PlayOrder);
            s.Volume = Json.Num(o, "volume", s.Volume);
            s.MinimizeOnListen = Json.Bool(o, "minimize_on_listen", s.MinimizeOnListen);
            s.ShowOverlay = Json.Bool(o, "show_overlay", s.ShowOverlay);
            s.SaveTriggerShots = Json.Bool(o, "save_trigger_shots", s.SaveTriggerShots);
            s.FadeIn = Json.Bool(o, "fade_in", s.FadeIn);
            var layouts = Json.AsObject(o.ContainsKey("layouts") ? o["layouts"] : null);
            foreach (var kv in layouts)
            {
                var arr = Json.AsArray(kv.Value);
                if (arr.Count != 4) continue;
                var v = new double[4];
                bool ok = true;
                for (int i = 0; i < 4; i++)
                {
                    try { v[i] = Convert.ToDouble(arr[i], CultureInfo.InvariantCulture); }
                    catch { ok = false; break; }
                }
                if (ok) s.Layouts[kv.Key] = v;
            }
            s.Normalize();
            return s;
        }

        public static Settings Load(string path)
        {
            if (!File.Exists(path)) return new Settings();
            try
            {
                return FromDict(Json.AsObject(Json.Parse(File.ReadAllText(path, Encoding.UTF8))));
            }
            catch (Exception ex)
            {
                AppLog.Error("config.json 读取失败，尝试备份：" + ex.Message);
                string bak = path + ".bak";
                if (File.Exists(bak))
                {
                    try
                    {
                        return FromDict(Json.AsObject(Json.Parse(File.ReadAllText(bak, Encoding.UTF8))));
                    }
                    catch { }
                }
                return new Settings();
            }
        }

        public void Save(string path)
        {
            Normalize();
            string text = Json.Write(ToDict());
            string tmp = path + ".tmp";
            try
            {
                File.WriteAllText(tmp, text, new UTF8Encoding(false));
                if (File.Exists(path)) File.Copy(path, path + ".bak", true);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                AppLog.Error("config.json 保存失败：" + ex.Message);
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }
    }
}
