package com.housevictoria.companion.data

import java.util.UUID

enum class MessageRole {
    USER,
    ASSISTANT,
    SYSTEM,
    ERROR
}

data class ChatMessage(
    val id: String = UUID.randomUUID().toString(),
    val role: MessageRole,
    val content: String,
    /** Correlates streaming `chat.delta` / `chat.done` frames (Host frame id). */
    val frameId: String? = null,
    val timestampMs: Long = System.currentTimeMillis()
)
