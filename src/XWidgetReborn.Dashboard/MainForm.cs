using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using XWidgetReborn.Shared;

namespace XWidgetReborn
{
    internal sealed class MainForm : Form
    {
        private const string ServiceName = "XWidgetWeatherBridge";

        private readonly Panel _content;
        private readonly Panel _dashboardPage;
        private readonly Panel _weatherPage;
        private readonly Panel _dockPage;
        private readonly Panel _designerPage;
        private readonly Panel _settingsPage;
        private readonly Panel _aboutPage;
        private readonly Panel _developerToolsPage;
        private readonly Label _weatherValue;
        private readonly Label _nativeWidgetsValue;
        private readonly Label _attentionValue;
        private readonly Label _providerValue;
        private readonly Label _providerHealthValue;
        private readonly Label _cacheModeValue;
        private readonly ComboBox _providerCombo;
        private readonly TextBox _providerApiKey;
        private readonly Label _providerApiKeyStatus;
        private readonly JavaScriptSerializer _json =
            new JavaScriptSerializer();
        private readonly System.Windows.Forms.Timer _startupStatusTimer;
        private readonly System.Windows.Forms.Timer _engineStatusTimer;
        private readonly Label _engineStatusValue;
        private readonly Button _startEngineButton;
        private readonly Button _widgetsNavButton;
        private readonly Label _dockStatusValue;
        private readonly Dictionary<string, Button> _dockThemeButtons =
            new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private FlowLayoutPanel _dockThemeButtonsPanel;
        private ComboBox _designerWidgetCombo;
        private ComboBox _designerThemeCombo;
        private readonly Button _closeDockButton;
        private VirtualWidgetManagerForm _widgetManager;
        private WidgetManagerForm _normalGalleryDiagnostic;
        private WidgetManagerForm _formOnlyGalleryTest;
        private WidgetManagerForm _headerOnlyGalleryTest;
        private WidgetManagerForm _completeGalleryTest;
        private readonly Dictionary<
            WidgetManagerForm.GalleryTestMode,
            WidgetManagerForm> _gallerySubsystemTests =
                new Dictionary<
                    WidgetManagerForm.GalleryTestMode,
                    WidgetManagerForm>();
        private Form _plainDragTest;
        private VirtualGalleryDiagnosticForm _virtualGalleryTest;
        private DragDiagnosticLauncher _dragDiagnostics;
        private int _startupStatusAttempts;
        private bool _engineKnownOnline;
        private int _consecutiveEngineStatusFailures;
        private int _lastNativeRunningCount;
        private readonly string _dashboardStatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XWidget Reborn", "dashboard.window");

        public MainForm()
        {
            Text = "EmilyDesk Dashboard — " + AppConstants.BuildDisplay;
            StartPosition = FormStartPosition.Manual;
            MinimumSize = new Size(940, 640);
            Size = new Size(1080, 730);
            RestoreDashboardBounds();
            Font = new Font("Segoe UI", 9F);
            BackColor = EmilyDeskDesignTokens.Canvas;

            try
            {
                Icon = Icon.ExtractAssociatedIcon(
                    Application.ExecutablePath);
            }
            catch { }

            var sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 220,
                BackColor = EmilyDeskDesignTokens.Sidebar
            };
            Controls.Add(sidebar);

            var brand = new Label
            {
                Text = "EMILYDESK",
                ForeColor = EmilyDeskDesignTokens.Gold,
                Font = new Font("Segoe UI", 17F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(24, 25)
            };
            sidebar.Controls.Add(brand);

            var slogan = new Label
            {
                Text = "Your desktop, calmly organized.",
                ForeColor = EmilyDeskDesignTokens.SidebarMuted,
                AutoSize = true,
                Location = new Point(26, 96)
            };
            sidebar.Controls.Add(slogan);

            AddNavButton(sidebar, "Home", 145, delegate
            {
                ShowPage(_dashboardPage);
            }, true);

            AddNavButton(sidebar, "Widgets", 195, delegate
            {
                ShowWidgetManager();
            }, false);

            AddNavButton(sidebar, "Wallpaper", 245, delegate
            {
                using (var form = new WallpaperSettingsForm())
                    form.ShowDialog(this);
            }, false);

            AddNavButton(sidebar, "Docks", 295, delegate
            {
                ShowPage(_dockPage);
                RefreshDockControls();
            }, false);

            AddNavButton(sidebar, "Designer", 345, delegate
            {
                RefreshDesignerChoices();
                ShowPage(_designerPage);
            }, false);

            AddNavButton(sidebar, "Widget Templates", 395, delegate
            {
                LaunchTemplatePicker();
            }, false);

            AddNavButton(sidebar, "Settings", 445, delegate
            {
                ShowPage(_settingsPage);
            }, false);

            var version = new Label
            {
                Text = "Community Release\r\nVersion " +
                    AppConstants.Version,
                ForeColor = EmilyDeskDesignTokens.SidebarMuted,
                AutoSize = true,
                Location = new Point(26, 610),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom
            };
            sidebar.Controls.Add(version);

            _content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = EmilyDeskDesignTokens.Canvas
            };
            Controls.Add(_content);
            _content.BringToFront();

            _dashboardPage = new Panel { Dock = DockStyle.Fill };
            _weatherPage = new Panel { Dock = DockStyle.Fill };
            _dockPage = new Panel { Dock = DockStyle.Fill };
            _designerPage = new Panel { Dock = DockStyle.Fill };
            _settingsPage = new Panel { Dock = DockStyle.Fill };
            _aboutPage = new Panel { Dock = DockStyle.Fill };
            _developerToolsPage = new Panel { Dock = DockStyle.Fill };

            _weatherValue = BuildDashboard(_dashboardPage);
            Label[] weatherLabels = BuildWeatherPage(_weatherPage);
            BuildDockPage(_dockPage);
            BuildDesignerPage(_designerPage);
            BuildSettingsPage(_settingsPage);
            BuildAboutPage(_aboutPage);
            _providerValue = weatherLabels[0];
            _providerHealthValue = weatherLabels[1];
            _cacheModeValue = weatherLabels[2];
            _providerCombo = FindControlRecursive<ComboBox>(_weatherPage, "ProviderCombo");
            _providerApiKey = FindControlRecursive<TextBox>(_weatherPage,
                "ProviderApiKey");
            _providerApiKeyStatus = FindControlRecursive<Label>(_weatherPage,
                "ProviderApiKeyStatus");
            _providerCombo.SelectedIndexChanged += delegate
            {
                UpdateProviderApiKeyStatus();
            };

            _nativeWidgetsValue = FindControlRecursive<Label>(
                _dashboardPage,
                "NativeWidgetsValue");
            _attentionValue = FindControlRecursive<Label>(
                _dashboardPage,
                "AttentionValue");
            _engineStatusValue = FindControlRecursive<Label>(_dashboardPage, "EngineStatusValue");
            _startEngineButton = FindControlRecursive<Button>(_dashboardPage, "StartEngineButton");
            _widgetsNavButton = FindControlRecursive<Button>(
                _dashboardPage,
                "OpenGalleryButton");
            _dockStatusValue = FindControlRecursive<Label>(
                _dockPage,
                "DockStatusValue");
            _closeDockButton = FindControlRecursive<Button>(
                _dockPage,
                "CloseDockButton");

            _content.Controls.Add(_dashboardPage);
            _content.Controls.Add(_weatherPage);
            _content.Controls.Add(_dockPage);
            _content.Controls.Add(_designerPage);
            _content.Controls.Add(_settingsPage);
            _content.Controls.Add(_aboutPage);
            _content.Controls.Add(_developerToolsPage);
            ShowPage(_dashboardPage);

            _engineStatusTimer = new System.Windows.Forms.Timer();
            _engineStatusTimer.Interval = 1000;
            _engineStatusTimer.Tick += delegate
            {
                RefreshEngineControls();
                RefreshDockControls();
            };
            _engineStatusTimer.Start();

            _startupStatusTimer = new System.Windows.Forms.Timer();
            _startupStatusTimer.Interval = 1000;
            _startupStatusTimer.Tick += delegate
            {
                _startupStatusAttempts++;
                bool healthy = RefreshStatus();

                if (healthy || _startupStatusAttempts >= 15)
                {
                    _startupStatusTimer.Stop();
                    if (healthy)
                        LoadProviders();
                }
            };

            Activated += delegate
            {
                WidgetLayerMenuFactory.RaiseAfterActivation(this);
            };
            ContextMenuStrip = WidgetLayerMenuFactory.CreateContextMenu(this);

            Shown += delegate
            {
                WidgetLayerMenuFactory.RaiseAfterActivation(this);
                _startupStatusAttempts = 0;
                RefreshStatus();
                RefreshEngineControls();
                RefreshDockControls();
                _startupStatusTimer.Start();
            };

            FormClosed += delegate
            {
                if (_widgetManager != null &&
                    !_widgetManager.IsDisposed)
                    _widgetManager.Close();
                _widgetManager = null;
                CloseDiagnosticWindow(_normalGalleryDiagnostic);
                _normalGalleryDiagnostic = null;
                CloseDiagnosticWindow(_formOnlyGalleryTest);
                _formOnlyGalleryTest = null;
                CloseDiagnosticWindow(_headerOnlyGalleryTest);
                _headerOnlyGalleryTest = null;
                CloseDiagnosticWindow(_completeGalleryTest);
                _completeGalleryTest = null;
                foreach (WidgetManagerForm form in
                    new List<WidgetManagerForm>(
                        _gallerySubsystemTests.Values))
                    CloseDiagnosticWindow(form);
                _gallerySubsystemTests.Clear();
                CloseDiagnosticWindow(_virtualGalleryTest);
                _virtualGalleryTest = null;
                CloseDiagnosticWindow(_plainDragTest);
                _plainDragTest = null;
                CloseDiagnosticWindow(_dragDiagnostics);
                _dragDiagnostics = null;
                SaveDashboardBounds();
                _engineStatusTimer.Stop();
                _engineStatusTimer.Dispose();
            };

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    SaveDashboardBounds();
                    e.Cancel = true;
                    Hide();
                }
            };
        }


        private void RestoreDashboardBounds()
        {
            Rectangle fallback = new Rectangle(
                Math.Max(20, Screen.PrimaryScreen.WorkingArea.Right - 1120),
                Math.Max(20, Screen.PrimaryScreen.WorkingArea.Top + 40),
                1080, 730);
            Bounds = fallback;

            try
            {
                if (!File.Exists(_dashboardStatePath)) return;
                string[] parts = File.ReadAllText(_dashboardStatePath).Split(',');
                if (parts.Length != 4) return;
                int x, y, width, height;
                if (!int.TryParse(parts[0], out x) || !int.TryParse(parts[1], out y) ||
                    !int.TryParse(parts[2], out width) || !int.TryParse(parts[3], out height)) return;
                Rectangle saved = new Rectangle(x, y, Math.Max(940, width), Math.Max(640, height));
                foreach (Screen screen in Screen.AllScreens)
                {
                    if (screen.WorkingArea.IntersectsWith(saved))
                    {
                        Bounds = saved;
                        return;
                    }
                }
            }
            catch { }
        }

        private void SaveDashboardBounds()
        {
            if (WindowState == FormWindowState.Minimized) return;
            try
            {
                string folder = Path.GetDirectoryName(_dashboardStatePath);
                Directory.CreateDirectory(folder);
                Rectangle bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                File.WriteAllText(_dashboardStatePath,
                    bounds.X + "," + bounds.Y + "," + bounds.Width + "," + bounds.Height);
            }
            catch { }
        }

        private Label BuildDashboard(Panel page)
        {
            AddHeading(
                page,
                "Welcome to EmilyDesk",
                "Your desktop is ready");

            AddStatusCard(
                page, "ENGINE", 32, 118, "EngineStatusValue");
            Label weather = AddStatusCard(
                page, "WEATHER", 255, 118, "WeatherValue");
            AddStatusCard(
                page,
                "NATIVE WIDGETS RUNNING",
                478,
                118,
                "NativeWidgetsValue");

            var startEngine = MakePrimaryButton(
                "Start Engine", 32, 270, 145);
            startEngine.Name = "StartEngineButton";
            startEngine.Click += delegate
            {
                startEngine.Enabled = false;
                EnsureEngineRunning();
                RefreshEngineControls();
            };
            page.Controls.Add(startEngine);

            var launch = MakePrimaryButton(
                "Add Widget / Open Gallery", 32, 365, 245);
            launch.Name = "OpenGalleryButton";
            launch.Click += delegate { ShowWidgetManager(); };
            page.Controls.Add(launch);

            var settings = MakeSecondaryButton(
                "Settings", 292, 365, 145);
            settings.Click += delegate { ShowPage(_settingsPage); };
            page.Controls.Add(settings);

            var designer = MakeSecondaryButton(
                "Open Designer", 452, 365, 180);
            designer.Name = "OpenDesignerButton";
            designer.Click += delegate { ShowPage(_designerPage); };
            page.Controls.Add(designer);

            var attention = new Label
            {
                Name = "AttentionValue",
                Text = string.Empty,
                ForeColor = Color.FromArgb(183, 28, 28),
                AutoSize = false,
                Location = new Point(32, 290),
                Size = new Size(665, 50)
            };
            page.Controls.Add(attention);

            return weather;
        }

        private void BuildDesignerPage(Panel page)
        {
            AddHeading(
                page,
                "Designer",
                "Visually arrange and restyle EmilyDesk widgets");

            var panel = new Panel
            {
                BackColor = EmilyDeskDesignTokens.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(32, 118),
                Size = new Size(665, 390)
            };
            page.Controls.Add(panel);

            panel.Controls.Add(new Label
            {
                Text = "Edit an existing widget",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(22, 20)
            });
            panel.Controls.Add(new Label
            {
                Text = "Choose any built-in theme widget or an installed optional widget.",
                ForeColor = Color.DimGray,
                AutoSize = true,
                Location = new Point(24, 60)
            });

            panel.Controls.Add(new Label
            {
                Text = "WIDGET",
                ForeColor = EmilyDeskDesignTokens.Muted,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(24, 98)
            });
            panel.Controls.Add(new Label
            {
                Text = "THEME",
                ForeColor = EmilyDeskDesignTokens.Muted,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(334, 98)
            });

            _designerWidgetCombo = new ComboBox
            {
                Name = "DesignerWidgetCombo",
                Location = new Point(24, 120),
                Size = new Size(286, 28),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _designerThemeCombo = new ComboBox
            {
                Name = "DesignerThemeCombo",
                Location = new Point(334, 120),
                Size = new Size(286, 28),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (string theme in XWidgetReborn.WidgetSdk.
                EmilyDeskThemeCatalog.Names)
                _designerThemeCombo.Items.Add(theme);
            _designerThemeCombo.SelectedItem = "Industrial";
            _designerWidgetCombo.SelectedIndexChanged += delegate
            {
                DesignerWidgetChoice choice = _designerWidgetCombo.SelectedItem
                    as DesignerWidgetChoice;
                _designerThemeCombo.Enabled = choice == null ||
                    !choice.IsOptional;
                if (choice != null && choice.IsOptional)
                    _designerThemeCombo.SelectedItem = "Steampunk";
            };
            panel.Controls.Add(_designerWidgetCombo);
            panel.Controls.Add(_designerThemeCombo);

            var open = MakePrimaryButton(
                "Open Selected Widget in Designer", 24, 174, 286);
            open.Name = "LaunchSelectedDesignerButton";
            open.Click += delegate { LaunchSelectedDesigner(); };
            panel.Controls.Add(open);

            panel.Controls.Add(new Label
            {
                Text = "Create a separate design from Widget Templates in the sidebar.",
                ForeColor = Color.DimGray,
                AutoSize = true,
                Location = new Point(24, 245)
            });
            RefreshDesignerChoices();
        }

        private void BuildDockPage(Panel page)
        {
            AddHeading(
                page,
                "Docks",
                "Launch and personalize EmilyDesk desktop docks");

            var panel = new Panel
            {
                BackColor = EmilyDeskDesignTokens.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(32, 118),
                Size = new Size(665, 500)
            };
            page.Controls.Add(panel);

            panel.Controls.Add(new Label
            {
                Text = "EmilyDesk Themed Docks",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(24, 22)
            });
            panel.Controls.Add(new Label
            {
                Text =
                    "Built-in and imported themed docks with shared items, " +
                    "hover magnification, auto-hide, and four screen-edge positions.",
                ForeColor = EmilyDeskDesignTokens.Muted,
                AutoSize = false,
                Location = new Point(26, 63),
                Size = new Size(605, 44)
            });
            panel.Controls.Add(new Label
            {
                Text = "STATUS",
                ForeColor = EmilyDeskDesignTokens.Muted,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(26, 122)
            });
            panel.Controls.Add(new Label
            {
                Name = "DockStatusValue",
                Text = "Not running",
                ForeColor = EmilyDeskDesignTokens.Muted,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(26, 145)
            });

            _dockThemeButtonsPanel = new FlowLayoutPanel
            {
                Name = "DockThemeButtonsPanel",
                Location = new Point(26, 190),
                Size = new Size(605, 156),
                WrapContents = true,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            panel.Controls.Add(_dockThemeButtonsPanel);
            RebuildDockThemeButtons();

            var settings = MakeSecondaryButton(
                "Dock Settings", 26, 358, 170);
            settings.Name = "DockSettingsButton";
            settings.Click += delegate
            {
                EmilyDeskDockController.ShowSettings(this);
                RefreshDockControls();
            };
            panel.Controls.Add(settings);

            var close = MakeSecondaryButton(
                "Close Dock", 211, 358, 150);
            close.Name = "CloseDockButton";
            close.Click += delegate
            {
                EmilyDeskDockController.CloseDock();
                RefreshDockControls();
            };
            panel.Controls.Add(close);

            panel.Controls.Add(new Label
            {
                Text =
                    "Use + to add applications, folders, library locations " +
                    "such as Pictures, or webpage links. Right-click a dock " +
                    "item to rename it, change its icon, open its location, " +
                    "or remove it.",
                ForeColor = Color.DimGray,
                AutoSize = false,
                Location = new Point(26, 414),
                Size = new Size(600, 58)
            });
        }

        private void RebuildDockThemeButtons()
        {
            if (_dockThemeButtonsPanel == null) return;
            _dockThemeButtonsPanel.SuspendLayout();
            try
            {
                _dockThemeButtonsPanel.Controls.Clear();
                _dockThemeButtons.Clear();
                foreach (string themeName in XWidgetReborn.WidgetSdk.
                    EmilyDeskThemeCatalog.Names)
                {
                    string selectedTheme = themeName;
                    var button = MakePrimaryButton(
                        "Launch " + selectedTheme + " Dock", 0, 0, 185);
                    button.Margin = new Padding(0, 0, 15, 8);
                    button.Click += delegate
                    {
                        EmilyDeskDockController.ShowDockForTheme(
                            selectedTheme);
                        RefreshDockControls();
                    };
                    _dockThemeButtonsPanel.Controls.Add(button);
                    _dockThemeButtons[selectedTheme] = button;
                }
            }
            finally { _dockThemeButtonsPanel.ResumeLayout(); }
        }

        private void LaunchSelectedDesigner()
        {
            DesignerWidgetChoice choice = _designerWidgetCombo == null
                ? null : _designerWidgetCombo.SelectedItem as
                    DesignerWidgetChoice;
            if (choice == null) return;
            if (choice.IsOptional)
            {
                LaunchOptionalDesigner(choice);
                return;
            }
            string theme = _designerThemeCombo == null ||
                _designerThemeCombo.SelectedItem == null
                ? "Industrial" : _designerThemeCombo.SelectedItem.ToString();
            LaunchDesigner(choice.Kind, theme);
        }

        private void RefreshDesignerChoices()
        {
            if (_designerWidgetCombo == null) return;
            string previous = _designerWidgetCombo.SelectedItem == null
                ? null : _designerWidgetCombo.SelectedItem.ToString();
            _designerWidgetCombo.Items.Clear();
            _designerWidgetCombo.Items.Add(new DesignerWidgetChoice(
                "Weather", "weather", null, null));
            _designerWidgetCombo.Items.Add(new DesignerWidgetChoice(
                "Clock", "clock", null, null));
            _designerWidgetCombo.Items.Add(new DesignerWidgetChoice(
                "Calendar", "calendar", null, null));
            _designerWidgetCombo.Items.Add(new DesignerWidgetChoice(
                "Recycle Bin", "recyclebin", null, null));

            string root = WidgetPackagePaths.ImportedWidgetsRoot;
            if (Directory.Exists(root))
                foreach (string directory in Directory.GetDirectories(root))
                {
                    if (File.Exists(Path.Combine(directory,
                        WidgetPackagePaths.DisabledMarkerFileName))) continue;
                    string manifestPath = Path.Combine(directory,
                        "manifest.json");
                    if (!File.Exists(manifestPath)) continue;
                    try
                    {
                        WidgetPackageManifest manifest = _json.Deserialize<
                            WidgetPackageManifest>(File.ReadAllText(manifestPath));
                        if (manifest == null ||
                            string.IsNullOrWhiteSpace(manifest.assembly) ||
                            string.IsNullOrWhiteSpace(manifest.type) ||
                            manifest.capabilities == null ||
                            !Array.Exists(manifest.capabilities,
                                capability => string.Equals(capability,
                                    "designer",
                                    StringComparison.OrdinalIgnoreCase)))
                            continue;
                        string assembly = Path.GetFullPath(Path.Combine(
                            directory, manifest.assembly));
                        if (!File.Exists(assembly)) continue;
                        _designerWidgetCombo.Items.Add(
                            new DesignerWidgetChoice(
                                string.IsNullOrWhiteSpace(manifest.name)
                                    ? manifest.id : manifest.name,
                                null, assembly, manifest.type));
                    }
                    catch { }
                }
            int selected = 0;
            if (!string.IsNullOrEmpty(previous))
                for (int index = 0;
                    index < _designerWidgetCombo.Items.Count; index++)
                    if (string.Equals(
                        _designerWidgetCombo.Items[index].ToString(), previous,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        selected = index;
                        break;
                    }
            if (_designerWidgetCombo.Items.Count > 0)
                _designerWidgetCombo.SelectedIndex = selected;
        }

        private void LaunchOptionalDesigner(DesignerWidgetChoice choice)
        {
            string designerPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "EmilyDesk.Designer.exe");
            try
            {
                if (!File.Exists(designerPath))
                    throw new FileNotFoundException(
                        "EmilyDesk Designer is not installed.");
                Process.Start(new ProcessStartInfo
                {
                    FileName = designerPath,
                    Arguments = "--optional-widget-assembly \"" +
                        choice.AssemblyPath + "\" --type \"" +
                        choice.TypeName + "\"",
                    WorkingDirectory = Path.GetDirectoryName(designerPath),
                    UseShellExecute = true
                });
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "EmilyDesk Designer",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LaunchDesigner(string widget, string theme)
        {
            string widgetId = string.Equals(widget, "clock",
                StringComparison.OrdinalIgnoreCase) ? "native.clock" :
                string.Equals(widget, "calendar",
                    StringComparison.OrdinalIgnoreCase) ? "native.calendar" :
                string.Equals(widget, "recyclebin",
                    StringComparison.OrdinalIgnoreCase) ? "native.recyclebin" :
                "native.weather";
            EngineClient.OpenWidget(widgetId);

            string designerPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "EmilyDesk.Designer.exe");

            if (!File.Exists(designerPath))
            {
                MessageBox.Show(
                    "EmilyDesk Designer is not installed. Run the latest " +
                    "EmilyDesk installer, then try again.",
                    "EmilyDesk Designer",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = designerPath,
                    Arguments = "--widget \"" + widget +
                        "\" --theme \"" + theme + "\"",
                    WorkingDirectory = Path.GetDirectoryName(designerPath),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "EmilyDesk Designer could not be opened.\r\n\r\n" +
                    ex.Message,
                    "EmilyDesk Designer",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void LaunchTemplatePicker()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "EmilyDesk.Designer.exe");
            try
            {
                if (!File.Exists(path)) throw new FileNotFoundException(
                    "EmilyDesk Designer is not installed. Install this build first.");
                // A template is a draft: do not start or modify a live widget.
                Process.Start(new ProcessStartInfo { FileName = path,
                    Arguments = "--templates", WorkingDirectory = Path.GetDirectoryName(path),
                    UseShellExecute = true });
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "Widget Templates",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BuildSettingsPage(Panel page)
        {
            AddHeading(
                page,
                "Settings",
                "Manage the options available in EmilyDesk");

            var panel = new Panel
            {
                BackColor = EmilyDeskDesignTokens.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(32, 118),
                Size = new Size(665, 312)
            };
            page.Controls.Add(panel);
            panel.Controls.Add(new Label
            {
                Text = "Widget preferences",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(22, 20)
            });
            panel.Controls.Add(new Label
            {
                Text =
                    "Clock, Calendar, Weather, and Recycle Bin settings are available " +
                    "from each widget's context menu.",
                ForeColor = Color.DimGray,
                AutoSize = false,
                Location = new Point(24, 60),
                Size = new Size(600, 48)
            });
            var weatherProviders = MakeSecondaryButton(
                "Weather Providers", 24, 135, 140);
            weatherProviders.Click += delegate
            {
                ShowPage(_weatherPage);
                LoadProviders();
            };
            panel.Controls.Add(weatherProviders);

            var about = MakeSecondaryButton(
                "About EmilyDesk", 176, 135, 140);
            about.Click += delegate { ShowPage(_aboutPage); };
            panel.Controls.Add(about);

            var importTheme = MakePrimaryButton(
                "Import Theme...", 328, 135, 140);
            importTheme.Click += delegate { ImportThemePackage(); };
            panel.Controls.Add(importTheme);

            var importWidget = MakePrimaryButton(
                "Import Widget...", 480, 135, 140);
            importWidget.Click += delegate { ImportWidgetPackage(); };
            panel.Controls.Add(importWidget);

            panel.Controls.Add(new Label
            {
                Text = "Import .emilytheme files for complete visual themes, or .emilywidget files for optional widgets. Neither requires rebuilding or reinstalling EmilyDesk.",
                ForeColor = Color.DimGray,
                AutoSize = false,
                Location = new Point(24, 198),
                Size = new Size(580, 48)
            });

            var openThemes = MakeSecondaryButton(
                "Open Theme Folder", 24, 252, 180);
            openThemes.Click += delegate
            {
                Directory.CreateDirectory(XWidgetReborn.WidgetSdk.
                    EmilyDeskThemeCatalog.ThemesRoot);
                Process.Start("explorer.exe", XWidgetReborn.WidgetSdk.
                    EmilyDeskThemeCatalog.ThemesRoot);
            };
            panel.Controls.Add(openThemes);

            var openWidgets = MakeSecondaryButton(
                "Open Widget Folder", 220, 252, 180);
            openWidgets.Click += delegate
            {
                Directory.CreateDirectory(
                    WidgetPackagePaths.ImportedWidgetsRoot);
                Process.Start(
                    "explorer.exe",
                    WidgetPackagePaths.ImportedWidgetsRoot);
            };
            panel.Controls.Add(openWidgets);
        }

        private void ImportWidgetPackage()
        {
            WidgetPackageInstallResult result = WidgetPackageUi.Import(this);
            if (result == null) return;
            if (_widgetManager != null && !_widgetManager.IsDisposed)
                _widgetManager.Close();
            ShowWidgetManager();
        }

        private void ImportThemePackage()
        {
            using (var dialog = new OpenFileDialog
            {
                Title = "Import EmilyDesk Theme",
                Filter = "EmilyDesk themes (*.emilytheme)|*.emilytheme",
                CheckFileExists = true,
                Multiselect = false
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    ThemePackageInspection inspection = ThemePackageInstaller.Inspect(
                        dialog.FileName);
                    bool replace = inspection.IsInstalled;
                    if (replace && MessageBox.Show(
                        inspection.Name + " is already installed. Replace it with version " +
                        inspection.Version + "?\r\n\r\nPersonal layouts and settings will not be removed.",
                        "Replace Theme", MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) != DialogResult.Yes) return;
                    ThemePackageInspection installed = ThemePackageInstaller.Install(
                        dialog.FileName, replace);
                    if (_widgetManager != null && !_widgetManager.IsDisposed)
                        _widgetManager.Close();
                    WidgetPreviewResolver.Clear();
                    VirtualThumbnailCache.Clear();
                    RebuildDockThemeButtons();
                    EmilyDeskDockController.ShowDockForTheme(installed.Name);
                    RefreshDockControls();
                    MessageBox.Show(installed.Name + " " + installed.Version +
                        " is ready. Its dock is now open, and its widgets are available " +
                        "in Gallery and each widget's Theme menu.",
                        "Theme Imported", MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception error)
                {
                    MessageBox.Show("EmilyDesk could not import this theme.\r\n\r\n" +
                        error.Message, "Theme Import", MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void BuildAboutPage(Panel page)
        {
            AddHeading(
                page,
                "About EmilyDesk",
                "A modern desktop widget platform");

            var panel = new Panel
            {
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(32, 118),
                Size = new Size(665, 225)
            };
            page.Controls.Add(panel);
            panel.Controls.Add(new Label
            {
                Text = "EmilyDesk",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(24, 24)
            });
            panel.Controls.Add(new Label
            {
                Text = "Community Release\r\nVersion " +
                    AppConstants.Version + "\r\n" +
                    AppConstants.BuildDisplay,
                ForeColor = Color.FromArgb(63, 75, 91),
                AutoSize = true,
                Location = new Point(26, 72)
            });
        }

        private void BuildDeveloperToolsPage(Panel page)
        {
            AddHeading(
                page,
                "Developer Tools",
                "Internal diagnostics and compatibility controls");

            var systemHealth = MakeSecondaryButton(
                "System Health", 32, 130, 170);
            systemHealth.Click += delegate { ShowSystemHealth(); };
            page.Controls.Add(systemHealth);

            var diagnostics = MakeSecondaryButton(
                "Drag Diagnostics", 216, 130, 170);
            diagnostics.Click += delegate { ShowDragDiagnostics(); };
            page.Controls.Add(diagnostics);

            var weatherTest = MakeSecondaryButton(
                "Test Weather", 32, 190, 170);
            weatherTest.Click += delegate { RunWeatherTest(); };
            page.Controls.Add(weatherTest);

            var refresh = MakeSecondaryButton(
                "Refresh Status", 216, 190, 170);
            refresh.Click += delegate { RefreshStatus(); };
            page.Controls.Add(refresh);
        }

        private Label[] BuildWeatherPage(Panel page)
        {
            AddHeading(
                page,
                "Weather providers",
                "Provider changes do not require editing existing skins");

            var panel = new Panel
            {
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(32, 118),
                Size = new Size(890, 440)
            };
            page.Controls.Add(panel);

            panel.Controls.Add(new Label
            {
                Text = "Active provider",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(24, 24)
            });

            var combo = new ComboBox
            {
                Name = "ProviderCombo",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(24, 58),
                Width = 280
            };
            panel.Controls.Add(combo);
            page.Controls.Add(combo);
            combo.Parent = panel;

            var apply = MakePrimaryButton(
                "Use selected provider", 324, 52, 190);
            apply.Click += delegate { SelectProvider(); };
            panel.Controls.Add(apply);

            Label provider = AddDetail(
                panel, "Provider:", "Checking...", 24, 118);
            Label health = AddDetail(
                panel, "Provider health:", "Checking...", 24, 158);
            Label cache = AddDetail(
                panel, "Outage fallback:", "Checking...", 24, 198);

            panel.Controls.Add(new Label
            {
                Text = "API key (only for providers that require one)",
                AutoSize = true,
                Location = new Point(24, 242)
            });

            var apiKey = new TextBox
            {
                Name = "ProviderApiKey",
                UseSystemPasswordChar = true,
                Location = new Point(24, 265),
                Size = new Size(330, 24)
            };
            panel.Controls.Add(apiKey);

            var saveApiKey = MakeSecondaryButton(
                "Save API key", 370, 260, 145);
            saveApiKey.Click += delegate { SaveProviderApiKey(); };
            panel.Controls.Add(saveApiKey);

            var getFreeKey = MakeSecondaryButton(
                "Get free API key", 530, 260, 160);
            getFreeKey.Click += delegate { OpenWeatherApiSignup(); };
            panel.Controls.Add(getFreeKey);

            panel.Controls.Add(new Label
            {
                Name = "ProviderApiKeyStatus",
                Text = "Select a provider to view its API-key status.",
                ForeColor = Color.DimGray,
                AutoSize = false,
                Location = new Point(24, 301),
                Size = new Size(730, 22)
            });

            panel.Controls.Add(new Label
            {
                Text =
                    "WeatherAPI.com setup:\r\n" +
                    "1. Click Get free API key and create a free account.\r\n" +
                    "2. Verify your email, log in, and copy the key from your account dashboard.\r\n" +
                    "3. Paste it above, click Save API key, then select WeatherAPI.com. Never share your key.",
                ForeColor = Color.DimGray,
                AutoSize = false,
                Location = new Point(24, 336),
                Size = new Size(820, 78)
            });

            var refresh = MakeSecondaryButton(
                "Refresh provider status", 32, 580, 190);
            refresh.Click += delegate { LoadProviders(); };
            page.Controls.Add(refresh);

            var reload = MakeSecondaryButton(
                "Reload provider profiles", 236, 580, 200);
            reload.Click += delegate { ReloadProviderProfiles(); };
            page.Controls.Add(reload);

            return new[] { provider, health, cache };
        }

        private static void AddHeading(
            Control page,
            string title,
            string subtitle)
        {
            page.Controls.Add(new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(32, 26)
            });

            page.Controls.Add(new Label
            {
                Text = subtitle,
                ForeColor = Color.DimGray,
                AutoSize = true,
                Location = new Point(35, 74)
            });
        }

        private static Label AddStatusCard(
            Control parent,
            string title,
            int left,
            int top,
            string name)
        {
            var panel = new Panel
            {
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(left, top),
                Size = new Size(205, 120)
            };
            parent.Controls.Add(panel);

            panel.Controls.Add(new Label
            {
                Text = title,
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, 16)
            });

            var value = new Label
            {
                Name = name,
                Text = "Checking...",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(16, 50),
                Size = new Size(172, 48)
            };
            panel.Controls.Add(value);
            parent.Controls.Add(value);
            value.Parent = panel;
            return value;
        }

        private static Label AddDetail(
            Control parent,
            string caption,
            string value,
            int left,
            int top)
        {
            parent.Controls.Add(new Label
            {
                Text = caption,
                AutoSize = true,
                Location = new Point(left, top)
            });

            var result = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(left + 145, top)
            };
            parent.Controls.Add(result);
            return result;
        }

        private static Button AddNavButton(
            Panel parent,
            string text,
            int top,
            EventHandler click,
            bool selected)
        {
            var button = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Width = 188,
                Height = 40,
                Location = new Point(16, top),
                ForeColor = Color.White,
                BackColor = selected
                    ? Color.FromArgb(37, 111, 181)
                    : Color.FromArgb(22, 55, 92),
                Font = new Font(
                    "Segoe UI",
                    10F,
                    selected ? FontStyle.Bold : FontStyle.Regular)
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += click;
            parent.Controls.Add(button);
            return button;
        }

        private static Button MakePrimaryButton(
            string text,
            int left,
            int top,
            int width)
        {
            var button = new Button
            {
                Text = text,
                Location = new Point(left, top),
                Size = new Size(width, 44),
                BackColor = Color.FromArgb(37, 111, 181),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private static Button MakeSecondaryButton(
            string text,
            int left,
            int top,
            int width)
        {
            return new Button
            {
                Text = text,
                Location = new Point(left, top),
                Size = new Size(width, 44),
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 10F)
            };
        }

        private static T FindControlRecursive<T>(
            Control parent,
            string name) where T : Control
        {
            if (parent == null)
                throw new ArgumentNullException("parent");

            foreach (Control control in parent.Controls)
            {
                if (control is T &&
                    string.Equals(
                        control.Name,
                        name,
                        StringComparison.Ordinal))
                {
                    return (T)control;
                }

                T nested = FindControlRecursiveOrNull<T>(control, name);
                if (nested != null)
                    return nested;
            }

            throw new InvalidOperationException(
                "Required UI control was not found: " + name);
        }

        private static T FindControlRecursiveOrNull<T>(
            Control parent,
            string name) where T : Control
        {
            foreach (Control control in parent.Controls)
            {
                if (control is T &&
                    string.Equals(
                        control.Name,
                        name,
                        StringComparison.Ordinal))
                {
                    return (T)control;
                }

                T nested = FindControlRecursiveOrNull<T>(control, name);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private void ShowPage(Panel page)
        {
            _dashboardPage.Visible = page == _dashboardPage;
            _weatherPage.Visible = page == _weatherPage;
            _dockPage.Visible = page == _dockPage;
            _designerPage.Visible = page == _designerPage;
            _settingsPage.Visible = page == _settingsPage;
            _aboutPage.Visible = page == _aboutPage;
            _developerToolsPage.Visible =
                page == _developerToolsPage;
            page.BringToFront();
        }

        private void RefreshDockControls()
        {
            if (_dockStatusValue == null) return;
            bool running = EmilyDeskDockController.IsRunning;
            _dockStatusValue.Text =
                EmilyDeskDockController.StatusText;
            _dockStatusValue.ForeColor = running
                ? EmilyDeskDesignTokens.Success
                : EmilyDeskDesignTokens.Muted;
            foreach (KeyValuePair<string, Button> item in
                _dockThemeButtons)
                SetDockLaunchEnabled(item.Value, item.Key, running);
            _closeDockButton.Enabled = running;
        }

        private static void SetDockLaunchEnabled(
            Button button, string theme, bool running)
        {
            if (button == null) return;
            button.Enabled = !running || !string.Equals(
                EmilyDeskDockController.CurrentThemeName,
                theme, StringComparison.OrdinalIgnoreCase);
        }

#if false
        private void RefreshLegacyRuntimeStatus()
        {
            LegacyRuntimeManager.Status legacyStatus =
                LegacyRuntimeManager.GetStatus();

            SetCard(
                _xwidgetValue,
                !legacyStatus.IsInstalled
                    ? "Not installed"
                    : legacyStatus.IsRunning
                        ? "Running (on demand)"
                        : "Installed — stopped",
                legacyStatus.IsInstalled);

            _pathValue.Text = legacyStatus.IsInstalled
                ? "Legacy compatibility: " +
                    (legacyStatus.IsRunning ? "running on demand" : "ready on demand") +
                    ". " + legacyStatus.ExecutablePath
                : "Legacy XWidget is not installed. Native EmilyDesk widgets remain fully available.";
        }

#endif
        private bool RefreshStatus()
        {
            Dictionary<string, object> status = GetStatus();
            bool serviceOk = status != null;
            bool apiOk = status != null;
            bool endpointOk = SecureLoopbackEndpointConfigured();
            bool engineOk = ReadEngineStatus() != null;
            SetCard(
                _weatherValue,
                serviceOk && apiOk && endpointOk
                    ? "Ready"
                    : "Needs attention",
                serviceOk && apiOk && endpointOk);

            _attentionValue.Text = !engineOk
                ? "The EmilyDesk Engine is offline."
                : !(serviceOk && apiOk && endpointOk)
                    ? "Weather needs attention. Open Weather Providers for details."
                    : string.Empty;

            LogDashboardStatus(
                serviceOk,
                apiOk,
                endpointOk,
                "disabled in EmilyDesk V1",
                status);

            return serviceOk && apiOk;
        }

        private void LoadProviders()
        {
            try
            {
                using (var client = new TimeoutWebClient(4000))
                {
                    client.Encoding = Encoding.UTF8;
                    string text = client.DownloadString(
                        AppConstants.BridgeBaseUrl +
                        "/xwidgetbridge/providers.json");

                    Dictionary<string, object> data =
                        _json.Deserialize<Dictionary<string, object>>(text);

                    _providerCombo.Items.Clear();
                    ArrayList providers = data["providers"] as ArrayList;
                    if (providers != null)
                    {
                        foreach (object item in providers)
                        {
                            Dictionary<string, object> provider =
                                item as Dictionary<string, object>;
                            if (provider == null)
                                continue;

                            _providerCombo.Items.Add(new ProviderItem(
                                Convert.ToString(provider["id"]),
                                Convert.ToString(provider["name"]),
                                provider.ContainsKey("requiresApiKey") &&
                                    Convert.ToBoolean(provider["requiresApiKey"]),
                                provider.ContainsKey("hasApiKey") &&
                                    Convert.ToBoolean(provider["hasApiKey"])));
                        }
                    }

                    string activeId =
                        Convert.ToString(data["activeProviderId"]);
                    for (int i = 0; i < _providerCombo.Items.Count; i++)
                    {
                        ProviderItem item =
                            (ProviderItem)_providerCombo.Items[i];
                        if (string.Equals(
                            item.Id,
                            activeId,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            _providerCombo.SelectedIndex = i;
                            break;
                        }
                    }

                    _providerValue.Text =
                        Convert.ToString(data["activeProvider"]);
                    _cacheModeValue.Text =
                        Convert.ToBoolean(data["allowStaleCache"])
                        ? "Cached weather for up to " +
                          Convert.ToString(data["staleCacheHours"]) +
                          " hours"
                        : "Disabled";
                    UpdateProviderApiKeyStatus();
                }

                Dictionary<string, object> status = GetStatus();
                if (status != null)
                {
                    bool stale = status.ContainsKey("usingStaleCache") &&
                        Convert.ToBoolean(status["usingStaleCache"]);
                    string error = status.ContainsKey("providerLastError")
                        ? Convert.ToString(status["providerLastError"])
                        : "";

                    _providerHealthValue.Text = stale
                        ? "Using cached weather"
                        : string.IsNullOrWhiteSpace(error)
                            ? "Healthy"
                            : "Last request failed";
                    _providerHealthValue.ForeColor =
                        string.IsNullOrWhiteSpace(error)
                            ? Color.FromArgb(24, 126, 72)
                            : Color.FromArgb(183, 28, 28);
                }
            }
            catch (Exception ex)
            {
                _providerValue.Text = "Unavailable";
                _providerHealthValue.Text = ex.Message;
                _cacheModeValue.Text = "Unknown";
            }
        }

        private void UpdateProviderApiKeyStatus()
        {
            if (_providerApiKeyStatus == null || _providerCombo == null)
                return;

            ProviderItem selected = _providerCombo.SelectedItem as ProviderItem;
            if (selected == null)
            {
                _providerApiKeyStatus.Text =
                    "Select a provider to view its API-key status.";
                return;
            }

            if (!selected.RequiresApiKey)
            {
                _providerApiKeyStatus.Text = selected.Name +
                    " does not require an API key.";
                return;
            }

            _providerApiKeyStatus.Text = selected.HasApiKey
                ? "A protected API key is saved for " + selected.Name + "."
                : selected.Name +
                    " needs a free API key. Click Get free API key to begin.";
        }

        private static void OpenWeatherApiSignup()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.weatherapi.com/signup.aspx",
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show(
                    "Open https://www.weatherapi.com/signup.aspx to create " +
                    "a free WeatherAPI.com account.",
                    "EmilyDesk", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void SaveProviderApiKey()
        {
            ProviderItem selected = _providerCombo.SelectedItem as ProviderItem;
            if (selected == null || !selected.RequiresApiKey)
            {
                MessageBox.Show(
                    "Select a provider that requires an API key first.",
                    "EmilyDesk", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                using (var client = new TimeoutWebClient(4000))
                {
                    var values = new NameValueCollection();
                    values["id"] = selected.Id;
                    values["key"] = _providerApiKey.Text ?? string.Empty;
                    client.UploadValues(
                        AppConstants.BridgeBaseUrl +
                        "/xwidgetbridge/provider/key",
                        "POST", values);
                }

                _providerApiKey.Text = string.Empty;
                LoadProviders();
                EngineClient.RefreshWeatherWidgets();
                MessageBox.Show(
                    "The API key was saved for " + selected.Name +
                    ".\r\n\r\nIt is protected for this Windows account.",
                    "EmilyDesk", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The API key could not be saved.\r\n\r\n" + ex.Message,
                    "EmilyDesk", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ReloadProviderProfiles()
        {
            try
            {
                using (var client = new TimeoutWebClient(4000))
                {
                    client.DownloadString(
                        AppConstants.BridgeBaseUrl +
                        "/xwidgetbridge/provider/reload");
                }

                LoadProviders();
                MessageBox.Show(
                    "Provider profiles were reloaded.\r\n\r\n" +
                    "Existing skins remain unchanged.",
                    "EmilyDesk",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Provider profiles could not be reloaded.\r\n\r\n" +
                    ex.Message,
                    "EmilyDesk",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ShowSystemHealth()
        {
            Dictionary<string, object> status = GetStatus();
            if (status == null)
            {
                MessageBox.Show(
                    "The local weather engine is not responding.\r\n\r\n" +
                    "The user-mode Weather Engine may not have started, or the local " +
                    "compatibility endpoint may be unavailable. Restart " +
                    "EmilyDesk and refresh this page.",
                    "System health",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string provider = Value(status, "provider", "Unknown");
            string lastError = Value(status, "providerLastError", "");
            bool stale = BoolValue(status, "usingStaleCache");
            long uptime = LongValue(status, "serviceUptimeSeconds");
            string lastLive = Value(status, "providerLastLiveFetchUtc", "");
            string profiles = Value(status, "loadedProviderProfiles", "0");

            string explanation;
            MessageBoxIcon icon;

            if (stale)
            {
                explanation =
                    "The provider is temporarily unavailable. EmilyDesk " +
                    "is serving recently cached weather so your skins do not " +
                    "go blank.";
                icon = MessageBoxIcon.Warning;
            }
            else if (!string.IsNullOrWhiteSpace(lastError))
            {
                explanation =
                    "The most recent provider request failed. The Weather Engine is " +
                    "still running. Error: " + lastError;
                icon = MessageBoxIcon.Warning;
            }
            else
            {
                explanation =
                    "The service and weather provider are operating normally.";
                icon = MessageBoxIcon.Information;
            }

            string message =
                explanation + "\r\n\r\n" +
                "Provider: " + provider + "\r\n" +
                "Service uptime: " + FormatDuration(uptime) + "\r\n" +
                "Last live weather: " +
                    (string.IsNullOrWhiteSpace(lastLive)
                        ? "Not recorded yet"
                        : lastLive) + "\r\n" +
                "Loaded provider profiles: " + profiles + "\r\n" +
                "Automatic startup: Windows service";

            MessageBox.Show(
                message,
                "EmilyDesk system health",
                MessageBoxButtons.OK,
                icon);
        }

        private static string Value(
            Dictionary<string, object> data,
            string key,
            string fallback)
        {
            object value;
            return data != null &&
                   data.TryGetValue(key, out value) &&
                   value != null
                ? Convert.ToString(value)
                : fallback;
        }

        private static bool BoolValue(
            Dictionary<string, object> data,
            string key)
        {
            object value;
            return data != null &&
                   data.TryGetValue(key, out value) &&
                   value != null &&
                   Convert.ToBoolean(value);
        }

        private static long LongValue(
            Dictionary<string, object> data,
            string key)
        {
            object value;
            long result;
            return data != null &&
                   data.TryGetValue(key, out value) &&
                   value != null &&
                   long.TryParse(Convert.ToString(value), out result)
                ? result
                : 0;
        }

        private static string FormatDuration(long seconds)
        {
            TimeSpan duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
            if (duration.TotalDays >= 1)
                return ((int)duration.TotalDays) + " day(s), " +
                       duration.Hours + " hour(s)";
            if (duration.TotalHours >= 1)
                return ((int)duration.TotalHours) + " hour(s), " +
                       duration.Minutes + " minute(s)";
            return duration.Minutes + " minute(s), " +
                   duration.Seconds + " second(s)";
        }

        private void SelectProvider()
        {
            ProviderItem selected = _providerCombo.SelectedItem as ProviderItem;
            if (selected == null)
                return;

            try
            {
                using (var client = new TimeoutWebClient(4000))
                {
                    client.DownloadString(
                        AppConstants.BridgeBaseUrl +
                        "/xwidgetbridge/provider/select?id=" +
                        Uri.EscapeDataString(selected.Id));
                }

                LoadProviders();
                bool refreshQueued = EngineClient.RefreshWeatherWidgets();
                MessageBox.Show(
                    "Active provider: " + selected.Name +
                    "\r\n\r\n" + (refreshQueued
                        ? "Open Weather widgets are refreshing now."
                        : "The provider was saved. Open Weather widgets will refresh shortly.") +
                    "\r\n\r\nExisting skins do not need to be edited.",
                    "EmilyDesk",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The provider could not be selected.\r\n\r\n" + ex.Message,
                    "EmilyDesk",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private Dictionary<string, object> GetStatus()
        {
            try
            {
                using (var client = new TimeoutWebClient(4000))
                {
                    client.Encoding = Encoding.UTF8;
                    string text = client.DownloadString(
                        AppConstants.BridgeBaseUrl +
                        "/xwidgetbridge/status.json");
                    return _json.Deserialize<Dictionary<string, object>>(text);
                }
            }
            catch
            {
                return null;
            }
        }

        private static void SetCard(Label label, string text, bool good)
        {
            if (label == null)
                return;

            label.Text = text;
            label.ForeColor = good
                ? Color.FromArgb(24, 126, 72)
                : Color.FromArgb(183, 28, 28);
        }

        private static bool IsServiceRunning()
        {
            try
            {
                using (var service = new ServiceController(ServiceName))
                {
                    service.Refresh();
                    return service.Status ==
                        ServiceControllerStatus.Running;
                }
            }
            catch { return false; }
        }

        private static bool SecureLoopbackEndpointConfigured()
        {
            try
            {
                return new Uri(AppConstants.ListenerPrefix).IsLoopback;
            }
            catch { return false; }
        }

        internal static string FindXWidgetExecutable()
        {
            return XWidgetLocator.FindExecutable();
        }


        private void RefreshEngineControls()
        {
            Dictionary<string, string> status =
                ReadEngineStatus();
            if (status != null)
            {
                _engineKnownOnline = true;
                _consecutiveEngineStatusFailures = 0;
                _lastNativeRunningCount =
                    CountNativeOpenWidgets(status);
            }
            else if (_engineKnownOnline)
            {
                _consecutiveEngineStatusFailures++;
                if (_consecutiveEngineStatusFailures >= 3)
                {
                    _engineKnownOnline = false;
                    _lastNativeRunningCount = 0;
                }
            }

            bool running = _engineKnownOnline;

            SetCard(
                _engineStatusValue,
                running ? "Online" : "Offline",
                running);

            SetCard(
                _nativeWidgetsValue,
                _lastNativeRunningCount.ToString(),
                true);

            _startEngineButton.Visible = !running;
            _startEngineButton.Enabled = !running;
            _widgetsNavButton.Enabled = running;

            const string engineOffline =
                "The EmilyDesk Engine is offline.";
            if (!running)
                _attentionValue.Text = engineOffline;
            else if (string.Equals(
                _attentionValue.Text,
                engineOffline,
                StringComparison.Ordinal))
                _attentionValue.Text = string.Empty;
        }

        private static int CountNativeOpenWidgets(
            Dictionary<string, string> status)
        {
            string openWidgets;
            if (status == null ||
                !status.TryGetValue(
                    "widgets",
                    out openWidgets) ||
                string.IsNullOrWhiteSpace(openWidgets))
                return 0;

            int count = 0;
            foreach (string widgetId in
                openWidgets.Split(','))
            {
                if (widgetId.Trim().StartsWith(
                    "native.",
                    StringComparison.OrdinalIgnoreCase))
                    count++;
            }
            return count;
        }

        internal void ShowWidgetManager()
        {
            if (_widgetManager != null &&
                !_widgetManager.IsDisposed)
            {
                if (_widgetManager.WindowState ==
                    FormWindowState.Minimized)
                    _widgetManager.WindowState =
                        FormWindowState.Normal;
                _widgetManager.BringToFront();
                _widgetManager.Activate();
                return;
            }

            Dictionary<string, string> status = ReadEngineStatus();
            if (status == null)
            {
            MessageBox.Show("The EmilyDesk Engine is offline.", "EmilyDesk Gallery",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Stopwatch discovery = Stopwatch.StartNew();
            IList<WidgetDescriptor> widgets =
                EngineClient.ListAvailableWidgets();
            DragDiagnosticTrace.RecordDuration(
                "Production Virtual Gallery",
                "widget discovery",
                discovery.ElapsedMilliseconds,
                "testName=Production Virtual Gallery");
            if (widgets.Count == 0)
            {
            MessageBox.Show("The Engine did not report any registered widgets.", "EmilyDesk Gallery",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _widgetManager = new VirtualWidgetManagerForm(widgets);
            _widgetManager.FormClosed += delegate
            {
                _widgetManager = null;
            };
            _widgetManager.Show();
        }

        internal void ShowSettings()
        {
            ShowPage(_settingsPage);
            BringToFront();
            Activate();
        }

        internal void ShowAbout()
        {
            ShowPage(_aboutPage);
            BringToFront();
            Activate();
        }

        private void ShowDragDiagnostics()
        {
            if (_dragDiagnostics != null &&
                !_dragDiagnostics.IsDisposed)
            {
                _dragDiagnostics.BringToFront();
                _dragDiagnostics.Activate();
                return;
            }

            _dragDiagnostics = new DragDiagnosticLauncher(
                ShowPlainDragTest,
                ShowFormOnlyGalleryTest,
                ShowHeaderOnlyGalleryTest,
                ShowCompleteGalleryTest,
                ShowGalleryC1,
                ShowGalleryC2,
                ShowGalleryC3,
                ShowGalleryC4,
                ShowGalleryC5,
                ShowNormalGalleryDiagnostic,
                ShowGalleryD1,
                ShowGalleryD2,
                ShowGalleryD3);
            _dragDiagnostics.FormClosed += delegate
            {
                _dragDiagnostics = null;
            };
            _dragDiagnostics.Show();
        }

        private void ShowPlainDragTest()
        {
            if (_plainDragTest != null &&
                !_plainDragTest.IsDisposed)
            {
                if (_plainDragTest.WindowState ==
                    FormWindowState.Minimized)
                    _plainDragTest.WindowState =
                        FormWindowState.Normal;
                _plainDragTest.BringToFront();
                _plainDragTest.Activate();
                return;
            }

            var form = new Form
            {
                Text = "EmilyDesk Drag Test — Plain Form",
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.Sizable,
                ClientSize = new Size(1080, 720),
                TopMost = false,
                ShowInTaskbar = true
            };
            form.Controls.Add(new Label
            {
                AutoSize = true,
                Location = new Point(18, 18),
                Text = "Begin every drag from the actual Windows title bar " +
                    "above this content."
            });
            _plainDragTest = form;
            form.FormClosed += delegate
            {
                _plainDragTest = null;
            };
            DragDiagnosticTrace.Attach(
                form,
                "TEST A — Plain Form",
                delegate { return false; },
                delegate { return false; });
            form.Show();
        }

        private void ShowFormOnlyGalleryTest()
        {
            if (_formOnlyGalleryTest != null &&
                !_formOnlyGalleryTest.IsDisposed)
            {
                if (_formOnlyGalleryTest.WindowState ==
                    FormWindowState.Minimized)
                    _formOnlyGalleryTest.WindowState =
                        FormWindowState.Normal;
                _formOnlyGalleryTest.BringToFront();
                _formOnlyGalleryTest.Activate();
                return;
            }

            _formOnlyGalleryTest =
                WidgetManagerForm.CreateDiagnosticFormOnly();
            _formOnlyGalleryTest.FormClosed += delegate
            {
                _formOnlyGalleryTest = null;
            };
            DragDiagnosticTrace.Attach(
                _formOnlyGalleryTest,
                "TEST B1 — Form Properties Only",
                delegate { return false; },
                delegate { return false; });
            _formOnlyGalleryTest.Show();
        }

        private void ShowHeaderOnlyGalleryTest()
        {
            if (_headerOnlyGalleryTest != null &&
                !_headerOnlyGalleryTest.IsDisposed)
            {
                if (_headerOnlyGalleryTest.WindowState ==
                    FormWindowState.Minimized)
                    _headerOnlyGalleryTest.WindowState =
                        FormWindowState.Normal;
                _headerOnlyGalleryTest.BringToFront();
                _headerOnlyGalleryTest.Activate();
                return;
            }

            _headerOnlyGalleryTest =
                WidgetManagerForm.CreateDiagnosticHeaderOnly();
            _headerOnlyGalleryTest.FormClosed += delegate
            {
                _headerOnlyGalleryTest = null;
            };
            DragDiagnosticTrace.Attach(
                _headerOnlyGalleryTest,
                "TEST B2 — Header Only",
                delegate { return false; },
                delegate { return false; });
            _headerOnlyGalleryTest.Show();
        }

        private void ShowCompleteGalleryTest()
        {
            if (_completeGalleryTest != null &&
                !_completeGalleryTest.IsDisposed)
            {
                if (_completeGalleryTest.WindowState ==
                    FormWindowState.Minimized)
                    _completeGalleryTest.WindowState =
                        FormWindowState.Normal;
                _completeGalleryTest.BringToFront();
                _completeGalleryTest.Activate();
                return;
            }

            _completeGalleryTest =
                WidgetManagerForm.CreateDiagnosticCompleteShell();
            _completeGalleryTest.FormClosed += delegate
            {
                _completeGalleryTest = null;
            };
            DragDiagnosticTrace.Attach(
                _completeGalleryTest,
                "TEST B3 — Complete Empty Shell",
                delegate { return false; },
                delegate { return false; });
            _completeGalleryTest.Show();
        }

        private void ShowGalleryC1()
        {
            ShowGallerySubsystemTest(
                WidgetManagerForm.GalleryTestMode.CardsNoPreviews,
                "TEST C1 — Cards, No Previews");
        }

        private void ShowGalleryC2()
        {
            ShowGallerySubsystemTest(
                WidgetManagerForm.GalleryTestMode.PreviewsNoState,
                "TEST C2 — Previews, No Running-State Polling");
        }

        private void ShowGalleryC3()
        {
            ShowGallerySubsystemTest(
                WidgetManagerForm.GalleryTestMode.PreloadedPreviews,
                "TEST C3 — Previews Loaded Before Display");
        }

        private void ShowGalleryC4()
        {
            ShowGallerySubsystemTest(
                WidgetManagerForm.GalleryTestMode.SinglePreviewApply,
                "TEST C4 — One Preview Application Pass");
        }

        private void ShowGalleryC5()
        {
            ShowGallerySubsystemTest(
                WidgetManagerForm.GalleryTestMode.StateSnapshot,
                "TEST C5 — Running-State Snapshot Only");
        }

        private void ShowGalleryD1()
        {
            ShowGallerySubsystemTest(
                WidgetManagerForm.GalleryTestMode.NativeCards25,
                "TEST D1 — 25 Native Cards");
        }

        private void ShowGalleryD2()
        {
            ShowGallerySubsystemTest(
                WidgetManagerForm.GalleryTestMode.NativeCards100,
                "TEST D2 — 100 Native Cards");
        }

        private void ShowGalleryD3()
        {
            if (_virtualGalleryTest != null &&
                !_virtualGalleryTest.IsDisposed)
            {
                if (_virtualGalleryTest.WindowState ==
                    FormWindowState.Minimized)
                    _virtualGalleryTest.WindowState =
                        FormWindowState.Normal;
                _virtualGalleryTest.BringToFront();
                _virtualGalleryTest.Activate();
                return;
            }

            const string testName =
                "TEST D3 — Single Virtual Gallery Surface";
            Stopwatch discovery = Stopwatch.StartNew();
            IList<WidgetDescriptor> widgets =
                EngineClient.ListAvailableWidgets();
            DragDiagnosticTrace.RecordDuration(
                testName,
                "widget discovery",
                discovery.ElapsedMilliseconds,
                "testName=" + testName);
            if (widgets.Count == 0)
            {
                MessageBox.Show(
                    "The Engine did not report any registered widgets.",
                    testName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            _virtualGalleryTest =
                new VirtualGalleryDiagnosticForm(widgets);
            VirtualGalleryDiagnosticForm gallery =
                _virtualGalleryTest;
            gallery.FormClosed += delegate
            {
                if (ReferenceEquals(
                    _virtualGalleryTest,
                    gallery))
                    _virtualGalleryTest = null;
            };
            DragDiagnosticTrace.Attach(
                gallery,
                testName,
                delegate { return false; },
                delegate { return false; },
                delegate
                {
                    return gallery.IsDisposed
                        ? string.Empty
                        : gallery.DiagnosticSubsystemState;
                });
            gallery.Show();
        }

        private void ShowGallerySubsystemTest(
            WidgetManagerForm.GalleryTestMode mode,
            string testName)
        {
            WidgetManagerForm existing;
            if (_gallerySubsystemTests.TryGetValue(
                mode,
                out existing) &&
                existing != null &&
                !existing.IsDisposed)
            {
                if (existing.WindowState ==
                    FormWindowState.Minimized)
                    existing.WindowState =
                        FormWindowState.Normal;
                existing.BringToFront();
                existing.Activate();
                return;
            }

            Stopwatch discovery = Stopwatch.StartNew();
            IList<WidgetDescriptor> widgets =
                EngineClient.ListAvailableWidgets();
            DragDiagnosticTrace.RecordDuration(
                testName,
                "widget discovery",
                discovery.ElapsedMilliseconds,
                "testName=" + testName);
            if (widgets.Count == 0)
            {
                MessageBox.Show(
                    "The Engine did not report any registered widgets.",
                    testName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            WidgetManagerForm gallery =
                WidgetManagerForm.CreateGalleryDiagnostic(
                    widgets,
                    mode,
                    testName);
            _gallerySubsystemTests[mode] = gallery;
            gallery.FormClosed += delegate
            {
                WidgetManagerForm current;
                if (_gallerySubsystemTests.TryGetValue(
                    mode,
                    out current) &&
                    ReferenceEquals(current, gallery))
                    _gallerySubsystemTests.Remove(mode);
            };
            AttachGalleryDiagnostic(gallery, testName);
            gallery.Show();
        }

        private void ShowNormalGalleryDiagnostic()
        {
            if (_normalGalleryDiagnostic != null &&
                !_normalGalleryDiagnostic.IsDisposed)
            {
                if (_normalGalleryDiagnostic.WindowState ==
                    FormWindowState.Minimized)
                    _normalGalleryDiagnostic.WindowState =
                        FormWindowState.Normal;
                _normalGalleryDiagnostic.BringToFront();
                _normalGalleryDiagnostic.Activate();
                return;
            }

            if (ReadEngineStatus() == null)
            {
                MessageBox.Show(
                    "The EmilyDesk Engine is offline.",
                    "TEST C — Normal Gallery",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            Stopwatch discovery = Stopwatch.StartNew();
            IList<WidgetDescriptor> widgets =
                EngineClient.ListAvailableWidgets();
            DragDiagnosticTrace.RecordDuration(
                "TEST C — Normal Gallery",
                "widget discovery",
                discovery.ElapsedMilliseconds,
                "testName=TEST C — Normal Gallery");
            if (widgets.Count == 0)
            {
                MessageBox.Show(
                    "The Engine did not report any registered widgets.",
                    "TEST C — Normal Gallery",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            _normalGalleryDiagnostic =
                new WidgetManagerForm(widgets);
            WidgetManagerForm gallery =
                _normalGalleryDiagnostic;
            gallery.FormClosed += delegate
            {
                if (ReferenceEquals(
                    _normalGalleryDiagnostic,
                    gallery))
                    _normalGalleryDiagnostic = null;
            };
            const string instruction =
                "Begin every drag from the actual Windows title bar " +
                "above this content.";
            if (!gallery.Text.Contains(instruction))
                gallery.Text += " — " + instruction;
            AttachGalleryDiagnostic(
                gallery,
                "TEST C — Normal Gallery");
            gallery.Show();
        }

        private static void AttachGalleryDiagnostic(
            WidgetManagerForm gallery,
            string testName)
        {
            DragDiagnosticTrace.Attach(
                gallery,
                testName,
                delegate
                {
                    return !gallery.IsDisposed &&
                        gallery.DiagnosticPreviewLoading;
                },
                delegate
                {
                    return !gallery.IsDisposed &&
                        gallery.DiagnosticStatePolling;
                },
                delegate
                {
                    return gallery.IsDisposed
                        ? string.Empty
                        : gallery.DiagnosticSubsystemState;
                });
        }

        private static void CloseDiagnosticWindow(Form form)
        {
            if (form != null && !form.IsDisposed)
                form.Close();
        }

        private static void EnsureEngineRunning()
        {
            Dictionary<string, string> status = ReadEngineStatus();
            if (status != null) return;
            string runtime = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppConstants.RuntimeExecutableName);
            if (File.Exists(runtime))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = runtime,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = false
                });
                System.Threading.Thread.Sleep(500);
            }
        }

        private static void SignalEngine(string eventName)
        {
            try
            {
                using (EventWaitHandle signal = EventWaitHandle.OpenExisting(eventName)) signal.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
            MessageBox.Show("The EmilyDesk Engine is still starting. Try again in a moment.",
                    "EmilyDesk", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static Dictionary<string, string> ReadEngineStatus()
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    string path = Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder
                                .LocalApplicationData),
                        AppConstants.ProductName,
                        AppConstants.RuntimeStatusFileName);
                    if (!File.Exists(path))
                        return null;

                    var values =
                        new Dictionary<string, string>(
                            StringComparer.OrdinalIgnoreCase);
                    foreach (string line in
                        File.ReadAllLines(path))
                    {
                        int equals = line.IndexOf('=');
                        if (equals > 0)
                            values[line.Substring(
                                0,
                                equals)] =
                                line.Substring(equals + 1);
                    }

                    int pid;
                    if (!values.ContainsKey("pid") ||
                        !int.TryParse(
                            values["pid"],
                            out pid))
                        return null;

                    using (Process process =
                        Process.GetProcessById(pid))
                    {
                        if (process.HasExited ||
                            !string.Equals(
                                process.ProcessName,
                                Path.GetFileNameWithoutExtension(
                                    AppConstants
                                        .RuntimeExecutableName),
                                StringComparison
                                    .OrdinalIgnoreCase))
                            return null;
                    }

                    using (EventWaitHandle commandEvent =
                        EventWaitHandle.OpenExisting(
                            AppConstants.RuntimeCommandEvent))
                    {
                    }
                    return values;
                }
                catch (IOException)
                {
                    if (attempt < 2)
                        Thread.Sleep(15);
                }
                catch (UnauthorizedAccessException)
                {
                    if (attempt < 2)
                        Thread.Sleep(15);
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        private static void ShowWidgetIntegrationOptions()
        {
            DialogResult result = MessageBox.Show(
                "Apply the EmilyDesk tray icon and refreshed dock theme?\r\n\r\n" +
                "Yes: Apply the EmilyDesk tray and dock theme\r\n" +
                "No: Restore the original XWidget resources\r\n" +
                "Cancel: Make no changes\r\n\r\n" +
                "Original tray and dock files are backed up before replacement.",
                "Original XWidget appearance",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Cancel)
                return;

            PatchOriginalXWidgetIcon(result == DialogResult.Yes);
        }

        private static void PatchOriginalXWidgetIcon(bool apply)
        {
            string xwidget = XWidgetLocator.FindExecutable();
            if (string.IsNullOrEmpty(xwidget))
            {
                MessageBox.Show(
                    "The original XWidget executable could not be found.",
                    "EmilyDesk",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string applicationDirectory =
                AppDomain.CurrentDomain.BaseDirectory;
            string helper = Path.Combine(
                applicationDirectory,
                "EmilyDesk.SetupHelper.exe");
            string theme = Path.Combine(
                applicationDirectory,
                "XWidgetTheme");

            if (!File.Exists(helper) ||
                (apply && !Directory.Exists(theme)))
            {
                MessageBox.Show(
                    "The tray and dock theme components are missing. Reinstall " +
                    "EmilyDesk and try again.",
                    "EmilyDesk",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            string command = apply
                ? "apply-xwidget-theme"
                : "restore-xwidget-theme";

            string arguments = apply
                ? command + " " + Quote(xwidget) + " " + Quote(theme)
                : command + " " + Quote(xwidget);

            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = helper,
                    Arguments = arguments,
                    WorkingDirectory = applicationDirectory,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                using (Process process = Process.Start(start))
                {
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                        throw new InvalidOperationException(
                            "The icon patcher returned exit code " +
                            process.ExitCode.ToString() + ".");
                }

                MessageBox.Show(
                    apply
                    ? "The EmilyDesk tray icon and dock theme were applied. " +
                          "XWidget has been restarted."
                        : "The original XWidget tray and dock resources were " +
                          "restored and XWidget was restarted.",
                    "EmilyDesk",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                if (ex.NativeErrorCode != 1223)
                {
                    MessageBox.Show(
                        ex.Message,
                        "EmilyDesk",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The original XWidget appearance could not be changed.\r\n\r\n" +
                    ex.Message,
                    "EmilyDesk",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static void LaunchXWidget()
        {
            string path = FindXWidgetExecutable();
            if (path == null)
            {
                MessageBox.Show(
                    "XWidget could not be found.",
                    "EmilyDesk",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            Process.Start(path);
        }

        internal static void RunWeatherTest()
        {
            try
            {
                string currentText;
                string statusText;

                using (var client = new TimeoutWebClient(8000))
                {
                    client.Encoding = Encoding.UTF8;

                    statusText = client.DownloadString(
                        AppConstants.BridgeBaseUrl +
                        "/xwidgetbridge/status.json");

                    currentText = client.DownloadString(
                        AppConstants.BridgeBaseUrl +
                        "/currentconditions/v1/" +
                        "54704.json?apikey=test&details=true&language=en");
                }

                var json = new JavaScriptSerializer();
                Dictionary<string, object> status =
                    json.Deserialize<Dictionary<string, object>>(statusText);

                ArrayList payload = json.Deserialize<ArrayList>(currentText);
                Dictionary<string, object> current =
                    payload != null && payload.Count > 0
                        ? payload[0] as Dictionary<string, object>
                        : null;

                if (current == null ||
                    !current.ContainsKey("Temperature"))
                {
                    throw new InvalidDataException(
                        "The current-conditions payload was incomplete.");
                }

                string condition = current.ContainsKey("WeatherText")
                    ? Convert.ToString(current["WeatherText"])
                    : "Unknown";

                string temperature = ExtractMetricTemperature(
                    current["Temperature"]);

                string provider =
                    status != null && status.ContainsKey("provider")
                        ? Convert.ToString(status["provider"])
                        : "Unknown";

                string cacheMode =
                    status != null &&
                    status.ContainsKey("usingStaleCache") &&
                    Convert.ToBoolean(status["usingStaleCache"])
                        ? "Cached fallback"
                        : "Live provider response";

                MessageBox.Show(
                    "Weather test passed." +
                    "\r\n\r\nLocation ID: 54704" +
                    "\r\nCondition: " + condition +
                    "\r\nTemperature: " + temperature +
                    "\r\nProvider: " + provider +
                    "\r\nMode: " + cacheMode,
                    "EmilyDesk",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                string logPath = WriteDashboardDiagnostic(
                    "WEATHER_TEST_FAILED",
                    ex.ToString());

                MessageBox.Show(
                    "Weather test failed." +
                    "\r\n\r\n" + ex.Message +
                    "\r\n\r\nDiagnostic log:" +
                    "\r\n" + logPath,
                    "EmilyDesk",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static string ExtractMetricTemperature(object temperatureObject)
        {
            Dictionary<string, object> temperature =
                temperatureObject as Dictionary<string, object>;

            Dictionary<string, object> metric =
                temperature != null &&
                temperature.ContainsKey("Metric")
                    ? temperature["Metric"] as Dictionary<string, object>
                    : null;

            if (metric == null || !metric.ContainsKey("Value"))
                return "Unknown";

            string value = Convert.ToString(metric["Value"]);
            string unit = metric.ContainsKey("Unit")
                ? Convert.ToString(metric["Unit"])
                : "C";

            return value + "°" + unit;
        }

        private static void LogDashboardStatus(
            bool serviceOk,
            bool apiOk,
            bool endpointOk,
            string xwidgetPath,
            Dictionary<string, object> status)
        {
            string provider =
                status != null && status.ContainsKey("provider")
                    ? Convert.ToString(status["provider"])
                    : "";

            WriteDashboardDiagnostic(
                "STATUS",
                "Service=" + serviceOk +
                "; API=" + apiOk +
                "; LoopbackEndpoint=" + endpointOk +
                "; XWidget=" + (xwidgetPath ?? "not found") +
                "; Provider=" + provider);
        }

        private static string WriteDashboardDiagnostic(
            string eventName,
            string message)
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "EmilyDesk");

            string path = Path.Combine(
                directory,
                "dashboard-diagnostics.log");

            try
            {
                Directory.CreateDirectory(directory);
                File.AppendAllText(
                    path,
                    DateTime.UtcNow.ToString("o") +
                    " [" + eventName + "] " +
                    message + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch
            {
                // The original dashboard operation remains more important.
            }

            return path;
        }

        private sealed class TimeoutWebClient : WebClient
        {
            private readonly int _timeout;

            public TimeoutWebClient(int timeout)
            {
                _timeout = timeout;
            }

            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);
                if (request != null)
                    request.Timeout = _timeout;
                return request;
            }
        }

        private sealed class DesignerWidgetChoice
        {
            public string Name { get; private set; }
            public string Kind { get; private set; }
            public string AssemblyPath { get; private set; }
            public string TypeName { get; private set; }
            public bool IsOptional
            {
                get { return !string.IsNullOrEmpty(AssemblyPath); }
            }

            public DesignerWidgetChoice(string name, string kind,
                string assemblyPath, string typeName)
            {
                Name = name;
                Kind = kind;
                AssemblyPath = assemblyPath;
                TypeName = typeName;
            }

            public override string ToString()
            {
                return Name;
            }
        }

        private sealed class ProviderItem
        {
            public string Id { get; private set; }
            public string Name { get; private set; }
            public bool RequiresApiKey { get; private set; }
            public bool HasApiKey { get; private set; }

            public ProviderItem(string id, string name,
                bool requiresApiKey, bool hasApiKey)
            {
                Id = id;
                Name = name;
                RequiresApiKey = requiresApiKey;
                HasApiKey = hasApiKey;
            }

            public override string ToString()
            {
                return Name;
            }
        }
    }
}
