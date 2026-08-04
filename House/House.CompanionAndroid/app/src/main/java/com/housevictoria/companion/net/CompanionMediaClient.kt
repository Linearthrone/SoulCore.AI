package com.housevictoria.companion.net

import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONArray
import org.json.JSONObject
import java.util.concurrent.TimeUnit

data class CompanionContact(
    val id: String,
    val name: String,
    val isPrimary: Boolean = true,
    val description: String = ""
)

data class MediaModelInfo(
    val id: String,
    val label: String,
    val available: Boolean
)

data class MediaGenerateResult(
    val mediaId: String,
    val fileUrl: String,
    val sizeBytes: Long
)

/**
 * HTTP client for SoulCore /api/companion/v1 (Victoria Link media + contacts).
 */
object CompanionMediaClient {
    private val http = OkHttpClient.Builder()
        .connectTimeout(30, TimeUnit.SECONDS)
        .readTimeout(300, TimeUnit.SECONDS)
        .writeTimeout(60, TimeUnit.SECONDS)
        .build()

    fun httpBaseFromWs(wsUrl: String): String {
        val trimmed = wsUrl.trim().trimEnd('/')
        val httpBase = when {
            trimmed.startsWith("ws://") -> "http://" + trimmed.removePrefix("ws://")
            trimmed.startsWith("wss://") -> "https://" + trimmed.removePrefix("wss://")
            else -> trimmed
        }
        return httpBase.removeSuffix("/ws").trimEnd('/')
    }

    fun listContacts(httpBase: String, token: String): Result<List<CompanionContact>> = runCatching {
        val url = "${httpBase.trimEnd('/')}/api/companion/v1/contacts"
        val builder = Request.Builder().url(url).get()
        CompanionAuthHeaders.applyBearer(builder, token)
        http.newCall(builder.build()).execute().use { resp ->
            val body = resp.body?.string().orEmpty()
            if (!resp.isSuccessful) error("HTTP ${resp.code}: ${body.take(120)}")
            val arr = JSONObject(body).optJSONArray("contacts") ?: JSONArray()
            buildList {
                for (i in 0 until arr.length()) {
                    val o = arr.getJSONObject(i)
                    add(
                        CompanionContact(
                            id = o.optString("id", "victoria"),
                            name = o.optString("name", "Victoria"),
                            isPrimary = o.optBoolean("isPrimary", true),
                            description = o.optString("description", "")
                        )
                    )
                }
            }
        }
    }

    fun listModels(httpBase: String, token: String): Result<List<MediaModelInfo>> = runCatching {
        val url = "${httpBase.trimEnd('/')}/api/companion/v1/media/models"
        val builder = Request.Builder().url(url).get()
        CompanionAuthHeaders.applyBearer(builder, token)
        http.newCall(builder.build()).execute().use { resp ->
            val body = resp.body?.string().orEmpty()
            if (!resp.isSuccessful) error("HTTP ${resp.code}: ${body.take(120)}")
            val arr = JSONObject(body).optJSONArray("models") ?: JSONArray()
            buildList {
                for (i in 0 until arr.length()) {
                    val o = arr.getJSONObject(i)
                    add(
                        MediaModelInfo(
                            id = o.optString("id", "comfyui"),
                            label = o.optString("label", o.optString("id", "ComfyUI")),
                            available = o.optBoolean("available", true)
                        )
                    )
                }
            }
        }
    }

    fun generate(
        httpBase: String,
        token: String,
        positivePrompt: String,
        negativePrompt: String?,
        model: String?,
        contactId: String,
        pushToChat: Boolean
    ): Result<MediaGenerateResult> = runCatching {
        val url = "${httpBase.trimEnd('/')}/api/companion/v1/media/generate"
        val payload = JSONObject()
            .put("positivePrompt", positivePrompt)
            .put("contactId", contactId)
            .put("pushToChat", pushToChat)
        if (!negativePrompt.isNullOrBlank()) payload.put("negativePrompt", negativePrompt)
        if (!model.isNullOrBlank()) payload.put("model", model)
        val builder = Request.Builder()
            .url(url)
            .post(payload.toString().toRequestBody("application/json; charset=utf-8".toMediaType()))
        CompanionAuthHeaders.applyBearer(builder, token)
        http.newCall(builder.build()).execute().use { resp ->
            val body = resp.body?.string().orEmpty()
            val json = runCatching { JSONObject(body) }.getOrNull()
            if (!resp.isSuccessful || json?.optBoolean("ok", false) == false) {
                error(json?.optString("error") ?: "HTTP ${resp.code}: ${body.take(160)}")
            }
            val mediaId = json!!.getString("mediaId")
            MediaGenerateResult(
                mediaId = mediaId,
                fileUrl = json.optString(
                    "fileUrl",
                    "/api/companion/v1/media/$mediaId/file"
                ),
                sizeBytes = json.optLong("sizeBytes", 0)
            )
        }
    }

    fun downloadMedia(httpBase: String, token: String, mediaId: String): Result<ByteArray> = runCatching {
        val path = if (mediaId.startsWith("http")) {
            mediaId
        } else {
            "${httpBase.trimEnd('/')}/api/companion/v1/media/$mediaId/file"
        }
        val builder = Request.Builder().url(path).get()
        CompanionAuthHeaders.applyBearer(builder, token)
        http.newCall(builder.build()).execute().use { resp ->
            if (!resp.isSuccessful) error("HTTP ${resp.code}")
            resp.body?.bytes() ?: error("empty body")
        }
    }
}
