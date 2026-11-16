package com.wilkenstech.movilcamara.camera

class FramePacer(fps: Int) {
    private val interval = 1_000_000_000L / fps.coerceAtLeast(1)
    private var next = Long.MIN_VALUE
    fun accept(timestampNs: Long): Boolean {
        if (next != Long.MIN_VALUE && timestampNs < next) return false
        next = if (next == Long.MIN_VALUE || timestampNs - next > 2 * interval) timestampNs + interval else next + interval
        return true
    }
}
