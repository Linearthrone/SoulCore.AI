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
        // Default false: START_STICKY / FGS-only process restarts never get Activity
        // onStart/onStop, so seeding true would permanently suppress reply alerts.
        // Cold start: onStart fires before WS is up; brief window is harmless.
        isInForeground = false
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
