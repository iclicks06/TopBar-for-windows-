using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;
using System.Management;

namespace TopBar
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer? timer;
        private ManagementObjectSearcher? searcher;
        private bool gpuAvailable = false;
        private PerformanceCounter? cpuCounter;
        private PerformanceCounter? ramCounter;

        public MainWindow()
        {
            InitializeComponent();
            SetupWindow();
            InitializeTimer();
            InitializePerformanceCounters();
            DetectGPU();
            UpdateDisplay();
        }

        private void InitializePerformanceCounters()
        {
            try
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                cpuCounter.NextValue();
                ramCounter.NextValue();
            }
            catch
            {
                cpuCounter?.Dispose();
                ramCounter?.Dispose();
                cpuCounter = null;
                ramCounter = null;
            }
        }

        private void SetupWindow()
        {
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = new SolidColorBrush(Colors.Transparent);
            this.Topmost = true;
            this.ResizeMode = ResizeMode.NoResize;
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            this.Left = 0;
            this.Top = 2;
            this.Width = SystemParameters.PrimaryScreenWidth;
            this.Height = 25;

            ApplyAcrylicBlur();

            var hwnd = new WindowInteropHelper(this).Handle;
            IntPtr extendedStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(
                (long)extendedStyle | (long)WS_EX_LAYERED | (long)WS_EX_TRANSPARENT));
        }

        private void InitializeTimer()
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (s, e) => UpdateDisplay();
            timer.Start();
        }

        private void DetectGPU()
        {
            try
            {
                searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                foreach (ManagementObject obj in searcher.Get())
                {
                    string? name = obj["Name"]?.ToString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        gpuAvailable = true;
                        break;
                    }
                }
            }
            catch
            {
                gpuAvailable = false;
            }
        }

        private void UpdateDisplay()
        {
            DateTime now = DateTime.Now;
            DateText.Text = now.ToString("ddd, MMM dd");
            TimeText.Text = now.ToString("HH:mm:ss");

            try
            {
                if (cpuCounter != null)
                {
                    double cpuUsage = cpuCounter.NextValue();
                    CpuText.Text = $"CPU {cpuUsage:F0}%";
                }
                else
                {
                    CpuText.Text = "CPU --%";
                }
            }
            catch
            {
                CpuText.Text = "CPU --%";
            }

            try
            {
                if (ramCounter != null)
                {
                    double ramUsage = ramCounter.NextValue();
                    RamText.Text = $"RAM {ramUsage:F0}%";
                }
                else
                {
                    RamText.Text = "RAM --%";
                }
            }
            catch
            {
                RamText.Text = "RAM --%";
            }

            if (gpuAvailable)
            {
                try
                {
                    GpuText.Text = "GPU --%";
                }
                catch
                {
                    GpuText.Text = "GPU --%";
                }
            }
            else
            {
                GpuText.Text = "";
            }
        }

        private void ApplyAcrylicBlur()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var accent = new AccentPolicy
            {
                AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND,
                GradientColor = 0x88111111
            };
            
            var accentStructSize = Marshal.SizeOf(accent);
            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = accentStructSize,
                Data = accentPtr
            };

            try
            {
                SetWindowCompositionAttribute(hwnd, ref data);
            }
            catch
            {
            }

            Marshal.FreeHGlobal(accentPtr);
        }

        #region Win32 API
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        public static extern bool SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        public const int GWL_EXSTYLE = -20;
        public const uint WS_EX_LAYERED = 0x80000;
        public const uint WS_EX_TRANSPARENT = 0x20;

        public enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_INVALID_STATE = 4
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public uint GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        public enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }
        #endregion

        protected override void OnClosed(EventArgs e)
        {
            timer?.Stop();
            cpuCounter?.Dispose();
            ramCounter?.Dispose();
            searcher?.Dispose();
            base.OnClosed(e);
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Application.Current.Shutdown();
            }
            base.OnKeyDown(e);
        }
    }
}