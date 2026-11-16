import com.wilkenstech.movilcamara.camera.ProfileRules
import com.wilkenstech.movilcamara.camera.FramePacer

fun main() {
    val pacing = FramePacer(24)
    val accepted = (0 until 300).count { pacing.accept(it * 1_000_000_000L / 30) }
    check(accepted in 239..241) { "24 FPS from 30 FPS source drifted: $accepted frames in 10 seconds" }
    val sixty = FramePacer(60)
    check((0 until 600).count { sixty.accept(it * 1_000_000_000L / 60) } == 600)
    check(ProfileRules.supportsRate(60, 16_666_666, listOf(30 to 60)))
    check(!ProfileRules.supportsRate(60, 33_333_333, listOf(30 to 60)))
    check(!ProfileRules.supportsRate(60, 0, listOf(30 to 60)))
    check(!ProfileRules.supportsRate(60, 16_666_666, listOf(15 to 30)))
    check(ProfileRules.supportsRate(30, 0, listOf(15 to 30)))
    check(!ProfileRules.supportsRate(30, 66_666_666, listOf(15 to 30)))
    println("PASS: 60 FPS requires both a matching AE range and sufficient known stream timing; slow/unknown combinations are excluded.")
}
