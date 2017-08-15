using System.Collections.Generic;
using System.Timers;

namespace NeteaseReverseLadder
{
    class Cache<TKey, TValue>
    {
        private int interval;
        private class Entry
        {
            public TValue value;
            public bool mark;
        }
        private Dictionary<TKey, Entry> pairs = new Dictionary<TKey, Entry>();
        public Cache(int interval = 30)
        {
            this.interval = interval;
            var timer = new Timer();
            timer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            timer.Interval = interval * 1000;
            timer.Enabled = true;
        }
        public void Put(TKey key, TValue value)
        {
            lock (this)
                pairs.Add(key, new Entry { value = value });
        }
        public TValue Get(TKey key)
        {
            lock (this)
            {
                if (pairs.ContainsKey(key))
                {
                    var value = pairs[key].value;
                    pairs.Remove(key);
                    return value;
                }
                else return default(TValue);
            }
        }
        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            lock (this)
                foreach (var pair in pairs)
                {
                    if (pair.Value.mark)
                        pairs.Remove(pair.Key);
                    else
                        pair.Value.mark = true;
                }
        }
    }
}
