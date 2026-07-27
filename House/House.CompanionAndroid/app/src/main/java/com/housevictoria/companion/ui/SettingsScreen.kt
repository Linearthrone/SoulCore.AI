package com.housevictoria.companion.ui

import androidx.compose.foundation.layout.Column
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
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import com.housevictoria.companion.data.CompanionConfig
import com.housevictoria.companion.data.CompanionPrefs
import com.housevictoria.companion.net.CompanionAuthHeaders
import com.housevictoria.companion.net.HealthClient
import com.housevictoria.companion.notify.NotificationPlaceholder
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

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
            Spacer(Modifier.height(8.dp))
            Text(
                text = "Notifications / foreground WS: placeholders only (FED-150/151).",
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
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
                    NotificationPlaceholder.ensureChannel(context)
                    NotificationPlaceholder.scheduleBackgroundPersistence(context)
                    status = "Settings saved. Auth ${CompanionAuthHeaders.redactForLog(draft.token)}."
                    statusOk = true
                },
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("Save")
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
                    CompanionPrefs.clearAll(context)
                    token = ""
                    wsUrl = CompanionPrefs.DEFAULT_WS_URL
                    status = "Credentials cleared; URL reset to loopback default."
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
