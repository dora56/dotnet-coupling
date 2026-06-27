namespace DotnetCoupling.Cli.Analysis;

internal static class ExternalCouplingDetector
{
    internal static void AddExternalUsingCouplings(
        List<CouplingMetrics> couplings,
        IReadOnlyCollection<Component> components,
        IReadOnlyDictionary<string, List<UsingNamespace>> usingNamespacesByFile,
        IReadOnlySet<string> internalNamespaces)
    {
        foreach (IGrouping<string, Component> fileComponents in components.GroupBy(component => component.FilePath, StringComparer.Ordinal))
        {
            if (!usingNamespacesByFile.TryGetValue(fileComponents.Key, out List<UsingNamespace>? usingNamespaces))
            {
                continue;
            }

            foreach (UsingNamespace usingNamespace in usingNamespaces)
            {
                if (IsFrameworkUsing(usingNamespace.Name) || IsInternalUsing(usingNamespace.Name, internalNamespaces))
                {
                    continue;
                }

                string packageName = NormalizeExternalPackageName(usingNamespace.Name);
                foreach (Component source in fileComponents)
                {
                    couplings.Add(new CouplingMetrics(
                        source.Id,
                        packageName,
                        IntegrationStrength.Contract,
                        Distance.ExternalPackage,
                        Volatility.Low,
                        source.ProjectName,
                        packageName,
                        Visibility.Public,
                        new SourceLocation(source.FilePath, usingNamespace.Line)));
                }
            }
        }
    }

    internal static bool IsFrameworkUsing(string namespaceName)
    {
        return namespaceName == "System"
            || namespaceName.StartsWith("System.", StringComparison.Ordinal)
            || namespaceName == "Microsoft"
            || namespaceName.StartsWith("Microsoft.", StringComparison.Ordinal);
    }

    internal static bool IsInternalUsing(string namespaceName, IReadOnlySet<string> internalNamespaces)
    {
        return internalNamespaces.Any(internalNamespace =>
            namespaceName == internalNamespace
            || namespaceName.StartsWith(internalNamespace + ".", StringComparison.Ordinal)
            || internalNamespace.StartsWith(namespaceName + ".", StringComparison.Ordinal));
    }

    internal static string NormalizeExternalPackageName(string namespaceName)
    {
        string[] segments = namespaceName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2)
        {
            return $"{segments[0]}.{segments[1]}";
        }

        return segments.FirstOrDefault() ?? namespaceName;
    }
}

internal sealed record UsingNamespace(string Name, int Line);
