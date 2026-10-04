using System;
using System.Windows.Forms;
using XWidgetReborn.Shared;

namespace XWidgetReborn
{
    internal static class WidgetLayerMenuFactory
    {
        public static ToolStripMenuItem Create(Form owner)
        {
            var root = new ToolStripMenuItem("Widget Layer");
            Add(root, owner, "Desktop Layer", WidgetLayerMode.NormalDesktop);
            Add(root, owner, "Always on Top", WidgetLayerMode.AlwaysOnTop);
            root.DropDownItems.Add(new ToolStripSeparator());
            Add(root, owner, "Hide All Widgets", WidgetLayerMode.Hidden);
            root.DropDownOpening += delegate { RefreshChecks(root); };
            RefreshChecks(root);
            return root;
        }

        public static ContextMenuStrip CreateContextMenu(Form owner)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(Create(owner));
            return menu;
        }

        public static void RaiseAfterActivation(Form owner)
        {
            if (owner == null || owner.IsDisposed || !owner.IsHandleCreated) return;
            var timer = new Timer { Interval = 180 };
            timer.Tick += delegate
            {
                timer.Stop();
                timer.Dispose();
                if (!owner.IsDisposed && owner.IsHandleCreated)
                    EngineClient.PlaceNormalWidgetsBehind(owner.Handle);
            };
            timer.Start();
        }

        private static void Add(ToolStripMenuItem root, Form owner, string text, WidgetLayerMode mode)
        {
            var item = new ToolStripMenuItem(text) { Tag = mode };
            item.Click += delegate
            {
                EngineClient.SetWidgetLayerMode(mode);
                RefreshChecks(root);
                RaiseAfterActivation(owner);
            };
            root.DropDownItems.Add(item);
        }

        private static void RefreshChecks(ToolStripMenuItem root)
        {
            WidgetLayerMode current = EngineClient.GetWidgetLayerMode();
            foreach (ToolStripItem raw in root.DropDownItems)
            {
                ToolStripMenuItem item = raw as ToolStripMenuItem;
                if (item != null && item.Tag is WidgetLayerMode)
                    item.Checked = (WidgetLayerMode)item.Tag == current;
            }
        }
    }
}
