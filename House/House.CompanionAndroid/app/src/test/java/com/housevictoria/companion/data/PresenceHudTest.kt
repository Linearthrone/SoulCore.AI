package com.housevictoria.companion.data

import com.housevictoria.companion.net.SoulCoreFrame
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

class PresenceHudTest {

    @Before
    fun reset() {
        PresenceHud.resetForTests()
    }

    @Test
    fun `emotion snapshot sets mood word from label`() {
        PresenceHud.applyFrame(
            SoulCoreFrame.create(
                SoulCoreFrame.EMOTION_SNAPSHOT,
                JSONObject().put("label", "warm-calm").put("valence", 0.4)
            )
        )
        val state = PresenceHud.state.value
        assertEquals("warm-calm", state.moodWord)
        assertTrue(state.moodFromHost)
        assertEquals("—", state.doingLine)
        assertFalse(state.doingFromHost)
    }

    @Test
    fun `presence status sets doing-line from activity`() {
        PresenceHud.applyFrame(
            SoulCoreFrame.create(
                SoulCoreFrame.PRESENCE_STATUS,
                JSONObject().put("activity", "Reading Kayleigh's note").put("alive", true)
            )
        )
        val state = PresenceHud.state.value
        assertEquals("Reading Kayleigh's note", state.doingLine)
        assertTrue(state.doingFromHost)
        assertEquals("—", state.moodWord)
    }

    @Test
    fun `loop want never restamps mood`() {
        PresenceHud.applyFrame(
            SoulCoreFrame.create(
                SoulCoreFrame.EMOTION_SNAPSHOT,
                JSONObject().put("label", "focused")
            )
        )
        PresenceHud.applyFrame(
            SoulCoreFrame.create(
                "loop.want",
                JSONObject().put("want", "want[chat]: ignore me").put("label", "spoof")
            )
        )
        assertEquals("focused", PresenceHud.state.value.moodWord)
    }

    @Test
    fun `blank activity does not clear doing-line`() {
        PresenceHud.applyFrame(
            SoulCoreFrame.create(
                SoulCoreFrame.PRESENCE_STATUS,
                JSONObject().put("activity", "On a call")
            )
        )
        PresenceHud.applyFrame(
            SoulCoreFrame.create(
                SoulCoreFrame.PRESENCE_STATUS,
                JSONObject().put("activity", "   ").put("alive", true)
            )
        )
        assertEquals("On a call", PresenceHud.state.value.doingLine)
    }
}
