// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using MonoGame.Interop;

namespace Microsoft.Xna.Framework.Graphics;

/// <summary>
/// Describes the GPU execution duration of one completed presentation submission.
/// </summary>
public readonly struct GpuFrameTiming
{
    /// <summary>Gets the monotonically increasing presentation submission identifier.</summary>
    public ulong SubmissionId { get; }

    /// <summary>Gets the measured GPU execution duration in nanoseconds.</summary>
    public ulong DurationNanoseconds { get; }

    /// <summary>Gets the measured GPU execution duration in milliseconds.</summary>
    public double DurationMilliseconds => DurationNanoseconds / 1_000_000.0;

    /// <summary>Gets the cumulative number of timing samples dropped before this sample.</summary>
    public ulong DroppedTimingCount { get; }

    internal GpuFrameTiming(ulong submissionId, ulong durationNanoseconds, ulong droppedTimingCount)
    {
        SubmissionId = submissionId;
        DurationNanoseconds = durationNanoseconds;
        DroppedTimingCount = droppedTimingCount;
    }
}

public partial class GraphicsDevice
{
    /// <summary>
    /// Gets whether the selected native backend can report completed presentation GPU timings.
    /// </summary>
    public bool SupportsCompletedGpuFrameTiming =>
        (NativeFeatures & NativeGraphicsFeatures.CompletedGpuFrameTiming) != 0;

    /// <summary>
    /// Attempts to dequeue one completed presentation GPU timing without blocking.
    /// </summary>
    /// <remarks>
    /// The duration covers GPU command execution for the presentation submission. It excludes
    /// CPU waits, display presentation latency, auxiliary transfers, and OpenXR submissions.
    /// </remarks>
    public unsafe bool TryDequeueCompletedGpuFrameTiming(out GpuFrameTiming timing)
    {
        timing = default;
        if (Handle == null || !SupportsCompletedGpuFrameTiming)
            return false;
        if (MGG.GraphicsDevice_TryDequeueCompletedGpuFrameTiming(Handle, out var nativeTiming) == 0)
            return false;

        timing = new GpuFrameTiming(
            nativeTiming.SubmissionId,
            nativeTiming.DurationNanoseconds,
            nativeTiming.DroppedTimingCount);
        return true;
    }
}
