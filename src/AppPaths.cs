using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sf6Rain
{
    public static class AppPaths
    {
        public static readonly string BaseDir =
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        public static string MusicDir { get { return Path.Combine(BaseDir, "music"); } }
        public static string CaptureDir { get { return Path.Combine(BaseDir, "captures"); } }
        public static string LogFile { get { return Path.Combine(BaseDir, "日志.txt"); } }
        public static string ConfigFile { get { return Path.Combine(BaseDir, "config.json"); } }
        public static string SelfTestFile { get { return Path.Combine(BaseDir, "自检.txt"); } }
        public static string CrashFile { get { return Path.Combine(BaseDir, "崩溃.log"); } }

        public static void Ensure()
        {
            try
            {
                Directory.CreateDirectory(MusicDir);
                string readme = Path.Combine(MusicDir, "说明.txt");
                if (!File.Exists(readme))
                    File.WriteAllText(readme,
                        "把你的歌放在这个文件夹里。\r\n" +
                        "------------------------\r\n\r\n" +
                        "程序会递归查找 mp3 / wav / wma / m4a / aac / flac。\r\n" +
                        "文件名里带「雨爱」或「rain love」的歌会被优先选中。\r\n\r\n" +
                        "程序本身不附带任何录音；这里的歌曲由使用者自行提供，\r\n" +
                        "请确认你有权使用。\r\n",
                        new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                AppLog.Error("创建 music 文件夹失败：" + ex.Message);
            }
        }
    }

    public static class AppLog
    {
        static readonly object Gate = new object();
        static readonly List<string> Recent = new List<string>();
        public static event Action<string> Line;

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Error(string message)
        {
            Write("ERR ", message);
        }

        static void Write(string level, string message)
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + " [" + level + "] " + message;
            lock (Gate)
            {
                Recent.Add(line);
                if (Recent.Count > 80) Recent.RemoveAt(0);
                try
                {
                    var info = new FileInfo(AppPaths.LogFile);
                    if (info.Exists && info.Length > 2 * 1024 * 1024)
                    {
                        string old = AppPaths.LogFile + ".old";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(AppPaths.LogFile, old);
                    }
                    File.AppendAllText(AppPaths.LogFile, line + "\r\n", new UTF8Encoding(false));
                }
                catch { }
            }
            var h = Line;
            if (h != null)
            {
                try { h(line); } catch { }
            }
        }

        public static string[] RecentLines()
        {
            lock (Gate) return Recent.ToArray();
        }
    }
}
