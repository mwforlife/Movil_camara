import com.wilkenstech.movilcamara.camera.PreviewGeometry
import kotlin.math.abs
import kotlin.math.hypot

fun main() {
    var cases = 0
    for ((bw, bh) in listOf(1280 to 720, 640 to 480)) {
        for (sensor in listOf(0, 90, 180, 270)) {
            for (display in listOf(0, 90, 180, 270)) {
                for ((vw, vh) in listOf(360 to 720, 800 to 300, 900 to 220, 350 to 730, 600 to 600)) {
                    val geometry = PreviewGeometry.calculate(vw, vh, bw, bh, sensor, display)
                    val naturalWidth = if (sensor % 180 == 0) bw else bh
                    val naturalHeight = if (sensor % 180 == 0) bh else bw
                    val m = geometry.matrix
                    // Model TextureView's default stretching of its sensor-oriented buffer.
                    fun point(x: Float, y: Float): Pair<Float, Float> {
                        val defaultX = x / naturalWidth * vw
                        val defaultY = y / naturalHeight * vh
                        return (m[0] * defaultX + m[1] * defaultY + m[2]) to
                            (m[3] * defaultX + m[4] * defaultY + m[5])
                    }
                    val origin = point(0f, 0f)
                    val xPixel = point(1f, 0f)
                    val yPixel = point(0f, 1f)
                    val xScale = hypot(xPixel.first - origin.first, xPixel.second - origin.second)
                    val yScale = hypot(yPixel.first - origin.first, yPixel.second - origin.second)
                    check(abs(xScale - yScale) < 0.001f) { "Pixels stretched: sensor=$sensor display=$display view=$vw x $vh" }
                    val corners = listOf(point(0f, 0f), point(naturalWidth.toFloat(), 0f),
                        point(0f, naturalHeight.toFloat()), point(naturalWidth.toFloat(), naturalHeight.toFloat()))
                    val minX = corners.minOf { it.first }; val maxX = corners.maxOf { it.first }
                    val minY = corners.minOf { it.second }; val maxY = corners.maxOf { it.second }
                    check(minX >= -0.01 && maxX <= vw + 0.01 && minY >= -0.01 && maxY <= vh + 0.01) { "Frame cropped" }
                    check(abs((minX + maxX) / 2 - vw / 2f) < 0.01 && abs((minY + maxY) / 2 - vh / 2f) < 0.01) { "Frame not centered" }
                    check(abs((maxX - minX) / (maxY - minY) - geometry.imageWidth.toFloat() / geometry.imageHeight) < 0.001f) { "Wrong output aspect ratio" }
                    if (display == 0) check(abs(m[1]) < 0.001f && abs(m[3]) < 0.001f && m[0] > 0 && m[4] > 0) { "Sensor rotation applied twice" }
                    cases++
                }
            }
        }
    }
    val portrait = PreviewGeometry.calculate(360, 640, 1280, 720, 90, 0)
    val landscape = PreviewGeometry.calculate(640, 360, 1280, 720, 90, 90)
    check(portrait.imageWidth == 720 && portrait.imageHeight == 1280)
    check(landscape.imageWidth == 1280 && landscape.imageHeight == 720)
    val reversed = PreviewGeometry.calculate(360, 640, 1280, 720, 90, 180)
    check(reversed.matrix[0] < 0 && reversed.matrix[4] < 0) { "180-degree turn was ignored" }
    println("PASS: " + cases + " preview combinations preserve square pixels, complete frame, center and aspect ratio.")
    println("PASS: portrait 9:16, landscape 16:9, 180-degree rotation and no double sensor rotation.")
}
