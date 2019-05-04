﻿using SimpleRemote.Modes;
using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using SimpleRemote.Bll;
using SimpleRemote.DllLibrary;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleRemote.Container
{
    /// <summary>
    /// RemoteControl_SSH.xaml 的交互逻辑
    /// </summary>
    public partial class RemoteControl_telnet : BaseRemoteControl
    {
        const int ERROR_NOTIC = 0;
        const int ERROR_FATAL = 1;

        private Putty _putty;

        private Putty.ErrorEvent _event_error;
        private Putty.ConnectedEvent _event_connected;
        private Putty.DebugEvent _event_debug;
        private Putty.KeyDownEvent _event_keydown;
        private static int i;

        public RemoteControl_telnet(ContentControl contentControl)
            : base(contentControl)
        {
            InitializeComponent();
            try
            {
                _putty = new Putty();
                _event_error = Error_Event;
                _event_connected = Connected_Event;
                _event_keydown = KeyDown_Event;
                _event_debug = OnDebug;

                _putty.SetCallback(_event_error, _event_connected, null, null, null, _event_keydown, _event_debug);
                if (!_putty.Init())
                {
                    string errorText = Marshal.PtrToStringAnsi(_putty.GetError());
                    throw new Exception(errorText);
                }
            }
            catch (Exception e)
            {
                throw new Exception($"加载putty失败,{e.Message}。");
            }
        }
        private void OnDebug(IntPtr text)
        {
            Debug.WriteLine(Marshal.PtrToStringAnsi(text));
        }

        public override void Connect(DbItemRemoteLink linkSettings, FinalItemSetting finalItemSetting)
        {
            string[] values = linkSettings.Server.Split(':');
            if (values.Length > 2) throw new Exception($"服务器地址不正确。");//地址不正确
            string server = values[0];
            string port = values.Length == 2 ? $"-P {values[1]}" : "-P 23";

            FinalItemSetting_telnet finalSetting = (FinalItemSetting_telnet)finalItemSetting;

            IntPtr parentHwnd = FormsControl.Handle;
            Window windows = Window.GetWindow(this);
            int width = (int)windows.Width - 4;
            int height = (int)windows.Height - 34;

            Task.Run(() =>
            {
                Putty.Settings settings = new Putty.Settings
                {
                    fontname = finalSetting.FontName,
                    fontsize = finalSetting.FontSize,
                    curtype = finalSetting.Cursor - 1,
                    linecodepage = Encoding.GetEncoding(finalSetting.Character).BodyName,
                    backspaceisdelete = finalSetting.Fallbackkeys - 1,
                    mouseisxterm = finalSetting.MouseAction - 1,
                    puttycolor = new Putty.PuttyColor(finalSetting.GetPuttyColor()),
                    rxvthomeend = finalSetting.HomeAndEnd - 1,
                    functionkeys = finalSetting.FnAndKeypad - 1,
                    cjkambigwide = finalSetting.CJKAmbigWide.Value,
                    capslockcyr = finalSetting.CapsLockCyr.Value,
                    crimplieslf = finalSetting.CRImpliesLF.Value,
                    lfimpliescr = finalSetting.LFImpliesCR.Value,
                };

                string user = string.IsNullOrEmpty(linkSettings.UserName) ? "" : $"-l {linkSettings.UserName}";
                string pw = string.IsNullOrEmpty(linkSettings.Password) ? "" : $"-pw {linkSettings.Password}";

                if (!_putty.Create(parentHwnd, $"-telnet {port} {user} {pw} {server}", 0, 0, width, height, settings))
                {
                    string errorText = Marshal.PtrToStringAnsi(_putty.GetError());
                    Dispatcher.Invoke(() => { OnFatalError?.Invoke("错误", errorText); });
                }

                _putty.Dispose();
                _putty = null;
                GC.Collect();
                GC.SuppressFinalize(this);
            });
        }

        public override void Release()
        {
            _putty?.Exit();
        }
        /// <summary>
        /// 连接成功
        /// </summary>
        public void Connected_Event()
        {
            Dispatcher.Invoke(() =>
            {
                OnConnected.Invoke();
            });
            _putty.Show();
        }
        /// <summary>
        /// 键盘按下
        /// </summary>
        private void KeyDown_Event(IntPtr wParam, IntPtr lParam)
        {
            if (wParam.ToInt32() == 0x1B)//ESC按键释放
            {
                Dispatcher.Invoke(() => { FullScreen?.Invoke(false); });
            }
        }

        private void Error_Event(int level, IntPtr title, IntPtr text, int lParam)
        {
            Dispatcher.Invoke(() =>
            {
                if (level == ERROR_FATAL) OnFatalError?.Invoke(Marshal.PtrToStringAnsi(title), Marshal.PtrToStringAnsi(text));
                if (level == ERROR_NOTIC) OnNonfatal?.Invoke(Marshal.PtrToStringAnsi(title), Marshal.PtrToStringAnsi(text));
            });
        }

        private void FormsControl_SizeChanged(object sender, EventArgs e)
        {
            if (FormsControl.Width > 0 && FormsControl.Height > 0)
                _putty?.Move.Invoke(0, 0, FormsControl.Width, FormsControl.Height);
        }
    }
}