using System;
using System.Collections.Generic;
using System.Windows.Forms;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn.Runtime.Core
{
    internal sealed class EngineScheduler : IDisposable
    {
        // Every widget, including the one being dragged, shares this UI
        // thread. Polling system hardware and repainting the other widgets
        // here can block pointer messages for hundreds of milliseconds.
        private static int _activeWidgetDrags;

        internal static bool IsWidgetDragActive
        {
            get { return _activeWidgetDrags > 0; }
        }

        internal static void BeginWidgetDrag()
        {
            _activeWidgetDrags++;
        }

        internal static void EndWidgetDrag()
        {
            if (_activeWidgetDrags > 0)
                _activeWidgetDrags--;
        }

        private sealed class Subscription
        {
            public WidgetUpdateRate Rate;
            public Action<DateTime> Callback;
            public DateTime LastRunUtc;
        }

        private readonly Timer _timer;
        private readonly IRuntimeLoggingService _logging;
        private readonly List<Subscription> _subscriptions = new List<Subscription>();
        private bool _disposed;

        public EngineScheduler(IRuntimeLoggingService logging)
        {
            if (logging == null) throw new ArgumentNullException("logging");
            _logging = logging;
            _timer = new Timer { Interval = 1000 };
            _timer.Tick += OnTick;
            _timer.Start();
            _logging.Debug("Engine scheduler started.");
        }

        public IDisposable Subscribe(WidgetUpdateRate rate, Action<DateTime> callback)
        {
            if (callback == null) throw new ArgumentNullException("callback");
            var item = new Subscription { Rate = rate, Callback = callback, LastRunUtc = DateTime.MinValue };
            _subscriptions.Add(item);
            RecalculateInterval();
            return new Unsubscriber(_subscriptions, item, RecalculateInterval);
        }

        private void RecalculateInterval()
        {
            int interval = 1000;
            foreach (Subscription item in _subscriptions)
            {
                if (item.Rate == WidgetUpdateRate.Frame) { interval = 16; break; }
                if (item.Rate == WidgetUpdateRate.QuarterSecond) interval = Math.Min(interval, 250);
            }
            _timer.Interval = interval;
        }

        private void OnTick(object sender, EventArgs e)
        {
            // Widget data is sampled on the next tick after release. Pausing
            // it only while the pointer is held avoids a catch-up burst in
            // the middle of the movement.
            if (IsWidgetDragActive)
                return;
            DateTime now = DateTime.Now;
            DateTime utc = DateTime.UtcNow;
            foreach (Subscription item in _subscriptions.ToArray())
            {
                TimeSpan interval = GetInterval(item.Rate);
                if (item.LastRunUtc != DateTime.MinValue && utc - item.LastRunUtc < interval) continue;
                item.LastRunUtc = utc;
                try { item.Callback(now); }
                catch (Exception ex)
                {
                    _logging.Error("Scheduled widget callback failed.", ex);
                }
            }
        }

        private static TimeSpan GetInterval(WidgetUpdateRate rate)
        {
            switch (rate)
            {
                case WidgetUpdateRate.Frame: return TimeSpan.FromMilliseconds(16);
                case WidgetUpdateRate.QuarterSecond: return TimeSpan.FromMilliseconds(250);
                case WidgetUpdateRate.Minute: return TimeSpan.FromMinutes(1);
                default: return TimeSpan.FromSeconds(1);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();
            _timer.Dispose();
            _subscriptions.Clear();
            _logging.Debug("Engine scheduler stopped.");
        }

        private sealed class Unsubscriber : IDisposable
        {
            private List<Subscription> _items;
            private Subscription _item;
            private Action _changed;
            public Unsubscriber(List<Subscription> items, Subscription item, Action changed) { _items = items; _item = item; _changed = changed; }
            public void Dispose()
            {
                if (_items != null && _item != null) _items.Remove(_item);
                if (_changed != null) _changed();
                _items = null; _item = null; _changed = null;
            }
        }
    }
}
