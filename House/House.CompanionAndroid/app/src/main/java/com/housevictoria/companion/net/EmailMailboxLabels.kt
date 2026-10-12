package com.housevictoria.companion.net

/**
 * Friendly labels for Host mailbox ids.
 * Host owns the account set — never invent victoria|personal|business slots.
 */
object EmailMailboxLabels {
    private val SAFE_ID = Regex("^[A-Za-z0-9_-]{1,64}$")

    fun titleFor(id: String): String {
        val key = id.trim()
        if (key.isEmpty()) return "Mailbox"
        return when (key.lowercase()) {
            "victoria" -> "Victoria's mailbox"
            "personal" -> "LinearThrone's personal mail"
            "business" -> "LinearThrone's business mail"
            else -> key
        }
    }

    fun defaultDisplayNameFor(id: String): String = when (id.trim().lowercase()) {
        "victoria" -> "Victoria"
        "personal", "business" -> "LinearThrone"
        else -> ""
    }

    fun subtitleFor(id: String, account: EmailAccountDto?): String {
        val whose = if (id.isBlank()) "Mailbox" else "Id · ${id.trim()}"
        if (account == null) return "$whose · not set up"
        val owner = when {
            account.displayName.isNotBlank() -> account.displayName
            account.address.isNotBlank() -> account.address
            else -> "owner unset"
        }
        return when {
            !account.enabled -> "$whose · $owner · turned off"
            account.isConfigured -> "$whose · $owner · ready"
            account.hasPassword && account.address.isNotBlank() -> "$whose · $owner · almost ready"
            else -> "$whose · needs setup"
        }
    }

    /** Host accounts only, sorted by id. */
    fun buildList(accounts: List<EmailAccountDto>): List<EmailAccountDto> =
        accounts
            .filter { it.id.isNotBlank() }
            .sortedBy { it.id.lowercase() }

    fun isSafeMailboxId(id: String): Boolean = SAFE_ID.matches(id.trim())
}
