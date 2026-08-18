package com.housevictoria.companion.ui

import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AutoAwesome
import androidx.compose.material.icons.filled.Chat
import androidx.compose.material.icons.filled.PhotoLibrary
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.Videocam
import androidx.compose.material3.Icon
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.navigation.NavGraph.Companion.findStartDestination
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController

private data class LinkDest(val route: String, val label: String, val icon: androidx.compose.ui.graphics.vector.ImageVector)

private val destinations = listOf(
    LinkDest("home", "Home", Icons.Default.Chat),
    LinkDest("call", "Call", Icons.Default.Videocam),
    LinkDest("mediagen", "MediaGen", Icons.Default.AutoAwesome),
    LinkDest("gallery", "Gallery", Icons.Default.PhotoLibrary),
    LinkDest("settings", "Settings", Icons.Default.Settings)
)

@Composable
fun VictoriaLinkShell() {
    val nav = rememberNavController()
    val backStack by nav.currentBackStackEntryAsState()
    val current = backStack?.destination?.route

    Scaffold(
        bottomBar = {
            NavigationBar {
                destinations.forEach { dest ->
                    NavigationBarItem(
                        selected = current == dest.route,
                        onClick = {
                            nav.navigate(dest.route) {
                                popUpTo(nav.graph.findStartDestination().id) { saveState = true }
                                launchSingleTop = true
                                restoreState = true
                            }
                        },
                        icon = { Icon(dest.icon, contentDescription = dest.label) },
                        label = { Text(dest.label) }
                    )
                }
            }
        }
    ) { padding ->
        NavHost(
            navController = nav,
            startDestination = "home",
            modifier = Modifier.padding(padding)
        ) {
            composable("home") { ChatScreen(onOpenSettings = { nav.navigate("settings") }) }
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
