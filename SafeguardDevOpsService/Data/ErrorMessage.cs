#pragma warning disable 1591

namespace OneIdentity.DevOps.Data
{
    public class ErrorMessage
    {
        public string Message { get; }

        public ErrorMessage(string message)
        {
            Message = message;
        }
    }
}
