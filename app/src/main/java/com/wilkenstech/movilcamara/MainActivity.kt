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
        if (granted) showCamera() else Toast.makeText(this, R.string.camera_permission_needed, Toast.LENGTH_LONG).show()
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
            runOnUiThread { if (!isDestroyed) binding.cameraPanel.videoStatus.text = status }
        }
        microphone = com.wilkenstech.movilcamara.audio.MicrophoneSender(applicationContext, connection::videoEndpoint) { status ->
            runOnUiThread { if (!isDestroyed) binding.cameraPanel.audioStatus.text = status }
        }
        camera = CameraController(this, binding.cameraPanel.previewTexture,
            { frame -> sender.offer(frame); binding.cameraPanel.wifiPreview.offer(frame) },
            { status -> runOnUiThread { if (!isDestroyed) binding.cameraPanel.captureStatus.text = status } },
            { error -> runOnUiThread {
                if (!isDestroyed) {
                    sender.stop()
                    microphone.stop()
                    binding.cameraPanel.captureStatus.text = error
                    binding.cameraPanel.videoStatus.text = getString(R.string.video_stopped)
                }
            } }, jpegQuality = { sender.jpegQuality })
        sender.requestKeyFrame = camera::requestKeyFrame
        connection.controlsProvider = camera::remoteControls
        connection.controlHandler = camera::applyRemoteControl
        binding.cameraChoice.onItemSelectedListener = object : AdapterView.OnItemSelectedListener {
            override fun onItemSelected(parent: AdapterView<*>?, view: View?, position: Int, id: Long) {
                (binding.cameraChoice.selectedItem as? CameraChoice)?.let { loadProfiles(it) }
            }
            override fun onNothingSelected(parent: AdapterView<*>?) = Unit
        }
        binding.qualityProfile.onItemSelectedListener = object : AdapterView.OnItemSelectedListener {
            override fun onItemSelected(parent: AdapterView<*>?, view: View?, position: Int, id: Long) { refreshStartButton() }
            override fun onNothingSelected(parent: AdapterView<*>?) { refreshStartButton() }
        }
        try {
            val choices = CameraCapabilities(getSystemService(android.hardware.camera2.CameraManager::class.java)).cameras()
            binding.cameraChoice.adapter = ArrayAdapter(this, R.layout.spinner_profile_item, choices)
        } catch (ex: Exception) { binding.capabilityStatus.text = ex.localizedMessage }
        binding.startVideoButton.setOnClickListener {
            if (checkSelfPermission(Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED) showCamera()
            else cameraPermission.launch(Manifest.permission.CAMERA)
        }
        binding.cameraPanel.stopVideoButton.setOnClickListener { stopCamera() }
        binding.cameraPanel.switchCameraButton.setOnClickListener {
            stopCamera()
            if (binding.cameraChoice.count > 1) binding.cameraChoice.setSelection((binding.cameraChoice.selectedItemPosition + 1) % binding.cameraChoice.count)
            Toast.makeText(this, R.string.profile_after_switch, Toast.LENGTH_LONG).show()
        }
        onBackPressedDispatcher.addCallback(this, object : OnBackPressedCallback(true) {
            override fun handleOnBackPressed() {
                if (binding.cameraPanel.root.visibility == View.VISIBLE) stopCamera() else finish()
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
        binding.cameraPanel.root.visibility = View.VISIBLE
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
                    }
                }
            } catch (ex: Exception) { runOnUiThread { if (!isDestroyed) binding.capabilityStatus.text = ex.localizedMessage } }
        }
    }

    private fun stopCamera() {
        microphone.stop()
        binding.cameraPanel.wifiPreview.stop()
        camera.stop()
        sender.stop()
        binding.root.keepScreenOn = false
        binding.cameraPanel.root.visibility = View.GONE
        binding.connectionScreen.visibility = View.VISIBLE
    }

    override fun onConfigurationChanged(newConfig: Configuration) {
        super.onConfigurationChanged(newConfig)
        binding.cameraPanel.previewTexture.post { camera.transform() }
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
