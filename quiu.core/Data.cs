using System;

namespace quiu.core
{
    public struct Data
    {
        public static Data Empty = new Data (0, null);

        public Int64 Timestamp;
        public byte[]? Value;

        public int Size => this.Value!.Length;

        public Data (Int64 timestamp, byte[]? value)
        {
            this.Timestamp = timestamp;
            this.Value = value;
        }

        public Data (byte[]? value)
            : this(DateTime.Now.Ticks, value)
        {
        }
    }
}

