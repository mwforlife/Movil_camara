package com.wilkenstech.movilcamara.camera

import android.graphics.ImageFormat
import android.graphics.SurfaceTexture
import android.hardware.camera2.CameraCharacteristics
import android.hardware.camera2.CameraManager
import android.media.MediaCodec
import android.media.MediaCodecInfo
import android.media.MediaCodecList
import android.os.Build
import android.util.Size

data class CaptureProfile(val cameraId: String, val facing: Int, val width: Int, val height: Int,
    val fps: Int, val codec: String, val encoder: String? = null) {
    override fun toString() = width.toString() + " × " + height + " · " + fps + " FPS · " +
        if (codec == "h264") "H.264" else "JPEG"
}
data class CameraChoice(val id: String, val facing: Int, val title: String) { override fun toString() = title }

/** Reported compatibility, not a guarantee that concurrent capture/encode will sustain the rate. */
class CameraCapabilities(private val manager: CameraManager) {
    fun diagnostic(camera: CameraChoice): String = buildString {
        appendLine("${Build.MANUFACTURER} ${Build.MODEL}; Android ${Build.VERSION.RELEASE}")
        appendLine("Cámara: ${camera.title}")
        val c = manager.getCameraCharacteristics(camera.id)
        val map = c.get(CameraCharacteristics.SCALER_STREAM_CONFIGURATION_MAP)
        appendLine("Rangos FPS normales: " + c.get(CameraCharacteristics.CONTROL_AE_AVAILABLE_TARGET_FPS_RANGES)?.joinToString())
        val size = Size(1920, 1080)
        fun timing(label: String, type: Class<*>) {
            if (map == null || map.getOutputSizes(type)?.contains(size) != true) {
                appendLine("$label 1080p: no anunciado")
                return
            }
            val ns = map.getOutputMinFrameDuration(type, size)
            appendLine("$label 1080p: mínimo $ns ns; " + if (ns > 0) "máximo teórico ${1_000_000_000.0 / ns} FPS" else "duración desconocida")
        }
        timing("Captura H.264", MediaCodec::class.java)
        timing("Visor", SurfaceTexture::class.java)
        for (info in MediaCodecList(MediaCodecList.ALL_CODECS).codecInfos.filter {
            it.isEncoder && it.supportedTypes.any { type -> type.equals("video/avc", true) }
        }) {
            try {
                val caps = info.getCapabilitiesForType("video/avc")
                appendLine("${info.name}: Surface=${caps.colorFormats.contains(MediaCodecInfo.CodecCapabilities.COLOR_FormatSurface)}, 1080p60=${caps.videoCapabilities?.areSizeAndRateSupported(1920, 1080, 60.0)}")
            } catch (_: Exception) { appendLine("${info.name}: consulta no disponible") }
        }
        val highSpeed = c.get(CameraCharacteristics.REQUEST_AVAILABLE_CAPABILITIES)
            ?.contains(CameraCharacteristics.REQUEST_AVAILABLE_CAPABILITIES_CONSTRAINED_HIGH_SPEED_VIDEO) == true
        appendLine("Sesiones de alta velocidad: $highSpeed")
        if (highSpeed && map != null && map.highSpeedVideoSizes.contains(size))
            appendLine("Rangos alta velocidad 1080p: " + map.getHighSpeedVideoFpsRangesFor(size).joinToString())
        appendLine("Esta versión ofrece hasta 30 FPS; alta velocidad aplazada.")
    }
    fun cameras(): List<CameraChoice> = manager.cameraIdList.mapNotNull { id ->
        val facing = manager.getCameraCharacteristics(id).get(CameraCharacteristics.LENS_FACING) ?: return@mapNotNull null
        if (facing != CameraCharacteristics.LENS_FACING_BACK && facing != CameraCharacteristics.LENS_FACING_FRONT) return@mapNotNull null
        CameraChoice(id, facing, (if (facing == CameraCharacteristics.LENS_FACING_BACK) "Trasera" else "Frontal") + " · " + id)
    }
    fun profiles(camera: CameraChoice): List<CaptureProfile> {
        val c = manager.getCameraCharacteristics(camera.id)
        val map = c.get(CameraCharacteristics.SCALER_STREAM_CONFIGURATION_MAP) ?: return emptyList()
        val ranges = c.get(CameraCharacteristics.CONTROL_AE_AVAILABLE_TARGET_FPS_RANGES) ?: return emptyList()
        val previewSizes = map.getOutputSizes(SurfaceTexture::class.java)?.toSet() ?: emptySet()
        val hardwareEncoders = MediaCodecList(MediaCodecList.ALL_CODECS).codecInfos.filter { info ->
            info.isEncoder && !info.name.endsWith(".secure") && info.supportedTypes.any { it.equals("video/avc", true) } &&
                (if (Build.VERSION.SDK_INT >= 29) info.isHardwareAccelerated else
                    !info.name.startsWith("OMX.google.") && !info.name.startsWith("c2.android.") && !info.name.startsWith("OMX.ffmpeg."))
        }
        val result = mutableListOf<CaptureProfile>()
        fun eligible(size: Size) = size.width in 320..1920 && size.height in 240..1080 && size.width % 2 == 0 && size.height % 2 == 0
        for (size in (map.getOutputSizes(MediaCodec::class.java) ?: emptyArray()).filter(::eligible).filter { it in previewSizes }) {
            val duration = maxOf(map.getOutputMinFrameDuration(MediaCodec::class.java, size),
                map.getOutputMinFrameDuration(SurfaceTexture::class.java, size))
            for (fps in listOf(15, 24, 30)) {
                if (!ProfileRules.supportsRate(fps, duration, ranges.map { it.lower to it.upper })) continue
                val encoder = hardwareEncoders.firstOrNull { info ->
                    try {
                        val caps = info.getCapabilitiesForType("video/avc")
                        caps.colorFormats.contains(MediaCodecInfo.CodecCapabilities.COLOR_FormatSurface) &&
                            caps.videoCapabilities?.areSizeAndRateSupported(size.width, size.height, fps.toDouble()) == true
                    }
                    catch (_: Exception) { false }
                } ?: continue
                result.add(CaptureProfile(camera.id, camera.facing, size.width, size.height, fps, "h264", encoder.name))
            }
        }
        for (size in (map.getOutputSizes(ImageFormat.YUV_420_888) ?: emptyArray()).filter(::eligible).filter { it in previewSizes }) {
            val duration = map.getOutputMinFrameDuration(ImageFormat.YUV_420_888, size)
            for (fps in listOf(15, 24, 30)) {
                if (ranges.none { it.lower <= fps && it.upper >= fps }) continue
                if (duration > 0 && 1_000_000_000.0 / duration + 0.5 < fps) continue
                result.add(CaptureProfile(camera.id, camera.facing, size.width, size.height, fps, "jpeg"))
            }
        }
        return result.distinct().sortedWith(compareBy<CaptureProfile> { if (it.codec == "h264") 0 else 1 }
            .thenByDescending { it.width * it.height }.thenByDescending { it.fps })
    }
}
