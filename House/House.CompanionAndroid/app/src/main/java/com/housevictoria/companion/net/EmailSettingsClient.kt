package com.housevictoria.companion.net

import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONArray
import org.json.JSONObject
import java.util.concurrent.TimeUnit

data class EmailAccountDto(
    val id: String,
    val role: String = "",
    val displayName: String = "",
    val address: String = "",
    val imapHost: String = "imap.gmail.com",
    val imapPort: Int = 993,
    val imapUseSsl: Boolean = true,
    val smtpHost: String = "smtp.gmail.com",
    val smtpPort: Int = 587,
    val smtpUseSsl: Boolean = false,
    val username: String = "",
    val enabled: Boolean = true,
    val hasPassword: Boolean = false,
    val isConfigured: Boolean = false
)

data class EmailSettingsResult(
    val accounts: List<EmailAccountDto> = emptyList(),
    val note: String? = null,
    val ok: Boolean,
    val detail: String? = null
)

/**
 * Host `/settings/email` — IMAP/SMTP credentials for Presence Settings.
 * Passwords are write-only (never returned by GET).
 */
object EmailSettingsClient {
    private val http = OkHttpClient.Builder()
        .connectTimeout(8, TimeUnit.SECONDS)
        .readTimeout(12, TimeUnit.SECONDS)
        .build()

    private val jsonMedia = "application/json; charset=utf-8".toMediaType()

    fun list(httpBase: String, token: String): EmailSettingsResult = runCatching {
        val url = "${httpBase.trimEnd('/')}/settings/email"
        val builder = Request.Builder().url(url).get()
        CompanionAuthHeaders.applyBearer(builder, token)
        val response = http.newCall(builder.build()).execute()
        response.use { resp ->
            val body = resp.body?.string().orEmpty()
            if (!resp.isSuccessful) {
                return EmailSettingsResult(
                    ok = false,
                    detail = "HTTP ${resp.code}" + if (body.isBlank()) "" else ": ${body.take(160)}"
                )
            }
            val root = JSONObject(body)
            val accounts = parseAccounts(root.optJSONArray("accounts"))
            val note = if (root.has("note") && !root.isNull("note")) root.optString("note") else null
            EmailSettingsResult(
                accounts = accounts,
                note = note,
                ok = true
            )
        }
    }.getOrElse { ex ->
        EmailSettingsResult(ok = false, detail = ex.message ?: "email settings failed")
    }

    fun upsert(
        httpBase: String,
        token: String,
        account: EmailAccountDto,
        password: String?
    ): EmailSettingsResult = runCatching {
        val url = "${httpBase.trimEnd('/')}/settings/email"
        val payload = JSONObject()
            .put("id", account.id)
            .put("role", account.role.ifBlank { account.id })
            .put("displayName", account.displayName)
            .put("address", account.address)
            .put("imapHost", account.imapHost)
            .put("imapPort", account.imapPort)
            .put("imapUseSsl", account.imapUseSsl)
            .put("smtpHost", account.smtpHost)
            .put("smtpPort", account.smtpPort)
            .put("smtpUseSsl", account.smtpUseSsl)
            .put("username", account.username)
            .put("enabled", account.enabled)
        if (!password.isNullOrBlank()) {
            payload.put("password", password.trim())
        }
        val builder = Request.Builder()
            .url(url)
            .post(payload.toString().toRequestBody(jsonMedia))
        CompanionAuthHeaders.applyBearer(builder, token)
        val response = http.newCall(builder.build()).execute()
        response.use { resp ->
            val body = resp.body?.string().orEmpty()
            if (!resp.isSuccessful) {
                return EmailSettingsResult(
                    ok = false,
                    detail = "HTTP ${resp.code}" + if (body.isBlank()) "" else ": ${body.take(160)}"
                )
            }
            // Re-list so editor stays consistent.
            list(httpBase, token).copy(detail = "Saved")
        }
    }.getOrElse { ex ->
        EmailSettingsResult(ok = false, detail = ex.message ?: "email save failed")
    }

    private fun parseAccounts(arr: JSONArray?): List<EmailAccountDto> {
        if (arr == null) return emptyList()
        val out = ArrayList<EmailAccountDto>(arr.length())
        for (i in 0 until arr.length()) {
            val o = arr.optJSONObject(i) ?: continue
            out.add(
                EmailAccountDto(
                    id = o.optString("id"),
                    role = o.optString("role"),
                    displayName = o.optString("displayName"),
                    address = o.optString("address"),
                    imapHost = o.optString("imapHost", "imap.gmail.com"),
                    imapPort = o.optInt("imapPort", 993),
                    imapUseSsl = o.optBoolean("imapUseSsl", true),
                    smtpHost = o.optString("smtpHost", "smtp.gmail.com"),
                    smtpPort = o.optInt("smtpPort", 587),
                    smtpUseSsl = o.optBoolean("smtpUseSsl", false),
                    username = o.optString("username"),
                    enabled = o.optBoolean("enabled", true),
                    hasPassword = o.optBoolean("hasPassword", false),
                    isConfigured = o.optBoolean("isConfigured", false)
                )
            )
        }
        return out
    }
}
