namespace ByeMoney.Domain.Common.Exceptions
{
    /// <summary>
    /// نقض قاعدهٔ دامنه که به‌خاطر وضعیت فعلی داده رخ داده (مثلاً انتقال وضعیت نامعتبر یا رقابت دو عملیات)؛ پاسخ HTTP آن ۴۰۹ است.
    /// </summary>
    public class ConflictDomainException(string message) : DomainException(message)
    {
    }
}
