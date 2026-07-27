package com.housevictoria.companion.net

import org.json.JSONObject
import java.time.Instant
import java.util.UUID

/**
 * Canonical SoulCore WebSocket JSON envelope — mirrors
 * `SoulCore.Protocol.SoulCoreFrame` / House.ChatDesktop.
 *
 * ```
 * { "v": 1, "type": "…", "id": "…", "ts": "…", "payload": { … } }
 * ```
 */
data class SoulCoreFrame(
    val v: Int = PROTOCOL_VERSION,
    val type: String,
    val id: String = UUID.randomUUID().toString().replace("-", ""),
    val ts: String = Instant.now().toString(),
    val payload: JSONObject? = null
) {
    fun toJson(): String {
        val root = JSONObject()
            .put("v", v)
            .put("type", type)
            .put("id", id)
            .put("ts", ts)
        if (payload != null) {
            root.put("payload", payload)
        }
        return root.toString()
    }

    /** Payload `text` when present (may be empty string). Null if key absent. */
    fun payloadText(): String? {
        val p = payload ?: return null
        if (!p.has("text") || p.isNull("text")) return null
        return p.optString("text")
    }

    fun payloadString(key: String): String? {
        val p = payload ?: return null
        if (!p.has(key) || p.isNull(key)) return null
        return p.optString(key)
    }

    companion object {
        const val PROTOCOL_VERSION = 1

        const val CHAT_SEND = "chat.send"
        const val CHAT_DELTA = "chat.delta"
        const val CHAT_DONE = "chat.done"
        const val PRESENCE_STATUS = "presence.status"
        const val EMOTION_SNAPSHOT = "emotion.snapshot"
        const val ERROR = "error"
        const val PING = "ping"
        const val PONG = "pong"

        fun create(type: String, payload: JSONObject? = null, id: String? = null): SoulCoreFrame =
            SoulCoreFrame(
                type = type,
                id = id ?: UUID.randomUUID().toString().replace("-", ""),
                payload = payload
            )

        fun chatSend(text: String, sessionId: String?): SoulCoreFrame {
            val payload = JSONObject().put("text", text)
            if (!sessionId.isNullOrBlank()) {
                payload.put("sessionId", sessionId)
            }
            return create(CHAT_SEND, payload)
        }

        fun tryParse(json: String): SoulCoreFrame? {
            return try {
                val root = JSONObject(json)
                val type = root.optString("type", "").trim()
                if (type.isEmpty()) return null
                SoulCoreFrame(
                    v = root.optInt("v", PROTOCOL_VERSION),
                    type = type,
                    id = root.optString("id", ""),
                    ts = root.optString("ts", ""),
                    payload = if (root.has("payload") && !root.isNull("payload")) {
                        root.getJSONObject("payload")
                    } else {
                        null
                    }
                )
            } catch (_: Exception) {
                null
            }
        }
    }
}
