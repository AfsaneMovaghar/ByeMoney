using ByeMoney.Domain.Resources;

namespace ByeMoney.Domain.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key) :
        base(string.Format(DomainErrors.Common_NotFound, entityName, key))
    { }
}