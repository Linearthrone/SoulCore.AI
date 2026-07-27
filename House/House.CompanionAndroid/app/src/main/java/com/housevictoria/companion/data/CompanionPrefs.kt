package com.housevictoria.companion.data

import android.content.Context
import android.content.SharedPreferences
import android.util.Log
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey

/**
 * Connection settings for SoulCore.Host.
 *
 * - WS URL: plain SharedPreferences (non-secret).
 * - API token: Android Keystore-backed [EncryptedSharedPreferences]
 *   (MasterKey AES256_GCM + AES256_SIV keys / AES256_GCM values).
 *
 * Never log the raw token.
 */
data class CompanionConfig(
    val wsUrl: String,
    val token: String
) {
    fun validate(): String? {
        if (wsUrl.isBlank()) return "WebSocket URL is required."
        if (!(wsUrl.startsWith("ws://") || wsUrl.startsWith("wss://"))) {
            return "URL must start with ws:// or wss://"
        }
        if (!wsUrl.contains("/ws")) {
            return "URL should end with /ws (SoulCore chat endpoint)."
        }
        return null
    }
}

/**
 * Local reply-alert preferences (FED-151) — mirrors desktop
 * `NotificationSettings` (Enabled + SoundPath) plus a vibration toggle.
 */
data class NotificationPrefs(
    val enabled: Boolean = true,
    /** Absolute path to imported custom sound file; empty = OS default notification sound. */
    val soundPath: String = "",
    val vibration: Boolean = true
)

object CompanionPrefs {
    private const val TAG = "CompanionPrefs"
    private const val PREFS = "companion_prefs"
    private const val SECURE_PREFS = "companion_secure_prefs"
    private const val KEY_WS_URL = "ws_url"
    private const val KEY_TOKEN = "token"

    /** FED-151 reply notification keys (plain prefs — non-secret). */
    const val KEY_NOTIF_ENABLED = "notif_enabled"
    const val KEY_NOTIF_SOUND_PATH = "notif_sound_path"
    const val KEY_NOTIF_VIBRATION = "notif_vibration"

    /** Emulator / on-device loopback default — mirrors House.ChatDesktop ConnectionDefaults. */
    const val DEFAULT_WS_URL = "ws://127.0.0.1:7700/ws"

    /**
     * Tailscale serve placeholder for a real phone (OPS/SEC path).
     * Replace host + tailnet; use `wss://` when TLS is terminated by Tailscale.
     */
    const val TAILSCALE_WS_URL_PLACEHOLDER = "wss://<host>.<tailnet>.ts.net/ws"

    fun load(context: Context): CompanionConfig {
        migratePlaintextTokenIfNeeded(context)
        val prefs = plainPrefs(context)
        return CompanionConfig(
            wsUrl = prefs.getString(KEY_WS_URL, DEFAULT_WS_URL).orEmpty(),
            token = securePrefs(context).getString(KEY_TOKEN, "").orEmpty()
        )
    }

    fun save(context: Context, config: CompanionConfig) {
        plainPrefs(context)
            .edit()
            .putString(KEY_WS_URL, config.wsUrl.trim())
            .apply()
        // Drop any leftover plaintext token from Phase 0 shell.
        plainPrefs(context).edit().remove(KEY_TOKEN).apply()
        securePrefs(context)
            .edit()
            .putString(KEY_TOKEN, config.token.trim())
            .apply()
        Log.d(TAG, "Settings saved (token ${tokenPresence(config.token)})")
    }

    /** Clears Keystore-backed token only; keeps connect URL. */
    fun clearToken(context: Context) {
        plainPrefs(context).edit().remove(KEY_TOKEN).apply()
        securePrefs(context).edit().remove(KEY_TOKEN).apply()
        Log.d(TAG, "Token cleared")
    }

    /** Clears token and resets WS URL to loopback default (logout / clear credentials). */
    fun clearAll(context: Context) {
        clearToken(context)
        plainPrefs(context)
            .edit()
            .putString(KEY_WS_URL, DEFAULT_WS_URL)
            .apply()
        Log.d(TAG, "Credentials cleared; URL reset to default")
    }

    fun hasToken(context: Context): Boolean =
        load(context).token.isNotBlank()

    fun loadNotification(context: Context): NotificationPrefs {
        val prefs = plainPrefs(context)
        return NotificationPrefs(
            enabled = prefs.getBoolean(KEY_NOTIF_ENABLED, true),
            soundPath = prefs.getString(KEY_NOTIF_SOUND_PATH, "").orEmpty(),
            vibration = prefs.getBoolean(KEY_NOTIF_VIBRATION, true)
        )
    }

    fun saveNotification(context: Context, notification: NotificationPrefs) {
        plainPrefs(context)
            .edit()
            .putBoolean(KEY_NOTIF_ENABLED, notification.enabled)
            .putString(KEY_NOTIF_SOUND_PATH, notification.soundPath.trim())
            .putBoolean(KEY_NOTIF_VIBRATION, notification.vibration)
            .apply()
        Log.d(TAG, "Notification prefs saved (enabled=${notification.enabled}, vibration=${notification.vibration}, sound=${if (notification.soundPath.isBlank()) "default" else "custom"})")
    }

    private fun plainPrefs(context: Context): SharedPreferences =
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)

    private fun securePrefs(context: Context): SharedPreferences {
        val masterKey = MasterKey.Builder(context)
            .setKeyScheme(MasterKey.KeyScheme.AES256_GCM)
            .build()
        return EncryptedSharedPreferences.create(
            context,
            SECURE_PREFS,
            masterKey,
            EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
            EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
        )
    }

    /**
     * One-shot migration: Phase 0 stored token in plaintext [PREFS].
     * Move into encrypted store and wipe the plaintext key.
     */
    private fun migratePlaintextTokenIfNeeded(context: Context) {
        val plain = plainPrefs(context)
        val legacy = plain.getString(KEY_TOKEN, null)
        if (legacy.isNullOrEmpty()) return

        val secure = securePrefs(context)
        if (secure.getString(KEY_TOKEN, "").isNullOrEmpty()) {
            secure.edit().putString(KEY_TOKEN, legacy.trim()).apply()
            Log.i(TAG, "Migrated plaintext token into EncryptedSharedPreferences")
        }
        plain.edit().remove(KEY_TOKEN).apply()
    }

    private fun tokenPresence(token: String): String =
        if (token.trim().isEmpty()) "absent" else "present"
}
