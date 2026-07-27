package com.housevictoria.companion.net

import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicReference

enum class WsConnectionState {
    Disconnected,
    Connecting,
    Connected,
    Failed
}

/**
 * OkHttp WebSocket client for SoulCore.Host `/ws`.
 *
 * Mirrors `House.ChatDesktop.Services.SoulCoreWsClient`:
 * - outbound: `chat.send` `{ text, sessionId? }`
 * - inbound: `chat.delta` / `chat.done`, `presence.status`, `emotion.snapshot`, `error`
 *
 * Token (optional on loopback): [CompanionAuthHeaders] — preferred
 * `Authorization: Bearer`, plus `X-Api-Key` alias (BED-155 / FED-149).
 * Never log the raw token.
 */
class SoulCoreWsClient(
    private val sessionId: String = "companion-android"
) {
    private val http = OkHttpClient.Builder()
        .connectTimeout(8, TimeUnit.SECONDS)
        .readTimeout(0, TimeUnit.MILLISECONDS) // WS long-lived
        .pingInterval(30, TimeUnit.SECONDS)
        .build()

    private val socketRef = AtomicReference<WebSocket?>(null)
    private val stateRef = AtomicReference(WsConnectionState.Disconnected)

    @Volatile
    var lastError: String = ""
        private set

    @Volatile
    var endpoint: String = ""
        private set

    val state: WsConnectionState get() = stateRef.get()

    val isConnected: Boolean get() = state == WsConnectionState.Connected

    var onStateChanged: ((WsConnectionState, String) -> Unit)? = null
    var onFrame: ((SoulCoreFrame) -> Unit)? = null

    fun connect(wsUrl: String, token: String = "") {
        disconnect(notify = false)
        endpoint = wsUrl.trim()
        setState(WsConnectionState.Connecting, "Connecting $endpoint")

        val builder = Request.Builder().url(endpoint)
        // Prefer Bearer; also send X-Api-Key alias (Host accepts either).
        CompanionAuthHeaders.applyBearer(builder, token)
        CompanionAuthHeaders.applyApiKeyAlias(builder, token)

        val socket = http.newWebSocket(builder.build(), object : WebSocketListener() {
            override fun onOpen(webSocket: WebSocket, response: Response) {
                setState(WsConnectionState.Connected, "WS connected · $endpoint")
            }

            override fun onMessage(webSocket: WebSocket, text: String) {
                val frame = SoulCoreFrame.tryParse(text) ?: return
                onFrame?.invoke(frame)
            }

            override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                webSocket.close(1000, null)
            }

            override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                socketRef.compareAndSet(webSocket, null)
                setState(
                    WsConnectionState.Disconnected,
                    "Host closed WS ($code). Reconnect when Host is back."
                )
            }

            override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                socketRef.compareAndSet(webSocket, null)
                val detail = response?.let { "HTTP ${it.code}" } ?: t.javaClass.simpleName
                setState(
                    WsConnectionState.Failed,
                    "Host WS down at $endpoint ($detail: ${t.message}). " +
                        "Start SoulCore.Host, then reconnect. " +
                        "Emulator tip: adb reverse tcp:7700 tcp:7700"
                )
            }
        })
        socketRef.set(socket)
    }

    fun disconnect() = disconnect(notify = true)

    private fun disconnect(notify: Boolean) {
        val socket = socketRef.getAndSet(null)
        socket?.close(1000, "client close")
        if (notify && state !in listOf(WsConnectionState.Failed)) {
            setState(WsConnectionState.Disconnected, "Disconnected from SoulCore WS")
        } else if (!notify) {
            stateRef.set(WsConnectionState.Disconnected)
        }
    }

    fun sendChat(text: String, sessionId: String? = this.sessionId): Result<Unit> {
        val socket = socketRef.get()
        if (socket == null || state != WsConnectionState.Connected) {
            val msg = "WS unavailable — chat.send not sent. Host must be up on loopback /ws. " +
                lastError.ifBlank { "state=$state" }
            lastError = msg
            return Result.failure(IllegalStateException(msg))
        }
        val frame = SoulCoreFrame.chatSend(text, sessionId)
        val ok = socket.send(frame.toJson())
        return if (ok) {
            Result.success(Unit)
        } else {
            Result.failure(IllegalStateException("chat.send enqueue failed (socket closing?)"))
        }
    }

    private fun setState(next: WsConnectionState, detail: String) {
        stateRef.set(next)
        lastError = detail
        onStateChanged?.invoke(next, detail)
    }
}
