package com.wilkenstech.movilcamara.camera

import org.json.JSONObject

data class RemoteCameraState(
    val cameraId: String = "", val active: Boolean = false,
    val minZoom: Double = 1.0, val maxZoom: Double = 1.0, val zoom: Double = 1.0,
    val torchSupported: Boolean = false, val torch: Boolean = false,
    val minExposure: Int = 0, val maxExposure: Int = 0, val exposure: Int = 0,
    val focusSupported: Boolean = false
) {
    fun json(): JSONObject = JSONObject().put("cameraId", cameraId).put("active", active)
        .put("minZoom", minZoom).put("maxZoom", maxZoom).put("zoom", zoom)
        .put("torchSupported", torchSupported).put("torch", torch)
        .put("minExposure", minExposure).put("maxExposure", maxExposure).put("exposure", exposure)
        .put("focusSupported", focusSupported)
}
