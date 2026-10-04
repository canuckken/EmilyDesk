using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using XWidgetReborn.WidgetSdk;

namespace EmilyDesk.Widgets.SystemInfo
{
    public sealed class SystemInfoWidget :
        IWidget,
        IOfficialWidget,
        IWidgetSnapBoundsProvider,
        IWidgetDesignerProvider,
        IWidgetDesignerBackgroundLayerProvider,
        IWidgetThemeProvider,
        IRuntimeAwareWidget
    {
        private const int BaseWidth = 550;
#if ART_DECO_WIDGET || INDUSTRIAL_WIDGET || WOODLAND_WIDGET || BOTANICAL_WIDGET || EMBER_GLOW_WIDGET
        private const int BaseHeight = 327;
#else
        private const int BaseHeight = 363;
#endif
#if ART_DECO_WIDGET
        private const string WidgetId = "utility.art-deco-system-info";
        private const string WidgetName = "Art Deco System Information";
        private const string WidgetVersion = "1.0.3";
        private const string ThemeName = "Art Deco";
        private const string SkinFileName = "art-deco-system-info-skin.png";
        private const int PreferredWidth = 480;
        private const int PreferredHeight = 285;
#elif EMBER_GLOW_WIDGET
        private const string WidgetId = "utility.ember-glow-system-info";
        private const string WidgetName = "Ember Glow System Information";
        private const string WidgetVersion = "1.0.7";
        private const string ThemeName = "Ember Glow";
        private const string SkinFileName = "ember-glow-system-info-skin.png";
        private const int PreferredWidth = BaseWidth;
        private const int PreferredHeight = BaseHeight;
#elif INDUSTRIAL_WIDGET
        private const string WidgetId = "utility.industrial-system-info";
        private const string WidgetName = "Industrial System Information";
        private const string WidgetVersion = "1.0.2";
        private const string ThemeName = "Industrial";
        private const string SkinFileName = "industrial-system-info-skin.png";
        private const int PreferredWidth = 480;
        private const int PreferredHeight = 285;
#elif WOODLAND_WIDGET
        private const string WidgetId = "utility.woodland-system-info";
        private const string WidgetName = "Woodland Nature System Information";
        private const string WidgetVersion = "1.0.1";
        private const string ThemeName = "Woodland Nature";
        private const string SkinFileName = "woodland-system-info-skin.png";
        private const int PreferredWidth = 480;
        private const int PreferredHeight = 285;
#elif BOTANICAL_WIDGET
        private const string WidgetId = "utility.botanical-system-info";
        private const string WidgetName = "Botanical Nature System Information";
        private const string WidgetVersion = "1.0.0";
        private const string ThemeName = "Botanical Nature";
        private const string SkinFileName = "botanical-system-info-skin.png";
        private const int PreferredWidth = 480;
        private const int PreferredHeight = 285;
#else
        private const string WidgetId = "utility.system-info";
        private const string WidgetName = "Steampunk System Information";
        private const string WidgetVersion = "1.0.5";
        private const string ThemeName = "Steampunk";
        private const string SkinFileName = "steampunk-system-info-skin.png";
        private const int PreferredWidth = BaseWidth;
        private const int PreferredHeight = BaseHeight;
#endif
#if ART_DECO_WIDGET
        private const int CpuY = 86;
        private const int MemoryY = 130;
        private const int DiskY = 174;
        private const int SystemLabelY = 216;
        private const int WindowsY = 236;
        private const int ComputerY = 258;
        private const int TemperatureLabelY = 86;
        private const int TemperatureY = 104;
        private const int TemperatureStatusY = 145;
        private const int NetworkLabelY = 166;
        private const int DownloadLabelY = 188;
        private const int DownloadValueY = 187;
        private const int UploadLabelY = 215;
        private const int UploadValueY = 214;
        private const int UptimeLabelY = 238;
        private const int UptimeValueY = 260;
        private const int DividerTop = 84;
        private const int DividerBottom = 282;
#elif EMBER_GLOW_WIDGET
        private const int CpuY = 81;
        private const int MemoryY = 125;
        private const int DiskY = 169;
        private const int SystemLabelY = 213;
        private const int WindowsY = 235;
        private const int ComputerY = 247;
        private const int TemperatureLabelY = 81;
        private const int TemperatureY = 98;
        private const int TemperatureStatusY = 143;
        private const int NetworkLabelY = 162;
        private const int DownloadLabelY = 185;
        private const int DownloadValueY = 184;
        private const int UploadLabelY = 209;
        private const int UploadValueY = 208;
        private const int UptimeLabelY = 234;
        private const int UptimeValueY = 247;
        private const int DividerTop = 76;
        private const int DividerBottom = 272;
#elif INDUSTRIAL_WIDGET || WOODLAND_WIDGET || BOTANICAL_WIDGET
        private const int CpuY = 62;
        private const int MemoryY = 115;
        private const int DiskY = 168;
        private const int SystemLabelY = 224;
        private const int WindowsY = 246;
        private const int ComputerY = 273;
        private const int TemperatureLabelY = 62;
        private const int TemperatureY = 80;
        private const int TemperatureStatusY = 127;
        private const int NetworkLabelY = 162;
        private const int DownloadLabelY = 184;
        private const int DownloadValueY = 183;
        private const int UploadLabelY = 211;
        private const int UploadValueY = 210;
        private const int UptimeLabelY = 248;
        private const int UptimeValueY = 272;
        private const int DividerTop = 61;
        private const int DividerBottom = 307;
#else
        private const int CpuY = 68;
        private const int MemoryY = 115;
        private const int DiskY = 162;
        private const int SystemLabelY = 207;
        private const int WindowsY = 228;
        private const int ComputerY = 250;
        private const int TemperatureLabelY = 68;
        private const int TemperatureY = 86;
        private const int TemperatureStatusY = 132;
        private const int NetworkLabelY = 157;
        private const int DownloadLabelY = 178;
        private const int DownloadValueY = 177;
        private const int UploadLabelY = 203;
        private const int UploadValueY = 202;
        private const int UptimeLabelY = 231;
        private const int UptimeValueY = 252;
        private const int DividerTop = 66;
        private const int DividerBottom = 298;
#endif
        private const long CoreTempTjMaxOffset = 1024;
        private const long CoreTempCoreCountOffset = 1536;
        private const long CoreTempCpuCountOffset = 1540;
        private const long CoreTempTemperatureOffset = 1544;
        private const long CoreTempFahrenheitOffset = 2684;
        private const long CoreTempDeltaOffset = 2685;

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatus
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhysical;
            public ulong AvailablePhysical;
            public ulong TotalPageFile;
            public ulong AvailablePageFile;
            public ulong TotalVirtual;
            public ulong AvailableVirtual;
            public ulong AvailableExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileTimeValue
        {
            public uint Low;
            public uint High;
            public ulong Value
            {
                get { return ((ulong)High << 32) | Low; }
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(
            out FileTimeValue idle,
            out FileTimeValue kernel,
            out FileTimeValue user);

        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

        private Action _invalidate;
        private IWidgetHostContext _host;
        private IRuntimeContext _runtime;
        private bool _paused;
        private ulong _lastIdle;
        private ulong _lastKernel;
        private ulong _lastUser;
        private int _cpu;
        private int _memory;
        private int _disk;
        private ulong _usedMemory;
        private ulong _totalMemory;
        private string _systemDrive = "C:";
        private bool _temperatureAvailable;
        private double _temperature;
        private int _hottestCoreIndex = -1;
        private long _lastNetworkReceived = -1;
        private long _lastNetworkSent = -1;
        private DateTime _lastNetworkSampleUtc = DateTime.MinValue;
        private double _downloadRate;
        private double _uploadRate;
        private string _operatingSystem = "Windows";
        private Image _panelSkin;
        private bool _panelSkinAttempted;

        public string Id { get { return WidgetId; } }
        public string Name { get { return WidgetName; } }
        public string Description
        {
            get { return "Live CPU, optional Core Temp temperature, memory, disk, network, and uptime information."; }
        }
        public string Version { get { return WidgetVersion; } }
        public Size DefaultSize
        { get { return new Size(PreferredWidth, PreferredHeight); } }
        public Point DefaultLocation { get { return new Point(430, 110); } }
        public WidgetUpdateRate UpdateRate { get { return WidgetUpdateRate.Second; } }
        public IEnumerable<string> Themes
        {
            get { return OptionalWidgetThemeCatalog.AvailableThemes(WidgetId); }
        }
        public string Theme
        {
            get { return ThemeName; }
            set
            {
                string target = OptionalWidgetThemeCatalog.TargetWidgetId(
                    WidgetId, value);
                if (_runtime == null || string.IsNullOrWhiteSpace(target) ||
                    string.Equals(target, WidgetId,
                        StringComparison.OrdinalIgnoreCase))
                    return;
                Point position = _runtime.Settings.GetPosition(DefaultLocation);
                _runtime.Events.Publish("widget.switch-theme",
                    WidgetId + "\t" + target + "\t" +
                    position.X.ToString(CultureInfo.InvariantCulture) + "\t" +
                    position.Y.ToString(CultureInfo.InvariantCulture));
            }
        }

        public void AttachHost(IWidgetHostContext host)
        {
            _host = host;
            if (_host == null) return;
            _host.SetPreferredSize(DefaultSize);
            _host.SetWindowShape(WidgetWindowShape.AlphaRectangle);
        }

        public void AttachRuntime(IRuntimeContext runtime)
        {
            if (_runtime != null)
                _runtime.Events.Published -= DesignerSaved;
            _runtime = runtime;
            if (_runtime != null)
                _runtime.Events.Published += DesignerSaved;
        }

        private void DesignerSaved(object sender, WidgetRuntimeEventArgs e)
        {
            if (e != null && e.Topic == "designer.saved")
                RequestInvalidate();
        }

        public void Start(Action invalidate)
        {
            _invalidate = invalidate;
            _systemDrive = Path.GetPathRoot(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.System)) ?? "C:\\";
            _operatingSystem = ReadOperatingSystemName();
            Sample();
            RequestInvalidate();
        }

        public void Tick(DateTime now)
        {
            if (_paused) return;
            Sample();
            RequestInvalidate();
        }

        public void Pause() { _paused = true; }
        public void Resume() { _paused = false; Sample(); RequestInvalidate(); }

        private void Sample()
        {
            SampleCpu();
            SampleTemperature();
            SampleNetwork();
            var memory = new MemoryStatus();
            memory.Length = (uint)Marshal.SizeOf(typeof(MemoryStatus));
            if (GlobalMemoryStatusEx(ref memory))
            {
                _memory = (int)memory.MemoryLoad;
                _totalMemory = memory.TotalPhysical;
                _usedMemory = memory.TotalPhysical - memory.AvailablePhysical;
            }
            try
            {
                var drive = new DriveInfo(_systemDrive);
                if (drive.IsReady && drive.TotalSize > 0)
                    _disk = (int)Math.Round(
                        100D * (drive.TotalSize - drive.AvailableFreeSpace) /
                        drive.TotalSize);
            }
            catch { }
        }

        private void SampleCpu()
        {
            FileTimeValue idle;
            FileTimeValue kernel;
            FileTimeValue user;
            if (!GetSystemTimes(out idle, out kernel, out user)) return;
            if (_lastKernel != 0 || _lastUser != 0)
            {
                ulong idleDelta = idle.Value - _lastIdle;
                ulong kernelDelta = kernel.Value - _lastKernel;
                ulong userDelta = user.Value - _lastUser;
                ulong total = kernelDelta + userDelta;
                if (total > 0)
                    _cpu = Math.Max(0, Math.Min(100,
                        (int)Math.Round(100D * (total - idleDelta) / total)));
            }
            _lastIdle = idle.Value;
            _lastKernel = kernel.Value;
            _lastUser = user.Value;
        }

        private void SampleTemperature()
        {
            double temperature;
            int hottestCore;
            _temperatureAvailable = TryReadCoreTemp(
                out temperature, out hottestCore);
            if (_temperatureAvailable) _temperature = temperature;
            _hottestCoreIndex = _temperatureAvailable ? hottestCore : -1;
        }

        private static bool TryReadCoreTemp(
            out double temperature, out int hottestCoreIndex)
        {
            temperature = 0D;
            hottestCoreIndex = -1;
            foreach (string mappingName in new[]
            {
                "CoreTempMappingObjectEx",
                "Global\\CoreTempMappingObjectEx",
                "CoreTempMappingObject",
                "Global\\CoreTempMappingObject"
            })
            {
                try
                {
                    using (MemoryMappedFile mapping =
                        MemoryMappedFile.OpenExisting(
                            mappingName,
                            MemoryMappedFileRights.Read))
                    using (MemoryMappedViewAccessor data =
                        mapping.CreateViewAccessor(
                            0,
                            0,
                            MemoryMappedFileAccess.Read))
                    {
                        if (data.Capacity <= CoreTempDeltaOffset) continue;
                        uint coreCount = data.ReadUInt32(
                            CoreTempCoreCountOffset);
                        uint cpuCount = data.ReadUInt32(
                            CoreTempCpuCountOffset);
                        ulong reportedCount = (ulong)coreCount *
                            (cpuCount == 0 ? 1UL : (ulong)cpuCount);
                        int count = (int)Math.Min(256UL, reportedCount);
                        if (count <= 0) continue;
                        bool fahrenheit = data.ReadByte(
                            CoreTempFahrenheitOffset) != 0;
                        bool deltaToMaximum = data.ReadByte(
                            CoreTempDeltaOffset) != 0;
                        double hottest = double.MinValue;
                        int hottestIndex = -1;
                        for (int index = 0; index < count; index++)
                        {
                            double current = data.ReadSingle(
                                CoreTempTemperatureOffset + index * 4L);
                            if (deltaToMaximum && index < 128)
                            {
                                uint maximum = data.ReadUInt32(
                                    CoreTempTjMaxOffset + index * 4L);
                                if (maximum > 0) current = maximum - current;
                            }
                            if (fahrenheit)
                                current = (current - 32D) * 5D / 9D;
                            if (current >= -20D && current <= 150D)
                            {
                                if (current > hottest)
                                {
                                    hottest = current;
                                    hottestIndex = index;
                                }
                            }
                        }
                        if (hottest == double.MinValue) continue;
                        temperature = hottest;
                        hottestCoreIndex = hottestIndex;
                        return true;
                    }
                }
                catch
                {
                    // Core Temp is optional. Missing or protected shared
                    // memory simply leaves temperature unavailable.
                }
            }
            return false;
        }

        private void SampleNetwork()
        {
            long received = 0;
            long sent = 0;
            try
            {
                foreach (NetworkInterface adapter in
                    NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up ||
                        adapter.NetworkInterfaceType ==
                            NetworkInterfaceType.Loopback ||
                        adapter.NetworkInterfaceType ==
                            NetworkInterfaceType.Tunnel)
                        continue;
                    try
                    {
                        IPv4InterfaceStatistics statistics =
                            adapter.GetIPv4Statistics();
                        received += statistics.BytesReceived;
                        sent += statistics.BytesSent;
                    }
                    catch { }
                }
            }
            catch { return; }

            DateTime now = DateTime.UtcNow;
            if (_lastNetworkSampleUtc != DateTime.MinValue &&
                received >= _lastNetworkReceived && sent >= _lastNetworkSent)
            {
                double seconds = (now - _lastNetworkSampleUtc).TotalSeconds;
                if (seconds > 0.05D)
                {
                    _downloadRate = (received - _lastNetworkReceived) / seconds;
                    _uploadRate = (sent - _lastNetworkSent) / seconds;
                }
            }
            _lastNetworkReceived = received;
            _lastNetworkSent = sent;
            _lastNetworkSampleUtc = now;
        }

        public void Render(Graphics graphics, Rectangle bounds)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint =
                System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            GraphicsState state = graphics.Save();
            float scale = Math.Min(
                bounds.Width / (float)BaseWidth,
                bounds.Height / (float)BaseHeight);
            graphics.TranslateTransform(
                bounds.Left + (bounds.Width - BaseWidth * scale) / 2F,
                bounds.Top + (bounds.Height - BaseHeight * scale) / 2F);
            graphics.ScaleTransform(scale, scale);
            try
            {
                SavedDesignerLayout layout = SavedDesignerLayout.Current(
                    DesignerTheme, DesignerKind);
                if (layout == null)
                    DrawPanel(graphics, true);
                else
                    DrawSavedLayout(graphics, layout);
                if (_paused)
                    using (var paused = new SolidBrush(
                        Color.FromArgb(155, 20, 12, 9)))
                        graphics.FillRectangle(paused, 1, 1,
                            BaseWidth - 2, BaseHeight - 2);
            }
            finally { graphics.Restore(state); }
        }

        private void DrawPanel(Graphics graphics, bool drawText)
        {
            DrawPanelBackground(graphics, null);
            DrawPanelChrome(graphics, drawText);
        }

        private void DrawPanelBackground(
            Graphics graphics, string imagePath)
        {
            if (!string.IsNullOrWhiteSpace(imagePath) &&
                DesignerLayerPainter.DrawImage(graphics,
                    ResolvePackageAsset(imagePath),
                    new RectangleF(0, 0, BaseWidth, BaseHeight), 1F))
                return;
            Image skin = PanelSkin;
            if (skin != null)
                graphics.DrawImage(skin, new Rectangle(0, 0, BaseWidth, BaseHeight));
            else
                DrawFallbackPanel(graphics);
        }

        private void DrawPanelChrome(Graphics graphics, bool drawText)
        {
#if INDUSTRIAL_WIDGET && !ART_DECO_WIDGET
            DrawIndustrialTitlePlate(graphics,
                new Rectangle(112, 11, 326, 42));
#elif WOODLAND_WIDGET
            DrawWoodlandTitlePlate(graphics,
                new Rectangle(112, 11, 326, 42));
#elif BOTANICAL_WIDGET
            DrawBotanicalTitlePlate(graphics,
                new Rectangle(112, 11, 326, 42));
#endif

            Color headingColor = HeadingInk;
            Color labelColor = LabelInk;
            Color valueColor = ValueInk;
            Color mutedColor = MutedInk;
            using (var title = new Font("Georgia", 14.5F, FontStyle.Bold))
            using (var label = new Font("Segoe UI", 10.5F, FontStyle.Bold))
            using (var value = new Font("Segoe UI", 11.5F, FontStyle.Bold))
            using (var small = new Font("Segoe UI", 9.5F, FontStyle.Regular))
            using (var systemDetails = new Font(
                "Segoe UI", 9.5F, FontStyle.Bold))
            using (var temperatureFont = new Font(
                "Georgia", 27F, FontStyle.Bold))
            {
                if (drawText) DrawText(graphics, "SYSTEM INFORMATION", title,
                    headingColor,
                    new Rectangle(121, 54, 308, 30),
                    StringAlignment.Center);

                using (var divider = new Pen(DividerInk, 1F))
                    graphics.DrawLine(divider, 275, DividerTop,
                        275, DividerBottom);

                DrawMeter(graphics, "CPU", _cpu, "" + _cpu + "%",
                    55, CpuY, 212, label, value, drawText);
                DrawMeter(graphics, "MEMORY", _memory,
                    FormatBytes(_usedMemory) + " / " + FormatBytes(_totalMemory),
                    55, MemoryY, 212, label, value, drawText);
                DrawMeter(graphics, "SYSTEM DISK", _disk,
                    _disk + "% used", 55, DiskY, 212, label, value,
                    drawText);

                if (drawText) DrawText(graphics, "SYSTEM", label, labelColor,
                    new Rectangle(55, SystemLabelY, 212, 20), StringAlignment.Near);
                if (drawText) DrawText(graphics, _operatingSystem, systemDetails, valueColor,
                    new Rectangle(55, WindowsY, 212, 19), StringAlignment.Near);
                if (drawText) DrawText(graphics,
                    Environment.MachineName + "  •  " +
                    (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"),
                    systemDetails, mutedColor,
                    new Rectangle(55, ComputerY, 212, 19), StringAlignment.Near);

                if (drawText) DrawText(graphics, "CPU TEMPERATURE", label, labelColor,
                    new Rectangle(293, TemperatureLabelY, 202, 20), StringAlignment.Near);
                if (drawText) DrawText(graphics,
                    _temperatureAvailable
                        ? Math.Round(_temperature).ToString("0") + "°C"
                        : "—",
                    temperatureFont,
                    TemperatureColor(),
                    new Rectangle(293, TemperatureY, 202, 48), StringAlignment.Near);
                if (drawText) DrawText(graphics,
                    _temperatureAvailable
                        ? HottestCoreText()
                        : "Optional • Core Temp not running",
                    small, mutedColor,
                    new Rectangle(293, TemperatureStatusY, 202, 19), StringAlignment.Near);

                if (drawText) DrawText(graphics, "NETWORK", label, labelColor,
                    new Rectangle(293, NetworkLabelY, 202, 20), StringAlignment.Near);
                if (drawText) DrawText(graphics, "DOWN", small, mutedColor,
                    new Rectangle(293, DownloadLabelY, 52, 20), StringAlignment.Near);
                if (drawText) DrawText(graphics, FormatRate(_downloadRate), value, valueColor,
                    new Rectangle(345, DownloadValueY, 150, 22), StringAlignment.Near);
                if (drawText) DrawText(graphics, "UP", small, mutedColor,
                    new Rectangle(293, UploadLabelY, 52, 20), StringAlignment.Near);
                if (drawText) DrawText(graphics, FormatRate(_uploadRate), value, valueColor,
                    new Rectangle(345, UploadValueY, 150, 22), StringAlignment.Near);

                if (drawText) DrawText(graphics, "UPTIME", label, labelColor,
                    new Rectangle(293, UptimeLabelY, 202, 20), StringAlignment.Near);
                if (drawText) DrawText(graphics, FormatUptime(), value, valueColor,
                    new Rectangle(293, UptimeValueY, 202, 22), StringAlignment.Near);
            }
        }

        private string HottestCoreText()
        {
            return _hottestCoreIndex >= 0
                ? "Core Temp • Core #" + _hottestCoreIndex
                : "Core Temp • hottest core";
        }

        private static void DrawMeter(
            Graphics graphics,
            string name,
            int percent,
            string detail,
            int x,
            int y,
            int width,
            Font label,
            Font value,
            bool drawText)
        {
            if (drawText) DrawText(graphics, name, label,
                LabelInk,
                new Rectangle(x, y, width, 21), StringAlignment.Near);
            if (drawText) DrawText(graphics, detail, value, ValueInk,
                new Rectangle(x, y, width, 21),
                StringAlignment.Far);
            Rectangle track = new Rectangle(x, y + 27, width, 9);
            using (GraphicsPath trackPath = RoundedRectangle(track, 4))
            using (var background = new SolidBrush(MeterTrack))
            using (var outline = new Pen(MeterOutline, 1F))
            {
                graphics.FillPath(background, trackPath);
                graphics.DrawPath(outline, trackPath);
            }
            Rectangle fill = new Rectangle(
                track.X,
                track.Y,
                (int)Math.Round(track.Width * Math.Max(0, Math.Min(100, percent)) / 100D),
                track.Height);
            if (fill.Width > 0)
                using (var foreground = new LinearGradientBrush(
                    fill,
                    percent >= 85
                        ? Color.FromArgb(237, 118, 55)
                        : MeterFillLight,
                    percent >= 85
                        ? Color.FromArgb(139, 45, 26)
                        : MeterFillDark,
                    0F))
                {
                    if (fill.Width >= 8)
                        using (GraphicsPath fillPath =
                            RoundedRectangle(fill, 4))
                            graphics.FillPath(foreground, fillPath);
                    else
                        graphics.FillRectangle(foreground, fill);
                }
        }

        private static string FormatBytes(ulong value)
        {
            return (value / 1073741824D).ToString("0.0") + " GB";
        }

        private static string FormatRate(double bytesPerSecond)
        {
            if (bytesPerSecond >= 1073741824D)
                return (bytesPerSecond / 1073741824D).ToString("0.0") + " GB/s";
            if (bytesPerSecond >= 1048576D)
                return (bytesPerSecond / 1048576D).ToString("0.0") + " MB/s";
            if (bytesPerSecond >= 1024D)
                return (bytesPerSecond / 1024D).ToString("0.0") + " KB/s";
            return Math.Max(0D, bytesPerSecond).ToString("0") + " B/s";
        }

        private static string FormatUptime()
        {
            TimeSpan uptime = TimeSpan.FromMilliseconds(GetTickCount64());
            return uptime.Days > 0
                ? uptime.Days + "d " + uptime.Hours + "h " + uptime.Minutes + "m"
                : uptime.Hours + "h " + uptime.Minutes + "m";
        }

        private static string ReadOperatingSystemName()
        {
            try
            {
                object name = Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                    "ProductName",
                    "Windows");
                string value = Convert.ToString(name);
                return string.IsNullOrWhiteSpace(value) ? "Windows" : value;
            }
            catch { return "Windows"; }
        }

        private Color TemperatureColor()
        {
            if (!_temperatureAvailable) return MutedInk;
            if (_temperature >= 85D) return Color.FromArgb(241, 105, 65);
            if (_temperature >= 70D) return Color.FromArgb(255, 178, 72);
            return ValueInk;
        }

        private Image PanelSkin
        {
            get
            {
                if (_panelSkinAttempted) return _panelSkin;
                _panelSkinAttempted = true;
                try
                {
                    string assemblyFolder = Path.GetDirectoryName(
                        GetType().Assembly.Location);
                    string skinPath = Path.Combine(
                        assemblyFolder ?? string.Empty,
                        SkinFileName);
                    if (File.Exists(skinPath))
                        using (Image source = Image.FromFile(skinPath))
                            _panelSkin = new Bitmap(source);
                }
                catch
                {
                    _panelSkin = null;
                }
                return _panelSkin;
            }
        }

        public Rectangle GetSnapBounds(Size renderedSurface)
        {
            double scaleX = renderedSurface.Width / (double)BaseWidth;
            double scaleY = renderedSurface.Height / (double)BaseHeight;
#if EMBER_GLOW_WIDGET
            // Alpha > 10 bounds of the current 1484x1060 Bakelite skin,
            // scaled onto the 550x327 widget surface.
            int left = (int)Math.Round(4D * scaleX);
            int top = (int)Math.Round(14D * scaleY);
            int right = (int)Math.Round(546D * scaleX);
            int bottom = (int)Math.Round(307D * scaleY);
#elif INDUSTRIAL_WIDGET || WOODLAND_WIDGET || BOTANICAL_WIDGET
            int left = (int)Math.Round(4D * scaleX);
            int top = (int)Math.Round(4D * scaleY);
            int right = (int)Math.Round(546D * scaleX);
            int bottom = (int)Math.Round(323D * scaleY);
#else
            int left = (int)Math.Round(6D * scaleX);
            int top = (int)Math.Round(8D * scaleY);
            int right = (int)Math.Round(545D * scaleX);
            int bottom = (int)Math.Round(355D * scaleY);
#endif
            return Rectangle.FromLTRB(
                Math.Max(0, left),
                Math.Max(0, top),
                Math.Min(renderedSurface.Width, Math.Max(left + 1, right)),
                Math.Min(renderedSurface.Height, Math.Max(top + 1, bottom)));
        }

        private static void DrawFallbackPanel(Graphics graphics)
        {
            Rectangle full = new Rectangle(2, 2, BaseWidth - 4, BaseHeight - 4);
            using (GraphicsPath path = RoundedRectangle(full, 18))
            using (var brush = new LinearGradientBrush(
                full,
                FallbackTop,
                FallbackBottom,
                90F))
            using (var pen = new Pen(FallbackBorder, 3F))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(pen, path);
            }
        }

#if ART_DECO_WIDGET
        private static void DrawArtDecoTitlePlate(
            Graphics graphics, Rectangle bounds)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, 7))
            using (var brush = new LinearGradientBrush(bounds,
                Color.FromArgb(111, 68, 18),
                Color.FromArgb(255, 238, 174), 90F))
            using (var border = new Pen(
                Color.FromArgb(244, 198, 92), 1.5F))
            {
                brush.InterpolationColors = new ColorBlend
                {
                    Colors = new[]
                    {
                        Color.FromArgb(111, 68, 18),
                        Color.FromArgb(255, 238, 174),
                        Color.FromArgb(205, 145, 52),
                        Color.FromArgb(96, 56, 16)
                    },
                    Positions = new[] { 0F, .3F, .67F, 1F }
                };
                graphics.FillPath(brush, path);
                graphics.DrawPath(border, path);
                using (var shine = new Pen(
                    Color.FromArgb(185, 255, 250, 214), 1F))
                    graphics.DrawLine(shine, bounds.Left + 16,
                        bounds.Top + 7, bounds.Right - 16,
                        bounds.Top + 7);
            }
        }
#elif INDUSTRIAL_WIDGET
        private static void DrawIndustrialTitlePlate(
            Graphics graphics, Rectangle bounds)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, 7))
            using (var brush = new LinearGradientBrush(bounds,
                Color.FromArgb(70, 74, 76),
                Color.FromArgb(205, 208, 208), 90F))
            using (var border = new Pen(Color.FromArgb(37, 40, 42), 1.5F))
            {
                brush.InterpolationColors = new ColorBlend
                {
                    Colors = new[]
                    {
                        Color.FromArgb(57, 61, 63),
                        Color.FromArgb(226, 228, 228),
                        Color.FromArgb(132, 136, 137),
                        Color.FromArgb(48, 52, 54)
                    },
                    Positions = new[] { 0F, .3F, .67F, 1F }
                };
                graphics.FillPath(brush, path);
                graphics.DrawPath(border, path);
                using (var shine = new Pen(
                    Color.FromArgb(165, 255, 255, 255), 1F))
                    graphics.DrawLine(shine, bounds.Left + 16,
                        bounds.Top + 7, bounds.Right - 16,
                        bounds.Top + 7);
            }
        }
#elif WOODLAND_WIDGET
        private static void DrawWoodlandTitlePlate(
            Graphics graphics, Rectangle bounds)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, 7))
            using (var brush = new LinearGradientBrush(bounds,
                Color.FromArgb(60, 35, 20),
                Color.FromArgb(174, 119, 64), 90F))
            using (var border = new Pen(Color.FromArgb(195, 153, 83), 1.5F))
            {
                brush.InterpolationColors = new ColorBlend
                {
                    Colors = new[]
                    {
                        Color.FromArgb(48, 29, 18),
                        Color.FromArgb(177, 124, 69),
                        Color.FromArgb(95, 55, 31),
                        Color.FromArgb(38, 24, 16)
                    },
                    Positions = new[] { 0F, .3F, .67F, 1F }
                };
                graphics.FillPath(brush, path);
                graphics.DrawPath(border, path);
                using (var shine = new Pen(
                    Color.FromArgb(125, 255, 231, 180), 1F))
                    graphics.DrawLine(shine, bounds.Left + 16,
                        bounds.Top + 7, bounds.Right - 16,
                        bounds.Top + 7);
            }
        }
#elif BOTANICAL_WIDGET
        private static void DrawBotanicalTitlePlate(
            Graphics graphics, Rectangle bounds)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, 7))
            using (var brush = new LinearGradientBrush(bounds,
                Color.FromArgb(249, 231, 226),
                Color.FromArgb(217, 146, 142), 90F))
            using (var border = new Pen(Color.FromArgb(102, 116, 85), 1.5F))
            {
                brush.InterpolationColors = new ColorBlend
                {
                    Colors = new[]
                    {
                        Color.FromArgb(233, 188, 183),
                        Color.FromArgb(255, 244, 240),
                        Color.FromArgb(223, 164, 160),
                        Color.FromArgb(207, 132, 130)
                    },
                    Positions = new[] { 0F, .3F, .67F, 1F }
                };
                graphics.FillPath(brush, path);
                graphics.DrawPath(border, path);
                using (var shine = new Pen(
                    Color.FromArgb(185, 255, 255, 255), 1F))
                    graphics.DrawLine(shine, bounds.Left + 16,
                        bounds.Top + 7, bounds.Right - 16,
                        bounds.Top + 7);
            }
        }
#endif

        public void ShowSettings()
        {
            System.Windows.Forms.DialogResult result =
                System.Windows.Forms.MessageBox.Show(
                    "CPU, memory, disk, network, uptime, and Windows details work without additional software.\r\n\r\n" +
                    "CPU temperature is optional. To enable it, install and run Core Temp and allow its shared-memory interface. EmilyDesk reads that local data directly and does not send system information anywhere.\r\n\r\n" +
                    "Open the official Core Temp website?",
                    WidgetName,
                    System.Windows.Forms.MessageBoxButtons.YesNo,
                    System.Windows.Forms.MessageBoxIcon.Information);
            if (result == System.Windows.Forms.DialogResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(
                        "https://www.alcpu.com/CoreTemp/")
                    {
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        public void ResetSettings()
        {
            if (_host != null)
            {
                _host.Scale = 1F;
                _host.Opacity = 1D;
            }
            RequestInvalidate();
        }

        public IEnumerable<WidgetMenuCommand> GetMenuCommands()
        {
            return new[]
            {
                new WidgetMenuCommand(
                    "Edit in Designer", false, OpenDesigner)
            };
        }

        public string DesignerKind { get { return "system-info"; } }
        public string DesignerTheme { get { return ThemeName; } }

        public SavedDesignerLayout CreateDesignerLayout()
        {
            SavedDesignerLayout layout = NewDesignerLayout(
                WidgetName, BaseWidth, BaseHeight);
            AddBackground(layout);
            Color heading = HeadingInk;
            Color label = LabelInk;
            Color value = ValueInk;
            Color muted = MutedInk;
            AddText(layout, "title", "Title", "SYSTEM INFORMATION", "None",
                121, 54, 308, 30, "Georgia", 14.5F, true, heading, 1);
            AddText(layout, "cpu-label", "CPU Label", "CPU", "None",
                55, CpuY, 212, 21, "Segoe UI", 10.5F, true, label, 0);
            AddText(layout, "cpu-value", "CPU Value", "37%", "System: CPU",
                55, CpuY, 212, 21, "Segoe UI", 11.5F, true, value, 2);
            AddText(layout, "memory-label", "Memory Label", "MEMORY", "None",
                55, MemoryY, 212, 21, "Segoe UI", 10.5F, true, label, 0);
            AddText(layout, "memory-value", "Memory Value", "7.8 GB / 16.0 GB", "System: Memory",
                55, MemoryY, 212, 21, "Segoe UI", 11.5F, true, value, 2);
            AddText(layout, "disk-label", "Disk Label", "SYSTEM DISK", "None",
                55, DiskY, 212, 21, "Segoe UI", 10.5F, true, label, 0);
            AddText(layout, "disk-value", "Disk Value", "42% used", "System: Disk",
                55, DiskY, 212, 21, "Segoe UI", 11.5F, true, value, 2);
            AddText(layout, "system-label", "System Label", "SYSTEM", "None",
                55, SystemLabelY, 212, 20, "Segoe UI", 10.5F, true, label, 0);
            AddText(layout, "windows", "Windows Version", "Windows 10 IoT Enterprise LTSC", "System: Windows",
                55, WindowsY, 212, 19, "Segoe UI", 9.5F, true, value, 0);
            AddText(layout, "computer", "Computer Name", "EMILYDESK-PC  •  64-bit", "System: Computer",
                55, ComputerY, 212, 19, "Segoe UI", 9.5F, true, muted, 0);
            AddText(layout, "temperature-label", "Temperature Label", "CPU TEMPERATURE", "None",
                293, TemperatureLabelY, 202, 20, "Segoe UI", 10.5F, true, label, 0);
            AddText(layout, "temperature", "Temperature", "58°C", "System: Temperature",
                293, TemperatureY, 202, 48, "Georgia", 27F, true, value, 0);
            AddText(layout, "temperature-status", "Temperature Status", "Core Temp • Core #2", "System: Temperature Status",
                293, TemperatureStatusY, 202, 19, "Segoe UI", 9.5F, false, muted, 0);
            AddText(layout, "network-label", "Network Label", "NETWORK", "None",
                293, NetworkLabelY, 202, 20, "Segoe UI", 10.5F, true, label, 0);
            AddText(layout, "down-label", "Download Label", "DOWN", "None",
                293, DownloadLabelY, 52, 20, "Segoe UI", 9.5F, false, muted, 0);
            AddText(layout, "down-value", "Download Speed", "2.4 MB/s", "System: Download",
                345, DownloadValueY, 150, 22, "Segoe UI", 11.5F, true, value, 0);
            AddText(layout, "up-label", "Upload Label", "UP", "None",
                293, UploadLabelY, 52, 20, "Segoe UI", 9.5F, false, muted, 0);
            AddText(layout, "up-value", "Upload Speed", "184 KB/s", "System: Upload",
                345, UploadValueY, 150, 22, "Segoe UI", 11.5F, true, value, 0);
            AddText(layout, "uptime-label", "Uptime Label", "UPTIME", "None",
                293, UptimeLabelY, 202, 20, "Segoe UI", 10.5F, true, label, 0);
            AddText(layout, "uptime", "Uptime", "2d 6h 14m", "System: Uptime",
                293, UptimeValueY, 202, 22, "Segoe UI", 11.5F, true, value, 0);
            return layout;
        }

        public void RenderDesignerBackground(Graphics graphics,
            Rectangle bounds)
        {
            RenderDesignerBackgroundLayer(
                graphics, bounds, SkinFileName, 1F);
        }

        public void RenderDesignerBackgroundLayer(
            Graphics graphics,
            Rectangle bounds,
            string imagePath,
            float opacity)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            int cpu = _cpu;
            int memory = _memory;
            int disk = _disk;
            using (var surface = new Bitmap(BaseWidth, BaseHeight,
                PixelFormat.Format32bppArgb))
            using (Graphics layer = Graphics.FromImage(surface))
            {
                layer.SmoothingMode = SmoothingMode.AntiAlias;
                layer.CompositingQuality = CompositingQuality.HighQuality;
                layer.InterpolationMode =
                    InterpolationMode.HighQualityBicubic;
                layer.PixelOffsetMode = PixelOffsetMode.HighQuality;
                layer.TextRenderingHint =
                    System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                try
                {
                    if (_host == null)
                    {
                        _cpu = 37;
                        _memory = 49;
                        _disk = 42;
                    }
                    DrawPanelBackground(layer, imagePath);
                    DrawPanelChrome(layer, false);
                    using (var attributes = new ImageAttributes())
                    {
                        attributes.SetColorMatrix(new ColorMatrix
                        {
                            Matrix33 = Math.Max(0F,
                                Math.Min(1F, opacity))
                        });
                        graphics.DrawImage(surface, bounds, 0, 0,
                            surface.Width, surface.Height,
                            GraphicsUnit.Pixel, attributes);
                    }
                }
                finally
                {
                    _cpu = cpu;
                    _memory = memory;
                    _disk = disk;
                }
            }
        }

        private void DrawSavedLayout(
            Graphics graphics, SavedDesignerLayout layout)
        {
            DesignerLayer background = null;
            if (layout != null && layout.Elements != null)
                background = layout.Elements.Find(delegate(DesignerLayer item)
                {
                    return IsBackgroundLayer(item);
                });
            if (background == null)
                DrawPanel(graphics, false);
            else if (background.Visible)
                RenderDesignerBackgroundLayer(graphics,
                    Rectangle.Round(background.Bounds),
                    background.ImagePath, background.Opacity);
            if (layout == null || layout.Elements == null) return;
            foreach (DesignerLayer layer in layout.Elements)
                if (!IsBackgroundLayer(layer))
                    DesignerLayerPainter.Draw(
                        graphics, layer, ResolveDesignerText);
        }

        private static bool IsBackgroundLayer(DesignerLayer layer)
        {
            return layer != null && string.Equals(layer.Id,
                "main-background", StringComparison.OrdinalIgnoreCase);
        }

        private string ResolvePackageAsset(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            if (Path.IsPathRooted(path)) return path;
            string folder = Path.GetDirectoryName(
                GetType().Assembly.Location) ?? string.Empty;
            string candidate = Path.Combine(folder,
                path.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(candidate) ? candidate : path;
        }

        public string ResolveDesignerText(DesignerLayer layer)
        {
            if (layer == null) return string.Empty;
            bool sample = _host == null;
            switch (layer.Binding ?? string.Empty)
            {
                case "System: CPU": return sample ? "37%" : _cpu + "%";
                case "System: Memory":
                    return sample ? "7.8 GB / 16.0 GB" :
                        FormatBytes(_usedMemory) + " / " +
                        FormatBytes(_totalMemory);
                case "System: Disk": return sample ? "42% used" :
                    _disk + "% used";
                case "System: Windows": return sample ?
                    "Windows 10 IoT Enterprise LTSC" : _operatingSystem;
                case "System: Computer":
                    return sample ? "EMILYDESK-PC  •  64-bit" :
                        Environment.MachineName + "  •  " +
                        (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit");
                case "System: Temperature":
                    return sample ? "58°C" : _temperatureAvailable
                        ? Math.Round(_temperature).ToString("0") + "°C" : "—";
                case "System: Temperature Status":
                    return sample ? "Core Temp • Core #2" :
                        _temperatureAvailable ? HottestCoreText() :
                        "Optional • Core Temp not running";
                case "System: Download": return sample ? "2.4 MB/s" :
                    FormatRate(_downloadRate);
                case "System: Upload": return sample ? "184 KB/s" :
                    FormatRate(_uploadRate);
                case "System: Uptime": return sample ? "2d 6h 14m" :
                    FormatUptime();
                default: return layer.Text;
            }
        }

        private void OpenDesigner()
        {
            try
            {
                string path = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "EmilyDesk.Designer.exe");
                if (!File.Exists(path)) throw new FileNotFoundException(
                    "EmilyDesk Designer is not installed.");
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "--optional-widget-assembly \"" +
                        GetType().Assembly.Location +
                        "\" --type \"" +
                        GetType().FullName + "\"",
                    WorkingDirectory = Path.GetDirectoryName(path),
                    UseShellExecute = true
                });
            }
            catch (Exception error)
            {
                System.Windows.Forms.MessageBox.Show(error.Message,
                    "EmilyDesk Designer",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        private static SavedDesignerLayout NewDesignerLayout(
            string name, int width, int height)
        {
            return new SavedDesignerLayout
            {
                Name = name, CanvasWidth = width, CanvasHeight = height,
                EditableLayerVersion = 1,
                BackgroundLayerVersion = 1,
                Elements = new List<DesignerLayer>(),
                DeletedElementIds = new List<string>()
            };
        }

        private static void AddBackground(SavedDesignerLayout layout)
        {
            layout.Elements.Add(new DesignerLayer
            {
                Id = "main-background",
                Name = "Widget Background",
                Binding = "Layout: Main Background",
                Kind = 1,
                Surface = 0,
                X = 0F,
                Y = 0F,
                Width = BaseWidth,
                Height = BaseHeight,
                Visible = true,
                Opacity = 1F,
                Scale = 1F,
                ImagePath = SkinFileName
            });
        }

        private static Color HeadingInk { get {
#if ART_DECO_WIDGET
            return Color.FromArgb(236, 218, 177);
#elif EMBER_GLOW_WIDGET
            return Color.FromArgb(246, 223, 189);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(31, 34, 35);
#elif WOODLAND_WIDGET
            return Color.FromArgb(245, 226, 184);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(66, 82, 59);
#else
            return Color.FromArgb(73, 38, 18);
#endif
        } }
        private static Color LabelInk { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(220, 129, 49);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(216, 166, 73);
#elif WOODLAND_WIDGET
            return Color.FromArgb(214, 174, 91);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(66, 82, 59);
#else
            return Color.FromArgb(211, 164, 88);
#endif
        } }
        private static Color ValueInk { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(246, 223, 189);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(239, 232, 211);
#elif WOODLAND_WIDGET
            return Color.FromArgb(245, 238, 210);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(66, 82, 59);
#else
            return Color.FromArgb(255, 239, 201);
#endif
        } }
        private static Color MutedInk { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(181, 139, 108);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(161, 166, 166);
#elif WOODLAND_WIDGET
            return Color.FromArgb(174, 181, 149);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(122, 117, 105);
#else
            return Color.FromArgb(183, 137, 82);
#endif
        } }
        private static Color DividerInk { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(173, 82, 27);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(150, 126, 131, 133);
#elif WOODLAND_WIDGET
            return Color.FromArgb(150, 168, 132, 66);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(170, 102, 116, 85);
#else
            return Color.FromArgb(150, 119, 66, 31);
#endif
        } }
        private static Color MeterTrack { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(110, 22, 13, 10);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(110, 14, 17, 18);
#elif WOODLAND_WIDGET
            return Color.FromArgb(120, 8, 22, 16);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(205, 231, 222, 208);
#else
            return Color.FromArgb(92, 27, 17, 13);
#endif
        } }
        private static Color MeterOutline { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(175, 161, 75, 25);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(165, 104, 110, 112);
#elif WOODLAND_WIDGET
            return Color.FromArgb(170, 120, 105, 58);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(190, 102, 116, 85);
#else
            return Color.FromArgb(153, 117, 68, 35);
#endif
        } }
        private static Color MeterFillLight { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(255, 173, 54);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(245, 177, 63);
#elif WOODLAND_WIDGET
            return Color.FromArgb(235, 188, 82);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(217, 146, 142);
#else
            return Color.FromArgb(242, 179, 68);
#endif
        } }
        private static Color MeterFillDark { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(124, 39, 11);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(127, 83, 28);
#elif WOODLAND_WIDGET
            return Color.FromArgb(113, 77, 32);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(169, 95, 97);
#else
            return Color.FromArgb(147, 74, 29);
#endif
        } }
        private static Color FallbackTop { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(79, 40, 22);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(109, 113, 115);
#elif WOODLAND_WIDGET
            return Color.FromArgb(55, 83, 60);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(255, 250, 240);
#else
            return Color.FromArgb(65, 36, 24);
#endif
        } }
        private static Color FallbackBottom { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(18, 9, 7);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(25, 28, 29);
#elif WOODLAND_WIDGET
            return Color.FromArgb(12, 27, 20);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(225, 218, 199);
#else
            return Color.FromArgb(20, 13, 11);
#endif
        } }
        private static Color FallbackBorder { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(244, 124, 24);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(202, 205, 205);
#elif WOODLAND_WIDGET
            return Color.FromArgb(144, 104, 58);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(102, 116, 85);
#else
            return Color.FromArgb(211, 151, 65);
#endif
        } }

        private static void AddText(SavedDesignerLayout layout,
            string id, string name, string text, string binding,
            float x, float y, float width, float height,
            string font, float size, bool bold, Color color, int alignment)
        {
            layout.Elements.Add(new DesignerLayer
            {
                Id = id, Name = name, Text = text, Binding = binding,
                Kind = 0, Surface = 0, X = x, Y = y,
                Width = width, Height = height, Visible = true,
                Opacity = 1F, Scale = 1F, FontName = font,
                FontSize = size, Bold = bold,
                ColorArgb = color.ToArgb(), Alignment = alignment,
                WordWrap = false, Trimming = 3
            });
        }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void DrawText(
            Graphics graphics,
            string text,
            Font font,
            Color color,
            Rectangle bounds,
            StringAlignment alignment)
        {
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat())
            {
                format.LineAlignment = StringAlignment.Center;
                format.Alignment = alignment;
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags = StringFormatFlags.NoWrap;
                graphics.DrawString(text, font, brush, bounds, format);
            }
        }

        private void RequestInvalidate()
        {
            if (_invalidate != null) _invalidate();
            if (_host != null) _host.Invalidate();
        }

        public void Dispose()
        {
            if (_runtime != null)
                _runtime.Events.Published -= DesignerSaved;
            _runtime = null;
            if (_panelSkin != null)
            {
                _panelSkin.Dispose();
                _panelSkin = null;
            }
            _invalidate = null;
            _host = null;
        }
    }
}
