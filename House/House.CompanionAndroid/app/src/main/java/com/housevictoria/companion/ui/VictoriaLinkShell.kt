package com.housevictoria.companion.ui

import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Scaffold
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.navigation.NavGraph.Companion.findStartDestination
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import com.housevictoria.companion.data.CompanionPrefs
import com.housevictoria.companion.data.HouseLamps
import com.housevictoria.companion.data.LampLight
import com.housevictoria.companion.net.CompanionConnection
import com.housevictoria.companion.net.HealthClient
import com.housevictoria.companion.net.WsConnectionState
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

@Composable
fun VictoriaLinkShell() {
    val context = LocalContext.current
    val nav = rememberNavController()
    val backStack by nav.currentBackStackEntryAsState()
    val current = backStack?.destination?.route
    var houseOpen by remember { mutableStateOf(false) }
    var lamps by remember { mutableStateOf(HouseLamps.unknown()) }
    val connPair by CompanionConnection.state.collectAsState()

    LaunchedEffect(connPair.first, houseOpen) {
        val wsUrl = CompanionPrefs.load(context).wsUrl
        val read = withContext(Dispatchers.IO) { HealthClient.readHouseLamps(wsUrl) }
        lamps = when {
            read != null -> read
            connPair.first == WsConnectionState.Connected ->
                lamps.copy(soulcore = LampLight.On)
            connPair.first == WsConnectionState.Failed ||
                connPair.first == WsConnectionState.Disconnected ->
                HouseLamps.hostUnreachable()
            else -> lamps
        }
    }

    fun go(route: String) {
        houseOpen = false
        nav.navigate(route) {
            popUpTo(nav.graph.findStartDestination().id) { saveState = true }
            launchSingleTop = true
            restoreState = true
        }
    }

    Scaffold(
        containerColor = HousePalette.Ink,
        bottomBar = {
            HouseDrawer(
                open = houseOpen,
                lamps = lamps,
                currentRoute = current,
                onToggle = { houseOpen = !houseOpen },
                onNavigate = { route -> go(route) }
            )
        }
    ) { padding ->
        NavHost(
            navController = nav,
            startDestination = "home",
            modifier = Modifier.padding(padding)
        ) {
            composable("home") { ChatScreen() }
            composable("call") { VideoCallScreen() }
            composable("mediagen") { MediaGenScreen() }
            composable("gallery") { GalleryScreen() }
            composable("settings") {
                SettingsScreen(onBack = {
                    if (!nav.popBackStack()) nav.navigate("home")
                })
            }
        }
    }
}
