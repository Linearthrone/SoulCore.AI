package com.housevictoria.companion.notify

import android.app.Application
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.media.AudioAttributes
import android.media.RingtoneManager
import android.net.Uri
import android.os.Build
import android.util.Log
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import androidx.core.content.FileProvider
import com.housevictoria.companion.MainActivity
import com.housevictoria.companion.R
import com.housevictoria.companion.data.CompanionPrefs
import com.housevictoria.companion.net.CompanionConnection
import com.housevictoria.companion.net.SoulCoreFrame
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import java.io.File

/**
 * Local alerts when Victoria finishes a reply (`chat.done`) while the app is
 * backgrounded / unfocused. Separate from [ConnectedNotification] (FGS ongoing).
 *
 * Channel id: [CHANNEL_ID]. Settings keys live on [CompanionPrefs] notification prefs.
 */
object ReplyNotification {
    const val CHANNEL_ID = "victoria_replies"
    const val NOTIFICATION_ID_BASE = 15101
    const val EXTRA_OPEN_CHAT = "open_chat"
    const val EXTRA_FRAME_ID = "frame_id"

    private const val TAG = "ReplyNotification"
    private const val SOUND_FILE_NAME = "reply_custom"
    private const val PREVIEW_MAX = 160

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Default)
    private var installed = false

    fun ensureChannel(context: Context, recreate: Boolean = false) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val mgr = context.getSystemService(NotificationManager::class.java) ?: return
        if (recreate) {
            mgr.deleteNotificationChannel(CHANNEL_ID)
        } else if (mgr.getNotificationChannel(CHANNEL_ID) != null) {
            return
        }

        val prefs = CompanionPrefs.loadNotification(context)
        val channel = NotificationChannel(
            CHANNEL_ID,
            context.getString(R.string.notif_channel_replies_name),
            NotificationManager.IMPORTANCE_HIGH
        ).apply {
            description = context.getString(R.string.notif_channel_replies_desc)
            setShowBadge(true)
            enableLights(true)
            enableVibration(prefs.vibration)
            if (prefs.vibration) {
                vibrationPattern = longArrayOf(0, 250, 120, 250)
            }
            val soundUri = resolveSoundUri(context, prefs.soundPath)
            val attrs = AudioAttributes.Builder()
                .setUsage(AudioAttributes.USAGE_NOTIFICATION)
                .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
                .build()
            setSound(soundUri, attrs)
        }
        mgr.createNotificationChannel(channel)
        Log.i(TAG, "Channel $CHANNEL_ID ready (vibration=${prefs.vibration}, customSound=${prefs.soundPath.isNotBlank()})")
    }

    /** Collect hub frames for the process lifetime (works while FGS holds WS). */
    fun install(app: Application) {
        if (installed) return
        installed = true
        ensureChannel(app)
        scope.launch {
            CompanionConnection.frames.collect { frame ->
                if (frame.type == SoulCoreFrame.CHAT_DONE) {
                    maybeNotify(app, frame)
                }
            }
        }
        Log.i(TAG, "Reply alerts installed on CompanionConnection.frames")
    }

    fun maybeNotify(context: Context, frame: SoulCoreFrame) {
        val prefs = CompanionPrefs.loadNotification(context)
        if (!prefs.enabled) return
        if (AppForeground.isInForeground) return

        val text = frame.payloadText()?.trim().orEmpty()
        if (text.isEmpty()) return

        ensureChannel(context)
        val preview = truncate(text, PREVIEW_MAX)
        val notifyId = notificationIdFor(frame.id)

        val openChat = PendingIntent.getActivity(
            context,
            notifyId,
            Intent(context, MainActivity::class.java).apply {
                flags = Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP
                putExtra(EXTRA_OPEN_CHAT, true)
                putExtra(EXTRA_FRAME_ID, frame.id)
            },
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val builder = NotificationCompat.Builder(context, CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_stat_victoria)
            .setContentTitle(context.getString(R.string.notif_reply_title))
            .setContentText(preview)
            .setStyle(NotificationCompat.BigTextStyle().bigText(preview))
            .setContentIntent(openChat)
            .setAutoCancel(true)
            .setOnlyAlertOnce(false)
            .setCategory(NotificationCompat.CATEGORY_MESSAGE)
            .setVisibility(NotificationCompat.VISIBILITY_PUBLIC)
            .setPriority(NotificationCompat.PRIORITY_HIGH)

        if (!prefs.vibration) {
            builder.setVibrate(longArrayOf(0))
        }

        try {
            NotificationManagerCompat.from(context).notify(notifyId, builder.build())
            Log.i(TAG, "Posted chat.done reply notification id=$notifyId frame=${frame.id}")
        } catch (e: SecurityException) {
            Log.w(TAG, "POST_NOTIFICATIONS denied — reply alert skipped", e)
        }
    }

    /** Clears reply alerts without touching the FGS connected notification (id 15001). */
    fun clearReplyAlerts(context: Context) {
        NotificationManagerCompat.from(context).cancel(NOTIFICATION_ID_BASE)
    }

    /**
     * Import a user-picked audio URI into app-private storage so the notification
     * channel can play it (system cannot read arbitrary SAF URIs).
     * @return absolute path stored in prefs, or null on failure
     */
    fun importCustomSound(context: Context, source: Uri): String? {
        return try {
            val dir = File(context.filesDir, "sounds").also { it.mkdirs() }
            val dest = File(dir, SOUND_FILE_NAME)
            context.contentResolver.openInputStream(source)?.use { input ->
                dest.outputStream().use { output -> input.copyTo(output) }
            } ?: return null
            if (!dest.isFile || dest.length() == 0L) return null
            dest.absolutePath
        } catch (e: Exception) {
            Log.w(TAG, "Failed to import custom sound", e)
            null
        }
    }

    fun clearCustomSoundFile(context: Context) {
        try {
            File(File(context.filesDir, "sounds"), SOUND_FILE_NAME).delete()
        } catch (_: Exception) {
            // ignore
        }
    }

    private fun resolveSoundUri(context: Context, soundPath: String): Uri {
        if (soundPath.isNotBlank()) {
            val file = File(soundPath)
            if (file.isFile) {
                return try {
                    val uri = FileProvider.getUriForFile(
                        context,
                        "${context.packageName}.fileprovider",
                        file
                    )
                    // Let the system / SystemUI read the sound when the channel fires.
                    context.grantUriPermission(
                        "com.android.systemui",
                        uri,
                        Intent.FLAG_GRANT_READ_URI_PERMISSION
                    )
                    uri
                } catch (e: Exception) {
                    Log.w(TAG, "FileProvider sound URI failed; using default", e)
                    RingtoneManager.getDefaultUri(RingtoneManager.TYPE_NOTIFICATION)
                }
            }
        }
        return RingtoneManager.getDefaultUri(RingtoneManager.TYPE_NOTIFICATION)
    }

    private fun notificationIdFor(@Suppress("UNUSED_PARAMETER") frameId: String): Int =
        // Stable single id so opening chat can cancel the latest reply without
        // wiping the FGS connected notification (ConnectedNotification.NOTIFICATION_ID).
        NOTIFICATION_ID_BASE

    private fun truncate(text: String, max: Int): String {
        if (text.length <= max) return text
        return text.take(max - 1).trimEnd() + "…"
    }
}
