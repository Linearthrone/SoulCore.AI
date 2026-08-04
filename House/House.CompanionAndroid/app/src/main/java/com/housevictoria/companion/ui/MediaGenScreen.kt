package com.housevictoria.companion.ui

import android.graphics.BitmapFactory
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Checkbox
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.housevictoria.companion.data.CompanionPrefs
import com.housevictoria.companion.data.GalleryStore
import com.housevictoria.companion.net.CompanionMediaClient
import com.housevictoria.companion.net.MediaModelInfo
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MediaGenScreen() {
    val context = LocalContext.current
    val config = remember { CompanionPrefs.load(context) }
    val scope = rememberCoroutineScope()
    var models by remember { mutableStateOf<List<MediaModelInfo>>(emptyList()) }
    var modelId by remember { mutableStateOf("") }
    var positive by remember { mutableStateOf("") }
    var negative by remember { mutableStateOf("low quality, blurry, watermark, text") }
    var pushToChat by remember { mutableStateOf(true) }
    var status by remember { mutableStateOf("Load models from SoulCore to ComfyUI.") }
    var busy by remember { mutableStateOf(false) }
    var previewPath by remember { mutableStateOf<String?>(null) }

    fun refreshModels() {
        scope.launch {
            busy = true
            status = "Fetching models…"
            val result = withContext(Dispatchers.IO) {
                CompanionMediaClient.listModels(config.resolvedHttpBase(), config.token)
            }
            busy = false
            result.fold(
                onSuccess = { list ->
                    models = list
                    if (modelId.isBlank()) modelId = list.firstOrNull()?.id.orEmpty()
                    status = if (list.isEmpty()) {
                        "No models returned (is ComfyUI up?)."
                    } else {
                        "Models: ${list.size}"
                    }
                },
                onFailure = { err ->
                    status = "Models failed: ${err.message}"
                }
            )
        }
    }

    LaunchedEffect(Unit) { refreshModels() }

    Scaffold(
        topBar = {
            TopAppBar(title = { Text("MediaGen") })
        }
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(16.dp)
                .verticalScroll(rememberScrollState()),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Text(
                "Generate via SoulCore to ComfyUI. Images save to Gallery; optional push into chat.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            OutlinedTextField(
                value = modelId,
                onValueChange = { modelId = it },
                label = { Text("Model / checkpoint id") },
                modifier = Modifier.fillMaxWidth(),
                supportingText = {
                    Text(
                        if (models.isEmpty()) "Refresh models when ComfyUI is online"
                        else models.joinToString { it.label }
                    )
                }
            )
            OutlinedTextField(
                value = positive,
                onValueChange = { positive = it },
                label = { Text("Positive prompt") },
                modifier = Modifier.fillMaxWidth(),
                minLines = 3
            )
            OutlinedTextField(
                value = negative,
                onValueChange = { negative = it },
                label = { Text("Negative prompt") },
                modifier = Modifier.fillMaxWidth(),
                minLines = 2
            )
            Row(verticalAlignment = Alignment.CenterVertically) {
                Checkbox(checked = pushToChat, onCheckedChange = { pushToChat = it })
                Text("Also push into Victoria chat")
            }
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Button(onClick = { refreshModels() }, enabled = !busy) { Text("Refresh models") }
                Button(
                    onClick = {
                        val prompt = positive.trim()
                        if (prompt.isEmpty()) {
                            status = "Positive prompt required."
                            return@Button
                        }
                        scope.launch {
                            busy = true
                            status = "Generating (ComfyUI may take a minute)…"
                            previewPath = null
                            val gen = withContext(Dispatchers.IO) {
                                CompanionMediaClient.generate(
                                    httpBase = config.resolvedHttpBase(),
                                    token = config.token,
                                    positivePrompt = prompt,
                                    negativePrompt = negative.trim().ifBlank { null },
                                    model = modelId.ifBlank { null },
                                    contactId = config.contactId,
                                    pushToChat = pushToChat
                                )
                            }
                            gen.fold(
                                onSuccess = { result ->
                                    val bytes = withContext(Dispatchers.IO) {
                                        CompanionMediaClient.downloadMedia(
                                            config.resolvedHttpBase(),
                                            config.token,
                                            result.mediaId
                                        )
                                    }
                                    bytes.fold(
                                        onSuccess = { png ->
                                            val item = GalleryStore.saveBytes(
                                                context,
                                                png,
                                                mediaId = result.mediaId,
                                                prompt = prompt
                                            )
                                            previewPath = item.localPath
                                            status = "Saved ${item.fileName} (${result.sizeBytes} bytes)" +
                                                if (pushToChat) " · pushed to chat" else ""
                                        },
                                        onFailure = { err ->
                                            status = "Generated but download failed: ${err.message}"
                                        }
                                    )
                                },
                                onFailure = { err ->
                                    status = "Generate failed: ${err.message}"
                                }
                            )
                            busy = false
                        }
                    },
                    enabled = !busy
                ) { Text(if (busy) "Working…" else "Generate") }
            }
            Text(status, style = MaterialTheme.typography.bodySmall)
            previewPath?.let { path ->
                val bmp = remember(path) { BitmapFactory.decodeFile(path) }
                if (bmp != null) {
                    Image(
                        bitmap = bmp.asImageBitmap(),
                        contentDescription = "Generated",
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(280.dp),
                        contentScale = ContentScale.Fit
                    )
                }
            }
        }
    }
}
