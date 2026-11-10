using System;

namespace UGS.State
{
    /// <summary>状态系统异常（目录配置错误、非法进入等）。</summary>
    public sealed class StateException : Exception
    {
        public StateException(string message) : base(message)
        {
        }

        public StateException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
