package com.housevictoria.companion.notify

import android.app.Application
import androidx.lifecycle.DefaultLifecycleObserver
import androidx.lifecycle.LifecycleOwner
import androidx.lifecycle.ProcessLifecycleOwner

/**
 * Process-level foreground tracker for FED-151 reply alerts.
 *
 * When [isInForeground] is true, `chat.done` must not post a local ding
 * (user is already looking at the app). Mirrors desktop "unfocused" check.
 */
object AppForeground {
    @Volatile
    var isInForeground: Boolean = false
        private set

    private var installed = false

    fun init(app: Application) {
        if (installed) return
        installed = true
        // Seed true if process is already started in foreground (typical cold start).
        isInForeground = true
        ProcessLifecycleOwner.get().lifecycle.addObserver(
            object : DefaultLifecycleObserver {
                override fun onStart(owner: LifecycleOwner) {
                    isInForeground = true
                }

                override fun onStop(owner: LifecycleOwner) {
                    isInForeground = false
                }
            }
        )
    }
}
