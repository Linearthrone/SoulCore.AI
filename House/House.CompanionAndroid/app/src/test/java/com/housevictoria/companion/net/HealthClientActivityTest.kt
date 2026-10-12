package com.housevictoria.companion.net

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class HealthClientActivityTest {

    @Test
    fun `parses currentActivity from health JSON`() {
        val phrase = HealthClient.parseCurrentActivity(
            """{"ok":true,"currentActivity":"Drafting reply","activitySource":"chat"}"""
        )
        assertEquals("Drafting reply", phrase)
    }

    @Test
    fun `absent or blank currentActivity is null`() {
        assertNull(HealthClient.parseCurrentActivity("""{"ok":true}"""))
        assertNull(HealthClient.parseCurrentActivity("""{"currentActivity":""}"""))
        assertNull(HealthClient.parseCurrentActivity("""{"currentActivity":null}"""))
        assertNull(HealthClient.parseCurrentActivity("not-json"))
    }
}
