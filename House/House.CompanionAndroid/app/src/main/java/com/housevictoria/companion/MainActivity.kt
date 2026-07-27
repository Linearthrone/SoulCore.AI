package com.housevictoria.companion

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.Composable
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.housevictoria.companion.notify.ReplyNotification
import com.housevictoria.companion.ui.ChatScreen
import com.housevictoria.companion.ui.CompanionTheme
import com.housevictoria.companion.ui.SettingsScreen

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        handleReplyTap(intent)
        enableEdgeToEdge()
        setContent {
            CompanionTheme {
                CompanionNav()
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
        // Opening the app dismisses the latest reply ding (FGS connected notif stays).
        ReplyNotification.clearReplyAlerts(this)
    }

    private fun handleReplyTap(intent: Intent?) {
        if (intent?.getBooleanExtra(ReplyNotification.EXTRA_OPEN_CHAT, false) == true) {
            ReplyNotification.clearReplyAlerts(this)
            // Nav host startDestination is already "chat"; SINGLE_TOP brings UI to front.
        }
    }
}

@Composable
private fun CompanionNav() {
    val nav = rememberNavController()
    NavHost(navController = nav, startDestination = "chat") {
        composable("chat") {
            ChatScreen(onOpenSettings = { nav.navigate("settings") })
        }
        composable("settings") {
            SettingsScreen(onBack = { nav.popBackStack() })
        }
    }
}
