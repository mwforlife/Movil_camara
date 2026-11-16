package com.wilkenstech.movilcamara.connection

import android.content.Context
import android.os.Build
import org.json.JSONObject
import java.io.BufferedReader
import java.io.InputStreamReader
import java.net.*
import java.security.MessageDigest
import java.security.SecureRandom
import java.security.cert.X509Certificate
import java.util.UUID
import java.util.concurrent.Executors
import java.util.concurrent.ScheduledFuture
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicBoolean
import java.nio.ByteBuffer
import javax.net.ssl.*

/** Discovery publishes metadata only. Pairing uses TLS pinned to the PC certificate in the QR. */
class ConnectionManager(
    private val context: Context,
    private val onStatus: (String) -> Unit,
    private val onInvitation: (String, String, String) -> Unit,
    private val onUsbInvitation: (String, String) -> Unit = { _, _ -> }
) {
    companion object {
        const val DISCOVERY_PORT = 47777
        const val METADATA_PORT = 47778
        fun addresses(): String = try {
            NetworkInterface.getNetworkInterfaces().toList()
                .filter { it.isUp && !it.isLoopback }
                .flatMap { it.inetAddresses.toList() }
                .filterIsInstance<Inet4Address>()
                .joinToString(" · ") { it.hostAddress ?: "" }
                .ifEmpty { "Sin dirección IPv4 de red" }
        } catch (_: Exception) { "Red no disponible" }
    }
    private val prefs = context.getSharedPreferences("connection_identity", Context.MODE_PRIVATE)
    private val id = prefs.getString("id", null) ?: UUID.randomUUID().toString().also {
        prefs.edit().putString("id", it).apply()
    }
    private val name = (Build.MANUFACTURER + " " + Build.MODEL).take(100)
    private val workers = Executors.newFixedThreadPool(4)
    private val scheduler = Executors.newSingleThreadScheduledExecutor()
    @Volatile private var running = false
    @Volatile private var closed = false
    private var udp: DatagramSocket? = null
    private var tcp: ServerSocket? = null
    private var pulse: ScheduledFuture<*>? = null
    private data class Session(val host: String, val port: Int, val pin: String, val secret: String)
    @Volatile private var session: Session? = null
    private val pairingGeneration = AtomicInteger()
    val isPaired: Boolean get() = session != null
    fun videoEndpoint(): com.wilkenstech.movilcamara.video.VideoEndpoint? = session?.let {
        com.wilkenstech.movilcamara.video.VideoEndpoint(id, name, it.host, it.port, it.pin, it.secret)
    }
    private val invitationPending = AtomicBoolean(false)
    @Volatile private var lastInvitation = 0L
    @Volatile private var suppressedUsbPin: String? = null

    private fun usbAttached(): Boolean = try {
        context.registerReceiver(null, android.content.IntentFilter("android.hardware.usb.action.USB_STATE"))
            ?.getBooleanExtra("connected", false) == true
    } catch (_: Exception) { false }

    private fun rememberPc(pin: String) = synchronized(prefs) {
        val trusted = (prefs.getStringSet("trusted_pc_pins", emptySet()) ?: emptySet()).toMutableSet()
        trusted.add(pin)
        prefs.edit().putStringSet("trusted_pc_pins", trusted).apply()
    }

    fun rejectUsbInvitation(payload: String) {
        suppressedUsbPin = try { JSONObject(payload).optString("pin") } catch (_: Exception) { null }
        invitationPending.set(false)
    }

    fun forgetUsbTrust() {
        suppressedUsbPin = session?.pin
        prefs.edit().remove("trusted_pc_pins").apply()
        disconnect()
    }

    private fun metadata() = JSONObject().put("app", "wtmulticam").put("v", 1)
        .put("id", id).put("name", name)
        .put("usbReceiverPin", session?.takeIf { it.host == "127.0.0.1" }?.pin ?: "").toString()

    @Synchronized fun start() {
        if (running || closed) return
        try {
            val u = DatagramSocket(null).apply {
                reuseAddress = true
                bind(InetSocketAddress(DISCOVERY_PORT))
                soTimeout = 1500
            }
            udp = u
            val t = ServerSocket().apply {
                reuseAddress = true
                bind(InetSocketAddress(METADATA_PORT))
                soTimeout = 1500
            }
            tcp = t
            running = true
            workers.execute {
                while (running && !u.isClosed) {
                    try {
                        val packet = DatagramPacket(ByteArray(256), 256)
                        u.receive(packet)
                        if (String(packet.data, 0, packet.length, Charsets.UTF_8) == "WTMULTICAM_DISCOVER_V1") {
                            val bytes = metadata().toByteArray(Charsets.UTF_8)
                            u.send(DatagramPacket(bytes, bytes.size, packet.address, packet.port))
                        }
                    } catch (_: SocketTimeoutException) { }
                    catch (_: Exception) { if (!u.isClosed && running) onStatus("Error en descubrimiento de red") }
                }
            }
            workers.execute {
                while (running && !t.isClosed) {
                    try {
                        t.accept().use { socket ->
                            socket.soTimeout = 2000
                            socket.getOutputStream().write((metadata() + "\n").toByteArray(Charsets.UTF_8))
                            try {
                                val payload = readLimited(BufferedReader(InputStreamReader(socket.getInputStream(), Charsets.UTF_8)))
                                val invite = JSONObject(payload)
                                val source = socket.inetAddress.hostAddress
                                val usb = invite.optString("action") == "usbInvite"
                                val usbAllowed = usb && socket.inetAddress.isLoopbackAddress && source == "127.0.0.1" && usbAttached() &&
                                    suppressedUsbPin != invite.optString("pin")
                                val valid = (invite.optString("action") == "invite" || usbAllowed) && invite.optString("app") == "wtmulticam" &&
                                    invite.optInt("v") == 1 && invite.optInt("port") == 47779 && invite.optString("host") == source &&
                                    invite.optString("pin").matches(Regex("[0-9a-f]{64}")) &&
                                    invite.optString("invitation").matches(Regex("[0-9a-f]{48}"))
                                val now = android.os.SystemClock.elapsedRealtime()
                                val accepted = valid && running && (usbAllowed || session == null) &&
                                    (usbAllowed || now - lastInvitation > 10000) && invitationPending.compareAndSet(false, true)
                                socket.getOutputStream().write((JSONObject().put("received", accepted).toString() + "\n").toByteArray(Charsets.UTF_8))
                                if (accepted) {
                                    lastInvitation = now
                                    val secret = ByteArray(32).also { SecureRandom().nextBytes(it) }.joinToString("") { "%02x".format(it.toInt() and 255) }
                                    val digest = MessageDigest.getInstance("SHA-256").digest((invite.getString("pin") + "|" + secret).toByteArray(Charsets.UTF_8))
                                    val code = ((ByteBuffer.wrap(digest).int.toLong() and 0xffffffffL) % 1000000).toString().padStart(6, '0')
                                    if (usbAllowed) {
                                        if (prefs.getStringSet("trusted_pc_pins", emptySet())?.contains(invite.getString("pin")) == true)
                                            acceptInvitation(payload, secret, "USB")
                                        else onUsbInvitation(payload, secret)
                                    } else onInvitation(payload, secret, code)
                                }
                            } catch (_: Exception) { /* Plain metadata clients close without an invitation. */ }
                        }
                    } catch (_: SocketTimeoutException) { }
                    catch (_: Exception) { if (!t.isClosed && running) onStatus("Error en búsqueda por IP") }
                }
            }
            pulse = scheduler.scheduleWithFixedDelay({ heartbeat() }, 1, 5, TimeUnit.SECONDS)
            onStatus(if (session == null) "Visible para PCs en tu red · sin emparejar" else "Reconectando con el PC…")
        } catch (ex: Exception) {
            udp?.close(); tcp?.close()
            onStatus("No se pudo iniciar la conexión: " + ex.localizedMessage)
        }
    }

    fun pair(payload: String) {
        val generation = pairingGeneration.incrementAndGet()
        workers.execute {
            try {
                require(payload.length <= 4096) { "Código demasiado largo" }
                val qr = JSONObject(payload)
                require(qr.getString("app") == "wtmulticam" && qr.getInt("v") == 1) { "No es un QR de WT MultiCam compatible" }
                val host = qr.getString("host")
                require(host.matches(Regex("([0-9]{1,3}\\.){3}[0-9]{1,3}")) && host.split('.').all { it.toInt() in 0..255 }) { "Dirección IPv4 inválida" }
                require(qr.getInt("port") == 47779) { "Puerto de emparejamiento inválido" }
                val pin = qr.getString("pin").lowercase()
                val token = qr.getString("token")
                require(pin.matches(Regex("[0-9a-f]{64}")) && token.matches(Regex("[0-9a-f]{48}"))) { "Código incompleto" }
                onStatus("Emparejando con el PC…")
                val initial = Session(host, 47779, pin, token)
                val result = request(initial, "pair")
                require(result.optBoolean("ok")) { "QR caducado o utilizado. Genera uno nuevo en el PC." }
                val secret = result.getString("secret")
                require(secret.matches(Regex("[0-9a-f]{64}"))) { "Respuesta de sesión inválida" }
                if (closed || generation != pairingGeneration.get()) return@execute
                session = initial.copy(secret = secret)
                rememberPc(pin)
                onStatus("Emparejado con " + host + " · video pendiente")
            } catch (ex: Exception) {
                if (!closed && generation == pairingGeneration.get()) onStatus("No se pudo emparejar: " + ex.localizedMessage)
            }
        }
    }

    fun dismissInvitation() { invitationPending.set(false) }

    fun acceptInvitation(payload: String, secret: String, code: String) {
        val generation = pairingGeneration.incrementAndGet()
        workers.execute {
            try {
                val invite = JSONObject(payload)
                val usb = invite.optString("action") == "usbInvite"
                if (usb) require(usbAttached()) { "El cable USB ya no está conectado" }
                val initial = Session(invite.getString("host"), 47779, invite.getString("pin"), secret)
                onStatus(if (usb) "Conectando automáticamente por USB…" else "Código " + code + " · confirma que coincide en el PC")
                val result = request(initial, "requestPair", invite.getString("invitation"))
                require(result.optBoolean("ok")) { "Conexión rechazada o solicitud caducada" }
                val sessionSecret = result.getString("secret")
                require(sessionSecret.matches(Regex("[0-9a-f]{64}"))) { "Respuesta inválida" }
                if (closed || generation != pairingGeneration.get()) return@execute
                session = initial.copy(secret = sessionSecret)
                rememberPc(initial.pin)
                onStatus(if (usb) "Conectado por USB · listo para iniciar cámara" else "Emparejado con " + initial.host + " · video pendiente")
            } catch (ex: Exception) {
                if (!closed && generation == pairingGeneration.get()) onStatus("No se pudo conectar: " + ex.localizedMessage)
            } finally { invitationPending.set(false) }
        }
    }

    private fun heartbeat() {
        val current = session ?: return
        if (!running) return
        try {
            val response = request(current, "heartbeat")
            if (session !== current) return
            if (response.optBoolean("ok")) onStatus("Emparejado con " + current.host + " · video pendiente")
            else {
                session = null
                onStatus("La sesión terminó. Escanea un QR nuevo en el PC.")
            }
        } catch (_: Exception) {
            if (session === current && running) onStatus("PC sin respuesta · reintentando conexión…")
        }
    }

    private fun request(s: Session, action: String, invitation: String? = null): JSONObject {
        PinnedTls.connect(s.host, s.port, s.pin).use { socket ->
                if (invitation != null) socket.soTimeout = 65000
                val message = JSONObject().put("action", action).put("id", id).put("name", name).put("secret", s.secret)
                if (invitation != null) message.put("invitation", invitation)
                socket.outputStream.write((message.toString() + "\n").toByteArray(Charsets.UTF_8))
                socket.outputStream.flush()
                return JSONObject(readLimited(BufferedReader(InputStreamReader(socket.inputStream, Charsets.UTF_8))))
        }
    }

    private fun readLimited(reader: BufferedReader): String {
        val result = StringBuilder()
        while (result.length < 4096) {
            val value = reader.read()
            if (value == -1) throw java.io.IOException("El PC cerró la conexión")
            if (value == 10) return result.toString()
            result.append(value.toChar())
        }
        throw java.io.IOException("Respuesta demasiado larga")
    }
    fun disconnect() {
        pairingGeneration.incrementAndGet()
        session?.takeIf { it.host == "127.0.0.1" }?.let { suppressedUsbPin = it.pin }
        session = null
        onStatus("Visible para PCs en tu red · sin emparejar")
    }
    @Synchronized fun stop() {
        running = false
        pulse?.cancel(true); pulse = null
        udp?.close(); tcp?.close()
    }
    fun close() {
        stop()
        closed = true
        session = null
        workers.shutdownNow(); scheduler.shutdownNow()
    }
}
