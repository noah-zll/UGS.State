using System.Text;

namespace UGS.State
{
    /// <summary>
    /// 状态变更环形流水，用于调试与问题回溯。
    /// </summary>
    public sealed class StateChangeLog
    {
        private readonly StateChange[] _items;
        private int _next;
        private int _count;

        public StateChangeLog(int capacity = 32)
        {
            if (capacity < 1)
            {
                capacity = 1;
            }

            _items = new StateChange[capacity];
        }

        public int Capacity
        {
            get { return _items.Length; }
        }

        public int Count
        {
            get { return _count; }
        }

        public StateChange this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                {
                    throw new StateException("StateChangeLog index out of range: " + index);
                }

                int start = (_next - _count + _items.Length) % _items.Length;
                return _items[(start + index) % _items.Length];
            }
        }

        internal void Add(StateChange change)
        {
            _items[_next] = change;
            _next = (_next + 1) % _items.Length;
            if (_count < _items.Length)
            {
                _count++;
            }
        }

        public string Dump()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("StateChangeLog(").Append(_count).Append("/").Append(_items.Length).Append(")\n");
            for (int i = 0; i < _count; i++)
            {
                builder.Append("  ").Append(this[i]).Append('\n');
            }

            return builder.ToString();
        }
    }
}
