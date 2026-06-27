using DotnetCoupling.Cli.Analysis;
using FsCheck;
using FsCheck.Fluent;
using Xunit;

namespace DotnetCoupling.Tests;

public sealed class CouplingScoringTests
{
    [Theory]
    [InlineData(IntegrationStrength.Contract, 0.25)]
    [InlineData(IntegrationStrength.Model, 0.50)]
    [InlineData(IntegrationStrength.Functional, 0.75)]
    [InlineData(IntegrationStrength.Intrusive, 1.00)]
    public void ToScore_IntegrationStrength_ReturnsExpectedValue(IntegrationStrength strength, double expected)
    {
        Assert.Equal(expected, CouplingScoring.ToScore(strength));
    }

    [Theory]
    [InlineData(Distance.SameNamespace, 0.25)]
    [InlineData(Distance.DifferentNamespace, 0.50)]
    [InlineData(Distance.DifferentProject, 0.75)]
    [InlineData(Distance.ExternalPackage, 1.00)]
    public void ToScore_Distance_ReturnsExpectedValue(Distance distance, double expected)
    {
        Assert.Equal(expected, CouplingScoring.ToScore(distance));
    }

    [Theory]
    [InlineData(Volatility.Low, 0.00)]
    [InlineData(Volatility.Medium, 0.50)]
    [InlineData(Volatility.High, 1.00)]
    public void ToScore_Volatility_ReturnsExpectedValue(Volatility volatility, double expected)
    {
        Assert.Equal(expected, CouplingScoring.ToScore(volatility));
    }

    [Theory]
    [InlineData(IntegrationStrength.Contract, Distance.ExternalPackage, Volatility.Low, 0.75, "Acceptable")]
    [InlineData(IntegrationStrength.Intrusive, Distance.ExternalPackage, Volatility.High, 0.00, "Critical")]
    [InlineData(IntegrationStrength.Functional, Distance.DifferentNamespace, Volatility.Low, 0.75, "Acceptable")]
    [InlineData(IntegrationStrength.Intrusive, Distance.SameNamespace, Volatility.Low, 0.75, "Acceptable")]
    public void Calculate_KnownBoundaries_ReturnsExpectedScoreAndInterpretation(
        IntegrationStrength strength,
        Distance distance,
        Volatility volatility,
        double expectedScore,
        string expectedInterpretation)
    {
        BalanceScore score = CouplingScoring.Calculate(Coupling(strength, distance, volatility));

        Assert.Equal(expectedScore, score.Score, precision: 2);
        Assert.Equal(expectedInterpretation, score.Interpretation);
        Assert.InRange(score.Alignment, 0.0, 1.0);
        Assert.InRange(score.VolatilityImpact, 0.0, 1.0);
    }

    [Fact]
    public void Calculate_HigherVolatility_NeverIncreasesScoreForSameStrengthAndDistance()
    {
        Prop.ForAll(
            Arb.From(Gen.Elements(Enum.GetValues<IntegrationStrength>())),
            Arb.From(Gen.Elements(Enum.GetValues<Distance>())),
            (IntegrationStrength strength, Distance distance) =>
            {
                double low = CouplingScoring.Calculate(Coupling(strength, distance, Volatility.Low)).Score;
                double medium = CouplingScoring.Calculate(Coupling(strength, distance, Volatility.Medium)).Score;
                double high = CouplingScoring.Calculate(Coupling(strength, distance, Volatility.High)).Score;

                return low >= medium && medium >= high;
            }).QuickCheckThrowOnFailure();
    }

    [Fact]
    public void Calculate_AnyEnumBackedValues_ClampsScorePartsWithinUnitInterval()
    {
        Prop.ForAll<int, int, int>((strength, distance, volatility) =>
        {
            BalanceScore score = CouplingScoring.Calculate(Coupling(
                (IntegrationStrength)strength,
                (Distance)distance,
                (Volatility)volatility));

            return IsUnitInterval(score.Score)
                && IsUnitInterval(score.Alignment)
                && IsUnitInterval(score.VolatilityImpact);
        }).QuickCheckThrowOnFailure();
    }

    [Fact]
    public void ToScore_UnknownEnumBackedValues_ReturnFallbackScore()
    {
        Prop.ForAll<int>(value =>
        {
            if (Enum.IsDefined((IntegrationStrength)value)
                || Enum.IsDefined((Distance)value)
                || Enum.IsDefined((Volatility)value))
            {
                return true;
            }

            return CouplingScoring.ToScore((IntegrationStrength)value) == 0.50
                && CouplingScoring.ToScore((Distance)value) == 0.50
                && CouplingScoring.ToScore((Volatility)value) == 0.00;
        }).QuickCheckThrowOnFailure();
    }

    [Theory]
    [InlineData(4, 0, 0, 10, "F")]
    [InlineData(3, 0, 0, 10, "D")]
    [InlineData(1, 0, 0, 10, "D")]
    [InlineData(0, 6, 0, 100, "D")]
    [InlineData(0, 5, 0, 100, "C")]
    [InlineData(0, 0, 26, 100, "C")]
    [InlineData(0, 0, 25, 100, "B")]
    [InlineData(0, 0, 1, 20, "S")]
    [InlineData(0, 0, 2, 20, "A")]
    [InlineData(0, 0, 1, 10, "A")]
    [InlineData(0, 0, 0, 1, "B")]
    public void CalculateGrade_ExactIssueDensityBoundaries_ReturnExpectedGrade(
        int critical,
        int high,
        int medium,
        int internalCouplings,
        string expectedGrade)
    {
        List<CouplingIssue> issues = [];
        issues.AddRange(CreateIssues(Severity.Critical, critical));
        issues.AddRange(CreateIssues(Severity.High, high));
        issues.AddRange(CreateIssues(Severity.Medium, medium));

        GradeResult grade = CouplingScoring.CalculateGrade(internalCouplings, issues);

        Assert.Equal(expectedGrade, grade.Letter);
        Assert.Equal("issue-density", grade.Basis);
    }

    [Fact]
    public void CalculateGrade_AddingIssue_NeverImprovesGrade()
    {
        Prop.ForAll<(int Critical, int High, int Medium, int InternalCouplings, int Severity)>(values =>
        {
            int critical = Bounded(values.Critical, 20);
            int high = Bounded(values.High, 20);
            int medium = Bounded(values.Medium, 50);
            int internalCouplings = Bounded(values.InternalCouplings, 200);
            Severity addedSeverity = Enum.GetValues<Severity>()[Bounded(values.Severity, Enum.GetValues<Severity>().Length - 1)];
            List<CouplingIssue> issues = CreateIssues(Severity.Critical, critical)
                .Concat(CreateIssues(Severity.High, high))
                .Concat(CreateIssues(Severity.Medium, medium))
                .ToList();

            GradeResult before = CouplingScoring.CalculateGrade(internalCouplings, issues);
            issues.Add(CreateIssue(addedSeverity));
            GradeResult after = CouplingScoring.CalculateGrade(internalCouplings, issues);

            return GradeRank(after.Letter) >= GradeRank(before.Letter);
        }).QuickCheckThrowOnFailure();
    }

    private static CouplingMetrics Coupling(IntegrationStrength strength, Distance distance, Volatility volatility)
    {
        return new CouplingMetrics(
            "Sample.Source",
            "Sample.Target",
            strength,
            distance,
            volatility,
            null,
            null,
            Visibility.Public,
            new SourceLocation("Sample.cs", 1));
    }

    private static bool IsUnitInterval(double value)
    {
        return value is >= 0.0 and <= 1.0;
    }

    private static int Bounded(int value, int maxInclusive)
    {
        return (int)((uint)value % (uint)(maxInclusive + 1));
    }

    private static IEnumerable<CouplingIssue> CreateIssues(Severity severity, int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return CreateIssue(severity);
        }
    }

    private static CouplingIssue CreateIssue(Severity severity)
    {
        return new CouplingIssue(
            IssueType.GlobalComplexity,
            severity,
            "Sample.Source",
            "Sample.Target",
            0.5,
            "Problem",
            "Recommendation",
            null);
    }

    private static int GradeRank(string grade)
    {
        return grade switch
        {
            "S" => 0,
            "A" => 1,
            "B" => 2,
            "C" => 3,
            "D" => 4,
            "F" => 5,
            _ => 3,
        };
    }
}
