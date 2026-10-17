package com.housevictoria.companion.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class HouseLampsTest {

    @Test
    fun `health flags light soulcore ollama unreal and cua`() {
        val lamps = HouseLamps.parse(
            """
            {
              "status": "degraded",
              "inference": { "enabled": false, "provider": "null" },
              "unreal": { "enabled": true, "connected": false },
              "tools": { "cuaDriverAvailable": true }
            }
            """.trimIndent()
        )
        assertEquals(LampLight.On, lamps!!.soulcore)
        assertEquals(LampLight.Off, lamps.ollama)
        assertEquals(LampLight.Off, lamps.unreal)
        assertEquals(LampLight.On, lamps.cua)
        assertEquals(LampLight.Unknown, lamps.comfy)
        assertEquals(LampLight.Unknown, lamps.sandbox)
    }

    @Test
    fun `missing flags stay unknown and a dead status is off`() {
        val lamps = HouseLamps.parse("""{"status":"down"}""")
        assertEquals(LampLight.Off, lamps!!.soulcore)
        assertEquals(LampLight.Unknown, lamps.ollama)
        assertEquals(LampLight.Unknown, lamps.unreal)
        assertEquals(LampLight.Unknown, lamps.cua)
    }

    @Test
    fun `comfy and sandbox json cannot light those wells`() {
        val lamps = HouseLamps.parse(
            """{"status":"ok","comfy":{"up":true},"sandbox":{"running":true}}"""
        )
        assertEquals(LampLight.On, lamps!!.soulcore)
        assertEquals(LampLight.Unknown, lamps.comfy)
        assertEquals(LampLight.Unknown, lamps.sandbox)
    }

    @Test
    fun `blank body is not a reading`() {
        assertNull(HouseLamps.parse("not-json"))
        assertNull(HouseLamps.parse("{}"))
    }
}
