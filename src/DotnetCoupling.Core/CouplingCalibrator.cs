namespace DotnetCoupling.Core;

internal static class CouplingCalibrator
{
    internal static List<CouplingMetrics> Calibrate(
        IReadOnlyCollection<CouplingMetrics> couplings,
        IReadOnlyDictionary<string, Component> componentsById,
        DomainContext domainContext)
    {
        if (domainContext.Subdomains.Count == 0 && domainContext.Areas.Count == 0)
        {
            return couplings.ToList();
        }

        List<CouplingMetrics> calibrated = [];
        foreach (CouplingMetrics coupling in couplings)
        {
            componentsById.TryGetValue(coupling.Source, out Component? source);
            componentsById.TryGetValue(coupling.Target, out Component? target);

            DomainSubdomain? targetSubdomain = target is null
                ? null
                : DomainContextMatcher.FindSubdomain(target.FilePath, domainContext);
            DomainArea? sourceArea = source is null
                ? null
                : DomainContextMatcher.FindArea(source.FilePath, domainContext);
            DomainArea? targetArea = target is null
                ? null
                : DomainContextMatcher.FindArea(target.FilePath, domainContext);

            calibrated.Add(coupling with
            {
                Strength = CalibrateStrength(coupling.Strength, coupling.Distance, sourceArea, targetArea),
                Volatility = targetSubdomain?.ExpectedVolatility ?? coupling.Volatility,
            });
        }

        return calibrated;
    }

    private static IntegrationStrength CalibrateStrength(
        IntegrationStrength strength,
        Distance distance,
        DomainArea? sourceArea,
        DomainArea? targetArea)
    {
        if (targetArea?.TechnicalRole == TechnicalRole.Contract)
        {
            strength = LowerToModel(strength);
        }

        if (sourceArea?.TechnicalRole == TechnicalRole.CompositionRoot
            && distance >= Distance.DifferentNamespace)
        {
            strength = LowerToModel(strength);
        }

        return strength;
    }

    private static IntegrationStrength LowerToModel(IntegrationStrength strength)
    {
        return strength > IntegrationStrength.Model ? IntegrationStrength.Model : strength;
    }
}
