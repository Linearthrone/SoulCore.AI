package com.housevictoria.companion.ui

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.expandVertically
import androidx.compose.animation.shrinkVertically
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.housevictoria.companion.data.HouseLamps
import com.housevictoria.companion.data.LampLight

private data class LampWell(val label: String, val light: LampLight, val onColor: Color)

private data class DrawerDest(val route: String, val label: String)

private val drawerDestinations = listOf(
    DrawerDest("home", "Chat"),
    DrawerDest("call", "Call"),
    DrawerDest("mediagen", "Media"),
    DrawerDest("gallery", "Gallery"),
    DrawerDest("settings", "Settings")
)

@Composable
fun HouseDrawer(
    open: Boolean,
    lamps: HouseLamps,
    currentRoute: String?,
    onToggle: () -> Unit,
    onNavigate: (String) -> Unit
) {
    val wells = listOf(
        LampWell("SOULCORE", lamps.soulcore, HousePalette.LampSoul),
        LampWell("OLLAMA", lamps.ollama, HousePalette.LampOllama),
        LampWell("UNREAL", lamps.unreal, HousePalette.LampUnreal),
        LampWell("COMFYUI", lamps.comfy, HousePalette.LampCool),
        LampWell("CUA", lamps.cua, HousePalette.LampCool),
        LampWell("SANDBOX", lamps.sandbox, HousePalette.LampCool)
    )
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(HousePalette.Metal)
            .border(width = 1.dp, color = HousePalette.GlassEdge)
    ) {
        AnimatedVisibility(
            visible = open,
            enter = expandVertically(),
            exit = shrinkVertically()
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(start = 12.dp, end = 12.dp, top = 14.dp, bottom = 4.dp)
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceEvenly
                ) {
                    wells.forEach { well -> LampWellView(well) }
                }
                Text(
                    text = "Lights are what the house last reported. This phone cannot start or stop them.",
                    style = MaterialTheme.typography.labelSmall,
                    color = HousePalette.Muted,
                    modifier = Modifier.padding(top = 12.dp, start = 4.dp, end = 4.dp)
                )
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .horizontalScroll(rememberScrollState()),
                    horizontalArrangement = Arrangement.spacedBy(2.dp)
                ) {
                    drawerDestinations.forEach { dest ->
                        val selected = currentRoute == dest.route
                        TextButton(onClick = { onNavigate(dest.route) }) {
                            Text(
                                text = dest.label,
                                color = if (selected) HousePalette.Ivory else HousePalette.Muted,
                                fontWeight = if (selected) FontWeight.SemiBold else FontWeight.Normal
                            )
                        }
                    }
                }
            }
        }
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .clickable(onClick = onToggle)
                .padding(horizontal = 16.dp, vertical = 12.dp)
                .semantics { contentDescription = if (open) "Close House" else "Open House" },
            horizontalArrangement = Arrangement.Center,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                text = "HOUSE",
                color = HousePalette.Ivory,
                style = MaterialTheme.typography.labelMedium,
                fontWeight = FontWeight.SemiBold,
                letterSpacing = 3.sp
            )
            Spacer(modifier = Modifier.size(10.dp))
            Box(
                modifier = Modifier
                    .size(8.dp)
                    .clip(CircleShape)
                    .background(pipColor(lamps.soulcore))
            )
        }
    }
}

@Composable
private fun LampWellView(well: LampWell) {
    val glow = when (well.light) {
        LampLight.On -> well.onColor
        LampLight.Off -> HousePalette.LampOff
        LampLight.Unknown -> Color.Transparent
    }
    val ring = when (well.light) {
        LampLight.On -> well.onColor
        LampLight.Off -> HousePalette.LampOff
        LampLight.Unknown -> HousePalette.GlassEdge
    }
    val spoken = when (well.light) {
        LampLight.On -> "${well.label} on"
        LampLight.Off -> "${well.label} off"
        LampLight.Unknown -> "${well.label} no reading"
    }
    Column(
        horizontalAlignment = Alignment.CenterHorizontally,
        modifier = Modifier.semantics { contentDescription = spoken }
    ) {
        Box(
            modifier = Modifier
                .size(28.dp)
                .clip(CircleShape)
                .background(if (well.light == LampLight.On) glow.copy(alpha = 0.85f) else HousePalette.Ink)
                .border(width = 2.dp, color = ring, shape = CircleShape)
        )
        Text(
            text = well.label,
            color = HousePalette.Muted,
            fontSize = 8.sp,
            letterSpacing = 0.4.sp,
            modifier = Modifier.padding(top = 6.dp)
        )
    }
}

private fun pipColor(light: LampLight): Color = when (light) {
    LampLight.On -> HousePalette.LampOllama
    LampLight.Off -> HousePalette.LampUnreal
    LampLight.Unknown -> HousePalette.LampOff
}
