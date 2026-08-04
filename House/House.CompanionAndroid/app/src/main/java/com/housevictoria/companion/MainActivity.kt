package com.housevictoria.companion

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import com.housevictoria.companion.notify.ReplyNotification
import com.housevictoria.companion.ui.CompanionTheme
import com.housevictoria.companion.ui.VictoriaLinkShell

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        handleReplyTap(intent)
        enableEdgeToEdge()
        setContent {
            CompanionTheme {
                VictoriaLinkShell()
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        handleReplyTap(intent)
    }

    override fun onResume() {
        super.onResume()
        ReplyNotification.clearReplyAlerts(this)
    }

    private fun handleReplyTap(intent: Intent?) {
        if (intent?.getBooleanExtra(ReplyNotification.EXTRA_OPEN_CHAT, false) == true) {
            ReplyNotification.clearReplyAlerts(this)
        }
    }
}
