package com.housevictoria.companion.ui

import android.Manifest
import android.content.pm.PackageManager
import android.graphics.BitmapFactory
import android.os.Build
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Send
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import com.housevictoria.companion.data.ChatMessage
import com.housevictoria.companion.data.ChatStore
import com.housevictoria.companion.data.CompanionPrefs
import com.housevictoria.companion.data.MessageRole
import com.housevictoria.companion.data.PresenceHud
import com.housevictoria.companion.net.CompanionConnection
import com.housevictoria.companion.net.WsConnectionState
import kotlinx.coroutines.flow.collectLatest

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ChatScreen(onOpenSettings: () -> Unit) {
    val context = LocalContext.current
    val config = remember { CompanionPrefs.load(context) }
    // Transcript lives in ChatStore (process-scoped + disk-cached) so leaving
    // this screen no longer wipes the thread. See PROP-3 Wave 1.
    val messages by ChatStore.messages.collectAsState()
    var draft by remember { mutableStateOf("") }
    var pendingQuote by remember { mutableStateOf<String?>(null) }
    var quoteEditFor by remember { mutableStateOf<ChatMessage?>(null) }
    var quoteEditText by remember { mutableStateOf("") }
    val listState = rememberLazyListState()

    val connPair by CompanionConnection.state.collectAsState()
    val presence by PresenceHud.state.collectAsState()
    val connLabel = when (connPair.first) {
        WsConnectionState.Connected -> "Connected"
        WsConnectionState.Connecting -> "Connecting…"
        WsConnectionState.Failed -> "Host down"
        WsConnectionState.Disconnected -> "Disconnected"
    }

    val permissionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { /* FGS still starts; notification may be hidden if denied */ }

    fun ensureNotifPermission() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU) return
        val granted = ContextCompat.checkSelfPermission(
            context,
            Manifest.permission.POST_NOTIFICATIONS
        ) == PackageManager.PERMISSION_GRANTED
        if (!granted) {
            permissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
        }
    }

    LaunchedEffect(Unit) {
        ensureNotifPermission()
        CompanionConnection.start(context, config.wsUrl, config.token)
    }

    LaunchedEffect(Unit) {
        var lastDetail: String? = null
        CompanionConnection.state.collectLatest { (state, detail) ->
            if (detail != lastDetail &&
                (state == WsConnectionState.Connected || state == WsConnectionState.Failed)
            ) {
                lastDetail = detail
                ChatStore.addSystem(detail)
            }
        }
    }

    // Frames are folded into ChatStore process-wide (see CompanionApp), so this
    // screen deliberately does not collect them.

    LaunchedEffect(messages.size) {
        if (messages.isNotEmpty()) {
            listState.animateScrollToItem(messages.lastIndex)
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Column {
                        Text("Victoria Link")
                        Text(
                            text = "$connLabel · ${config.wsUrl}",
                            style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                },
                actions = {
                    IconButton(onClick = onOpenSettings) {
                        Icon(Icons.Default.Settings, contentDescription = "Settings")
                    }
                }
            )
        }
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .imePadding()
        ) {
            // Mood word + doing-line only — no Link redesign.
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 16.dp, vertical = 6.dp)
            ) {
                Text(
                    text = "Mood — ${presence.moodWord}",
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Text(
                    text = "Doing — ${presence.doingLine}",
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            LazyColumn(
                state = listState,
                modifier = Modifier
                    .weight(1f)
                    .fillMaxWidth(),
                contentPadding = PaddingValues(16.dp),
                verticalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                items(messages, key = { it.id }) { msg ->
                    MessageBubble(
                        message = msg,
                        onQuote = { full ->
                            quoteEditFor = msg
                            quoteEditText = full
                        }
                    )
                }
            }

            pendingQuote?.let { quote ->
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 12.dp)
                        .background(
                            MaterialTheme.colorScheme.surfaceVariant,
                            RoundedCornerShape(10.dp)
                        )
                        .padding(horizontal = 10.dp, vertical = 8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            "Replying to excerpt",
                            style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.primary
                        )
                        Text(
                            quote.replace('\n', ' ').take(140),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            maxLines = 2
                        )
                    }
                    IconButton(onClick = { pendingQuote = null }) {
                        Icon(Icons.Default.Close, contentDescription = "Clear quote")
                    }
                }
            }

            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 12.dp, vertical = 8.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                OutlinedTextField(
                    value = draft,
                    onValueChange = { draft = it },
                    modifier = Modifier.weight(1f),
                    placeholder = { Text("Message…") },
                    maxLines = 4
                )
                IconButton(
                    onClick = {
                        val text = draft.trim()
                        if (text.isEmpty()) return@IconButton
                        val quoted = pendingQuote
                        val display = if (quoted.isNullOrBlank()) {
                            text
                        } else {
                            "↪ ${quoted.replace('\n', ' ').take(120)}\n$text"
                        }
                        // Send first so the turn can carry the frame id the Host will file
                        // it under; the bubble is still added either way, so a failed send
                        // does not silently swallow what the operator typed.
                        val result = CompanionConnection.client.sendChat(text, quotedText = quoted)
                        ChatStore.addUser(display, result.getOrNull())
                        draft = ""
                        pendingQuote = null
                        result.exceptionOrNull()?.message?.let { err ->
                            ChatStore.addSystem(err)
                        }
                    }
                ) {
                    Icon(
                        Icons.AutoMirrored.Filled.Send,
                        contentDescription = "Send",
                        tint = MaterialTheme.colorScheme.primary
                    )
                }
            }
        }
    }

    quoteEditFor?.let {
        AlertDialog(
            onDismissRequest = { quoteEditFor = null },
            title = { Text("Quote excerpt") },
            text = {
                Column {
                    Text(
                        "Trim to the part you want Victoria to see as context.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    SpacerHeight()
                    OutlinedTextField(
                        value = quoteEditText,
                        onValueChange = { quoteEditText = it },
                        modifier = Modifier.fillMaxWidth(),
                        minLines = 4,
                        maxLines = 10
                    )
                }
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        val excerpt = quoteEditText.trim()
                        if (excerpt.isNotEmpty()) pendingQuote = excerpt.take(2000)
                        quoteEditFor = null
                    }
                ) { Text("Use quote") }
            },
            dismissButton = {
                TextButton(onClick = { quoteEditFor = null }) { Text("Cancel") }
            }
        )
    }
}

@Composable
private fun SpacerHeight() {
    androidx.compose.foundation.layout.Spacer(modifier = Modifier.height(8.dp))
}

@Composable
private fun MessageBubble(
    message: ChatMessage,
    onQuote: (String) -> Unit
) {
    val isUser = message.role == MessageRole.USER
    val bg = when (message.role) {
        MessageRole.USER -> MaterialTheme.colorScheme.primary
        MessageRole.ASSISTANT -> MaterialTheme.colorScheme.surfaceVariant
        MessageRole.SYSTEM -> MaterialTheme.colorScheme.secondaryContainer
        MessageRole.ERROR -> MaterialTheme.colorScheme.errorContainer
    }
    val fg = when (message.role) {
        MessageRole.USER -> MaterialTheme.colorScheme.onPrimary
        MessageRole.ASSISTANT -> MaterialTheme.colorScheme.onSurfaceVariant
        MessageRole.SYSTEM -> MaterialTheme.colorScheme.onSecondaryContainer
        MessageRole.ERROR -> MaterialTheme.colorScheme.onErrorContainer
    }
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = if (isUser) Arrangement.End else Arrangement.Start
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth(0.85f)
                .background(bg, RoundedCornerShape(16.dp))
                .padding(horizontal = 14.dp, vertical = 10.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            if (message.content.isNotBlank()) {
                SelectionContainer {
                    Text(text = message.content, color = fg, style = MaterialTheme.typography.bodyMedium)
                }
            }
            message.localImagePath?.let { path ->
                val bmp = remember(path) { BitmapFactory.decodeFile(path) }
                if (bmp != null) {
                    Image(
                        bitmap = bmp.asImageBitmap(),
                        contentDescription = "Image from Victoria",
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(200.dp),
                        contentScale = ContentScale.Fit
                    )
                }
            }
            if (message.proactive && message.role == MessageRole.ASSISTANT) {
                Text(
                    "reached out",
                    color = fg.copy(alpha = 0.7f),
                    style = MaterialTheme.typography.labelSmall
                )
            }
            if (message.content.isNotBlank() &&
                (message.role == MessageRole.ASSISTANT || message.role == MessageRole.USER)
            ) {
                TextButton(
                    onClick = { onQuote(message.content) },
                    contentPadding = PaddingValues(0.dp)
                ) {
                    Text("Quote", color = fg.copy(alpha = 0.85f))
                }
            }
        }
    }
}
