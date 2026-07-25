// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#pragma once

#include <cstddef>
#include <cstdint>
#include <mutex>

class MGG_CompletedGpuFrameTimingQueue
{
public:
    static constexpr size_t Capacity = 64;

    void Push(uint64_t submissionId, uint64_t durationNanoseconds)
    {
        std::lock_guard lock(_mutex);
        if (_count == Capacity)
        {
            _readIndex = (_readIndex + 1) % Capacity;
            --_count;
            ++_droppedTimingCount;
        }

        MGG_GpuFrameTiming& timing = _timings[_writeIndex];
        timing.SubmissionId = submissionId;
        timing.DurationNanoseconds = durationNanoseconds;
        timing.DroppedTimingCount = _droppedTimingCount;
        _writeIndex = (_writeIndex + 1) % Capacity;
        ++_count;
    }

    bool TryPop(MGG_GpuFrameTiming& timing)
    {
        std::lock_guard lock(_mutex);
        if (_count == 0)
            return false;

        timing = _timings[_readIndex];
        timing.DroppedTimingCount = _droppedTimingCount;
        _readIndex = (_readIndex + 1) % Capacity;
        --_count;
        return true;
    }

private:
    std::mutex _mutex;
    MGG_GpuFrameTiming _timings[Capacity] = {};
    size_t _readIndex = 0;
    size_t _writeIndex = 0;
    size_t _count = 0;
    uint64_t _droppedTimingCount = 0;
};
