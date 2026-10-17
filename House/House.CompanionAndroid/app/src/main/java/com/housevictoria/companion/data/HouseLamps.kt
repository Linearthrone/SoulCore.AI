package com.housevictoria.companion.data

import org.json.JSONObject

/** Tri-state service light. Unknown means this phone has no reading, not "off". */
enum class LampLight { On, Off, Unknown }

/**
 * House drawer lights taken from SoulCore `/health`.
 *
 * Comfy and Sandbox stay [LampLight.Unknown]: the desktop probes those on the
 * computer's loopback, and the phone must not paint them green.
 * These are readings, not switches. The phone cannot start or stop the house.
 */
data class HouseLamps(
    val soulcore: LampLight = LampLight.Unknown,
    val ollama: LampLight = LampLight.Unknown,
    val unreal: LampLight = LampLight.Unknown,
    val comfy: LampLight = LampLight.Unknown,
    val cua: LampLight = LampLight.Unknown,
    val sandbox: LampLight = LampLight.Unknown
) {
    companion object {
        fun unknown(): HouseLamps = HouseLamps()

        /** Null when the body is not a health object. */
        fun parse(healthJson: String): HouseLamps? {
            val root = try {
                JSONObject(healthJson)
            } catch (_: Exception) {
                return null
            }
            if (root.length() == 0) return null
            val status = root.optString("status", "").trim()
            val soulcore = when (status) {
                "ok", "degraded" -> LampLight.On
                "" -> LampLight.Unknown
                else -> LampLight.Off
            }
            return HouseLamps(
                soulcore = soulcore,
                ollama = flag(root, "inference", "enabled"),
                unreal = flag(root, "unreal", "connected"),
                comfy = LampLight.Unknown,
                cua = flag(root, "tools", "cuaDriverAvailable"),
                sandbox = LampLight.Unknown
            )
        }

        private fun flag(root: JSONObject, objectName: String, key: String): LampLight {
            if (!root.has(objectName) || root.isNull(objectName)) return LampLight.Unknown
            val child = root.optJSONObject(objectName) ?: return LampLight.Unknown
            if (!child.has(key) || child.isNull(key)) return LampLight.Unknown
            return if (child.optBoolean(key, false)) LampLight.On else LampLight.Off
        }
    }
}
