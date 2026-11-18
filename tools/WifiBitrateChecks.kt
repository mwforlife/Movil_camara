import com.wilkenstech.movilcamara.video.WifiBitrateController

fun main() {
    val policy = WifiBitrateController()
    check(policy.percent == 100)
    policy.observe(5, 30, true)
    check(policy.percent == 75) // Queue pressure matters even when a few writes are fast.
    policy.observe(40, 30, false)
    check(policy.percent == 56)
    repeat(20) { policy.congested() }
    check(policy.percent == 25)
    repeat(7) { policy.observe(5, 30, false) }
    check(policy.percent == 25)
    policy.observe(5, 30, false)
    check(policy.percent == 30)
    repeat(7) { policy.observe(5, 30, false) }
    policy.observe(25, 30, false) // An intermediate window cancels the recovery streak.
    policy.observe(5, 30, false)
    check(policy.percent == 30)
    repeat(200) { policy.observe(5, 30, false) }
    check(policy.percent == 100)
    check(WifiBitrateController().percent == 100) // A new capture session starts independently.
    println("PASS: congestion, slow writes, floor, recovery hysteresis, ceiling, independent sessions")
}
