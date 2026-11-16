package com.wilkenstech.movilcamara.camera

import android.Manifest
import android.app.Activity
import android.content.pm.PackageManager
import android.graphics.*
import android.hardware.camera2.*
import android.hardware.display.DisplayManager
import android.media.Image
import android.media.ImageReader
import android.os.Handler
import android.os.HandlerThread
import android.os.SystemClock
import android.util.Size
import android.view.Surface
import android.view.TextureView
import com.wilkenstech.movilcamara.video.EncodedFrame
import java.io.ByteArrayOutputStream
import kotlin.math.abs

/** Camera capture and encoding are independent of Activity UI and network transmission. */
class CameraController(
    private val activity: Activity,
    private val preview: TextureView,
    private val onFrame: (EncodedFrame) -> Unit,
    private val onStatus: (String) -> Unit,
    private val onError: (String) -> Unit,
    private val jpegQuality: () -> Int = { 65 }
) {
    private val manager = activity.getSystemService(CameraManager::class.java)
    private val thread = HandlerThread("WT-Camera").apply { start() }
    private val handler = Handler(thread.looper)
    private val displayManager = activity.getSystemService(DisplayManager::class.java)
    private val displayListener = object : DisplayManager.DisplayListener {
        override fun onDisplayAdded(displayId: Int) = Unit
        override fun onDisplayRemoved(displayId: Int) = Unit
        override fun onDisplayChanged(displayId: Int) {
            if (wanted && preview.display?.displayId == displayId) preview.post { transform() }
        }
    }
    private var device: CameraDevice? = null
    private var session: CameraCaptureSession? = null
    private var reader: ImageReader? = null
    private var previewSurface: Surface? = null
    @Volatile private var wanted = false
    @Volatile private var generation = 0
    @Volatile private var closed = false
    private var pendingOpens = 0
    @Volatile private var facing = CameraCharacteristics.LENS_FACING_BACK
    @Volatile private var size = Size(1280, 720)
    @Volatile private var sensorRotation = 90
    private var pacer = FramePacer(30)
    private var nv21 = ByteArray(0)
    private var rowScratch = ByteArray(0)
    private val jpegBuffer = ByteArrayOutputStream(256 * 1024)
    private var encoder: com.wilkenstech.movilcamara.video.H264Encoder? = null
    @Volatile var selectedProfile: CaptureProfile? = null
    fun requestKeyFrame() { encoder?.requestKeyFrame() }
    @Volatile private var maxWidth = 1280
    @Volatile private var maxHeight = 720
    @Volatile private var profileName = "USB"
    @Volatile private var useTexturePreview = true

    fun configureForNetwork(wifi: Boolean) {
        maxWidth = if (wifi) 960 else 1280
        maxHeight = if (wifi) 540 else 720
        profileName = if (wifi) "Wi-Fi fluido" else "USB"
        useTexturePreview = !wifi
    }

    init {
        displayManager.registerDisplayListener(displayListener, Handler(activity.mainLooper))
        preview.addOnLayoutChangeListener { _, left, top, right, bottom, oldLeft, oldTop, oldRight, oldBottom ->
            if (wanted && (right - left != oldRight - oldLeft || bottom - top != oldBottom - oldTop)) transform()
        }
        preview.surfaceTextureListener = object : TextureView.SurfaceTextureListener {
            override fun onSurfaceTextureAvailable(texture: SurfaceTexture, width: Int, height: Int) {
                if (wanted) open()
            }
            override fun onSurfaceTextureSizeChanged(texture: SurfaceTexture, width: Int, height: Int) { transform() }
            override fun onSurfaceTextureDestroyed(texture: SurfaceTexture): Boolean { stop(); return true }
            override fun onSurfaceTextureUpdated(texture: SurfaceTexture) = Unit
        }
    }
    fun start() {
        if (closed) return
        wanted = true
        if (preview.isAvailable) open()
    }
    fun switchCamera() {
        selectedProfile = null
        facing = if (facing == CameraCharacteristics.LENS_FACING_BACK) CameraCharacteristics.LENS_FACING_FRONT else CameraCharacteristics.LENS_FACING_BACK
        if (wanted && preview.isAvailable) open()
    }
    @Suppress("DEPRECATION", "MissingPermission")
    private fun open() {
        val run = ++generation
        handler.post {
            releaseCamera()
            if (!wanted || run != generation) return@post
            try {
                if (activity.checkSelfPermission(Manifest.permission.CAMERA) != PackageManager.PERMISSION_GRANTED)
                    throw SecurityException("Permiso de cámara no concedido")
                val chosen = selectedProfile
                if (chosen != null) facing = chosen.facing
                val cameraId = chosen?.cameraId ?: manager.cameraIdList.firstOrNull {
                    manager.getCameraCharacteristics(it).get(CameraCharacteristics.LENS_FACING) == facing
                } ?: throw IllegalStateException("Este teléfono no tiene la cámara seleccionada")
                val characteristics = manager.getCameraCharacteristics(cameraId)
                sensorRotation = characteristics.get(CameraCharacteristics.SENSOR_ORIENTATION) ?: 90
                val map = characteristics.get(CameraCharacteristics.SCALER_STREAM_CONFIGURATION_MAP)
                    ?: throw IllegalStateException("La cámara no informa tamaños compatibles")
                val textures = map.getOutputSizes(SurfaceTexture::class.java).toSet()
                val candidates = map.getOutputSizes(ImageFormat.YUV_420_888).filter {
                    it.width <= maxWidth && it.height <= maxHeight && it.width % 2 == 0 && it.height % 2 == 0 && (!useTexturePreview || it in textures)
                }
                size = chosen?.let { Size(it.width, it.height) } ?: candidates.sortedWith(compareBy<Size> { abs(it.width.toDouble() / it.height - 16.0 / 9) }
                    .thenByDescending { it.width * it.height }).firstOrNull()
                    ?: throw IllegalStateException("No hay un perfil de vista previa compatible hasta 720p")
                val texture = preview.surfaceTexture ?: return@post
                texture.setDefaultBufferSize(size.width, size.height)
                val surface = Surface(texture)
                previewSurface = surface
                val targetFps = chosen?.fps ?: 30
                val h264 = chosen?.codec == "h264"
                useTexturePreview = h264 || useTexturePreview
                val imageReader = ImageReader.newInstance(size.width, size.height, ImageFormat.YUV_420_888, 3)
                reader = imageReader
                val videoSurface = if (h264) {
                    encoder = com.wilkenstech.movilcamara.video.H264Encoder(chosen!!,
                        { outputRotation() to (facing == CameraCharacteristics.LENS_FACING_FRONT) },
                        { frame -> if (wanted && generation == run) onFrame(frame) },
                        { message -> fail(run, message) }).also { it.start() }
                    encoder!!.surface
                } else imageReader.surface
                pacer = FramePacer(targetFps)
                imageReader.setOnImageAvailableListener({ source ->
                    val image = try { source.acquireLatestImage() } catch (_: Exception) { null }
                    image?.use {
                        if (!wanted || generation != run) return@use
                        val now = if (it.timestamp > 0) it.timestamp else SystemClock.elapsedRealtimeNanos()
                        if (!pacer.accept(now)) return@use
                        try {
                            val jpeg = encode(it)
                            onFrame(EncodedFrame(jpeg, outputRotation(), facing == CameraCharacteristics.LENS_FACING_FRONT))
                        } catch (ex: Exception) { fail(run, "No se pudo procesar la cámara: " + ex.localizedMessage) }
                    }
                }, handler)
                activity.runOnUiThread { transform() }
                var awaitingOpen = true
                val nativePreview = useTexturePreview
                fun completeOpen() {
                    if (awaitingOpen) { pendingOpens--; awaitingOpen = false }
                    if (closed && pendingOpens == 0) thread.quitSafely()
                }
                pendingOpens++
                try { manager.openCamera(cameraId, object : CameraDevice.StateCallback() {
                    override fun onOpened(camera: CameraDevice) {
                        if (!wanted || generation != run) { camera.close(); completeOpen(); return }
                        completeOpen()
                        device = camera
                        try {
                            val outputs = if (nativePreview) listOf(surface, videoSurface) else listOf(videoSurface)
                            camera.createCaptureSession(outputs, object : CameraCaptureSession.StateCallback() {
                                override fun onConfigured(configured: CameraCaptureSession) {
                                    if (!wanted || generation != run) { configured.close(); return }
                                    session = configured
                                    try {
                                        val request = camera.createCaptureRequest(if (h264) CameraDevice.TEMPLATE_RECORD else CameraDevice.TEMPLATE_PREVIEW).apply {
                                            if (nativePreview) addTarget(surface)
                                            addTarget(videoSurface)
                                            set(CaptureRequest.CONTROL_MODE, CameraMetadata.CONTROL_MODE_AUTO)
                                            characteristics.get(CameraCharacteristics.CONTROL_AE_AVAILABLE_TARGET_FPS_RANGES)
                                                ?.filter { it.lower <= targetFps && it.upper >= targetFps }
                                                ?.minByOrNull { (it.upper - targetFps) * 100 + (targetFps - it.lower) }
                                                ?.let { set(CaptureRequest.CONTROL_AE_TARGET_FPS_RANGE, it) }
                                            val afModes = characteristics.get(CameraCharacteristics.CONTROL_AF_AVAILABLE_MODES) ?: intArrayOf()
                                            if (afModes.contains(CameraMetadata.CONTROL_AF_MODE_CONTINUOUS_VIDEO))
                                                set(CaptureRequest.CONTROL_AF_MODE, CameraMetadata.CONTROL_AF_MODE_CONTINUOUS_VIDEO)
                                        }
                                        configured.setRepeatingRequest(request.build(), null, handler)
                                        activity.runOnUiThread { transform() }
                                    } catch (ex: Exception) { fail(run, "No se pudo iniciar captura: " + ex.localizedMessage) }
                                }
                                override fun onConfigureFailed(configured: CameraCaptureSession) { fail(run, "La cámara rechazó el perfil. Detén la cámara y selecciona menos resolución o FPS.") }
                            }, handler)
                        } catch (ex: Exception) { fail(run, "No se pudo preparar cámara: " + ex.localizedMessage) }
                    }
                    override fun onDisconnected(camera: CameraDevice) { camera.close(); completeOpen(); fail(run, "La cámara se desconectó") }
                    override fun onError(camera: CameraDevice, error: Int) { camera.close(); completeOpen(); fail(run, "Error de cámara " + error) }
                }, handler) } catch (ex: Exception) { completeOpen(); throw ex }
            } catch (ex: Exception) { fail(run, ex.localizedMessage ?: "No se pudo abrir la cámara") }
        }
    }
    private fun fail(run: Int, message: String) {
        if (generation != run) return
        wanted = false
        generation++
        handler.post { releaseCamera() }
        onError(message)
    }
    @Suppress("DEPRECATION")
    private fun displayDegrees(): Int = when (activity.windowManager.defaultDisplay.rotation) {
        Surface.ROTATION_90 -> 90
        Surface.ROTATION_180 -> 180
        Surface.ROTATION_270 -> 270
        else -> 0
    }
    private fun outputRotation(): Int = if (facing == CameraCharacteristics.LENS_FACING_FRONT)
        (sensorRotation + displayDegrees()) % 360 else (sensorRotation - displayDegrees() + 360) % 360

    /** TextureView already rotates the sensor buffer and mirrors the front camera.
     * Only compensate the display here. Raw frames keep their own outputRotation().
     */
    fun transform() {
        if (preview.width == 0 || preview.height == 0) return
        val geometry = PreviewGeometry.calculate(preview.width, preview.height,
            size.width, size.height, sensorRotation, displayDegrees())
        val matrix = Matrix().apply { setValues(geometry.matrix) }
        preview.setTransform(matrix)
        if (wanted) {
            val orientation = if (geometry.imageHeight > geometry.imageWidth) "Vertical" else "Horizontal"
            onStatus(profileName + " · " + orientation + " · " + geometry.imageWidth + " × " + geometry.imageHeight + " · " + (selectedProfile?.fps ?: 30) + " FPS · " + (selectedProfile?.codec ?: "JPEG"))
        }
    }

    private fun encode(image: Image): ByteArray {
        val w = image.width
        val h = image.height
        val required = w * h * 3 / 2
        if (nv21.size != required) nv21 = ByteArray(required)
        for (planeIndex in 0..2) {
            val plane = image.planes[planeIndex]
            val planeWidth = if (planeIndex == 0) w else w / 2
            val planeHeight = if (planeIndex == 0) h else h / 2
            if (rowScratch.size < planeWidth * plane.pixelStride) rowScratch = ByteArray(planeWidth * plane.pixelStride)
            val offset = if (planeIndex == 0) 0 else w * h + if (planeIndex == 2) 0 else 1
            YuvPacking.copyPlane(plane.buffer, plane.rowStride, plane.pixelStride, planeWidth, planeHeight,
                nv21, offset, w, if (planeIndex == 0) 1 else 2, rowScratch)
        }
        val bytes = jpegBuffer
        bytes.reset()
        check(YuvImage(nv21, ImageFormat.NV21, w, h, null).compressToJpeg(Rect(0, 0, w, h), jpegQuality().coerceIn(40, 80), bytes)) {
            "No se pudo codificar la imagen"
        }
        return bytes.toByteArray()
    }
    private fun releaseCamera() {
        try { session?.stopRepeating() } catch (_: Exception) { }
        session?.close(); session = null
        device?.close(); device = null
        encoder?.close(); encoder = null
        reader?.close(); reader = null
        previewSurface?.release(); previewSurface = null
    }
    fun stop() {
        wanted = false; generation++
        handler.post { releaseCamera() }
    }
    fun close() {
        displayManager.unregisterDisplayListener(displayListener)
        closed = true
        wanted = false; generation++
        handler.post { releaseCamera(); if (pendingOpens == 0) thread.quitSafely() }
    }
}
