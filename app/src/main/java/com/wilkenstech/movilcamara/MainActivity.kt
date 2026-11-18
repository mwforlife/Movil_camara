package com.wilkenstech.movilcamara

import android.os.Bundle
import android.Manifest
import android.content.pm.PackageManager
import android.content.res.Configuration
import android.view.View
import android.widget.Toast
import android.widget.ArrayAdapter
import android.widget.AdapterView
import com.wilkenstech.movilcamara.camera.CameraCapabilities
import com.wilkenstech.movilcamara.camera.CameraChoice
import com.wilkenstech.movilcamara.camera.CaptureProfile
import androidx.activity.OnBackPressedCallback
import androidx.activity.result.contract.ActivityResultContracts
import com.wilkenstech.movilcamara.camera.CameraController
import com.wilkenstech.movilcamara.video.VideoSender
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import androidx.core.view.ViewCompat
import androidx.core.view.WindowInsetsCompat
import com.journeyapps.barcodescanner.ScanContract
import com.journeyapps.barcodescanner.ScanOptions
import com.wilkenstech.movilcamara.connection.ConnectionManager
import com.wilkenstech.movilcamara.databinding.ActivityMainBinding
import com.wilkenstech.movilcamara.databinding.DialogManualPairingBinding

class MainActivity : AppCompatActivity() {
    private lateinit var binding: ActivityMainBinding
    private lateinit var connection: ConnectionManager
    private var invitationDialog: AlertDialog? = null
    private var waitingForApproval = false
    private lateinit var camera: CameraController
    private lateinit var sender: VideoSender
    private lateinit var microphone: com.wilkenstech.movilcamara.audio.MicrophoneSender
    @Volatile private var remoteMicrophoneAllowed = false
    private var captureEpoch = 0L
    private var microphoneCommandEpoch = 0L
    private var preparing = false
    private var wasPaired = false
    private var localMicrophoneBusy = false
    private var previewProfile: CaptureProfile? = null
    private var screenResumed = false
    private var pendingTransmitPermission = false
    private var livePermissionEpoch = -1L
    private var orientationBeforeTransmission: Int? = null
    private fun setTransmissionOrientation(orientation: Int) {
        if (binding.cameraPanel.root.visibility != View.VISIBLE) return
        if (orientationBeforeTransmission == null) orientationBeforeTransmission = requestedOrientation
        requestedOrientation = orientation
    }
    private fun refreshOrientationButtons() {
        val portrait = resources.configuration.orientation == Configuration.ORIENTATION_PORTRAIT
        tagged<com.google.android.material.button.MaterialButton>("orientation_portrait").isChecked = portrait
        tagged<com.google.android.material.button.MaterialButton>("orientation_landscape").isChecked = !portrait
    }
    private fun <T : View> tagged(name: String): T = binding.root.findViewWithTag(name)
    private fun showPreparation(show: Boolean) {
        preparing = show
        tagged<View>("connect_stage").visibility = if (show) View.GONE else View.VISIBLE
        tagged<View>("prepare_stage").visibility = if (show) View.VISIBLE else View.GONE
        binding.connectionScreen.smoothScrollTo(0, 0)
        if (show) startPreparationPreview() else if (previewProfile != null) { camera.stop(); previewProfile = null }
    }
    private fun startPreparationPreview() {
        if (!::camera.isInitialized || !screenResumed || !preparing || binding.connectionScreen.visibility != View.VISIBLE) return
        val permission = checkSelfPermission(Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED
        tagged<View>("preview_permission").visibility = if (permission) View.GONE else View.VISIBLE
        if (!permission) return
        val profile = binding.qualityProfile.selectedItem as? CaptureProfile ?: return
        if (!binding.qualityProfile.isEnabled || previewProfile == profile) return
        camera.usePreview(tagged("prepare_preview"))
        camera.configureForNetwork(false)
        camera.selectedProfile = profile
        previewProfile = profile
        tagged<android.widget.TextView>("prepare_preview_status").setText(R.string.mobile_preview_only)
        camera.start(previewOnly = true)
    }
    private fun refreshConnectionDesign() {
        if (!::connection.isInitialized) return
        val endpoint = connection.videoEndpoint()
        val transport = if (endpoint?.host == "127.0.0.1") "USB" else "Wi-Fi"
        val label = endpoint?.let { getString(R.string.mobile_transport, transport) + " · " + if (transport == "USB") "PC" else it.host }
            ?: getString(R.string.mobile_no_pc)
        tagged<android.widget.TextView>("linked_pc").text = label
        tagged<android.widget.TextView>("prepare_pc").text = label
        tagged<View>("prepare_open").isEnabled = connection.isPaired
        if (connection.isPaired != wasPaired) showPreparation(connection.isPaired)
        wasPaired = connection.isPaired
    }
    private fun refreshMicrophoneDesign() {
        val button = tagged<android.widget.Button>("microphone_toggle")
        button.isEnabled = !localMicrophoneBusy && connection.isPaired
        button.setText(if (microphone.isRecording) R.string.mobile_mic_stop else R.string.mobile_mic_start)
    }
    private val qualityPrefs by lazy { getSharedPreferences("camera_settings", MODE_PRIVATE) }
    private var qualityContext = "local"
    private fun profileKey(profile: CaptureProfile) = "${profile.width}x${profile.height}:${profile.fps}:${profile.codec}:${profile.fps}"
    private fun restoreQualityContext() {
        if (!::connection.isInitialized || binding.cameraChoice.count == 0) return
        val endpoint = connection.videoEndpoint() ?: return
        val key = endpoint.pin + if (endpoint.host == "127.0.0.1") ":usb" else ":wifi"
        if (key == qualityContext) return
        qualityContext = key
        val savedCamera = qualityPrefs.getString("$key:camera", null)
        val index = (0 until binding.cameraChoice.count).firstOrNull {
            (binding.cameraChoice.getItemAtPosition(it) as CameraChoice).id == savedCamera
        }
        if (index != null && index != binding.cameraChoice.selectedItemPosition) binding.cameraChoice.setSelection(index)
        else (binding.cameraChoice.selectedItem as? CameraChoice)?.let { loadProfiles(it) }
    }
    private val capabilityWorker = java.util.concurrent.Executors.newSingleThreadExecutor()
    private val capabilityGeneration = java.util.concurrent.atomic.AtomicInteger()
    private val cameraPermission = registerForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
        val transmit = pendingTransmitPermission
        pendingTransmitPermission = false
        if (granted) { if (transmit) showCamera() else startPreparationPreview() }
        else Toast.makeText(this, R.string.camera_permission_needed, Toast.LENGTH_LONG).show()
    }
    private val liveMicrophonePermission = registerForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
        if (granted && livePermissionEpoch == captureEpoch && binding.cameraPanel.root.visibility == View.VISIBLE && connection.isPaired) {
            remoteMicrophoneAllowed = true
            tagged<View>("microphone_toggle").performClick()
        } else if (!granted) Toast.makeText(this, R.string.microphone_denied, Toast.LENGTH_LONG).show()
        refreshMicrophoneDesign()
    }
    private val microphonePermission = registerForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
        if (!granted) {
            binding.sendMicrophone.isChecked = false
            Toast.makeText(this, R.string.microphone_denied, Toast.LENGTH_LONG).show()
        }
        showCamera()
    }
    private val scanner = registerForActivityResult(ScanContract()) { result ->
        result.contents?.let { connection.pair(it) }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)
        binding.capabilityDetails.setOnClickListener {
            val choice = binding.cameraChoice.selectedItem as? CameraChoice ?: return@setOnClickListener
            binding.capabilityDetails.isEnabled = false
            capabilityWorker.execute {
                val report = try {
                    CameraCapabilities(getSystemService(android.hardware.camera2.CameraManager::class.java)).diagnostic(choice)
                } catch (ex: Exception) { "No se pudo consultar: ${ex.localizedMessage}" }
                runOnUiThread {
                    if (!isDestroyed && !isFinishing) {
                        binding.capabilityDetails.isEnabled = true
                        AlertDialog.Builder(this).setTitle(R.string.capability_details).setMessage(report)
                            .setPositiveButton(R.string.copy_diagnostic) { _, _ ->
                                getSystemService(android.content.ClipboardManager::class.java)
                                    .setPrimaryClip(android.content.ClipData.newPlainText("WT MultiCam", report))
                            }.setNegativeButton(android.R.string.ok, null).show()
                    }
                }
            }
        }
        connection = ConnectionManager(applicationContext, onStatus = { status ->
            runOnUiThread {
                if (!isDestroyed) {
                    if (waitingForApproval && !status.startsWith("Código ")) {
                        invitationDialog?.dismiss()
                        invitationDialog = null
                        waitingForApproval = false
                    }
                    if (binding.connectionStatus.text.toString() != status) binding.connectionStatus.text = status
                    binding.networkAddresses.text = ConnectionManager.addresses()
                    refreshStartButton()
                    refreshConnectionDesign()
                    restoreQualityContext()
                    if (!connection.isPaired && binding.cameraPanel.root.visibility == View.VISIBLE) stopCamera()
                }
            }
        }, onInvitation = { payload, secret, code ->
            runOnUiThread {
                if (!isDestroyed && !isFinishing && lifecycle.currentState.isAtLeast(androidx.lifecycle.Lifecycle.State.STARTED)) {
                    val host = org.json.JSONObject(payload).getString("host")
                    invitationDialog = AlertDialog.Builder(this)
                        .setTitle(R.string.connection_request_title)
                        .setMessage(getString(R.string.connection_request_message, host, code))
                        .setPositiveButton(R.string.continue_connection) { _, _ ->
                            waitingForApproval = true
                            invitationDialog = AlertDialog.Builder(this)
                                .setTitle(R.string.verify_connection_title)
                                .setMessage(getString(R.string.verify_connection_message, code))
                                .setNegativeButton(R.string.cancel) { _, _ ->
                                    waitingForApproval = false
                                    connection.disconnect()
                                }
                                .setOnCancelListener { waitingForApproval = false; connection.disconnect() }
                                .show().apply { setCanceledOnTouchOutside(false) }
                            connection.acceptInvitation(payload, secret, code)
                        }
                        .setNegativeButton(R.string.reject_connection) { _, _ -> connection.dismissInvitation() }
                        .setOnCancelListener { connection.dismissInvitation() }
                        .show()
                } else connection.dismissInvitation()
            }
        }, onUsbInvitation = { payload, secret ->
            runOnUiThread {
                if (!isDestroyed && !isFinishing && lifecycle.currentState.isAtLeast(androidx.lifecycle.Lifecycle.State.STARTED)) {
                    invitationDialog = AlertDialog.Builder(this)
                        .setTitle(R.string.usb_authorize_title)
                        .setMessage(R.string.usb_authorize_message)
                        .setPositiveButton(R.string.usb_authorize) { _, _ -> connection.acceptInvitation(payload, secret, "USB") }
                        .setNegativeButton(R.string.reject_connection) { _, _ -> connection.rejectUsbInvitation(payload) }
                        .setOnCancelListener { connection.rejectUsbInvitation(payload) }
                        .show()
                } else connection.dismissInvitation()
            }
        })
        sender = VideoSender(connection::videoEndpoint) { status ->
            runOnUiThread {
                if (!isDestroyed) {
                    binding.cameraPanel.videoStatus.text = status
                    val transport = if (connection.videoEndpoint()?.host == "127.0.0.1") "USB" else "Wi-Fi"
                    tagged<android.widget.TextView>("transmission_badge").text = "$transport · $status"
                }
            }
        }
        microphone = com.wilkenstech.movilcamara.audio.MicrophoneSender(applicationContext, connection::videoEndpoint) { status ->
            runOnUiThread { if (!isDestroyed) { binding.cameraPanel.audioStatus.text = status; refreshMicrophoneDesign() } }
        }
        camera = CameraController(this, binding.cameraPanel.previewTexture,
            { frame -> sender.offer(frame); binding.cameraPanel.wifiPreview.offer(frame) },
            { status -> runOnUiThread { if (!isDestroyed) { binding.cameraPanel.captureStatus.text = status; refreshMicrophoneDesign() } } },
            { error -> runOnUiThread {
                if (!isDestroyed) {
                    if (binding.cameraPanel.root.visibility != View.VISIBLE) {
                        tagged<android.widget.TextView>("prepare_preview_status").text = error
                        previewProfile = null
                        return@runOnUiThread
                    }
                    sender.stop()
                    remoteMicrophoneAllowed = false
                    captureEpoch++
                    microphone.stop()
                    binding.cameraPanel.captureStatus.text = error
                    binding.cameraPanel.videoStatus.text = getString(R.string.video_stopped)
                    tagged<android.widget.TextView>("transmission_badge").text = getString(R.string.video_stopped)
                    tagged<View>("capture_details").visibility = View.VISIBLE
                    tagged<android.widget.Button>("controls_toggle").setText(R.string.mobile_controls_action)
                    refreshMicrophoneDesign()
                }
            } }, jpegQuality = { sender.jpegQuality })
        sender.requestKeyFrame = camera::requestKeyFrame
        sender.adjustBitrate = camera::adjustBitrate
        connection.controlsProvider = {
            camera.remoteControls().put("audioControlSupported", true)
                .put("microphoneAllowed", remoteMicrophoneAllowed && checkSelfPermission(Manifest.permission.RECORD_AUDIO) == PackageManager.PERMISSION_GRANTED)
                .put("microphoneActive", microphone.isRecording)
        }
        connection.controlHandler = { command ->
            if (command.optString("operation") == "microphone") applyRemoteMicrophone(command)
            else camera.applyRemoteControl(command)
        }
        binding.cameraChoice.onItemSelectedListener = object : AdapterView.OnItemSelectedListener {
            override fun onItemSelected(parent: AdapterView<*>?, view: View?, position: Int, id: Long) {
                (binding.cameraChoice.selectedItem as? CameraChoice)?.let { loadProfiles(it) }
            }
            override fun onNothingSelected(parent: AdapterView<*>?) = Unit
        }
        binding.qualityProfile.onItemSelectedListener = object : AdapterView.OnItemSelectedListener {
            override fun onItemSelected(parent: AdapterView<*>?, view: View?, position: Int, id: Long) { refreshStartButton(); startPreparationPreview() }
            override fun onNothingSelected(parent: AdapterView<*>?) { refreshStartButton() }
        }
        try {
            val choices = CameraCapabilities(getSystemService(android.hardware.camera2.CameraManager::class.java)).cameras()
            binding.cameraChoice.adapter = ArrayAdapter(this, R.layout.spinner_profile_item, choices)
        } catch (ex: Exception) { binding.capabilityStatus.text = ex.localizedMessage }
        binding.startVideoButton.setOnClickListener {
            if (checkSelfPermission(Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED) showCamera()
            else { pendingTransmitPermission = true; cameraPermission.launch(Manifest.permission.CAMERA) }
        }
        binding.cameraPanel.stopVideoButton.setOnClickListener { stopCamera() }
        tagged<View>("capture_actions").addOnLayoutChangeListener { _, _, top, _, bottom, _, oldTop, _, oldBottom ->
            if (bottom - top != oldBottom - oldTop) {
                val scroll = tagged<View>("capture_scroll")
                scroll.layoutParams = (scroll.layoutParams as android.widget.FrameLayout.LayoutParams).apply {
                    bottomMargin = bottom - top + (8 * resources.displayMetrics.density).toInt()
                }
            }
        }
        tagged<View>("prepare_preview_container").addOnLayoutChangeListener { view, left, _, right, _, oldLeft, _, oldRight, _ ->
            if (right - left > 0 && right - left != oldRight - oldLeft) {
                view.layoutParams = view.layoutParams.apply { height = (right - left) * 9 / 16 }
            }
        }
        tagged<View>("quality_settings").setOnClickListener { stopCamera() }
        tagged<View>("orientation_portrait").setOnClickListener {
            setTransmissionOrientation(android.content.pm.ActivityInfo.SCREEN_ORIENTATION_PORTRAIT)
            refreshOrientationButtons()
        }
        tagged<View>("orientation_landscape").setOnClickListener {
            setTransmissionOrientation(android.content.pm.ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE)
            refreshOrientationButtons()
        }
        tagged<View>("preview_permission").setOnClickListener {
            pendingTransmitPermission = false
            cameraPermission.launch(Manifest.permission.CAMERA)
        }
        tagged<View>("usb_help").setOnClickListener {
            AlertDialog.Builder(this).setTitle(R.string.mobile_usb).setMessage(R.string.mobile_usb_help)
                .setPositiveButton(android.R.string.ok, null).show()
        }
        tagged<View>("prepare_open").setOnClickListener { if (connection.isPaired) showPreparation(true) }
        tagged<View>("prepare_back").setOnClickListener { showPreparation(false) }
        tagged<View>("connection_details_toggle").setOnClickListener {
            val details = tagged<View>("connection_details")
            details.visibility = if (details.visibility == View.VISIBLE) View.GONE else View.VISIBLE
        }
        tagged<android.widget.Button>("controls_toggle").setOnClickListener {
            val details = tagged<View>("capture_details")
            val visible = details.visibility != View.VISIBLE
            details.visibility = if (visible) View.VISIBLE else View.GONE
            (it as android.widget.Button).setText(if (visible) R.string.mobile_controls_action else R.string.mobile_controls_action)
        }
        tagged<View>("microphone_toggle").setOnClickListener {
            if (localMicrophoneBusy || !camera.remoteControls().optBoolean("active")) return@setOnClickListener
            if (!remoteMicrophoneAllowed) {
                if (checkSelfPermission(Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
                    livePermissionEpoch = captureEpoch
                    liveMicrophonePermission.launch(Manifest.permission.RECORD_AUDIO)
                    return@setOnClickListener
                }
                remoteMicrophoneAllowed = true
            }
            microphoneCommandEpoch++
            if (microphone.isRecording) {
                microphone.stop(); binding.sendMicrophone.isChecked = false; refreshMicrophoneDesign()
            } else {
                val epoch = captureEpoch
                val commandEpoch = microphoneCommandEpoch
                localMicrophoneBusy = true; refreshMicrophoneDesign()
                try {
                    microphone.start().whenComplete { _, error -> runOnUiThread {
                        if (epoch == captureEpoch && commandEpoch == microphoneCommandEpoch && !isDestroyed) {
                            localMicrophoneBusy = false
                            binding.sendMicrophone.isChecked = error == null && microphone.isRecording
                            refreshMicrophoneDesign()
                        }
                    } }
                } catch (ex: Exception) {
                    localMicrophoneBusy = false; binding.cameraPanel.audioStatus.text = ex.localizedMessage; refreshMicrophoneDesign()
                }
            }
        }
        binding.cameraPanel.switchCameraButton.setOnClickListener {
            stopCamera()
            if (binding.cameraChoice.count > 1) binding.cameraChoice.setSelection((binding.cameraChoice.selectedItemPosition + 1) % binding.cameraChoice.count)
            Toast.makeText(this, R.string.profile_after_switch, Toast.LENGTH_LONG).show()
        }
        onBackPressedDispatcher.addCallback(this, object : OnBackPressedCallback(true) {
            override fun handleOnBackPressed() {
                if (binding.cameraPanel.root.visibility == View.VISIBLE) stopCamera()
                else if (preparing) showPreparation(false) else finish()
            }
        })
        ViewCompat.setOnApplyWindowInsetsListener(binding.root) { view, insets ->
            val bars = insets.getInsets(WindowInsetsCompat.Type.systemBars())
            view.setPadding(bars.left, bars.top, bars.right, bars.bottom)
            insets
        }
        ViewCompat.requestApplyInsets(binding.root)
        binding.scanQrButton.setOnClickListener {
            scanner.launch(ScanOptions().setDesiredBarcodeFormats(ScanOptions.QR_CODE)
                .setPrompt(getString(R.string.qr_prompt)).setBeepEnabled(false).setOrientationLocked(false))
        }
        binding.manualPairButton.setOnClickListener {
            val form = DialogManualPairingBinding.inflate(layoutInflater)
            AlertDialog.Builder(this)
                .setTitle(R.string.manual_pairing)
                .setView(form.root)
                .setPositiveButton(R.string.pair_action) { _, _ -> connection.pair(form.pairingCodeInput.text.toString().trim()) }
                .setNegativeButton(R.string.cancel, null).show()
        }
        binding.disconnectButton.setOnClickListener { stopCamera(); connection.disconnect() }
        binding.forgetUsbButton.setOnClickListener {
            stopCamera()
            connection.forgetUsbTrust()
            Toast.makeText(this, R.string.usb_trust_forgotten, Toast.LENGTH_LONG).show()
        }
        refreshConnectionDesign()
    }

    private fun showCamera() {
        if (!connection.isPaired) {
            Toast.makeText(this, R.string.pair_before_video, Toast.LENGTH_LONG).show()
            return
        }
        val profile = binding.qualityProfile.selectedItem as? CaptureProfile
        if (profile == null) { Toast.makeText(this, R.string.profile_needed, Toast.LENGTH_LONG).show(); return }
        if (binding.sendMicrophone.isChecked && checkSelfPermission(Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
            microphonePermission.launch(Manifest.permission.RECORD_AUDIO)
            return
        }
        qualityPrefs.edit().putString("$qualityContext:camera", profile.cameraId)
            .putString("$qualityContext:${profile.cameraId}:profile", profileKey(profile)).apply()
        binding.connectionScreen.visibility = View.GONE
        captureEpoch++
        remoteMicrophoneAllowed = binding.sendMicrophone.isChecked &&
            checkSelfPermission(Manifest.permission.RECORD_AUDIO) == PackageManager.PERMISSION_GRANTED
        binding.cameraPanel.root.visibility = View.VISIBLE
        refreshOrientationButtons()
        camera.stop()
        previewProfile = null
        camera.usePreview(binding.cameraPanel.previewTexture)
        tagged<android.widget.TextView>("profile_summary").text = getString(R.string.mobile_profile, profile.width, profile.height, profile.fps, profile.codec.uppercase(java.util.Locale.ROOT))
        tagged<View>("capture_details").visibility = View.GONE
        tagged<android.widget.Button>("controls_toggle").setText(R.string.mobile_controls_action)
        localMicrophoneBusy = false
        refreshMicrophoneDesign()
        binding.root.keepScreenOn = true
        binding.cameraPanel.captureStatus.setText(R.string.camera_preparing)
        val wifi = connection.videoEndpoint()?.host != "127.0.0.1"
        camera.configureForNetwork(wifi)
        camera.selectedProfile = profile
        sender.configure(profile)
        val jpegPreview = wifi && profile.codec == "jpeg"
        binding.cameraPanel.previewTexture.alpha = if (jpegPreview) 0f else 1f
        binding.cameraPanel.wifiPreview.visibility = if (jpegPreview) View.VISIBLE else View.GONE
        if (jpegPreview) binding.cameraPanel.wifiPreview.start() else binding.cameraPanel.wifiPreview.stop()
        sender.start()
        camera.start()
        if (binding.sendMicrophone.isChecked) {
            try { microphone.start() }
            catch (ex: Exception) { binding.cameraPanel.audioStatus.text = "Micrófono: " + ex.localizedMessage }
        }
    }

    private fun refreshStartButton() {
        binding.startVideoButton.isEnabled = ::connection.isInitialized && connection.isPaired &&
            binding.qualityProfile.isEnabled && binding.qualityProfile.selectedItem is CaptureProfile
    }

    private fun loadProfiles(choice: CameraChoice) {
        val contextKey = qualityContext
        val generation = capabilityGeneration.incrementAndGet()
        binding.qualityProfile.isEnabled = false
        binding.capabilityStatus.setText(R.string.loading_capabilities)
        refreshStartButton()
        capabilityWorker.execute {
            try {
                val profiles = CameraCapabilities(getSystemService(android.hardware.camera2.CameraManager::class.java)).profiles(choice)
                runOnUiThread {
                    if (!isDestroyed && generation == capabilityGeneration.get()) {
                        binding.qualityProfile.adapter = ArrayAdapter(this, R.layout.spinner_profile_item, profiles)
                        val saved = qualityPrefs.getString("$contextKey:${choice.id}:profile", null)
                        val restored = profiles.indexOfFirst { profileKey(it) == saved }
                        val preferred = if (restored >= 0) restored else profiles.indexOfFirst { it.codec == "h264" && it.width == 1920 && it.height == 1080 && it.fps == 30 }
                        if (preferred >= 0) binding.qualityProfile.setSelection(preferred)
                        binding.qualityProfile.isEnabled = profiles.isNotEmpty()
                        binding.capabilityStatus.setText(if (profiles.isNotEmpty()) R.string.capabilities_ready else R.string.profile_needed)
                        refreshStartButton()
                        startPreparationPreview()
                    }
                }
            } catch (ex: Exception) { runOnUiThread { if (!isDestroyed) binding.capabilityStatus.text = ex.localizedMessage } }
        }
    }

    private fun stopCamera() {
        orientationBeforeTransmission?.let { requestedOrientation = it }
        orientationBeforeTransmission = null
        captureEpoch++
        localMicrophoneBusy = false
        remoteMicrophoneAllowed = false
        microphone.stop()
        binding.cameraPanel.wifiPreview.stop()
        camera.stop()
        previewProfile = null
        sender.stop()
        binding.root.keepScreenOn = false
        binding.cameraPanel.root.visibility = View.GONE
        binding.connectionScreen.visibility = View.VISIBLE
        showPreparation(connection.isPaired)
    }

    private fun applyRemoteMicrophone(command: org.json.JSONObject): org.json.JSONObject {
        val result = java.util.concurrent.CompletableFuture<org.json.JSONObject>()
        runOnUiThread {
            if (result.isDone) return@runOnUiThread
            val state = camera.remoteControls()
            val value = command.optDouble("value", Double.NaN)
            if (isDestroyed || isFinishing || !lifecycle.currentState.isAtLeast(androidx.lifecycle.Lifecycle.State.RESUMED) ||
                !connection.isPaired || !state.optBoolean("active") || state.optString("cameraId") != command.optString("cameraId") ||
                (value != 0.0 && value != 1.0)) {
                result.complete(org.json.JSONObject().put("ok", false).put("message", "La cámara o sesión cambió; inicia el envío en Android"))
            } else if (value == 0.0) {
                microphoneCommandEpoch++
                localMicrophoneBusy = false
                microphone.stop()
                binding.sendMicrophone.isChecked = false
                refreshMicrophoneDesign()
                result.complete(org.json.JSONObject().put("ok", true).put("message", "Captura de micrófono detenida"))
            } else if (!remoteMicrophoneAllowed || checkSelfPermission(Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
                result.complete(org.json.JSONObject().put("ok", false).put("message", "Autoriza Enviar micrófono al iniciar la cámara en Android"))
            } else {
                val epoch = captureEpoch
                val commandEpoch = ++microphoneCommandEpoch
                localMicrophoneBusy = false
                try {
                    microphone.start().whenComplete { _, error ->
                        runOnUiThread {
                            if (captureEpoch != epoch || microphoneCommandEpoch != commandEpoch || isDestroyed) {
                                result.complete(org.json.JSONObject().put("ok", false).put("message", "La sesión cambió"))
                            } else if (result.isCancelled) {
                                microphone.stop()
                            } else {
                                val ok = error == null && microphone.isRecording && connection.isPaired
                                if (!ok) microphone.stop()
                                binding.sendMicrophone.isChecked = ok
                                refreshMicrophoneDesign()
                                result.complete(org.json.JSONObject().put("ok", ok).put("message",
                                    if (ok) "Captura de micrófono activa" else "No se pudo abrir el micrófono"))
                            }
                        }
                    }
                } catch (ex: Exception) {
                    result.complete(org.json.JSONObject().put("ok", false).put("message", ex.localizedMessage ?: "Error de micrófono"))
                }
            }
        }
        return try { result.get(5, java.util.concurrent.TimeUnit.SECONDS) }
        catch (_: Exception) {
            result.cancel(false)
            org.json.JSONObject().put("ok", false).put("message", "Android no pudo confirmar el micrófono a tiempo")
        }
    }

    override fun onConfigurationChanged(newConfig: Configuration) {
        super.onConfigurationChanged(newConfig)
        refreshOrientationButtons()
        binding.cameraPanel.previewTexture.post { camera.transform() }
    }
    override fun onResume() {
        super.onResume()
        screenResumed = true
        startPreparationPreview()
    }
    override fun onPause() {
        screenResumed = false
        if (previewProfile != null) { camera.stop(); previewProfile = null }
        super.onPause()
    }

    override fun onStart() {
        super.onStart()
        connection.start()
        binding.networkAddresses.text = ConnectionManager.addresses()
    }
    override fun onStop() {
        stopCamera()
        invitationDialog?.dismiss()
        invitationDialog = null
        waitingForApproval = false
        connection.dismissInvitation()
        connection.stop()
        super.onStop()
    }
    override fun onDestroy() {
        capabilityGeneration.incrementAndGet()
        capabilityWorker.shutdownNow()
        binding.cameraPanel.wifiPreview.close()
        camera.close()
        sender.close()
        microphone.close()
        connection.close()
        super.onDestroy()
    }
}
