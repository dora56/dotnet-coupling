# C# Rules for dotnet-coupling

## Language

- Target net10.0
- Nullable enabled
- Implicit usings enabled
- Prefer file-scoped namespace
- Use sealed record for immutable models

## Style

- Prefer sealed class unless inheritance is required
- Prefer pattern matching over casting
- Prefer early return over deep nesting
- Use nameof in guard clauses
- No region directives

## Analysis Constraints

- MVP is syntax-only Roslyn analysis
- Do not use SemanticModel in MVP
- Use CSharpSyntaxWalker for traversal

## Process/Git Safety

- Use ProcessStartInfo.ArgumentList, never interpolated Arguments
- Set UseShellExecute=false and redirect output
- Handle git-not-found as recoverable CLI error

## Error Handling

- Exceptions only for truly exceptional paths
- Expected parse/analyze failures should be typed results/nullables
- Top-level CLI must catch unhandled exceptions and return stable exit code

## Testing

- xUnit test framework
- MethodName_Scenario_ExpectedResult naming
- Use Theory + InlineData for boundaries
- Keep fixtures minimal and focused
