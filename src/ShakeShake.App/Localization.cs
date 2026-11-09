using System;
using System.Collections.Generic;
using System.Globalization;

namespace ShakeShake
{
    /// <summary>Two-language UI strings (Simplified Chinese + English).</summary>
    internal static class L
    {
        public const string Version = "1.2.0";

        private static readonly string[,] Table = new string[,]
        {
            { "app.name",       "晃晃  ShakeShake", "ShakeShake" },
            { "tray.tip",       "晃晃：晃动鼠标，指针变大", "ShakeShake: shake the mouse to enlarge the pointer" },
            { "tray.enable",    "启用晃动放大", "Enable shake-to-find" },
            { "tray.settings",  "设置…", "Settings…" },
            { "tray.exit",      "退出", "Exit" },
            { "tray.paused",    "晃晃已暂停：晃动不会再放大指针", "ShakeShake paused: shaking won't enlarge the pointer" },
            { "tray.enabled",   "晃晃已启用：晃一晃试试", "ShakeShake enabled: try shaking the mouse" },
            { "tray.run",       "晃晃正在运行", "ShakeShake is running" },

            { "form.title",     "晃晃  ShakeShake  设置", "ShakeShake Settings" },
            { "lang.label",     "界面语言", "Language" },
            { "lang.auto",      "跟随系统 (Follow system)", "Follow system (跟随系统)" },
            { "lang.zh",        "简体中文", "简体中文" },
            { "lang.en",        "English", "English" },

            { "chk.enable",     "晃动鼠标时放大指针", "Enlarge the pointer when I shake the mouse" },
            { "chk.enable.hint","快速左右（或上下）来回晃几下鼠标即可触发", "Shake the mouse quickly back and forth to trigger" },

            { "grp.zoom",       "放大倍数", "Magnification" },
            { "zoom.hint",      "拖动滑块调整放大后的指针大小", "Drag to change how big the pointer gets" },
            { "grp.exclude",    "在这些程序里不放大（例如游戏）", "Stay off in these apps (e.g. games)" },
            { "exclude.hint",   "按 exe 文件名匹配，不区分大小写", "Matched by exe file name, case-insensitive" },
            { "btn.browse",     "浏览添加…", "Browse…" },
            { "btn.addcurrent", "加入当前程序", "Add current app" },
            { "btn.remove",     "删除", "Remove" },
            { "exclude.capture","请在 3 秒内切换到目标程序…", "Switch to the target app within 3 seconds…" },
            { "exclude.wait",   "正在捕获前台程序… {0}", "Capturing foreground app… {0}" },
            { "exclude.dupe",   "这个程序已经在列表里了。", "That app is already in the list." },
            { "exclude.added",  "已加入：{0}", "Added: {0}" },
            { "exclude.fail",   "没能识别当前程序，请先切到目标窗口，再点一次。", "Couldn't identify the app. Switch to the target window and try again." },

            { "chk.autostart",  "开机自动启动", "Start with Windows" },
            { "lnk.advanced",   "高级  ▸", "Advanced  ▸" },
            { "lnk.advanced.open", "高级  ▾", "Advanced  ▾" },
            { "adv.sensitivity","灵敏度", "Sensitivity" },
            { "adv.restore",    "停止晃动后恢复", "Restore after" },
            { "adv.note",       "灵敏度越高越容易触发；恢复时间越短，指针缩回得越快。", "Higher sensitivity triggers more easily; shorter delay shrinks the pointer sooner." },
            { "adv.sens.low",   "迟钝", "Firm" },
            { "adv.sens.high",  "灵敏", "Sensitive" },

            { "btn.defaults",   "恢复默认", "Defaults" },
            { "btn.close",      "关闭", "Close" },
            { "defaults.done",  "已恢复默认设置。", "Defaults restored." },

            { "dlg.pickexe",    "选择要排除的程序", "Choose an app to exclude" },
            { "dlg.filter",     "程序 (*.exe)|*.exe|所有文件 (*.*)|*.*", "Programs (*.exe)|*.exe|All files (*.*)|*.*" },

            { "err.title",      "晃晃  ShakeShake", "ShakeShake" },
            { "err.engine",     "这次没能放大指针：系统光标文件读取失败。\n如果一直这样，请把设置里的倍数调小一点再试。",
                                "Couldn't enlarge the pointer this time (failed to read the system cursor files).\nIf it keeps happening, try a smaller magnification." },

            { "unit.ms",        "{0} 毫秒", "{0} ms" },
            { "about",          "版本 {0}", "Version {0}" },
            { "hotkey.hint",    "快捷键 {0}：暂停 / 恢复", "Hotkey {0}: pause / resume" },
            { "page.subtitle", "晃动鼠标，把指针变大", "Shake the mouse to make the pointer bigger" },
            { "advanced.title", "高级", "Advanced" },
            { "about.tip",     "配置文件：{0}", "Settings file: {0}" },
        };

        private static readonly Dictionary<string, string> Zh = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> En = new Dictionary<string, string>();

        static L()
        {
            for (int i = 0; i < Table.GetLength(0); i++)
            {
                Zh[Table[i, 0]] = Table[i, 1];
                En[Table[i, 0]] = Table[i, 2];
            }
            Current = "zh-CN";
        }

        public static string Current { get; private set; }

        public static string T(string key)
        {
            Dictionary<string, string> d = Current == "zh-CN" ? Zh : En;
            string s;
            return d.TryGetValue(key, out s) ? s : key;
        }

        public static string T(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        public static void SetLanguage(string language)
        {
            Current = Resolve(language);
        }

        /// <summary>auto -> zh-CN only for Simplified Chinese; everything else falls back to English.</summary>
        public static string Resolve(string setting)
        {
            if (setting == "zh-CN" || setting == "en-US") return setting;

            string name = CultureInfo.CurrentUICulture.Name;
            if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                if (name.IndexOf("Hant", StringComparison.OrdinalIgnoreCase) >= 0) return "en-US";
                if (name.EndsWith("TW", StringComparison.OrdinalIgnoreCase)) return "en-US";
                if (name.EndsWith("HK", StringComparison.OrdinalIgnoreCase)) return "en-US";
                if (name.EndsWith("MO", StringComparison.OrdinalIgnoreCase)) return "en-US";
                return "zh-CN";
            }
            return "en-US";
        }
    }
}
