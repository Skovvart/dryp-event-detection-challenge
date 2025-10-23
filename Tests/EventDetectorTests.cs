using Api;
using Shouldly;

namespace Tests;

public sealed class EventDetectorTests
{
    private static readonly TimeSpan _minDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _maxGap = TimeSpan.FromMinutes(10);

    [Fact]
    public void EmptyTimeSeries_ReturnsNoEvents()
    {
        var events = EventDetector.DetectEvents([], 0, _minDuration, _maxGap).ToList();

        events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    public void TimeSeries_WithNoOverflowEvents_ReturnsNoEvents(int numDryEvents)
    {
        var threshold = 0.01m;
        var timeSeries = Enumerable
            .Range(0, numDryEvents)
            .Select(i => DrySample(i * 2 * 60, threshold))
            .ToList();

        var events = EventDetector
            .DetectEvents(timeSeries, threshold, _minDuration, _maxGap)
            .ToList();

        events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1000)]
    public void TimeSeries_WithInterwovenDrySamplesNoMinDurationOrGap_ReturnsAllOverflowsAsEvents(
        int numOverflowEvents
    )
    {
        var threshold = 0.001m;
        var timeSeries = Enumerable
            .Range(0, numOverflowEvents * 2)
            .Select(i =>
                i % 2 == 0
                    ? DrySample(i * 2 * 60, threshold)
                    : OverflowSample(i * 2 * 60, threshold)
            )
            .ToList();

        var events = EventDetector
            .DetectEvents(timeSeries, 0, TimeSpan.Zero, TimeSpan.Zero)
            .ToList();

        events.Count.ShouldBe(numOverflowEvents);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    public void TimeSeries_WithAllOverflowSamplesNoMinDurationOrGap_ReturnsSingleEvent(
        int numOverflowEvents
    )
    {
        var threshold = 0.001m;
        var timeSeries = Enumerable
            .Range(0, numOverflowEvents)
            .Select(i => OverflowSample(i * 2 * 60, threshold))
            .ToList();

        var events = EventDetector
            .DetectEvents(timeSeries, 0, TimeSpan.Zero, TimeSpan.Zero)
            .ToList();

        events.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    public void TimeSeries_WithOneOverflowEventAndMinDuration_ReturnsNoEvents(int minDuration)
    {
        var threshold = 0.001m;

        var events = EventDetector
            .DetectEvents(
                [OverflowSample(0, threshold)],
                0,
                TimeSpan.FromSeconds(minDuration),
                _maxGap
            )
            .ToList();

        events.ShouldBeEmpty();
    }

    [Theory]
    // primary gap tests
    [InlineData(2, 1, 0)] // gap too small to stitch any entries together
    [InlineData(2, 1.99, 0)] // gap still too small to stitch any entries together
    [InlineData(2, 2, 1)] // gap joins the two overflows to one entry
    [InlineData(2, 3, 1)] // gap joins the two overflows to one entry
    // end primary
    [InlineData(0, 1, 3)] // gap too small to stitch any entries together, but minimum duration allows three 0-min events
    [InlineData(0, 2, 2)] // gap joins first overflows to one entry, though minimum duration would have allowed all
    public void TimeSeries_WithOverflows_ReturnsSpanningEvents(
        int minDuration,
        double maxGap,
        int numExpectedEvents
    )
    {
        var threshold = 0.001m;
        List<OverflowSample> timeSeries =
        [
            DrySample(0 * 2 * 60, threshold),
            OverflowSample(1 * 2 * 60, threshold),
            DrySample(2 * 2 * 60, threshold),
            OverflowSample(3 * 2 * 60, threshold),
            DrySample(4 * 2 * 60, threshold),
            DrySample(5 * 2 * 60, threshold),
            OverflowSample(6 * 2 * 60, threshold),
            DrySample(7 * 2 * 60, threshold),
        ];

        var events = EventDetector
            .DetectEvents(
                timeSeries,
                threshold,
                TimeSpan.FromMinutes(minDuration),
                TimeSpan.FromMinutes(maxGap)
            )
            .ToList();

        events.Count.ShouldBe(numExpectedEvents);
    }

    [Theory]
    [InlineData(0, 300, 600)]
    [InlineData(0.14, 300, 600)]
    [InlineData(0.14, 0, 0)]
    public async Task DetectedEventsForSampleTimeSeriesShouldSatisfyInvariants(
        decimal threshold,
        int minDurationSeconds,
        int maxGapSeconds
    )
    {
        var events = EventDetector
            .DetectEvents(
                await EventDetector.SampleTimeSeries.Value,
                threshold,
                TimeSpan.FromSeconds(minDurationSeconds),
                TimeSpan.FromSeconds(maxGapSeconds)
            )
            .ToList();

        events.ShouldNotBeEmpty();
        events.Select(e => e.Start).ShouldBeInOrder(SortDirection.Ascending);
        events.ShouldBeUnique();
        events.ShouldAllBe(e => e.Start <= e.End);
        events.ShouldAllBe(e => e.DurationMinutes >= 0);
        events.ShouldAllBe(e => e.PeakValue > 0);
    }

    [Theory]
    [InlineData(-0.1, 300, 600)]
    [InlineData(0, -1, 600)]
    [InlineData(0, 300, -1)]
    public void BadArgumentsReturnAppropriateErrors(
        decimal threshold,
        int minDurationSeconds,
        int maxGapSeconds
    ) =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            EventDetector.DetectEvents(
                [],
                threshold,
                TimeSpan.FromSeconds(minDurationSeconds),
                TimeSpan.FromSeconds(maxGapSeconds)
            )
        );

    private static DateTimeOffset _testStart = DateTimeOffset.UtcNow;

    private static OverflowSample DrySample(int secondsOffset, decimal threshold) =>
        new(_testStart.AddSeconds(secondsOffset).ToUnixTimeMilliseconds(), threshold - 0.001m);

    private static OverflowSample OverflowSample(int secondsOffset, decimal threshold) =>
        new(
            _testStart.AddSeconds(secondsOffset).ToUnixTimeMilliseconds(),
            threshold + (decimal)Random.Shared.NextDouble()
        );
}
