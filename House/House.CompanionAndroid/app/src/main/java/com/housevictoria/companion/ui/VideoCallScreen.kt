package com.housevictoria.companion.ui

import android.graphics.BitmapFactory
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CallEnd
import androidx.compose.material.icons.filled.Videocam
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.housevictoria.companion.data.CompanionPrefs
import com.housevictoria.companion.net.CompanionCallClient
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/**
 * Victoria Link video call — phone-call framing of her waist-up avatar feed.
 * MVP polls Host <c>/api/companion/v1/call/frame</c> (Unreal call_capture).
 * WebRTC duplex lands when Host webrtc.available flips true.
 */
@Composable
fun VideoCallScreen() {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    var sessionId by remember { mutableStateOf<String?>(null) }
    var status by remember { mutableStateOf("Ready to call Victoria") }
    var inCall by remember { mutableStateOf(false) }
    var frameBytes by remember { mutableStateOf<ByteArray?>(null) }
    var errorHint by remember { mutableStateOf<String?>(null) }
    var useEyeFallback by remember { mutableStateOf(false) }

    fun endCall() {
        val id = sessionId
        inCall = false
        sessionId = null
        frameBytes = null
        status = "Call ended"
        if (id != null) {
            scope.launch(Dispatchers.IO) {
                val cfg = CompanionPrefs.load(context)
                CompanionCallClient.endSession(cfg.resolvedHttpBase(), cfg.token, id)
            }
        }
    }

    fun startCall() {
        scope.launch {
            status = "Calling…"
            errorHint = null
            val cfg = CompanionPrefs.load(context)
            val started = withContext(Dispatchers.IO) {
                CompanionCallClient.startSession(cfg.resolvedHttpBase(), cfg.token)
            }
            started.onSuccess { info ->
                sessionId = info.sessionId
                inCall = true
                status = if (info.webrtcAvailable) {
                    "Connected (WebRTC)"
                } else {
                    "Connected · waist-up frames"
                }
            }.onFailure { e ->
                status = "Could not start call"
                errorHint = e.message
                inCall = false
            }
        }
    }

    LaunchedEffect(inCall, sessionId, useEyeFallback) {
        if (!inCall || sessionId == null) return@LaunchedEffect
        val id = sessionId!!
        while (isActive && inCall) {
            val cfg = CompanionPrefs.load(context)
            val result = withContext(Dispatchers.IO) {
                CompanionCallClient.fetchFrame(
                    cfg.resolvedHttpBase(),
                    cfg.token,
                    id,
                    fallbackEyes = useEyeFallback
                )
            }
            result.onSuccess { bytes ->
                frameBytes = bytes
                errorHint = null
            }.onFailure { e ->
                if (e.message == "no_frame") {
                    errorHint =
                        "Waiting for Victoria’s call camera (REX TASK-192). Toggle eye fallback to test Host path."
                } else {
                    errorHint = e.message
                }
            }
            delay(750)
        }
    }

    DisposableEffect(Unit) {
        onDispose {
            if (inCall) endCall()
        }
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color(0xFF0B0A0F))
    ) {
        val bmp = remember(frameBytes) { frameBytes?.let { BitmapFactory.decodeByteArray(it, 0, it.size) } }
        if (bmp != null) {
            Image(
                bitmap = bmp.asImageBitmap(),
                contentDescription = "Victoria on video call",
                modifier = Modifier.fillMaxSize(),
                contentScale = ContentScale.Crop
            )
        } else {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(24.dp),
                verticalArrangement = Arrangement.Center,
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Icon(
                    Icons.Default.Videocam,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(64.dp)
                )
                Text(
                    text = if (inCall) "Connecting to Victoria…" else "Video call",
                    style = MaterialTheme.typography.headlineSmall,
                    color = MaterialTheme.colorScheme.onBackground,
                    textAlign = TextAlign.Center,
                    modifier = Modifier.padding(top = 16.dp)
                )
                Text(
                    text = "She’ll appear waist-up — like holding a phone to talk.",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onBackground.copy(alpha = 0.7f),
                    textAlign = TextAlign.Center,
                    modifier = Modifier.padding(top = 8.dp)
                )
            }
        }

        // Local PiP placeholder (your camera — WebRTC later)
        Box(
            modifier = Modifier
                .align(Alignment.TopEnd)
                .padding(16.dp)
                .width(112.dp)
                .height(160.dp)
                .clip(RoundedCornerShape(12.dp))
                .background(Color(0xFF2A2433)),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = "You",
                color = MaterialTheme.colorScheme.onBackground.copy(alpha = 0.6f),
                style = MaterialTheme.typography.labelLarge
            )
        }

        Column(
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .fillMaxWidth()
                .background(Color(0xCC0B0A0F))
                .padding(20.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text(
                text = status,
                color = MaterialTheme.colorScheme.onBackground,
                style = MaterialTheme.typography.titleMedium
            )
            if (errorHint != null) {
                Text(
                    text = errorHint!!,
                    color = MaterialTheme.colorScheme.error,
                    style = MaterialTheme.typography.bodySmall,
                    textAlign = TextAlign.Center,
                    modifier = Modifier.padding(top = 6.dp)
                )
            }
            Row(
                modifier = Modifier.padding(top = 16.dp),
                horizontalArrangement = Arrangement.spacedBy(16.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                if (!inCall) {
                    Button(onClick = { startCall() }) {
                        Icon(Icons.Default.Videocam, contentDescription = null)
                        Text("  Call Victoria", modifier = Modifier.padding(start = 4.dp))
                    }
                } else {
                    FloatingActionButton(
                        onClick = { endCall() },
                        containerColor = Color(0xFFB3261E),
                        contentColor = Color.White,
                        shape = CircleShape
                    ) {
                        Icon(Icons.Default.CallEnd, contentDescription = "End call")
                    }
                    TextButton(onClick = { useEyeFallback = !useEyeFallback }) {
                        Text(
                            if (useEyeFallback) "Eye fallback ON" else "Eye fallback",
                            color = MaterialTheme.colorScheme.primary
                        )
                    }
                }
            }
            Text(
                text = "MVP: polled call frames · WebRTC duplex next",
                color = MaterialTheme.colorScheme.onBackground.copy(alpha = 0.45f),
                style = MaterialTheme.typography.labelSmall,
                modifier = Modifier.padding(top = 12.dp)
            )
        }
    }
}
