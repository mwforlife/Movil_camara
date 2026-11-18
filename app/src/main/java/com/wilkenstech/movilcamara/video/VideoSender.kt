package com.wilkenstech.movilcamara.video

import android.os.SystemClock
import com.wilkenstech.movilcamara.connection.PinnedTls
import org.json.JSONObject
import java.io.BufferedOutputStream
import java.io.DataOutputStream
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicReference
import javax.net.ssl.SSLSocket

data class EncodedFrame(val jpeg: ByteArray, val rotation: Int, val mirrored: Boolean,
    val codec: String = "jpeg", val keyFrame: Boolean = false, val ptsUs: Long = 0)
data class VideoEndpoint(val id: String, val name: String, val host: String, val port: Int, val pin: String, val secret: String)

/** Single-slot queue: slow networks drop old frames rather than accumulating latency. */
class VideoSender(private val endpoint: () -> VideoEndpoint?, private val status: (String) -> Unit) {
    private val latest = AtomicReference<EncodedFrame?>()
    private val generation = AtomicInteger()
    private val h264Queue = java.util.concurrent.ArrayBlockingQueue<EncodedFrame>(3)
    private var profile: com.wilkenstech.movilcamara.camera.CaptureProfile? = null
    @Volatile private var waitingForKey = true
    var requestKeyFrame: () -> Unit = {}
    var adjustBitrate: (Int) -> Unit = {}
    private val congestedFrames = AtomicInteger()
    fun configure(value: com.wilkenstech.movilcamara.camera.CaptureProfile) { profile = value }
    private val worker = Executors.newSingleThreadExecutor()
    private val watchdog = Executors.newSingleThreadScheduledExecutor()
    @Volatile private var socket: SSLSocket? = null
    @Volatile private var writingSince = 0L
    @Volatile private var active = false
    @Volatile var jpegQuality = 65
        private set

    init {
        watchdog.scheduleWithFixedDelay({
            if (writingSince > 0 && SystemClock.elapsedRealtime() - writingSince > 1500)
                try { socket?.close() } catch (_: Exception) { }
        }, 1, 1, TimeUnit.SECONDS)
    }
    fun offer(frame: EncodedFrame) {
        if (!active || frame.jpeg.size > 2097152) return
        if (frame.codec != "h264") { latest.set(frame); return }
        synchronized(h264Queue) {
            if (waitingForKey && !frame.keyFrame) return
            if (!h264Queue.offer(frame)) {
                congestedFrames.incrementAndGet()
                h264Queue.clear(); waitingForKey = true; requestKeyFrame()
                if (frame.keyFrame) { h264Queue.offer(frame); waitingForKey = false }
            } else if (frame.keyFrame) waitingForKey = false
        }
    }
    fun start() {
        if (active) return
        active = true
        jpegQuality = if (endpoint()?.host == "127.0.0.1") 70 else 58
        val run = generation.incrementAndGet()
        congestedFrames.set(0)
        worker.execute {
            val bitrate = WifiBitrateController()
            while (active && generation.get() == run) {
                val target = endpoint()
                if (target == null) {
                    active = false
                    status("Sesión terminada. Vuelve a emparejar con el PC.")
                    break
                }
                try {
                    val settings = profile
                    val h264 = settings?.codec == "h264"
                    if (h264) { synchronized(h264Queue) { h264Queue.clear(); waitingForKey = true }; requestKeyFrame() }
                    status("Conectando video al PC…")
                    val stream = PinnedTls.connect(target.host, target.port, target.pin)
                    socket = stream
                    stream.use {
                        if (!active || generation.get() != run) return@use
                        val output = DataOutputStream(BufferedOutputStream(stream.outputStream, 65536))
                        val hello = JSONObject().put("action", "video").put("id", target.id).put("name", target.name)
                            .put("secret", target.secret).put("videoProtocol", if (h264) 2 else 1).put("codec", if (h264) "h264" else "jpeg")
                        if (h264) hello.put("width", settings!!.width).put("height", settings.height).put("fps", settings.fps)
                        output.write((hello.toString() + "\n").toByteArray(Charsets.UTF_8)); output.flush()
                        val response = StringBuilder()
                        while (response.length < 4096) {
                            val value = stream.inputStream.read()
                            if (value == -1) throw java.io.IOException("El PC cerró la conexión")
                            if (value == 10) break
                            response.append(value.toChar())
                        }
                        val reply = JSONObject(response.toString())
                        require(reply.optBoolean("ok") && reply.optInt("videoProtocol") == if (h264) 2 else 1) {
                            reply.optString("error", "El PC rechazó el video. Actualiza ambas apps o selecciona JPEG.")
                        }
                        status("Video activo · " + (settings?.toString() ?: "JPEG"))
                        if (h264 && target.host != "127.0.0.1") adjustBitrate(bitrate.percent)
                        if (h264) requestKeyFrame()
                        var count = 0
                        var bytes = 0L
                        var start = SystemClock.elapsedRealtime()
                        var writeMillis = 0L
                        while (active && generation.get() == run) {
                            val frame = if (h264) h264Queue.poll() else latest.getAndSet(null)
                            if (frame == null) { Thread.sleep(3); continue }
                            writingSince = SystemClock.elapsedRealtime()
                            output.writeInt(frame.jpeg.size)
                            output.writeInt(frame.rotation)
                            output.writeInt((if (frame.mirrored) 1 else 0) or (if (h264 && frame.keyFrame) 2 else 0))
                            if (h264) output.writeLong(frame.ptsUs)
                            output.write(frame.jpeg); output.flush()
                            writeMillis += SystemClock.elapsedRealtime() - writingSince
                            writingSince = 0
                            count++; bytes += frame.jpeg.size
                            val elapsed = SystemClock.elapsedRealtime() - start
                            if (elapsed >= 1000) {
                                val averageWrite = writeMillis / count.coerceAtLeast(1)
                                if (h264 && target.host != "127.0.0.1") {
                                    bitrate.observe(averageWrite, settings?.fps ?: 30, congestedFrames.getAndSet(0) > 0)
                                    adjustBitrate(bitrate.percent)
                                }
                                if (!h264 && averageWrite > 60) jpegQuality = (jpegQuality - 4).coerceAtLeast(40)
                                else if (!h264 && averageWrite < 25 && count * 1000 / elapsed >= 18)
                                    jpegQuality = (jpegQuality + 2).coerceAtMost(if (target.host == "127.0.0.1") 76 else 68)
                                status("Enviando " + (count * 1000 / elapsed) + " FPS · " +
                                    String.format(java.util.Locale.US, "%.1f", bytes * 8.0 / elapsed / 1000) + " Mbps" +
                                    (if (h264 && target.host != "127.0.0.1") " · objetivo Wi-Fi ${bitrate.percent}%" else ""))
                                count = 0; bytes = 0; writeMillis = 0; start = SystemClock.elapsedRealtime()
                            }
                        }
                    }
                } catch (_: InterruptedException) { break }
                catch (ex: Exception) {
                    if (active && generation.get() == run) {
                        if (profile?.codec == "h264" && target.host != "127.0.0.1") {
                            bitrate.congested()
                        }
                        status("Video sin conexión: " + ex.localizedMessage + " · reintentando")
                        try { Thread.sleep(1000) } catch (_: InterruptedException) { break }
                    }
                } finally { socket = null; writingSince = 0 }
            }
        }
    }
    fun stop() {
        active = false; generation.incrementAndGet(); latest.set(null); h264Queue.clear(); waitingForKey = true
        try { socket?.close() } catch (_: Exception) { }
    }
    fun close() { stop(); worker.shutdownNow(); watchdog.shutdownNow() }
}
