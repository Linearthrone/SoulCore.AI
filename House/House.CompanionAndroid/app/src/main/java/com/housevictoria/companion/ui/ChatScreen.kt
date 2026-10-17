package com.housevictoria.companion.ui

import android.Manifest
import android.content.pm.PackageManager
import android.graphics.BitmapFactory
import android.os.Build
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Send
import androidx.compose.material.icons.filled.Close
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.ContextCompat
import com.housevictoria.companion.data.ChatMessage
import com.housevictoria.companion.data.ChatStore
import com.housevictoria.companion.data.CompanionPrefs
import com.housevictoria.companion.data.MessageRole
import com.housevictoria.companion.data.PresenceHud
import com.housevictoria.companion.net.CompanionConnection
import com.housevictoria.companion.net.WsConnectionState
import kotlinx.coroutines.flow.collectLatest
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter

@Composable
fun ChatScreen() {
    val context = LocalContext.current
    val config = remember { CompanionPrefs.load(context) }
    val messages by ChatStore.messages.collectAsState()
    var draft by remember { mutableStateOf("") }
    var pendingQuote by remember { mutableStateOf<String?>(null) }
    var quoteEditFor by remember { mutableStateOf<ChatMessage?>(null) }
    var quoteEditText by remember { mutableStateOf("") }
    val listState = rememberLazyListState()

    val connPair by CompanionConnection.state.collectAsState()
    val presence by PresenceHud.state.collectAsState()
    val online = connPair.first == WsConnectionState.Connected
    val connLabel = when (connPair.first) {
        WsConnectionState.Connected -> "online"
        WsConnectionState.Connecting -> "connecting"
        WsConnectionState.Failed -> "offline"
        WsConnectionState.Disconnected -> "offline"
    }
    val presenceLine = listOf(presence.moodWord, presence.doingLine)
        .map { it.trim() }
        .filter { it.isNotEmpty() && it != "—" }
        .joinToString(" · ")

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

    LaunchedEffect(messages.size) {
        if (messages.isNotEmpty()) {
            listState.animateScrollToItem(messages.lastIndex)
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(HousePalette.Ink)
            .imePadding()
    ) {
        IdentityStrip(online = online, connLabel = connLabel, presenceLine = presenceLine)
        LazyColumn(
            state = listState,
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth(),
            contentPadding = PaddingValues(horizontal = 16.dp, vertical = 12.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
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
                    .background(HousePalette.Glass, RoundedCornerShape(12.dp))
                    .border(1.dp, HousePalette.GlassEdge, RoundedCornerShape(12.dp))
                    .padding(horizontal = 10.dp, vertical = 8.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        "Replying to excerpt",
                        style = MaterialTheme.typography.labelSmall,
                        color = HousePalette.Accent
                    )
                    Text(
                        quote.replace('\n', ' ').take(140),
                        style = MaterialTheme.typography.bodySmall,
                        color = HousePalette.Muted,
                        maxLines = 2
                    )
                }
                IconButton(onClick = { pendingQuote = null }) {
                    Icon(Icons.Default.Close, contentDescription = "Clear quote", tint = HousePalette.Muted)
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
                    tint = HousePalette.Accent
                )
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
                    Spacer(modifier = Modifier.height(8.dp))
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
private fun IdentityStrip(online: Boolean, connLabel: String, presenceLine: String) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(HousePalette.Metal)
            .border(width = 1.dp, color = HousePalette.GlassEdge)
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier
                .size(36.dp)
                .clip(CircleShape)
                .background(HousePalette.Glass)
                .border(1.dp, HousePalette.GlassEdge, CircleShape),
            contentAlignment = Alignment.Center
        ) {
            Text("V", color = HousePalette.Ivory, fontWeight = FontWeight.SemiBold)
        }
        Column(modifier = Modifier.padding(start = 12.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    "Victoria",
                    color = HousePalette.Ivory,
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold
                )
                Spacer(modifier = Modifier.size(8.dp))
                Box(
                    modifier = Modifier
                        .size(7.dp)
                        .clip(CircleShape)
                        .background(if (online) HousePalette.LampOllama else HousePalette.LampOff)
                )
                Text(
                    text = connLabel,
                    color = HousePalette.Muted,
                    style = MaterialTheme.typography.labelSmall,
                    modifier = Modifier.padding(start = 6.dp)
                )
            }
            if (presenceLine.isNotEmpty()) {
                Text(
                    text = presenceLine,
                    color = HousePalette.Muted,
                    style = MaterialTheme.typography.labelMedium,
                    maxLines = 1
                )
            }
        }
    }
}

@Composable
private fun MessageBubble(
    message: ChatMessage,
    onQuote: (String) -> Unit
) {
    val isUser = message.role == MessageRole.USER
    if (message.role == MessageRole.SYSTEM || message.role == MessageRole.ERROR) {
        Text(
            text = message.content,
            color = if (message.role == MessageRole.ERROR) HousePalette.Danger else HousePalette.Muted,
            style = MaterialTheme.typography.labelSmall,
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 12.dp)
        )
        return
    }
    val bg = if (isUser) HousePalette.UserGlass else HousePalette.VictoriaGlass
    val shape = RoundedCornerShape(
        topStart = 16.dp,
        topEnd = 16.dp,
        bottomStart = if (isUser) 16.dp else 4.dp,
        bottomEnd = if (isUser) 4.dp else 16.dp
    )
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = if (isUser) Arrangement.End else Arrangement.Start
    ) {
        Column(
            modifier = Modifier.widthIn(max = 300.dp),
            horizontalAlignment = if (isUser) Alignment.End else Alignment.Start
        ) {
            Column(
                modifier = Modifier
                    .border(1.dp, HousePalette.GlassEdge, shape)
                    .background(bg, shape)
                    .padding(horizontal = 14.dp, vertical = 10.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                if (message.content.isNotBlank()) {
                    SelectionContainer {
                        Text(
                            text = message.content,
                            color = HousePalette.Ivory,
                            style = MaterialTheme.typography.bodyMedium
                        )
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
                                .height(180.dp)
                                .clip(RoundedCornerShape(8.dp)),
                            contentScale = ContentScale.Fit
                        )
                    }
                }
                if (message.proactive && message.role == MessageRole.ASSISTANT) {
                    Text(
                        "reached out",
                        color = HousePalette.Muted,
                        style = MaterialTheme.typography.labelSmall
                    )
                }
                if (message.content.isNotBlank()) {
                    TextButton(
                        onClick = { onQuote(message.content) },
                        contentPadding = PaddingValues(0.dp)
                    ) {
                        Text("Quote", color = HousePalette.Accent, fontSize = 12.sp)
                    }
                }
            }
            Text(
                text = formatClock(message.timestampMs),
                color = HousePalette.Muted,
                style = MaterialTheme.typography.labelSmall,
                modifier = Modifier.padding(top = 4.dp, start = 4.dp, end = 4.dp)
            )
        }
    }
}

private val clockFormat: DateTimeFormatter = DateTimeFormatter.ofPattern("h:mm a")

private fun formatClock(epochMs: Long): String {
    return Instant.ofEpochMilli(epochMs)
        .atZone(ZoneId.systemDefault())
        .toLocalTime()
        .format(clockFormat)
}
