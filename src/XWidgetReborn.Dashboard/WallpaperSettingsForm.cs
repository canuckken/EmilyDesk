using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn
{
    internal sealed class WallpaperSettingsForm : Form
    {
        private readonly ComboBox _theme;
        private readonly ComboBox _design;
        private readonly ComboBox _resolution;
        private readonly ComboBox _layout;
        private readonly RadioButton _collection;
        private readonly RadioButton _personal;
        private readonly RadioButton _keep;
        private readonly TextBox _personalPath;
        private readonly Button _browse;
        private readonly PictureBox _preview;
        private readonly Label _message;

        public WallpaperSettingsForm()
        {
            Text = "EmilyDesk Wallpaper";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(900, 700);
            Font = new Font("Segoe UI", 9F);

            var title = new Label
            {
                Text = "Wallpaper",
                Location = new Point(20, 16),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 15F)
            };
            var subtitle = new Label
            {
                Text = "Choose an EmilyDesk wallpaper or use one of your own.",
                Location = new Point(22, 49),
                AutoSize = true,
                ForeColor = Color.DimGray
            };
            Controls.Add(title);
            Controls.Add(subtitle);

            Controls.Add(Label("Theme", 20, 80));
            _theme = Combo(20, 103, 250);
            _theme.Items.AddRange(
                new List<string>(EmilyDeskThemeCatalog.Names).ToArray());
            _theme.SelectedIndex = 0;
            Controls.Add(_theme);

            Controls.Add(Label("Wallpaper", 290, 80));
            _design = Combo(290, 103, 160);
            _design.Items.AddRange(new object[]
            {
                "Wallpaper 1", "Wallpaper 2"
            });
            _design.SelectedIndex = 0;
            Controls.Add(_design);

            Controls.Add(Label("Resolution", 470, 80));
            _resolution = Combo(470, 103, 220);
            _resolution.Items.AddRange(new object[]
            {
                WallpaperManager.AutomaticResolution,
                WallpaperManager.WideResolution,
                WallpaperManager.TallResolution
            });
            _resolution.SelectedIndex = 0;
            Controls.Add(_resolution);

            Controls.Add(Label("Layout", 710, 80));
            _layout = Combo(710, 103, 160);
            _layout.Items.AddRange(new object[]
            {
                "Fill", "Fit", "Stretch", "Tile", "Center", "Span"
            });
            _layout.SelectedIndex = 0;
            Controls.Add(_layout);

            _collection = Radio(
                "Use selected EmilyDesk wallpaper", 20, 145, true, 280);
            _personal = Radio("Use a personal image", 20, 179, false, 145);
            _keep = Radio("Keep current wallpaper", 20, 213, false, 190);
            Controls.AddRange(new Control[]
            {
                _collection, _personal, _keep
            });

            _personalPath = new TextBox
            {
                Location = new Point(170, 176),
                Width = 520,
                ReadOnly = true
            };
            Controls.Add(_personalPath);
            _browse = new Button
            {
                Text = "Browse...",
                Location = new Point(710, 174),
                Size = new Size(100, 28)
            };
            _browse.Click += BrowsePersonal;
            Controls.Add(_browse);

            _preview = new PictureBox
            {
                Location = new Point(20, 255),
                Size = new Size(650, 360),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.FromArgb(31, 35, 36)
            };
            Controls.Add(_preview);

            var preview = Button("Refresh preview", 700, 255);
            preview.Click += delegate { PreviewSelection(true); };
            var apply = Button("Apply wallpaper", 700, 302);
            apply.Font = new Font("Segoe UI Semibold", 9F);
            apply.Click += delegate { ApplySelection(); };
            var restore = Button("Restore previous", 700, 349);
            restore.Enabled = WallpaperManager.CanRestore;
            restore.Click += delegate
            {
                Execute(delegate { WallpaperManager.RestorePrevious(); });
                restore.Enabled = WallpaperManager.CanRestore;
            };

            var wallpaperSites = new GroupBox
            {
                Text = "Find more wallpapers",
                Location = new Point(690, 397),
                Size = new Size(190, 184),
                Font = new Font("Segoe UI Semibold", 9F)
            };
            wallpaperSites.Controls.Add(new Label
            {
                Text = "Browse free wallpaper collections in your default browser.",
                Location = new Point(12, 25),
                Size = new Size(166, 42),
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.DimGray
            });
            var unsplash = new Button
            {
                Text = "Browse Unsplash",
                Location = new Point(12, 76),
                Size = new Size(166, 36)
            };
            unsplash.Click += delegate
            {
                OpenWallpaperSite("https://unsplash.com/wallpapers");
            };
            var pexels = new Button
            {
                Text = "Browse Pexels",
                Location = new Point(12, 126),
                Size = new Size(166, 36)
            };
            pexels.Click += delegate
            {
                OpenWallpaperSite("https://www.pexels.com/search/wallpaper/");
            };
            wallpaperSites.Controls.Add(unsplash);
            wallpaperSites.Controls.Add(pexels);
            Controls.Add(wallpaperSites);

            var close = Button("Close", 700, 626);
            close.DialogResult = DialogResult.Cancel;
            Controls.AddRange(new Control[]
            {
                preview, apply, restore, close
            });
            CancelButton = close;

            _message = new Label
            {
                Location = new Point(20, 679),
                Size = new Size(850, 18),
                ForeColor = Color.DimGray
            };
            Controls.Add(_message);

            _theme.SelectedIndexChanged += SelectionChanged;
            _design.SelectedIndexChanged += SelectionChanged;
            _resolution.SelectedIndexChanged += SelectionChanged;
            _collection.CheckedChanged += SelectionChanged;
            _personal.CheckedChanged += SelectionChanged;
            _keep.CheckedChanged += SelectionChanged;

            RefreshEnabledState();
            PreviewSelection(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _preview.Image != null)
            {
                _preview.Image.Dispose();
                _preview.Image = null;
            }
            base.Dispose(disposing);
        }

        private void SelectionChanged(object sender, EventArgs e)
        {
            RefreshEnabledState();
            PreviewSelection(false);
        }

        private void RefreshEnabledState()
        {
            bool collection = _collection.Checked;
            _theme.Enabled = collection;
            _design.Enabled = collection;
            _resolution.Enabled = collection;
            _personalPath.Enabled = _personal.Checked;
            _browse.Enabled = _personal.Checked;
        }

        private string SelectedPath()
        {
            if (_collection.Checked)
                return WallpaperManager.CollectionPath(
                    Convert.ToString(_theme.SelectedItem),
                    _design.SelectedIndex + 1,
                    Convert.ToString(_resolution.SelectedItem));
            if (_personal.Checked) return _personalPath.Text;
            return string.Empty;
        }

        private void BrowsePersonal(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog
            {
                Title = "Choose a wallpaper image",
                Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All files|*.*"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _personalPath.Text = dialog.FileName;
                _personal.Checked = true;
                PreviewSelection(false);
            }
        }

        private void PreviewSelection(bool showErrors)
        {
            if (_keep.Checked)
            {
                TryLoadPreview(WallpaperManager.CurrentPath(), showErrors);
                if (!showErrors)
                    _message.Text = "Current Windows wallpaper will be kept.";
                return;
            }

            string path = SelectedPath();
            if (!TryLoadPreview(path, showErrors))
            {
                if (!showErrors)
                    _message.Text = _personal.Checked &&
                        string.IsNullOrWhiteSpace(path)
                        ? "Choose a personal image to preview it."
                        : "The selected wallpaper image is not installed.";
                return;
            }

            if (_collection.Checked)
            {
                string resolved = WallpaperManager.ResolvedResolution(
                    Convert.ToString(_resolution.SelectedItem));
                _message.Text = string.Format(
                    "{0} - {1}. Windows changes only after Apply.",
                    Convert.ToString(_design.SelectedItem), resolved);
            }
            else
                _message.Text =
                    "Personal image previewed. Windows changes only after Apply.";
        }

        private void ApplySelection()
        {
            if (_keep.Checked)
            {
                _message.Text = "Current wallpaper kept.";
                return;
            }
            Execute(delegate
            {
                WallpaperManager.Apply(
                    SelectedPath(), Convert.ToString(_layout.SelectedItem));
            });
        }

        private bool TryLoadPreview(string path, bool showErrors)
        {
            try
            {
                WallpaperManager.ValidateImage(path);
                using (Image source = Image.FromFile(path))
                {
                    Image copy = CreatePreview(source, _preview.ClientSize);
                    if (_preview.Image != null) _preview.Image.Dispose();
                    _preview.Image = copy;
                }
                return true;
            }
            catch (Exception ex)
            {
                if (_preview.Image != null)
                {
                    _preview.Image.Dispose();
                    _preview.Image = null;
                }
                if (showErrors)
                    MessageBox.Show(this, ex.Message, "EmilyDesk Wallpaper",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
        }

        private static Image CreatePreview(Image source, Size bounds)
        {
            double scale = Math.Min(
                (double)bounds.Width / source.Width,
                (double)bounds.Height / source.Height);
            int width = Math.Max(1, (int)Math.Round(source.Width * scale));
            int height = Math.Max(1, (int)Math.Round(source.Height * scale));
            var result = new Bitmap(width, height);
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.DrawImage(source, 0, 0, width, height);
            }
            return result;
        }

        private void Execute(Action action)
        {
            Execute(action, "Wallpaper setting applied.");
        }

        private void Execute(Action action, string successMessage)
        {
            try
            {
                action();
                _message.Text = successMessage;
            }
            catch (Exception ex)
            {
                _message.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "EmilyDesk Wallpaper",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OpenWallpaperSite(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "EmilyDesk could not open the wallpaper site.\r\n\r\n" +
                    ex.Message,
                    "EmilyDesk Wallpaper",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static Label Label(string text, int left, int top)
        {
            return new Label
            {
                Text = text,
                Location = new Point(left, top),
                AutoSize = true
            };
        }

        private static ComboBox Combo(int left, int top, int width)
        {
            return new ComboBox
            {
                Location = new Point(left, top),
                Width = width,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
        }

        private static RadioButton Radio(
            string text, int left, int top, bool selected, int width)
        {
            return new RadioButton
            {
                Text = text,
                Location = new Point(left, top),
                Width = width,
                Checked = selected
            };
        }

        private static Button Button(string text, int left, int top)
        {
            return new Button
            {
                Text = text,
                Location = new Point(left, top),
                Size = new Size(170, 36)
            };
        }

    }
}
