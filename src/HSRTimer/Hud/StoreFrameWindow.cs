#if STORE_IL2CPP
using System;

namespace HSRTimer
{
    internal sealed class StoreFrameWindow
    {
        private readonly float[] _frames;
        private readonly float[] _sorted;
        private int _next, _count;
        private int _total;
        private double _seconds;
        private float _max;

        internal StoreFrameWindow(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _frames = new float[capacity];
            _sorted = new float[capacity];
        }

        internal void Add(float seconds)
        {
            if (!float.IsFinite(seconds) || seconds <= 0) return;
            _frames[_next] = seconds;
            _next = (_next + 1) % _frames.Length;
            _count = Math.Min(_count + 1, _frames.Length);
            _seconds += seconds;
            _max = Math.Max(_max, seconds);
            _total++;
        }

        internal readonly record struct Summary(int Frames, int PercentileFrames, double Fps,
            double MeanMs, double MaxMs, double P95Ms, double P99Ms, double LowFps);

        internal Summary Summarize()
        {
            if (_count == 0) return default;
            // All slots are populated after wrap. Their order does not matter for quantiles.
            Array.Copy(_frames, _sorted, _count);
            Array.Sort(_sorted, 0, _count);
            int Rank(double q) => (int)Math.Ceiling(q * _count) - 1;
            int tail = (int)Math.Ceiling(_count * 0.01);
            double slowSeconds = 0;
            for (int i = _count - tail; i < _count; i++) slowSeconds += _sorted[i];
            return new Summary(_total, _count, _total / _seconds, 1000 * _seconds / _total, 1000 * _max,
                1000 * _sorted[Rank(0.95)], 1000 * _sorted[Rank(0.99)], tail / slowSeconds);
        }

        internal void Reset()
        {
            _next = _count = _total = 0;
            _seconds = _max = 0;
        }
    }
}
#endif
