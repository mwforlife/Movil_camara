package com.wilkenstech.movilcamara.camera

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Canvas
import android.graphics.Paint
import android.util.AttributeSet
import android.view.View
import com.wilkenstech.movilcamara.video.EncodedFrame
import java.util.concurrent.Executors
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicReference

/** Wi-Fi preview of the exact encoded frame sent to Windows; no vendor TextureView transform. */
class JpegPreviewView @JvmOverloads constructor(context: Context, attrs: AttributeSet? = null) : View(context, attrs) {
    private data class Decoded(val bitmap: Bitmap, val rotation: Int, val mirrored: Boolean, val generation: Int)
    private val executor = Executors.newSingleThreadExecutor()
    private val latest = AtomicReference<EncodedFrame?>()
    private val decoded = AtomicReference<Decoded?>()
    private val processing = AtomicBoolean(false)
    private val uiPending = AtomicBoolean(false)
    private val generation = AtomicInteger()
    private val paint = Paint(Paint.ANTI_ALIAS_FLAG or Paint.FILTER_BITMAP_FLAG)
    @Volatile private var active = false
    private var current: Decoded? = null

    fun start() { generation.incrementAndGet(); active = true; current = null; invalidate() }
    fun offer(frame: EncodedFrame) {
        if (!active) return
        latest.set(frame)
        schedule()
    }
    private fun schedule() {
        if (!active || !processing.compareAndSet(false, true)) return
        val run = generation.get()
        try { executor.execute {
            try {
                while (active && generation.get() == run) {
                    val frame = latest.getAndSet(null) ?: break
                    val bitmap = BitmapFactory.decodeByteArray(frame.jpeg, 0, frame.jpeg.size) ?: continue
                    if (!active || generation.get() != run) break
                    decoded.set(Decoded(bitmap, frame.rotation, frame.mirrored, run))
                    if (uiPending.compareAndSet(false, true)) post {
                        uiPending.set(false)
                        val image = decoded.getAndSet(null)
                        if (image != null && active && image.generation == generation.get()) {
                            current = image
                            invalidate()
                        }
                    }
                }
            } finally {
                processing.set(false)
                if (active && latest.get() != null) schedule()
            }
        } } catch (_: java.util.concurrent.RejectedExecutionException) { processing.set(false) }
    }
    override fun onDraw(canvas: Canvas) {
        super.onDraw(canvas)
        canvas.drawColor(android.graphics.Color.BLACK)
        val frame = current ?: return
        val quarterTurn = frame.rotation % 180 != 0
        val imageWidth = if (quarterTurn) frame.bitmap.height else frame.bitmap.width
        val imageHeight = if (quarterTurn) frame.bitmap.width else frame.bitmap.height
        val scale = minOf(width.toFloat() / imageWidth, height.toFloat() / imageHeight)
        canvas.save()
        canvas.translate(width / 2f, height / 2f)
        canvas.scale(scale * if (frame.mirrored) -1 else 1, scale)
        canvas.rotate(frame.rotation.toFloat())
        canvas.drawBitmap(frame.bitmap, -frame.bitmap.width / 2f, -frame.bitmap.height / 2f, paint)
        canvas.restore()
    }
    fun stop() { active = false; generation.incrementAndGet(); latest.set(null); decoded.set(null); current = null; invalidate() }
    fun close() { stop(); executor.shutdownNow() }
}
