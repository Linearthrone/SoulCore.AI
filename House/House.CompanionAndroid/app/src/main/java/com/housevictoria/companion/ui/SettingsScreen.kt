package com.housevictoria.companion.ui

import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import com.housevictoria.companion.data.CompanionConfig
import com.housevictoria.companion.data.CompanionPrefs
import com.housevictoria.companion.data.NotificationPrefs
import com.housevictoria.companion.net.CompanionAuthHeaders
import com.housevictoria.companion.net.CompanionConnection
import com.housevictoria.companion.net.HealthClient
import com.housevictoria.companion.net.WsConnectionState
import com.housevictoria.companion.notify.ConnectedNotification
import com.housevictoria.companion.notify.ReplyNotification
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(onBack: () -> Unit) {
    val context = LocalContext.current
    val saved = remember { CompanionPrefs.load(context) }
    var wsUrl by remember { mutableStateOf(saved.wsUrl) }
    var token by remember { mutableStateOf(saved.token) }
    var status by remember { mutableStateOf("") }
    var statusOk by remember { mutableStateOf<Boolean?>(null) }
    val scope = rememberCoroutineScope()

    val savedNotif = remember { CompanionPrefs.loadNotification(context) }
    var notifEnabled by remember { mutableStateOf(savedNotif.enabled) }
    var notifVibration by remember { mutableStateOf(savedNotif.vibration) }
    var notifSoundPath by remember { mutableStateOf(savedNotif.soundPath) }

    val pickSound = rememberLauncherForActivityResult(
        ActivityResultContracts.OpenDocument()
    ) { uri: Uri? ->
        if (uri == null) return@rememberLauncherForActivityResult
        val imported = ReplyNotification.importCustomSound(context, uri)
        if (imported == null) {
            status = "Could not import sound file."
            statusOk = false
            return@rememberLauncherForActivityResult
        }
        notifSoundPath = imported
        CompanionPrefs.saveNotification(
            context,
            NotificationPrefs(
                enabled = notifEnabled,
                soundPath = imported,
                vibration = notifVibration
            )
        )
        ReplyNotification.ensureChannel(context, recreate = true)
        status = "Custom reply sound saved · channel recreated."
        statusOk = true
    }

    fun persistNotificationPrefs(recreateChannel: Boolean = false) {
        CompanionPrefs.saveNotification(
            context,
            NotificationPrefs(
                enabled = notifEnabled,
                soundPath = notifSoundPath,
                vibration = notifVibration
            )
        )
        if (recreateChannel) {
            ReplyNotification.ensureChannel(context, recreate = true)
        }
    }

    val connPair by CompanionConnection.state.collectAsState()
    val serviceRunning by CompanionConnection.serviceRunning.collectAsState()
    val fgsLabel = when {
        !serviceRunning -> "Foreground service: stopped"
        connPair.first == WsConnectionState.Connected -> "Foreground service: running · connected"
        connPair.first == WsConnectionState.Connecting -> "Foreground service: running · connecting"
        else -> "Foreground service: running · ${connPair.first.name.lowercase()}"
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Connection") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                }
            )
        }
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(20.dp)
                .verticalScroll(rememberScrollState())
        ) {
            Text(
                text = "SoulCore.Host WebSocket endpoint.",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Spacer(Modifier.height(8.dp))
            Text(
                text = "Loopback (emulator / USB reverse):\n  ${CompanionPrefs.DEFAULT_WS_URL}\n" +
                    "Tailscale (real phone):\n  ${CompanionPrefs.TAILSCALE_WS_URL_PLACEHOLDER}",
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Spacer(Modifier.height(16.dp))
            OutlinedTextField(
                value = wsUrl,
                onValueChange = { wsUrl = it },
                modifier = Modifier.fillMaxWidth(),
                label = { Text("WebSocket URL") },
                singleLine = true,
                placeholder = { Text(CompanionPrefs.DEFAULT_WS_URL) }
            )
            Spacer(Modifier.height(12.dp))
            OutlinedTextField(
                value = token,
                onValueChange = { token = it },
                modifier = Modifier.fillMaxWidth(),
                label = { Text("API token (optional on loopback)") },
                singleLine = true,
                visualTransformation = PasswordVisualTransformation(),
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
                placeholder = { Text("SOULCORE_COMPANION_API_TOKEN") }
            )
            Spacer(Modifier.height(8.dp))
            Text(
                text = "Token is stored in Android Keystore-backed EncryptedSharedPreferences. " +
                    "WS upgrade sends ${CompanionAuthHeaders.AUTHORIZATION}: " +
                    "${CompanionAuthHeaders.BEARER_PREFIX.trim()} … " +
                    "(alias ${CompanionAuthHeaders.API_KEY} also accepted by Host). " +
                    "Never logged in cleartext.",
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Spacer(Modifier.height(16.dp))
            Text(
                text = "Background connection (FED-150)",
                style = MaterialTheme.typography.titleSmall
            )
            Spacer(Modifier.height(4.dp))
            Text(
                text = fgsLabel,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Spacer(Modifier.height(4.dp))
            Text(
                text = "While connected, a persistent low-priority notification keeps the WS " +
                    "alive when the app is backgrounded (OkHttp ping every 30s). " +
                    "Stop from this screen, or via the notification Disconnect action. " +
                    "OEM caveat: Xiaomi / Huawei / Oppo / Samsung battery savers may still " +
                    "kill the socket after hours–days unless you exempt Victoria Companion " +
                    "from battery optimization. Expected keep-alive window on stock Android: " +
                    "hours while the FGS notification is visible.",
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Spacer(Modifier.height(20.dp))
            Text(
                text = "Reply notifications (FED-151)",
                style = MaterialTheme.typography.titleSmall
            )
            Spacer(Modifier.height(4.dp))
            Text(
                text = "When Victoria finishes a reply (chat.done) while the app is backgrounded, " +
                    "a high-importance local notification appears (channel " +
                    "${ReplyNotification.CHANNEL_ID}). Tap opens chat. Mirrors desktop " +
                    "NotificationService (Enabled + SoundPath) with a vibration toggle.",
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Spacer(Modifier.height(8.dp))
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "Enable reply alerts",
                    modifier = Modifier.weight(1f),
                    style = MaterialTheme.typography.bodyMedium
                )
                Switch(
                    checked = notifEnabled,
                    onCheckedChange = {
                        notifEnabled = it
                        persistNotificationPrefs()
                        status = if (it) "Reply alerts enabled." else "Reply alerts muted."
                        statusOk = true
                    }
                )
            }
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "Vibration",
                    modifier = Modifier.weight(1f),
                    style = MaterialTheme.typography.bodyMedium
                )
                Switch(
                    checked = notifVibration,
                    onCheckedChange = {
                        notifVibration = it
                        persistNotificationPrefs(recreateChannel = true)
                        status = "Vibration ${if (it) "on" else "off"} · channel recreated."
                        statusOk = true
                    }
                )
            }
            Spacer(Modifier.height(4.dp))
            Text(
                text = "Sound: " + if (notifSoundPath.isBlank()) {
                    "(system default)"
                } else {
                    File(notifSoundPath).name.ifBlank { notifSoundPath }
                },
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Spacer(Modifier.height(8.dp))
            OutlinedButton(
                onClick = {
                    pickSound.launch(arrayOf("audio/*", "audio/wav", "audio/x-wav"))
                },
                modifier = Modifier.fillMaxWidth(),
                enabled = notifEnabled
            ) {
                Text("Pick custom sound…")
            }
            Spacer(Modifier.height(8.dp))
            OutlinedButton(
                onClick = {
                    ReplyNotification.clearCustomSoundFile(context)
                    notifSoundPath = ""
                    persistNotificationPrefs(recreateChannel = true)
                    status = "Reply sound reset to system default."
                    statusOk = true
                },
                modifier = Modifier.fillMaxWidth(),
                enabled = notifEnabled && notifSoundPath.isNotBlank()
            ) {
                Text("Use system default sound")
            }
            Spacer(Modifier.height(20.dp))
            OutlinedButton(
                onClick = {
                    val draft = CompanionConfig(wsUrl = wsUrl.trim(), token = token.trim())
                    val err = draft.validate()
                    if (err != null) {
                        status = err
                        statusOk = false
                        return@OutlinedButton
                    }
                    status = "Checking /health…"
                    statusOk = null
                    scope.launch {
                        val result = withContext(Dispatchers.IO) {
                            HealthClient.checkHealth(draft.wsUrl)
                        }
                        status = result
                        statusOk = result.startsWith("Healthy")
                    }
                },
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("Test /health")
            }
            Spacer(Modifier.height(8.dp))
            Button(
                onClick = {
                    val draft = CompanionConfig(wsUrl = wsUrl.trim(), token = token.trim())
                    val err = draft.validate()
                    if (err != null) {
                        status = err
                        statusOk = false
                        return@Button
                    }
                    CompanionPrefs.save(context, draft)
                    persistNotificationPrefs()
                    ConnectedNotification.ensureChannel(context)
                    ReplyNotification.ensureChannel(context)
                    CompanionConnection.start(context, draft.wsUrl, draft.token)
                    status = "Settings saved · FGS reconnecting. Auth ${CompanionAuthHeaders.redactForLog(draft.token)}."
                    statusOk = true
                },
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("Save & reconnect")
            }
            Spacer(Modifier.height(8.dp))
            OutlinedButton(
                onClick = {
                    CompanionConnection.stop(context)
                    status = "Disconnected · foreground service stopped · notification cleared."
                    statusOk = true
                },
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("Disconnect (stop background WS)")
            }
            Spacer(Modifier.height(8.dp))
            OutlinedButton(
                onClick = {
                    CompanionPrefs.clearToken(context)
                    token = ""
                    status = "Token cleared from Keystore-backed store."
                    statusOk = true
                },
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("Clear token")
            }
            Spacer(Modifier.height(8.dp))
            OutlinedButton(
                onClick = {
                    CompanionConnection.stop(context)
                    CompanionPrefs.clearAll(context)
                    token = ""
                    wsUrl = CompanionPrefs.DEFAULT_WS_URL
                    status = "Credentials cleared; FGS stopped; URL reset to loopback default."
                    statusOk = true
                },
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("Clear all (logout)")
            }
            if (status.isNotBlank()) {
                Spacer(Modifier.height(16.dp))
                Text(
                    text = status,
                    color = when (statusOk) {
                        true -> MaterialTheme.colorScheme.primary
                        false -> MaterialTheme.colorScheme.error
                        null -> MaterialTheme.colorScheme.onSurfaceVariant
                    }
                )
            }
        }
    }
}
