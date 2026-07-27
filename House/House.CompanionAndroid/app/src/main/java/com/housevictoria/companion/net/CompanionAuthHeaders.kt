package com.housevictoria.companion.net

import okhttp3.Request

/**
 * Companion auth headers for SoulCore.Host `/ws` upgrades (BED-155 / SEC-152).
 *
 * Host accepts either:
 * - Preferred: `Authorization: Bearer <token>`
 * - Alias: `X-Api-Key: <token>`
 *
 * When `SOULCORE_COMPANION_API_TOKEN` is unset on the Host, headers are optional
 * (loopback desktop trust). When set, one of these must match.
 *
 * Never log the raw token — use [redactForLog].
 */
object CompanionAuthHeaders {
    const val AUTHORIZATION = "Authorization"
    const val API_KEY = "X-Api-Key"
    const val BEARER_PREFIX = "Bearer "

    /**
     * Attach the preferred Bearer header when [token] is non-blank.
     * Does not send `X-Api-Key` as well (Host needs only one).
     */
    fun applyBearer(builder: Request.Builder, token: String): Request.Builder {
        val trimmed = token.trim()
        if (trimmed.isNotEmpty()) {
            builder.header(AUTHORIZATION, "$BEARER_PREFIX$trimmed")
        }
        return builder
    }

    /**
     * Attach the `X-Api-Key` alias instead of Bearer (parity / diagnostics).
     * Prefer [applyBearer] for production clients.
     */
    fun applyApiKeyAlias(builder: Request.Builder, token: String): Request.Builder {
        val trimmed = token.trim()
        if (trimmed.isNotEmpty()) {
            builder.header(API_KEY, trimmed)
        }
        return builder
    }

    /** Safe for logs / UI status — never includes secret material. */
    fun redactForLog(token: String): String {
        val trimmed = token.trim()
        return if (trimmed.isEmpty()) "(no token)" else "(token present, len=${trimmed.length})"
    }
}
