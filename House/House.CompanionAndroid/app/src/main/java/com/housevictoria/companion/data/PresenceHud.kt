package com.housevictoria.companion.data

import android.app.Application
import android.util.Log
import com.housevictoria.companion.net.CompanionConnection
import com.housevictoria.companion.net.HealthClient
import com.housevictoria.companion.net.SoulCoreFrame
import com.housevictoria.companion.net.WsConnectionState
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/**
 * PROP-17.5: thin companion HUD for Host mood word + doing-line.
 *
 * Same sources as Presence activity band:
 * - mood ← `emotion.snapshot` payload `label`
 * - doing ← `presence.status` payload `activity` (bootstrap from `/health` `currentActivity`)
 *
 * Never restamps mood from `loop.want`.
 */
data class PresenceHudState(
    val moodWord: String = "—",
    val doingLine: String = "—",
    val moodFromHost: Boolean = false,
    val doingFromHost: Boolean = false
)

object PresenceHud {
    private const val TAG = "PresenceHud"

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val _state = MutableStateFlow(PresenceHudState())
    val state: StateFlow<PresenceHudState> = _state.asStateFlow()

    private var installed = false

    fun install(application: Application) {
        if (installed) return
        installed = true

        scope.launch {
            CompanionConnection.frames.collect { frame -> applyFrame(frame) }
        }
        scope.launch {
            CompanionConnection.state.collect { (connState, _) ->
                if (connState == WsConnectionState.Connected) {
                    bootstrapDoingFromHealth(application)
                }
            }
        }
        Log.i(TAG, "Presence HUD installed (mood + doing-line)")
    }

    /** Pure frame apply — unit-tested. */
    fun applyFrame(frame: SoulCoreFrame) {
        when (frame.type) {
            SoulCoreFrame.EMOTION_SNAPSHOT -> {
                val label = frame.payloadString("label")?.trim().orEmpty()
                if (label.isNotEmpty()) {
                    _state.value = _state.value.copy(moodWord = label, moodFromHost = true)
                }
            }
            SoulCoreFrame.PRESENCE_STATUS -> {
                val activity = frame.payloadString("activity")?.trim().orEmpty()
                if (activity.isNotEmpty()) {
                    _state.value = _state.value.copy(doingLine = activity, doingFromHost = true)
                }
            }
            else -> Unit
        }
    }

    /** Test / reset helper. */
    fun resetForTests() {
        _state.value = PresenceHudState()
    }

    private fun bootstrapDoingFromHealth(application: Application) {
        val cfg = CompanionPrefs.load(application)
        val phrase = HealthClient.currentActivity(cfg.wsUrl)?.trim().orEmpty()
        if (phrase.isEmpty()) return
        val cur = _state.value
        // Do not clobber a fresher presence.status push.
        if (cur.doingFromHost && cur.doingLine != "—") return
        _state.value = cur.copy(doingLine = phrase, doingFromHost = true)
    }
}
