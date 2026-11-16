package com.wilkenstech.movilcamara.video

import android.media.MediaCodec
import android.media.MediaCodecInfo
import android.media.MediaFormat
import android.os.Build
import android.os.Bundle
import android.view.Surface
import com.wilkenstech.movilcamara.camera.CaptureProfile
import java.util.concurrent.Executors

class H264Encoder(private val profile: CaptureProfile, private val orientation: () -> Pair<Int, Boolean>,
    private val output: (EncodedFrame) -> Unit, private val error: (String) -> Unit) {
    private val codec = MediaCodec.createByCodecName(requireNotNull(profile.encoder))
    private val worker = Executors.newSingleThreadExecutor()
    @Volatile private var active = false
    private var config = ByteArray(0)
    val surface: Surface

    init {
        val caps = requireNotNull(codec.codecInfo.getCapabilitiesForType("video/avc").encoderCapabilities)
        val desiredBitrate = (profile.width * profile.height * profile.fps * 0.10).toInt().coerceIn(800_000, 20_000_000)
        val range = requireNotNull(codec.codecInfo.getCapabilitiesForType("video/avc").videoCapabilities).bitrateRange
        val format = MediaFormat.createVideoFormat("video/avc", profile.width, profile.height).apply {
            setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatSurface)
            setInteger(MediaFormat.KEY_FRAME_RATE, profile.fps)
            setInteger(MediaFormat.KEY_BIT_RATE, desiredBitrate.coerceIn(range.lower, range.upper))
            setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 1)
            if (Build.VERSION.SDK_INT >= 29) setInteger(MediaFormat.KEY_MAX_B_FRAMES, 0)
            // Rate-limit Surface input, before AVC reference frames exist. Keep real-time PTS.
            if (Build.VERSION.SDK_INT >= 29) setFloat(MediaFormat.KEY_MAX_FPS_TO_ENCODER, profile.fps.toFloat())
            setInteger(MediaFormat.KEY_PRIORITY, 0)
            if (caps.isBitrateModeSupported(MediaCodecInfo.EncoderCapabilities.BITRATE_MODE_CBR))
                setInteger(MediaFormat.KEY_BITRATE_MODE, MediaCodecInfo.EncoderCapabilities.BITRATE_MODE_CBR)
        }
        try {
            codec.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE)
            surface = codec.createInputSurface()
        } catch (ex: Exception) { codec.release(); worker.shutdownNow(); throw ex }
    }
    fun start() {
        try { codec.start() }
        catch (ex: Exception) { codec.release(); surface.release(); worker.shutdownNow(); throw ex }
        active = true
        worker.execute {
            val info = MediaCodec.BufferInfo()
            try {
                while (active) {
                    val index = codec.dequeueOutputBuffer(info, 10000)
                    if (index == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED) {
                        val f = codec.outputFormat
                        config = listOf("csd-0", "csd-1").mapNotNull { key ->
                            f.getByteBuffer(key)?.duplicate()?.let { b -> ByteArray(b.remaining()).also { b.get(it) } }
                        }.fold(ByteArray(0)) { a, b -> a + b }
                    } else if (index >= 0) {
                        try {
                            val b = codec.getOutputBuffer(index)
                            if (b != null && info.size > 0) {
                                b.position(info.offset); b.limit(info.offset + info.size)
                                val bytes = ByteArray(info.size); b.get(bytes)
                                if (info.flags and MediaCodec.BUFFER_FLAG_CODEC_CONFIG != 0) config = bytes
                                else {
                                    val key = info.flags and MediaCodec.BUFFER_FLAG_KEY_FRAME != 0
                                    val (rotation, mirror) = orientation()
                                    output(EncodedFrame(if (key) config + bytes else bytes, rotation, mirror,
                                        "h264", key, info.presentationTimeUs))
                                }
                            }
                        } finally { codec.releaseOutputBuffer(index, false) }
                    }
                }
            } catch (ex: Exception) { if (active) error("Codificador H.264: " + ex.localizedMessage) }
            finally { try { codec.stop() } catch (_: Exception) { }; codec.release(); surface.release() }
        }
    }
    fun requestKeyFrame() {
        if (active) try { codec.setParameters(Bundle().apply { putInt(MediaCodec.PARAMETER_KEY_REQUEST_SYNC_FRAME, 0) }) } catch (_: Exception) { }
    }
    fun close() {
        val wasActive = active
        active = false
        if (!wasActive) { try { codec.release() } catch (_: Exception) { }; surface.release() }
        worker.shutdown()
    }
}
