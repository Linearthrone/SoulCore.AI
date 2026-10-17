package com.housevictoria.companion.ui

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

/** Dark metal and glass from the House shell mockup. One material, no theme picker. */
object HousePalette {
    val Ink = Color(0xFF0E1116)
    val Metal = Color(0xFF161B22)
    val Glass = Color(0xFF1C2430)
    val GlassEdge = Color(0xFF2C3848)
    val Ivory = Color(0xFFE7E4DC)
    val Muted = Color(0xFF9AA3B2)
    val UserGlass = Color(0xFF243044)
    val VictoriaGlass = Color(0xFF182028)
    val Accent = Color(0xFF8FB4D6)
    val LampSoul = Color(0xFF5EC8FF)
    val LampOllama = Color(0xFF3DDC84)
    val LampUnreal = Color(0xFFE25B5B)
    val LampCool = Color(0xFF6AA7FF)
    val LampOff = Color(0xFF2A3038)
    val Danger = Color(0xFFE7B4B0)
}

private val HouseColors = darkColorScheme(
    primary = HousePalette.Accent,
    onPrimary = HousePalette.Ink,
    secondary = HousePalette.Accent,
    onSecondary = HousePalette.Ink,
    background = HousePalette.Ink,
    onBackground = HousePalette.Ivory,
    surface = HousePalette.Metal,
    onSurface = HousePalette.Ivory,
    surfaceVariant = HousePalette.Glass,
    onSurfaceVariant = HousePalette.Muted,
    outline = HousePalette.GlassEdge,
    error = Color(0xFFE25B5B),
    onError = HousePalette.Ink,
    errorContainer = Color(0xFF3A2424),
    onErrorContainer = HousePalette.Danger
)

@Composable
fun CompanionTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = HouseColors,
        content = content
    )
}
