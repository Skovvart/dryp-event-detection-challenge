using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api;

[DebuggerDisplay("{Start} ({DurationMinutes} minutes): {PeakValue}")]
public sealed record OverflowEvent(DateTimeOffset Start, DateTimeOffset End, decimal PeakValue)
{
    public double DurationMinutes => (End - Start).TotalMinutes;
};

public sealed record OverflowSample(long UnixTimeMilliseconds, decimal Value);

[JsonSerializable(typeof(IEnumerable<decimal[]>))]
[JsonSerializable(typeof(IEnumerable<OverflowEvent>))]
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
public partial class EventJsonSerializerContext : JsonSerializerContext { }

public sealed class EventDetector
{
    internal static readonly Lazy<Task<IEnumerable<OverflowSample>>> SampleTimeSeries = new(
        static async () =>
            (
                await JsonSerializer.DeserializeAsync(
                    File.OpenRead("overflow-timeseries.json"),
                    EventJsonSerializerContext.Default.IEnumerableDecimalArray
                )
            )!
                .Select(static e => new OverflowSample((long)e[0], e[1]))
                .ToArray()
    );

    public static IEnumerable<OverflowEvent> DetectEvents(
        IEnumerable<OverflowSample> timeSeries,
        decimal threshold,
        TimeSpan minDuration,
        TimeSpan maxGap
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(threshold, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(minDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxGap, TimeSpan.Zero);

        return DetectEventsImpl(timeSeries, threshold, minDuration, maxGap);

        static IEnumerable<OverflowEvent> DetectEventsImpl(
            IEnumerable<OverflowSample> timeSeries,
            decimal threshold,
            TimeSpan minDuration,
            TimeSpan maxGap
        )
        {
            var inOverflowEvent = false;
            DateTimeOffset overflowStart = default;
            DateTimeOffset overflowLastSeen = default;
            var peak = decimal.MinValue;

            foreach (var (unixTimeMilliseconds, value) in timeSeries)
            {
                var sampleTime = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds);

                if (value > threshold) // overflow sample
                {
                    // It's possible that overflowLastSeen should be +2 minutes for the sample duration itself?
                    // If wrong it impacts minDuration filtering / gap stitching.
                    overflowLastSeen = sampleTime; /*.AddMinutes(2)*/

                    if (!inOverflowEvent)
                    {
                        // start event
                        inOverflowEvent = true;
                        overflowStart = sampleTime;
                        peak = value;
                    }
                    else
                    // extend event
                    if (value > peak)
                        peak = value;
                }
                else // dry sample
                if (inOverflowEvent)
                {
                    if (sampleTime - overflowLastSeen > maxGap)
                    {
                        if (overflowLastSeen - overflowStart >= minDuration)
                            yield return new OverflowEvent(overflowStart, overflowLastSeen, peak);

                        inOverflowEvent = false;
                        peak = decimal.MinValue;
                    }
                }
            }

            if (inOverflowEvent && overflowLastSeen - overflowStart >= minDuration)
                yield return new OverflowEvent(overflowStart, overflowLastSeen, peak);
        }
    }
}
