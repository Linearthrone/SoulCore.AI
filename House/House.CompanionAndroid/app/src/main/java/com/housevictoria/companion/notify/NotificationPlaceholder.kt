package com.housevictoria.companion.notify

import android.content.Context

/**
 * FED-151 placeholder — chat.done local notifications (channel + sound + vibration).
 *
 * FED-150 connected/FGS notification lives in [ConnectedNotification], not here.
 */
object NotificationPlaceholder {
    fun ensureChannel(context: Context) {
        // FED-151: NotificationChannel for Victoria reply alerts (separate from connected FGS).
    }
}
