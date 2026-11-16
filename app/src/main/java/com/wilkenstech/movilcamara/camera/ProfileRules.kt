package com.wilkenstech.movilcamara.camera

object ProfileRules {
    fun highSpeedCaptureRate(ranges: List<Pair<Int, Int>>): Int? = ranges
        .filter { it.first == it.second && it.second in 120..240 && it.second % 60 == 0 }
        .minOfOrNull { it.second }

    fun supportsRate(fps: Int, minimumFrameDurationNs: Long, ranges: List<Pair<Int, Int>>): Boolean {
        if (ranges.none { it.first <= fps && it.second >= fps }) return false
        if (fps == 60 && minimumFrameDurationNs <= 0) return false
        return minimumFrameDurationNs <= 0 || 1_000_000_000.0 / minimumFrameDurationNs + 0.5 >= fps
    }
}
