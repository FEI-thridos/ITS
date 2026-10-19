namespace KukaFm.Middleware.Api.Kuka;

/// <summary>Any failed KUKA FM call: success=false, non-2xx, timeout, transport error or unreadable response.</summary>
public class KukaException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}
