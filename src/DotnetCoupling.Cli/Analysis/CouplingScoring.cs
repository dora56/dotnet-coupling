namespace DotnetCoupling.Cli.Analysis;

public static class CouplingScoring
{
    public static BalanceScore Calculate(CouplingMetrics coupling)
    {
        double strength = Clamp(ToScore(coupling.Strength));
        double distance = Clamp(ToScore(coupling.Distance));
        double volatility = Clamp(ToScore(coupling.Volatility));

        double alignment = Clamp(1.0 - Math.Abs(strength - (1.0 - distance)));
        double volatilityImpact = Clamp(1.0 - Clamp(volatility * strength));
        double score = Clamp(alignment * volatilityImpact);

        return new BalanceScore(coupling, score, alignment, volatilityImpact, Interpret(score));
    }

    public static GradeResult CalculateGrade(int internalCouplings, IReadOnlyCollection<CouplingIssue> issues)
    {
        int denominator = Math.Max(internalCouplings, 1);
        int critical = issues.Count(issue => issue.Severity == Severity.Critical);
        int high = issues.Count(issue => issue.Severity == Severity.High);
        int medium = issues.Count(issue => issue.Severity == Severity.Medium);
        double highDensity = (double)high / denominator;
        double mediumDensity = (double)medium / denominator;

        if (critical > 3)
        {
            return new GradeResult("F", "Immediate action required", "issue-density", "More than three critical issues were found.");
        }

        if (critical > 0 || highDensity > 0.05)
        {
            return new GradeResult("D", "Attention needed", "issue-density", "Critical issues or high issue density were found.");
        }

        if (high > 0 || mediumDensity > 0.25)
        {
            return new GradeResult("C", "Room for improvement", "issue-density", "High issues or elevated medium issue density were found.");
        }

        if (mediumDensity <= 0.05 && internalCouplings >= 20)
        {
            return new GradeResult("S", "Over-optimized warning", "issue-density", "Very low issue density across many internal couplings can indicate over-abstraction.");
        }

        if (mediumDensity <= 0.10 && internalCouplings >= 10)
        {
            return new GradeResult("A", "Well-balanced", "issue-density", "Medium issue density is low and no severe issue was found.");
        }

        return new GradeResult("B", "Healthy", "issue-density", "Issue density is manageable or the project is too small for a stronger grade.");
    }

    public static double ToScore(IntegrationStrength strength)
    {
        return strength switch
        {
            IntegrationStrength.Contract => 0.25,
            IntegrationStrength.Model => 0.50,
            IntegrationStrength.Functional => 0.75,
            IntegrationStrength.Intrusive => 1.00,
            _ => 0.50,
        };
    }

    public static double ToScore(Distance distance)
    {
        return distance switch
        {
            Distance.SameNamespace => 0.25,
            Distance.DifferentNamespace => 0.50,
            Distance.DifferentProject => 0.75,
            Distance.ExternalPackage => 1.00,
            _ => 0.50,
        };
    }

    public static double ToScore(Volatility volatility)
    {
        return volatility switch
        {
            Volatility.Low => 0.00,
            Volatility.Medium => 0.50,
            Volatility.High => 1.00,
            _ => 0.00,
        };
    }

    private static double Clamp(double value)
    {
        return Math.Clamp(value, 0.0, 1.0);
    }

    private static string Interpret(double score)
    {
        return score switch
        {
            >= 0.80 => "Balanced",
            >= 0.60 => "Acceptable",
            >= 0.40 => "Needs Review",
            >= 0.20 => "Needs Refactoring",
            _ => "Critical",
        };
    }
}
