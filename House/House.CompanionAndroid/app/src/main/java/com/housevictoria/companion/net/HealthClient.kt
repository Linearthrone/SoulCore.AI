package com.housevictoria.companion.net

import okhttp3.OkHttpClient
import okhttp3.Request
import org.json.JSONObject
import java.util.concurrent.TimeUnit

/**
 * Optional HTTP `/health` probe derived from the WS URL.
 * This is NOT the LLMOD `:17890` remote companion API — it targets SoulCore.Host only.
 */
object HealthClient {
    private val http = OkHttpClient.Builder()
        .connectTimeout(8, TimeUnit.SECONDS)
        .readTimeout(8, TimeUnit.SECONDS)
        .build()

    fun healthUrlFromWs(wsUrl: String): String? {
        val trimmed = wsUrl.trim().trimEnd('/')
        val httpBase = when {
            trimmed.startsWith("ws://") -> "http://" + trimmed.removePrefix("ws://")
            trimmed.startsWith("wss://") -> "https://" + trimmed.removePrefix("wss://")
            else -> return null
        }
        val withoutWs = httpBase.removeSuffix("/ws")
        return "$withoutWs/health"
    }

    fun checkHealth(wsUrl: String): String {
        val url = healthUrlFromWs(wsUrl)
            ?: return "Invalid WebSocket URL (expected ws:// or wss://)."
        return runCatching {
            val req = Request.Builder().url(url).get().build()
            http.newCall(req).execute().use { resp ->
                val body = resp.body?.string().orEmpty().take(200)
                if (resp.isSuccessful) "Healthy HTTP ${resp.code} ← $url"
                else "HTTP ${resp.code}: $body"
            }
        }.getOrElse { "Health check failed: ${it.message}" }
    }

    /**
     * PROP-17.5 / PROP-4: Host `currentActivity` doing-now phrase from `/health`.
     * Never invents slogans when the field is absent.
     */
    fun currentActivity(wsUrl: String): String? {
        val url = healthUrlFromWs(wsUrl) ?: return null
        return runCatching {
            val req = Request.Builder().url(url).get().build()
            http.newCall(req).execute().use { resp ->
                if (!resp.isSuccessful) return@use null
                val body = resp.body?.string().orEmpty()
                if (body.isBlank()) return@use null
                parseCurrentActivity(body)
            }
        }.getOrNull()
    }

    /** Pure parser for unit tests. */
    fun parseCurrentActivity(healthJson: String): String? {
        return try {
            val root = JSONObject(healthJson)
            if (!root.has("currentActivity") || root.isNull("currentActivity")) return null
            val phrase = root.optString("currentActivity", "").trim()
            phrase.ifEmpty { null }
        } catch (_: Exception) {
            null
        }
    }
}
