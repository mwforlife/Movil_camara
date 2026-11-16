package com.wilkenstech.movilcamara.camera

import java.nio.ByteBuffer

/** Bulk-copy contiguous planes and read strided planes one row at a time, not one JNI call per pixel. */
object YuvPacking {
    fun copyPlane(
        input: ByteBuffer, rowStride: Int, pixelStride: Int,
        width: Int, height: Int, output: ByteArray, offset: Int,
        outputRowStride: Int, outputPixelStride: Int, scratch: ByteArray
    ) {
        val buffer = input.duplicate()
        val base = buffer.position()
        if (pixelStride == 1 && outputPixelStride == 1 && rowStride == width && outputRowStride == width) {
            buffer.get(output, offset, width * height)
            return
        }
        val length = (width - 1) * pixelStride + 1
        for (row in 0 until height) {
            buffer.position(base + row * rowStride)
            if (pixelStride == 1 && outputPixelStride == 1) {
                buffer.get(output, offset + row * outputRowStride, width)
            } else {
                buffer.get(scratch, 0, length)
                var target = offset + row * outputRowStride
                for (column in 0 until width) {
                    output[target] = scratch[column * pixelStride]
                    target += outputPixelStride
                }
            }
        }
    }
}
