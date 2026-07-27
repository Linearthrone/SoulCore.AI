package com.housevictoria.companion.net

import android.content.Context
import com.housevictoria.companion.service.CompanionWsService
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * Process-scoped SoulCore WS hub shared by [CompanionWsService] and Compose UI.
 *
 * The foreground service owns process priority + the persistent "connected"
 * notification; this object owns the OkHttp [SoulCoreWsClient] so Chat can
 * observe frames without tearing down the socket on Activity pause / navigation.
 */
object CompanionConnection {
    val client: SoulCoreWsClient = SoulCoreWsClient()

    private val _state = MutableStateFlow(WsConnectionState.Disconnected to "Idle")
    val state: StateFlow<Pair<WsConnectionState, String>> = _state.asStateFlow()

    private val _frames = MutableSharedFlow<SoulCoreFrame>(extraBufferCapacity = 64)
    val frames: SharedFlow<SoulCoreFrame> = _frames.asSharedFlow()

    private val _serviceRunning = MutableStateFlow(false)
    val serviceRunning: StateFlow<Boolean> = _serviceRunning.asStateFlow()

    /** Extra observers (e.g. FGS notification updates). Cleared when the service dies. */
    private val stateExtras = mutableListOf<(WsConnectionState, String) -> Unit>()

    init {
        installHubListeners()
    }

    private fun installHubListeners() {
        client.onStateChanged = { next, detail ->
            _state.value = next to detail
            synchronized(stateExtras) {
                stateExtras.toList()
            }.forEach { it(next, detail) }
        }
        client.onFrame = { frame ->
            _frames.tryEmit(frame)
        }
    }

    fun addStateObserver(observer: (WsConnectionState, String) -> Unit) {
        synchronized(stateExtras) { stateExtras.add(observer) }
    }

    fun removeStateObserver(observer: (WsConnectionState, String) -> Unit) {
        synchronized(stateExtras) { stateExtras.remove(observer) }
    }

    fun clearStateObservers() {
        synchronized(stateExtras) { stateExtras.clear() }
    }

    fun markServiceRunning(running: Boolean) {
        _serviceRunning.value = running
    }

    /**
     * Start the foreground service and connect WS using the given endpoint.
     * Safe to call repeatedly (service re-delivers START with new URL/token).
     */
    fun start(context: Context, wsUrl: String, token: String) {
        CompanionWsService.start(context.applicationContext, wsUrl, token)
    }

    /** Disconnect WS and stop the foreground service (clears persistent notification). */
    fun stop(context: Context) {
        CompanionWsService.stop(context.applicationContext)
    }

    val isConnected: Boolean get() = client.isConnected
}
