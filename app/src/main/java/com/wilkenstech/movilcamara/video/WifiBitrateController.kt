package com.wilkenstech.movilcamara.video

/** Sender-side backpressure heuristic; not a measurement of available Wi-Fi bandwidth. */
class WifiBitrateController {
    var percent = 100
        private set
    private var healthyWindows = 0
    fun congested() {
        percent = (percent * 75 / 100).coerceAtLeast(25)
        healthyWindows = 0
    }
    fun observe(averageWriteMs: Long, fps: Int, dropped: Boolean) {
        val budget = 1000L / fps.coerceIn(1, 30)
        if (dropped || averageWriteMs > budget) congested()
        else if (averageWriteMs < budget / 2) {
            healthyWindows++
            if (healthyWindows >= 8) {
                percent = (percent + 5).coerceAtMost(100)
                healthyWindows = 0
            }
        } else healthyWindows = 0
    }
}
