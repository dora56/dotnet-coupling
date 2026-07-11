namespace DotnetCoupling.Core;

public sealed class InvestigationQueryException : Exception
{
    public InvestigationQueryException(string message)
        : base(message)
    {
    }
}
