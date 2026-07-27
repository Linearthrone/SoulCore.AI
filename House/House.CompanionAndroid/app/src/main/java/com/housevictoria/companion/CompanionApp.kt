package com.housevictoria.companion

import android.app.Application
import com.housevictoria.companion.notify.AppForeground
import com.housevictoria.companion.notify.ConnectedNotification
import com.housevictoria.companion.notify.ReplyNotification

class CompanionApp : Application() {
    override fun onCreate() {
        super.onCreate()
        // Create FGS channel early so Settings / first connect never race the first notify.
        ConnectedNotification.ensureChannel(this)
        // FED-151: foreground tracker + reply channel + frame collector (does not touch FGS hub).
        AppForeground.init(this)
        ReplyNotification.ensureChannel(this)
        ReplyNotification.install(this)
    }
}
