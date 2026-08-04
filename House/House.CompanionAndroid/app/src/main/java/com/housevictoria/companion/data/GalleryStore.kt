package com.housevictoria.companion.data

import android.content.Context
import java.io.File
import java.io.FileOutputStream
import java.util.UUID

data class GalleryItem(
    val id: String,
    val fileName: String,
    val localPath: String,
    val createdAt: Long,
    val prompt: String? = null
)

/** Local gallery cache for ComfyUI / companion media downloads. */
object GalleryStore {
    fun galleryDir(context: Context): File {
        val dir = File(context.filesDir, "gallery")
        if (!dir.exists()) dir.mkdirs()
        return dir
    }

    fun list(context: Context): List<GalleryItem> {
        return galleryDir(context).listFiles()
            ?.filter { it.isFile && it.extension.lowercase() in setOf("png", "jpg", "jpeg", "webp") }
            ?.sortedByDescending { it.lastModified() }
            ?.map { file ->
                GalleryItem(
                    id = file.nameWithoutExtension,
                    fileName = file.name,
                    localPath = file.absolutePath,
                    createdAt = file.lastModified()
                )
            }
            .orEmpty()
    }

    fun saveBytes(context: Context, bytes: ByteArray, mediaId: String? = null, prompt: String? = null): GalleryItem {
        val id = mediaId?.takeIf { it.isNotBlank() } ?: UUID.randomUUID().toString().replace("-", "")
        val fileName = "$id.png"
        val file = File(galleryDir(context), fileName)
        FileOutputStream(file).use { it.write(bytes) }
        return GalleryItem(
            id = id,
            fileName = fileName,
            localPath = file.absolutePath,
            createdAt = file.lastModified(),
            prompt = prompt
        )
    }

    fun findById(context: Context, id: String): GalleryItem? =
        list(context).firstOrNull { it.id.equals(id, ignoreCase = true) }
}
