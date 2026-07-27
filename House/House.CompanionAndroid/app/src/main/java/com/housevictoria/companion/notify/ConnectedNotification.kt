package com.housevictoria.companion.notify

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.os.Build
import androidx.core.app.NotificationCompat
import com.housevictoria.companion.MainActivity
import com.housevictoria.companion.R
import com.housevictoria.companion.service.CompanionWsService

/**
 * Persistent notification for [CompanionWsService] while the companion WS is up.
 *
 * Separate from FED-151 chat-reply channel (`victoria_replies`) — this channel is
 * low-importance / ongoing only (no sound). Do not post `chat.done` alerts here.
 */
object ConnectedNotification {
    const val CHANNEL_ID = "victoria_connected"
    const val NOTIFICATION_ID = 15001

    fun ensureChannel(context: Context) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val mgr = context.getSystemService(NotificationManager::class.java) ?: return
        val existing = mgr.getNotificationChannel(CHANNEL_ID)
        if (existing != null) return
        val channel = NotificationChannel(
            CHANNEL_ID,
            context.getString(R.string.notif_channel_connected_name),
            NotificationManager.IMPORTANCE_LOW
        ).apply {
            description = context.getString(R.string.notif_channel_connected_desc)
            setShowBadge(false)
            enableVibration(false)
            setSound(null, null)
        }
        mgr.createNotificationChannel(channel)
    }

    fun build(
        context: Context,
        statusLine: String,
        connected: Boolean
    ): Notification {
        ensureChannel(context)

        val openApp = PendingIntent.getActivity(
            context,
            0,
            Intent(context, MainActivity::class.java).apply {
                flags = Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP
            },
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val stopIntent = PendingIntent.getService(
            context,
            1,
            CompanionWsService.stopIntent(context),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val title = if (connected) {
            context.getString(R.string.notif_connected_title)
        } else {
            context.getString(R.string.notif_connecting_title)
        }

        return NotificationCompat.Builder(context, CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_stat_victoria)
            .setContentTitle(title)
            .setContentText(statusLine)
            .setContentIntent(openApp)
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setSilent(true)
            .setCategory(NotificationCompat.CATEGORY_SERVICE)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .setForegroundServiceBehavior(NotificationCompat.FOREGROUND_SERVICE_IMMEDIATE)
            .addAction(
                0,
                context.getString(R.string.notif_action_disconnect),
                stopIntent
            )
            .build()
    }
}
