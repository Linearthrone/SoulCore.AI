package com.housevictoria.companion.service

import android.app.NotificationManager
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import android.util.Log
import androidx.core.app.ServiceCompat
import androidx.core.content.ContextCompat
import com.housevictoria.companion.data.CompanionPrefs
import com.housevictoria.companion.net.CompanionConnection
import com.housevictoria.companion.net.WsConnectionState
import com.housevictoria.companion.notify.ConnectedNotification

/**
 * Foreground service that keeps the SoulCore WebSocket alive while the app is
 * backgrounded. Shows a persistent low-importance notification while running.
 *
 * OEM caveats: aggressive battery savers (Xiaomi / Huawei / Oppo / Samsung) may
 * still restrict long-lived sockets after hours–days unless the user exempts
 * the app from battery optimization. Documented in README + FED-150 report.
 */
class CompanionWsService : Service() {

    private val onState: (WsConnectionState, String) -> Unit = { state, detail ->
        val connected = state == WsConnectionState.Connected
        val line = when (state) {
            WsConnectionState.Connected ->
                detail.ifBlank { getString(com.housevictoria.companion.R.string.notif_connected_title) }
            WsConnectionState.Connecting ->
                getString(com.housevictoria.companion.R.string.notif_connecting_title)
            WsConnectionState.Failed -> detail.ifBlank { "Connection failed" }
            WsConnectionState.Disconnected -> "Disconnected"
        }
        val notification = ConnectedNotification.build(this, line, connected)
        getSystemService(NotificationManager::class.java)
            ?.notify(ConnectedNotification.NOTIFICATION_ID, notification)
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        CompanionConnection.markServiceRunning(true)
        ConnectedNotification.ensureChannel(this)
        CompanionConnection.addStateObserver(onState)
        Log.i(TAG, "FGS created")
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_STOP -> {
                Log.i(TAG, "STOP requested (UI or notification action)")
                stopSelfSafely()
                return START_NOT_STICKY
            }
            else -> {
                val config = CompanionPrefs.load(this)
                val wsUrl = intent?.getStringExtra(EXTRA_WS_URL)?.takeIf { it.isNotBlank() }
                    ?: config.wsUrl
                val token = intent?.getStringExtra(EXTRA_TOKEN) ?: config.token

                promoteToForeground(connecting = true)
                CompanionConnection.client.connect(wsUrl, token)
                Log.i(TAG, "WS connect requested")
            }
        }
        return START_STICKY
    }

    override fun onDestroy() {
        Log.i(TAG, "FGS destroyed — disconnecting WS")
        CompanionConnection.removeStateObserver(onState)
        CompanionConnection.client.disconnect()
        CompanionConnection.markServiceRunning(false)
        super.onDestroy()
    }

    private fun promoteToForeground(connecting: Boolean) {
        val notification = ConnectedNotification.build(
            this,
            if (connecting) {
                getString(com.housevictoria.companion.R.string.notif_connecting_title)
            } else {
                getString(com.housevictoria.companion.R.string.notif_connected_title)
            },
            connected = !connecting
        )
        val type = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC
        } else {
            0
        }
        ServiceCompat.startForeground(
            this,
            ConnectedNotification.NOTIFICATION_ID,
            notification,
            type
        )
    }

    private fun stopSelfSafely() {
        CompanionConnection.client.disconnect()
        ServiceCompat.stopForeground(this, ServiceCompat.STOP_FOREGROUND_REMOVE)
        stopSelf()
    }

    companion object {
        private const val TAG = "CompanionWsService"
        const val ACTION_START = "com.housevictoria.companion.action.WS_START"
        const val ACTION_STOP = "com.housevictoria.companion.action.WS_STOP"
        const val EXTRA_WS_URL = "ws_url"
        const val EXTRA_TOKEN = "token"

        fun start(context: Context, wsUrl: String, token: String) {
            val intent = Intent(context, CompanionWsService::class.java).apply {
                action = ACTION_START
                putExtra(EXTRA_WS_URL, wsUrl)
                putExtra(EXTRA_TOKEN, token)
            }
            ContextCompat.startForegroundService(context, intent)
        }

        fun stop(context: Context) {
            // Must startService (not startForegroundService) for STOP — no FGS promote needed.
            context.startService(stopIntent(context))
        }

        fun stopIntent(context: Context): Intent =
            Intent(context, CompanionWsService::class.java).apply {
                action = ACTION_STOP
            }
    }
}
