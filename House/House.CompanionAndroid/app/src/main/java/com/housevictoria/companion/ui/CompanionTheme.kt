package com.housevictoria.companion.ui

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

private val Gold = Color(0xFFC9A227)
private val Deep = Color(0xFF1A1520)
private val Cream = Color(0xFFF5E6C8)

private val DarkColors = darkColorScheme(
    primary = Gold,
    onPrimary = Deep,
    secondary = Cream,
    background = Deep,
    surface = Color(0xFF241C2E),
    onBackground = Cream,
    onSurface = Cream
)

private val LightColors = lightColorScheme(
    primary = Color(0xFF7A5C10),
    onPrimary = Color.White,
    secondary = Deep,
    background = Color(0xFFF7F2EA),
    surface = Color.White,
    onBackground = Deep,
    onSurface = Deep
)

@Composable
fun CompanionTheme(content: @Composable () -> Unit) {
    val dark = isSystemInDarkTheme()
    MaterialTheme(
        colorScheme = if (dark) DarkColors else LightColors,
        content = content
    )
}
