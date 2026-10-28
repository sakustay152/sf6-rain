using System;
using System.Collections.Generic;
using System.IO;

namespace Sf6Rain
{
    public sealed class Track
    {
        public string Path = "";
        public string Name = "";
        public long Bytes;

        public override string ToString() { return Name; }
    }

    /// <summary>
    /// Files on disk, nothing embedded. The reference project ships no music for
    /// the same reason: 雨爱 belongs to its rights holders, not to this tool.
    /// </summary>
    public sealed class MusicLibrary
    {
        static readonly string[] Extensions =
        {
            ".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac"
        };

        public readonly List<Track> Tracks = new List<Track>();
        public string Folder = "";
        public string LastError = "";
        public int PreferredIndex = -1;

        readonly List<int> _queue = new List<int>();
        readonly Random _random = new Random();
        int _pos;
        bool _first = true;
        bool _shuffle = true;

        public int Count { get { return Tracks.Count; } }

        public static string ResolveFolder(string setting)
        {
            if (string.IsNullOrEmpty(setting)) return AppPaths.MusicDir;
            return Path.IsPathRooted(setting) ? setting : Path.Combine(AppPaths.BaseDir, setting);
        }

        public void Reload(string setting, bool preferRainLove, string playOrder)
        {
            Tracks.Clear();
            LastError = "";
            PreferredIndex = -1;
            Folder = ResolveFolder(setting);
            _shuffle = playOrder == "shuffle";

            if (!Directory.Exists(Folder))
            {
                LastError = "找不到音乐文件夹：" + Folder;
                RebuildQueue();
                return;
            }

            try
            {
                foreach (string path in Directory.GetFiles(Folder, "*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    bool ok = false;
                    foreach (string e in Extensions)
                        if (e == ext) { ok = true; break; }
                    if (!ok) continue;
                    var info = new FileInfo(path);
                    if (!info.Exists || info.Length < 4096) continue;
                    var t = new Track();
                    t.Path = path;
                    t.Name = Path.GetFileNameWithoutExtension(path);
                    t.Bytes = info.Length;
                    Tracks.Add(t);
                }
            }
            catch (Exception ex)
            {
                LastError = "读取音乐文件夹失败：" + ex.Message;
            }

            Tracks.Sort(delegate(Track a, Track b)
            {
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCulture);
            });

            if (preferRainLove)
            {
                for (int i = 0; i < Tracks.Count; i++)
                {
                    string n = Tracks[i].Name.Replace(" ", "").ToLowerInvariant();
                    if (n.Contains("雨爱") || n.Contains("rainlove"))
                    {
                        PreferredIndex = i;
                        break;
                    }
                }
            }

            RebuildQueue();
        }

        void RebuildQueue()
        {
            _queue.Clear();
            for (int i = 0; i < Tracks.Count; i++) _queue.Add(i);
            if (_shuffle)
            {
                for (int i = _queue.Count - 1; i > 0; i--)
                {
                    int j = _random.Next(i + 1);
                    int tmp = _queue[i]; _queue[i] = _queue[j]; _queue[j] = tmp;
                }
            }
            _pos = 0;
            _first = true;
        }

        public Track Next()
        {
            if (Tracks.Count == 0) return null;
            if (_first && PreferredIndex >= 0 && PreferredIndex < Tracks.Count)
            {
                _first = false;
                if (_queue.Count > 0 && _queue[_pos % _queue.Count] == PreferredIndex) _pos++;
                return Tracks[PreferredIndex];
            }
            _first = false;
            if (_pos >= _queue.Count)
            {
                if (_shuffle)
                {
                    RebuildQueue();
                    _first = false;
                }
                else
                {
                    _pos = 0;
                }
            }
            if (_queue.Count == 0) return Tracks[0];
            int idx = _queue[_pos % _queue.Count];
            _pos++;
            if (idx < 0 || idx >= Tracks.Count) return Tracks[0];
            return Tracks[idx];
        }

        public Track First()
        {
            if (Tracks.Count == 0) return null;
            if (PreferredIndex >= 0) return Tracks[PreferredIndex];
            return Tracks[0];
        }

        public Track ByPath(string path)
        {
            foreach (Track t in Tracks)
                if (string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }
    }
}
