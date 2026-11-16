package com.wilkenstech.movilcamara.audio

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import android.media.AudioFormat
import android.media.AudioRecord
import android.media.MediaRecorder
import android.os.SystemClock
import com.wilkenstech.movilcamara.connection.PinnedTls
import com.wilkenstech.movilcamara.video.VideoEndpoint
import org.json.JSONObject
import java.io.DataOutputStream
import java.util.concurrent.ArrayBlockingQueue
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
import javax.net.ssl.SSLSocket

/** Opt-in microphone; 20 ms PCM packets over the existing authenticated, pinned TLS receiver. */
class MicrophoneSender(private val context: Context, private val endpoint: () -> VideoEndpoint?, private val status: (String) -> Unit) {
    private data class Packet(val pcm: ByteArray, val timestampUs: Long)
    private val queue = ArrayBlockingQueue<Packet>(5)
    private val capture = Executors.newSingleThreadExecutor()
    private val transport = Executors.newSingleThreadExecutor()
    private val watchdog = Executors.newSingleThreadScheduledExecutor()
    private val generation = AtomicInteger()
    @Volatile private var active = false
    @Volatile private var recorder: AudioRecord? = null
    @Volatile private var socket: SSLSocket? = null
    @Volatile private var writingSince = 0L
    init {
        watchdog.scheduleWithFixedDelay({
            if (writingSince > 0 && SystemClock.elapsedRealtime() - writingSince > 1000)
                try { socket?.close() } catch (_: Exception) { }
        }, 500, 500, TimeUnit.MILLISECONDS)
    }
    fun start() {
        if (active) return
        require(context.checkSelfPermission(Manifest.permission.RECORD_AUDIO) == PackageManager.PERMISSION_GRANTED) { "Falta permiso de micrófono" }
        val run = generation.incrementAndGet()
        active = true; queue.clear()
        capture.execute {
            var local: AudioRecord? = null
            try {
                val minimum = AudioRecord.getMinBufferSize(48000, AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT)
                require(minimum > 0) { "El micrófono no admite PCM mono a 48 kHz" }
                local = AudioRecord.Builder().setAudioSource(MediaRecorder.AudioSource.MIC)
                    .setAudioFormat(AudioFormat.Builder().setSampleRate(48000).setChannelMask(AudioFormat.CHANNEL_IN_MONO)
                        .setEncoding(AudioFormat.ENCODING_PCM_16BIT).build())
                    .setBufferSizeInBytes(maxOf(minimum, 7680)).build()
                check(local.state == AudioRecord.STATE_INITIALIZED) { "No se pudo abrir el micrófono" }
                if (!active || generation.get() != run) return@execute
                recorder = local; local.startRecording()
                check(local.recordingState == AudioRecord.RECORDSTATE_RECORDING) { "No se pudo iniciar el micrófono" }
                while (active && generation.get() == run) {
                    val bytes = ByteArray(1920)
                    var offset = 0
                    while (offset < bytes.size && active && generation.get() == run) {
                        val count = local.read(bytes, offset, bytes.size - offset, AudioRecord.READ_BLOCKING)
                        check(count > 0) { "El micrófono dejó de entregar audio ($count)" }
                        offset += count
                    }
                    if (offset == bytes.size && active && generation.get() == run) {
                        val packet = Packet(bytes, SystemClock.elapsedRealtimeNanos() / 1000 - 20000)
                        if (!queue.offer(packet)) { queue.poll(); queue.offer(packet) }
                    }
                }
            } catch (ex: Exception) {
                if (active && generation.get() == run) { active = false; status("Micrófono: " + ex.localizedMessage); try { socket?.close() } catch (_: Exception) { } }
            } finally {
                try { local?.stop() } catch (_: Exception) { }
                local?.release()
                if (generation.get() == run) recorder = null
            }
        }
        transport.execute {
            while (active && generation.get() == run) {
                val target = endpoint()
                if (target == null) {
                    if (generation.get() == run) {
                        active = false
                        try { recorder?.stop() } catch (_: Exception) { }
                        status("Micrófono detenido · vínculo no disponible")
                    }
                    break
                }
                try {
                    PinnedTls.connect(target.host, target.port, target.pin).use { stream ->
                        socket = stream
                        if (!active || generation.get() != run) return@use
                        val output = DataOutputStream(stream.outputStream)
                        val hello = JSONObject().put("action", "audio").put("id", target.id).put("name", target.name)
                            .put("secret", target.secret).put("audioProtocol", 1).put("codec", "pcm_s16le").put("sampleRate", 48000).put("channels", 1)
                        output.write((hello.toString() + "\n").toByteArray(Charsets.UTF_8)); output.flush()
                        val reply = StringBuilder()
                        while (reply.length < 4096) {
                            val value = stream.inputStream.read()
                            if (value < 0) throw java.io.IOException("El PC cerró la conexión de audio")
                            if (value == 10) break
                            reply.append(value.toChar())
                        }
                        require(JSONObject(reply.toString()).optBoolean("ok")) { "Actualiza el receptor Windows para recibir audio" }
                        status("Micrófono activo · 48 kHz mono")
                        while (active && generation.get() == run) {
                            val packet = queue.poll(200, TimeUnit.MILLISECONDS) ?: continue
                            if (SystemClock.elapsedRealtimeNanos() / 1000 - packet.timestampUs > 120000) continue
                            writingSince = SystemClock.elapsedRealtime()
                            output.writeInt(packet.pcm.size); output.writeLong(packet.timestampUs); output.write(packet.pcm); output.flush()
                            writingSince = 0
                        }
                    }
                } catch (_: InterruptedException) { break }
                catch (ex: Exception) {
                    if (active && generation.get() == run) {
                        status("Audio sin conexión · reintentando")
                        try { Thread.sleep(500) } catch (_: InterruptedException) { break }
                    }
                } finally { socket = null; writingSince = 0 }
            }
        }
    }
    fun stop() {
        active = false; generation.incrementAndGet(); queue.clear()
        try { recorder?.stop() } catch (_: Exception) { }
        try { socket?.close() } catch (_: Exception) { }
        status("Micrófono apagado")
    }
    fun close() { stop(); capture.shutdownNow(); transport.shutdownNow(); watchdog.shutdownNow() }
}
