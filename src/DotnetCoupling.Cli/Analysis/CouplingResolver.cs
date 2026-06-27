namespace DotnetCoupling.Cli.Analysis;

internal static class CouplingResolver
{
    internal static List<CouplingMetrics> Resolve(
        IReadOnlyList<Component> components,
        IReadOnlyList<DependencyObservation> observations,
        IReadOnlyDictionary<string, int> changeCounts)
    {
        Dictionary<string, Component> componentsByName = components
            .GroupBy(component => component.Name)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        Dictionary<string, Component> componentsById = components.ToDictionary(component => component.Id, StringComparer.Ordinal);
        List<CouplingMetrics> couplings = [];

        foreach (DependencyObservation observation in observations)
        {
            if (!componentsByName.TryGetValue(observation.TargetName, out Component? target))
            {
                continue;
            }

            if (observation.SourceComponentId == target.Id)
            {
                continue;
            }

            if (!componentsById.TryGetValue(observation.SourceComponentId, out Component? source))
            {
                continue;
            }

            couplings.Add(new CouplingMetrics(
                source.Id,
                target.Id,
                ResolveStrength(observation, target),
                ResolveDistance(source, target),
                ResolveVolatility(target.FilePath, changeCounts),
                source.ProjectName,
                target.ProjectName,
                target.Visibility,
                new SourceLocation(observation.FilePath, observation.Line)));
        }

        return couplings;
    }

    internal static IntegrationStrength ResolveStrength(DependencyObservation observation, Component target)
    {
        if (target.Kind == ComponentKind.Interface)
        {
            return IntegrationStrength.Contract;
        }

        return observation.Usage switch
        {
            UsageContext.BaseType or UsageContext.InterfaceImplementation or UsageContext.GenericConstraint => IntegrationStrength.Contract,
            UsageContext.ObjectCreation or UsageContext.MethodCall or UsageContext.StaticCall => IntegrationStrength.Functional,
            UsageContext.FieldAccess or UsageContext.Reflection or UsageContext.DynamicDispatch => IntegrationStrength.Intrusive,
            _ => IntegrationStrength.Model,
        };
    }

    internal static Distance ResolveDistance(Component source, Component target)
    {
        if (source.Namespace == target.Namespace)
        {
            return Distance.SameNamespace;
        }

        int sharedSegments = source.Namespace
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Zip(target.Namespace.Split('.', StringSplitOptions.RemoveEmptyEntries))
            .TakeWhile(pair => pair.First == pair.Second)
            .Count();

        return sharedSegments >= 2 ? Distance.DifferentNamespace : Distance.DifferentProject;
    }

    internal static Volatility ResolveVolatility(string filePath, IReadOnlyDictionary<string, int> changeCounts)
    {
        if (!changeCounts.TryGetValue(filePath, out int changes))
        {
            return Volatility.Low;
        }

        return changes switch
        {
            <= 2 => Volatility.Low,
            <= 10 => Volatility.Medium,
            _ => Volatility.High,
        };
    }
}
