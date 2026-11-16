package com.wilkenstech.movilcamara.camera

import kotlin.math.min

/** Geometry only: TextureView already applies sensor orientation (and the front-camera mirror).
 * We undo its default stretch, compensate DISPLAY rotation, then fit the whole frame uniformly.
 * This transform must not be applied to ImageReader frames, which have raw sensor coordinates.
 */
object PreviewGeometry {
    data class Transform(val matrix: FloatArray, val imageWidth: Int, val imageHeight: Int)

    fun calculate(
        viewWidth: Int, viewHeight: Int,
        bufferWidth: Int, bufferHeight: Int,
        sensorDegrees: Int, displayDegrees: Int
    ): Transform {
        require(viewWidth > 0 && viewHeight > 0 && bufferWidth > 0 && bufferHeight > 0)
        require(sensorDegrees in listOf(0, 90, 180, 270) && displayDegrees in listOf(0, 90, 180, 270))
        val sensorTurnsAxes = sensorDegrees % 180 != 0
        val naturalWidth = if (sensorTurnsAxes) bufferHeight else bufferWidth
        val naturalHeight = if (sensorTurnsAxes) bufferWidth else bufferHeight
        val displayTurnsAxes = displayDegrees % 180 != 0
        val imageWidth = if (displayTurnsAxes) naturalHeight else naturalWidth
        val imageHeight = if (displayTurnsAxes) naturalWidth else naturalHeight
        val uniformScale = min(viewWidth.toFloat() / imageWidth, viewHeight.toFloat() / imageHeight)

        // Android Matrix operates on view coordinates; remove the view's initial non-uniform stretch.
        val x = naturalWidth.toFloat() / viewWidth * uniformScale
        val y = naturalHeight.toFloat() / viewHeight * uniformScale
        val (cos, sin) = when (displayDegrees) {
            0 -> 1f to 0f
            90 -> 0f to -1f
            180 -> -1f to 0f
            else -> 0f to 1f
        }
        val a = cos * x
        val b = -sin * y
        val c = sin * x
        val d = cos * y
        val centerX = viewWidth / 2f
        val centerY = viewHeight / 2f
        return Transform(floatArrayOf(
            a, b, centerX - a * centerX - b * centerY,
            c, d, centerY - c * centerX - d * centerY,
            0f, 0f, 1f
        ), imageWidth, imageHeight)
    }
}
