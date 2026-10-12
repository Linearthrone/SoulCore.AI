package com.housevictoria.companion.net

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class EmailMailboxLabelsTest {

    @Test
    fun `buildList returns Host accounts only — no invented three slots`() {
        assertTrue(EmailMailboxLabels.buildList(emptyList()).isEmpty())

        val host = listOf(
            EmailAccountDto(id = "ops", address = "ops@example.com"),
            EmailAccountDto(id = "", address = "skip-blank"),
            EmailAccountDto(id = "victoria", address = "v@example.com")
        )
        val list = EmailMailboxLabels.buildList(host)
        assertEquals(listOf("ops", "victoria"), list.map { it.id })
    }

    @Test
    fun `safe mailbox ids match desk rules`() {
        assertTrue(EmailMailboxLabels.isSafeMailboxId("victoria"))
        assertTrue(EmailMailboxLabels.isSafeMailboxId("my_box-2"))
        assertFalse(EmailMailboxLabels.isSafeMailboxId(""))
        assertFalse(EmailMailboxLabels.isSafeMailboxId("bad id"))
        assertFalse(EmailMailboxLabels.isSafeMailboxId("a@b"))
        assertFalse(EmailMailboxLabels.isSafeMailboxId("a".repeat(65)))
    }

    @Test
    fun `friendly titles keep LinearThrone naming`() {
        assertEquals("Victoria's mailbox", EmailMailboxLabels.titleFor("victoria"))
        assertEquals("LinearThrone's personal mail", EmailMailboxLabels.titleFor("personal"))
        assertEquals("ops-night", EmailMailboxLabels.titleFor("ops-night"))
        assertEquals("LinearThrone", EmailMailboxLabels.defaultDisplayNameFor("business"))
    }
}
