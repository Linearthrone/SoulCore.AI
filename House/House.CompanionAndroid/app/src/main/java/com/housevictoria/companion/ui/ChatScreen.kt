package com.housevictoria.companion.ui

import android.Manifest
import android.content.pm.PackageManager
import android.graphics.BitmapFactory
import android.os.Build
import android.os.Handler
import android.os.Looper
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
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Send
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
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
import com.housevictoria.companion.data.CompanionPrefs
import com.housevictoria.companion.data.GalleryStore
import com.housevictoria.companion.data.MessageRole
import com.housevictoria.companion.net.CompanionConnection
import com.housevictoria.companion.net.CompanionMediaClient
import com.housevictoria.companion.net.SoulCoreFrame
import com.housevictoria.companion.net.WsConnectionState
import kotlinx.coroutines.flow.collectLatest
import java.util.concurrent.atomic.AtomicReference

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ChatScreen(onOpenSettings: () -> Unit) {
    val context = LocalContext.current
    val config = remember { CompanionPrefs.load(context) }
    val messages = remember { mutableStateListOf<ChatMessage>() }
    val streamingAssistantId = remember { AtomicReference<String?>(null) }
    var draft by remember { mutableStateOf("") }
    val listState = rememberLazyListState()
    val mainHandler = remember { Handler(Looper.getMainLooper()) }

    val connPair by CompanionConnection.state.collectAsState()
    val connLabel = when (connPair.first) {
        WsConnectionState.Connected -> "Connected"
        WsConnectionState.Connecting -> "Connecting…"
        WsConnectionState.Failed -> "Host down"
        WsConnectionState.Disconnected -> "Disconnected"
    }

    fun onMain(block: () -> Unit) {
        if (Looper.myLooper() == Looper.getMainLooper()) block()
        else mainHandler.post(block)
    }

    fun appendSystem(text: String) {
        messages.add(ChatMessage(role = MessageRole.SYSTEM, content = text))
    }

    fun fetchMediaIntoMessage(messageId: String, mediaId: String) {
        Thread {
            val cfg = CompanionPrefs.load(context)
            val bytes = CompanionMediaClient.downloadMedia(cfg.resolvedHttpBase(), cfg.token, mediaId)
            bytes.onSuccess { png ->
                val item = GalleryStore.saveBytes(context, png, mediaId = mediaId)
                onMain {
                    val idx = messages.indexOfFirst { it.id == messageId }
                    if (idx >= 0) {
                        messages[idx] = messages[idx].copy(localImagePath = item.localPath)
                    }
                }
            }
        }.start()
    }

    fun appendOrUpdateAssistant(frame: SoulCoreFrame, finalize: Boolean) {
        val text = frame.payloadText()
        val mediaId = frame.payloadString("mediaId")
        val hasMedia = frame.payload?.optBoolean("hasMedia", false) == true || !mediaId.isNullOrBlank()
        val proactive = frame.payload?.optBoolean("proactive", false) == true
        if (text.isNullOrEmpty() && finalize && !hasMedia) {
            streamingAssistantId.set(null)
            return
        }
        val content = text.orEmpty()
        val streamId = streamingAssistantId.get()
        if (streamId != null) {
            val idx = messages.indexOfFirst { it.id == streamId }
            if (idx >= 0) {
                val existing = messages[idx]
                if (frame.id.isBlank() || existing.frameId == frame.id || existing.frameId.isNullOrBlank()) {
                    messages[idx] = existing.copy(
                        content = content.ifEmpty { existing.content },
                        frameId = frame.id.ifBlank { existing.frameId },
                        mediaId = mediaId ?: existing.mediaId,
                        proactive = proactive || existing.proactive
                    )
                    if (finalize) {
                        streamingAssistantId.set(null)
                        if (hasMedia && !mediaId.isNullOrBlank()) {
                            fetchMediaIntoMessage(messages[idx].id, mediaId)
                        }
                    }
                    return
                }
            }
        }
        val bubble = ChatMessage(
            role = MessageRole.ASSISTANT,
            content = content.ifEmpty { if (hasMedia) "(image)" else "" },
            frameId = frame.id.ifBlank { null },
            mediaId = mediaId,
            proactive = proactive
        )
        messages.add(bubble)
        streamingAssistantId.set(if (finalize) null else bubble.id)
        if (finalize && hasMedia && !mediaId.isNullOrBlank()) {
            fetchMediaIntoMessage(bubble.id, mediaId)
        }
    }

    fun applyFrame(frame: SoulCoreFrame) {
        when (frame.type) {
            SoulCoreFrame.CHAT_DELTA -> appendOrUpdateAssistant(frame, finalize = false)
            SoulCoreFrame.CHAT_DONE -> appendOrUpdateAssistant(frame, finalize = true)
            SoulCoreFrame.ERROR -> {
                streamingAssistantId.set(null)
                val code = frame.payloadString("code")
                val msg = frame.payloadString("message") ?: frame.payload?.toString().orEmpty()
                messages.add(
                    ChatMessage(
                        role = MessageRole.ERROR,
                        content = "error${code?.let { " [$it]" } ?: ""}: $msg"
                    )
                )
            }
            SoulCoreFrame.PRESENCE_STATUS,
            SoulCoreFrame.EMOTION_SNAPSHOT,
            SoulCoreFrame.PONG,
            "loop.want",
            "loop.tick.ok" -> Unit
            else -> appendSystem("frame ${frame.type} id=${frame.id}")
        }
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
                onMain { appendSystem(detail) }
            }
        }
    }

    LaunchedEffect(Unit) {
        CompanionConnection.frames.collectLatest { frame ->
            onMain { applyFrame(frame) }
        }
    }

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
            LazyColumn(
                state = listState,
                modifier = Modifier
                    .weight(1f)
                    .fillMaxWidth(),
                contentPadding = PaddingValues(16.dp),
                verticalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                items(messages, key = { it.id }) { msg ->
                    MessageBubble(msg)
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
                        streamingAssistantId.set(null)
                        messages.add(ChatMessage(role = MessageRole.USER, content = text))
                        draft = ""
                        val result = CompanionConnection.client.sendChat(text)
                        result.exceptionOrNull()?.message?.let { err ->
                            messages.add(ChatMessage(role = MessageRole.SYSTEM, content = err))
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
}

@Composable
private fun MessageBubble(message: ChatMessage) {
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
                Text(text = message.content, color = fg, style = MaterialTheme.typography.bodyMedium)
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
        }
    }
}
