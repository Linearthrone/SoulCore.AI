package com.housevictoria.companion.net

/**
 * PROP-17.5 / 17.4: friendly labels for Host mailbox ids.
 * No hardcoded victoria|personal|business slot list — Host owns the account set.
 */
object EmailMailboxLabels {
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

    /**
     * Lists every account Host returned. Empty when Host has none —
     * never invents victoria|personal|business placeholders.
     */
    fun buildList(accounts: List<EmailAccountDto>): List<EmailAccountDto> =
        accounts
            .filter { it.id.isNotBlank() }
            .sortedBy { it.id.lowercase() }

    private val SAFE_ID = Regex("^[A-Za-z0-9_-]+$")

    fun isSafeMailboxId(id: String): Boolean {
        val t = id.trim()
        return t.isNotEmpty() && SAFE_ID.matches(t)
    }
}
