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
import androidx.compose.foundation.layout.Arrangement
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
import com.housevictoria.companion.net.EmailAccountDto
import com.housevictoria.companion.net.EmailSettingsClient
import com.housevictoria.companion.net.HealthClient
import com.housevictoria.companion.net.WsConnectionState
import com.housevictoria.companion.notify.ConnectedNotification
import com.housevictoria.companion.notify.ReplyNotification
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.runtime.LaunchedEffect

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(onBack: () -> Unit) {
    val context = LocalContext.current
    val saved = remember { CompanionPrefs.load(context) }
    var wsUrl by remember { mutableStateOf(saved.wsUrl) }
    var httpBase by remember { mutableStateOf(saved.httpBaseUrl.ifBlank { saved.resolvedHttpBase() }) }
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
                value = httpBase,
                onValueChange = { httpBase = it },
                modifier = Modifier.fillMaxWidth(),
                label = { Text("HTTP base (MediaGen / Gallery)") },
                singleLine = true,
                placeholder = { Text("http://127.0.0.1:7700") }
            )
            Spacer(Modifier.height(4.dp))
            Text(
                text = "Used for /api/companion/v1 (ComfyUI generate + file download). " +
                    "Usually derived from WS URL; override for Tailscale HTTPS. " +
                    "Contact id stub: ${CompanionPrefs.DEFAULT_CONTACT_ID} (multi-persona later).",
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
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
            Text(
                text = "Mailboxes",
                style = MaterialTheme.typography.titleSmall
            )
            Spacer(Modifier.height(4.dp))
            Text(
                text = "Give Victoria a mailbox she can check, and optionally yours. " +
                    "For Gmail, paste an App Password (not your normal login password).",
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Spacer(Modifier.height(8.dp))
            EmailAccountsSection(
                httpBase = httpBase,
                token = token,
                onStatus = { msg, ok ->
                    status = msg
                    statusOk = ok
                }
            )
            Spacer(Modifier.height(20.dp))
            OutlinedButton(
                onClick = {
                    val draft = CompanionConfig(
                        wsUrl = wsUrl.trim(),
                        token = token.trim(),
                        httpBaseUrl = httpBase.trim(),
                        contactId = CompanionPrefs.DEFAULT_CONTACT_ID
                    )
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
                    val draft = CompanionConfig(
                        wsUrl = wsUrl.trim(),
                        token = token.trim(),
                        httpBaseUrl = httpBase.trim(),
                        contactId = CompanionPrefs.DEFAULT_CONTACT_ID
                    )
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

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun EmailAccountsSection(
    httpBase: String,
    token: String,
    onStatus: (String, Boolean?) -> Unit
) {
    val scope = rememberCoroutineScope()
    var accounts by remember { mutableStateOf(listOf<EmailAccountDto>()) }
    var selectedId by remember { mutableStateOf("victoria") }
    var expanded by remember { mutableStateOf(false) }
    var showAdvanced by remember { mutableStateOf(false) }
    var displayName by remember { mutableStateOf("") }
    var address by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var imapHost by remember { mutableStateOf("imap.gmail.com") }
    var imapPort by remember { mutableStateOf("993") }
    var imapSsl by remember { mutableStateOf(true) }
    var smtpHost by remember { mutableStateOf("smtp.gmail.com") }
    var smtpPort by remember { mutableStateOf("587") }
    var smtpSsl by remember { mutableStateOf(false) }
    var enabled by remember { mutableStateOf(true) }
    var passwordHint by remember { mutableStateOf("No app password saved yet") }
    var readyBanner by remember { mutableStateOf("Pick a mailbox to see its status.") }

    fun mailboxTitle(id: String): String = when (id.lowercase()) {
        "victoria" -> "Victoria's mailbox"
        "personal" -> "Kurt's personal mail"
        "business" -> "Kurt's business mail"
        else -> id
    }

    fun mailboxSubtitle(id: String): String = when (id.lowercase()) {
        "victoria" -> "Her inbox — create a mailbox for her"
        "personal" -> "Your personal inbox"
        "business" -> "Your work / business inbox"
        else -> "Mailbox"
    }

    fun defaultDisplayName(id: String): String = when (id.lowercase()) {
        "victoria" -> "Victoria"
        "personal", "business" -> "Kurt"
        else -> ""
    }

    fun applyAccount(account: EmailAccountDto) {
        selectedId = account.id
        displayName = account.displayName.ifBlank { defaultDisplayName(account.id) }
        address = account.address
        imapHost = account.imapHost.ifBlank { "imap.gmail.com" }
        imapPort = (if (account.imapPort > 0) account.imapPort else 993).toString()
        imapSsl = account.imapUseSsl
        smtpHost = account.smtpHost.ifBlank { "smtp.gmail.com" }
        smtpPort = (if (account.smtpPort > 0) account.smtpPort else 587).toString()
        smtpSsl = account.smtpUseSsl
        enabled = account.enabled
        password = ""
        passwordHint = if (account.hasPassword) {
            "App password already saved (won't show again — paste a new one only to replace it)"
        } else {
            "No app password saved yet"
        }
        val title = mailboxTitle(account.id)
        readyBanner = when {
            !account.enabled -> "$title: turned off — Victoria will skip this mailbox."
            account.isConfigured -> "$title: ready — ${account.address}"
            account.address.isBlank() && !account.hasPassword ->
                "$title: not set up yet — add the email address and an App Password, then Save."
            !account.hasPassword -> "$title: needs an App Password before she can sign in."
            else -> "$title: almost ready — check the email address and Save again."
        }
    }

    fun loadAccounts(preferId: String? = null) {
        val base = httpBase.trim().ifBlank { return }
        onStatus("Loading mailboxes…", null)
        scope.launch {
            val result = withContext(Dispatchers.IO) {
                EmailSettingsClient.list(base, token.trim())
            }
            if (!result.ok) {
                onStatus(result.detail ?: "Could not load mailboxes", false)
                return@launch
            }
            val list = result.accounts.ifEmpty {
                listOf(
                    EmailAccountDto("victoria"),
                    EmailAccountDto("personal"),
                    EmailAccountDto("business")
                )
            }
            accounts = list
            val pick = preferId?.let { id -> list.firstOrNull { it.id.equals(id, true) } }
                ?: list.firstOrNull { it.id.equals(selectedId, true) }
                ?: list.first()
            applyAccount(pick)
            onStatus("Mailboxes loaded", true)
        }
    }

    LaunchedEffect(httpBase, token) {
        if (httpBase.isNotBlank()) loadAccounts()
    }

    val slotIds = listOf("victoria", "personal", "business")
    val selectedTitle = mailboxTitle(selectedId)

    ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = it }) {
        OutlinedTextField(
            value = selectedTitle,
            onValueChange = {},
            readOnly = true,
            label = { Text("Which mailbox?") },
            supportingText = { Text(mailboxSubtitle(selectedId)) },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
            modifier = Modifier
                .menuAnchor()
                .fillMaxWidth()
        )
        ExposedDropdownMenu(
            expanded = expanded,
            onDismissRequest = { expanded = false }
        ) {
            slotIds.forEach { id ->
                DropdownMenuItem(
                    text = {
                        Column {
                            Text(mailboxTitle(id))
                            Text(
                                mailboxSubtitle(id),
                                style = MaterialTheme.typography.labelSmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    },
                    onClick = {
                        expanded = false
                        accounts.firstOrNull { it.id.equals(id, true) }?.let { applyAccount(it) }
                            ?: run {
                                selectedId = id
                                displayName = defaultDisplayName(id)
                                passwordHint = "No app password saved yet"
                                readyBanner = "${mailboxTitle(id)}: not set up yet."
                            }
                    }
                )
            }
        }
    }
    Spacer(Modifier.height(8.dp))
    Text(
        text = readyBanner,
        style = MaterialTheme.typography.bodySmall,
        color = MaterialTheme.colorScheme.onSurfaceVariant
    )
    Spacer(Modifier.height(8.dp))
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text("Victoria may use this mailbox", modifier = Modifier.weight(1f))
        Switch(checked = enabled, onCheckedChange = { enabled = it })
    }
    OutlinedTextField(
        value = address,
        onValueChange = { address = it },
        modifier = Modifier.fillMaxWidth(),
        label = { Text("Email address") },
        placeholder = { Text("name@gmail.com") },
        singleLine = true
    )
    Spacer(Modifier.height(8.dp))
    OutlinedTextField(
        value = password,
        onValueChange = { password = it },
        modifier = Modifier.fillMaxWidth(),
        label = { Text("App password") },
        singleLine = true,
        visualTransformation = PasswordVisualTransformation(),
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
        placeholder = { Text("Paste app password (leave blank to keep)") },
        supportingText = {
            Text(
                "Gmail: Google Account → Security → App passwords. " +
                    "Paste the 16-character password. Leave blank if already saved."
            )
        }
    )
    Text(
        text = passwordHint,
        style = MaterialTheme.typography.labelSmall,
        color = MaterialTheme.colorScheme.onSurfaceVariant
    )
    Spacer(Modifier.height(8.dp))
    OutlinedTextField(
        value = displayName,
        onValueChange = { displayName = it },
        modifier = Modifier.fillMaxWidth(),
        label = { Text("Name on outgoing mail") },
        placeholder = { Text("e.g. Victoria or Kurt") },
        singleLine = true
    )
    Spacer(Modifier.height(8.dp))
    TextButton(onClick = { showAdvanced = !showAdvanced }) {
        Text(if (showAdvanced) "Hide advanced server settings" else "Advanced server settings (usually leave alone)")
    }
    if (showAdvanced) {
        Text(
            text = "Defaults are already set for Gmail. Only change these if your provider gave you different servers.",
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Spacer(Modifier.height(4.dp))
        OutlinedButton(
            onClick = {
                imapHost = "imap.gmail.com"
                imapPort = "993"
                imapSsl = true
                smtpHost = "smtp.gmail.com"
                smtpPort = "587"
                smtpSsl = false
                onStatus("Gmail defaults filled in — tap Save this mailbox when ready", true)
            },
            modifier = Modifier.fillMaxWidth()
        ) { Text("Reset to Gmail defaults") }
        Spacer(Modifier.height(8.dp))
        OutlinedTextField(
            value = imapHost,
            onValueChange = { imapHost = it },
            modifier = Modifier.fillMaxWidth(),
            label = { Text("Incoming mail server") },
            singleLine = true
        )
        Spacer(Modifier.height(8.dp))
        OutlinedTextField(
            value = imapPort,
            onValueChange = { imapPort = it },
            modifier = Modifier.fillMaxWidth(),
            label = { Text("Incoming port") },
            singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number)
        )
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text("Incoming connection encrypted", modifier = Modifier.weight(1f))
            Switch(checked = imapSsl, onCheckedChange = { imapSsl = it })
        }
        OutlinedTextField(
            value = smtpHost,
            onValueChange = { smtpHost = it },
            modifier = Modifier.fillMaxWidth(),
            label = { Text("Outgoing mail server") },
            singleLine = true
        )
        Spacer(Modifier.height(8.dp))
        OutlinedTextField(
            value = smtpPort,
            onValueChange = { smtpPort = it },
            modifier = Modifier.fillMaxWidth(),
            label = { Text("Outgoing port") },
            singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number)
        )
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text("Outgoing uses port 465 (rare)", modifier = Modifier.weight(1f))
            Switch(checked = smtpSsl, onCheckedChange = { smtpSsl = it })
        }
    }
    Spacer(Modifier.height(8.dp))
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        OutlinedButton(
            onClick = { loadAccounts(selectedId) },
            modifier = Modifier.weight(1f)
        ) { Text("Reload") }
        Button(
            onClick = {
                val base = httpBase.trim()
                if (base.isBlank()) {
                    onStatus("Set HTTP base above first (Host address)", false)
                    return@Button
                }
                val addr = address.trim()
                if (addr.isBlank() || !addr.contains('@')) {
                    onStatus("Enter a full email address (like name@gmail.com)", false)
                    return@Button
                }
                val existing = accounts.firstOrNull { it.id.equals(selectedId, true) }
                if (password.isBlank() && existing?.hasPassword != true) {
                    onStatus("Paste an App Password once so she can sign in", false)
                    return@Button
                }
                val draft = EmailAccountDto(
                    id = selectedId,
                    role = selectedId,
                    displayName = displayName.trim().ifBlank { defaultDisplayName(selectedId) },
                    address = addr,
                    username = addr,
                    imapHost = imapHost.trim().ifBlank { "imap.gmail.com" },
                    imapPort = imapPort.toIntOrNull() ?: 993,
                    imapUseSsl = imapSsl,
                    smtpHost = smtpHost.trim().ifBlank { "smtp.gmail.com" },
                    smtpPort = smtpPort.toIntOrNull() ?: 587,
                    smtpUseSsl = smtpSsl,
                    enabled = enabled
                )
                onStatus("Saving ${mailboxTitle(selectedId)}…", null)
                scope.launch {
                    val result = withContext(Dispatchers.IO) {
                        EmailSettingsClient.upsert(base, token.trim(), draft, password)
                    }
                    if (!result.ok) {
                        onStatus(result.detail ?: "Save failed", false)
                        return@launch
                    }
                    accounts = result.accounts.ifEmpty { accounts }
                    accounts.firstOrNull { it.id.equals(selectedId, true) }?.let { applyAccount(it) }
                    password = ""
                    onStatus("Saved ${mailboxTitle(selectedId)}", true)
                }
            },
            modifier = Modifier.weight(1f)
        ) { Text("Save this mailbox") }
    }
}
