package com.wilkenstech.movilcamara.connection

import java.net.InetSocketAddress
import java.net.Socket
import java.security.MessageDigest
import java.security.SecureRandom
import java.security.cert.X509Certificate
import javax.net.ssl.*

/** Only certificates matching the fingerprint authorized during pairing are accepted. */
object PinnedTls {
    fun connect(host: String, port: Int, pin: String): SSLSocket {
        val trust = object : X509TrustManager {
            override fun getAcceptedIssuers(): Array<X509Certificate> = emptyArray()
            override fun checkClientTrusted(chain: Array<X509Certificate>, authType: String) {
                throw java.security.cert.CertificateException("No se aceptan certificados de cliente")
            }
            override fun checkServerTrusted(chain: Array<X509Certificate>, authType: String) {
                if (chain.isEmpty()) throw java.security.cert.CertificateException("PC sin certificado")
                val actual = MessageDigest.getInstance("SHA-256").digest(chain[0].encoded)
                    .joinToString("") { "%02x".format(it.toInt() and 255) }
                if (!MessageDigest.isEqual(actual.toByteArray(Charsets.UTF_8), pin.toByteArray(Charsets.UTF_8)))
                    throw java.security.cert.CertificateException("La identidad del PC no coincide con el emparejamiento")
            }
        }
        val context = SSLContext.getInstance("TLS").apply { init(null, arrayOf(trust), SecureRandom()) }
        val raw = Socket()
        try {
            raw.tcpNoDelay = true
            raw.connect(InetSocketAddress(host, port), 4000)
            raw.soTimeout = 5000
            val socket = context.socketFactory.createSocket(raw, host, port, true) as SSLSocket
            socket.soTimeout = 5000
            socket.enabledProtocols = socket.supportedProtocols.filter { it == "TLSv1.2" || it == "TLSv1.3" }.toTypedArray()
            socket.startHandshake()
            return socket
        } catch (ex: Exception) { raw.close(); throw ex }
    }
}
