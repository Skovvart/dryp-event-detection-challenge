using Xunit.v3;

namespace Tests;

internal static class TestConstants
{
    internal const string IntegrationTests = nameof(IntegrationTests);
}

[AttributeUsage(
    AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly,
    AllowMultiple = false
)]
internal class IntegrationTestAttribute : Attribute, ITraitAttribute
{
    private static readonly IReadOnlyCollection<KeyValuePair<string, string>> _trait =
    [
        new("Category", "Integration"),
    ];

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() => _trait;
}
