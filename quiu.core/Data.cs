using System;

namespace quiu.core
{
    public struct Data
    {
        public static Data Empty = new Data (0, null);

        public Int64 Timestamp { get; private set; }
        public byte[]? Value { get; private set; }

        public int Size => this.Value!.Length;

        public Data (Int64 timestamp, byte[]? value)
        {
            this.Timestamp = timestamp;
            this.Value = value;
        }
    }
}

