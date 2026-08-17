package com.housevictoria.companion.net

import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.util.concurrent.TimeUnit

data class CallSessionInfo(
    val sessionId: String,
    val contactId: String,
    val mode: String,
    val pollUrl: String,
    val webrtcAvailable: Boolean
)

/**
 * Victoria Link video-call HTTP (waist-up frames MVP; WebRTC later).
 */
object CompanionCallClient {
    private val http = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(20, TimeUnit.SECONDS)
        .writeTimeout(15, TimeUnit.SECONDS)
        .build()

    fun startSession(httpBase: String, token: String, contactId: String = "victoria"): Result<CallSessionInfo> =
        runCatching {
            val url = "${httpBase.trimEnd('/')}/api/companion/v1/call/session"
            val payload = JSONObject().put("contactId", contactId)
            val builder = Request.Builder()
                .url(url)
                .post(payload.toString().toRequestBody("application/json; charset=utf-8".toMediaType()))
            CompanionAuthHeaders.applyBearer(builder, token)
            http.newCall(builder.build()).execute().use { resp ->
                val body = resp.body?.string().orEmpty()
                val json = JSONObject(body)
                if (!resp.isSuccessful || !json.optBoolean("ok", false)) {
                    error(json.optString("error", "HTTP ${resp.code}"))
                }
                val webrtc = json.optJSONObject("webrtc")
                CallSessionInfo(
                    sessionId = json.getString("sessionId"),
                    contactId = json.optString("contactId", contactId),
                    mode = json.optString("mode", "frames"),
                    pollUrl = json.optString(
                        "pollUrl",
                        "/api/companion/v1/call/frame?sessionId=${json.getString("sessionId")}"
                    ),
                    webrtcAvailable = webrtc?.optBoolean("available", false) == true
                )
            }
        }

    fun fetchFrame(
        httpBase: String,
        token: String,
        sessionId: String,
        fallbackEyes: Boolean = false
    ): Result<ByteArray> = runCatching {
        val url =
            "${httpBase.trimEnd('/')}/api/companion/v1/call/frame?sessionId=$sessionId&fallbackEyes=$fallbackEyes"
        val builder = Request.Builder().url(url).get()
        CompanionAuthHeaders.applyBearer(builder, token)
        http.newCall(builder.build()).execute().use { resp ->
            if (resp.code == 503) error("no_frame")
            if (!resp.isSuccessful) error("HTTP ${resp.code}")
            val bytes = resp.body?.bytes() ?: error("empty")
            if (bytes.isEmpty()) error("empty")
            bytes
        }
    }

    fun endSession(httpBase: String, token: String, sessionId: String): Result<Unit> = runCatching {
        val url = "${httpBase.trimEnd('/')}/api/companion/v1/call/session/$sessionId"
        val builder = Request.Builder().url(url).delete()
        CompanionAuthHeaders.applyBearer(builder, token)
        http.newCall(builder.build()).execute().use { resp ->
            if (!resp.isSuccessful && resp.code != 404) error("HTTP ${resp.code}")
        }
    }
}
