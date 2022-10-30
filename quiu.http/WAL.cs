using System;
using quiu.core;

namespace quiu.http
{
    public class WAL
        : PersistentConcurrentQueue<(Channel channel, byte[] data)>
    {
        protected override bool Persist ((Channel channel, byte[] data) item)
        {
            try
            {
                item.channel.Append (item.data);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error (ex.Message);
                return false;
            }
        }
    }
}

