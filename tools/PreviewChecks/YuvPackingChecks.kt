import com.wilkenstech.movilcamara.camera.YuvPacking
import java.nio.ByteBuffer

fun main() {
    val y = ByteBuffer.wrap(byteArrayOf(90,0,1,2,3,88,88,4,5,6,7)).apply { position(1) }
    val u = ByteBuffer.wrap(byteArrayOf(90,10,99,11)).apply { position(1) }
    val v = ByteBuffer.wrap(byteArrayOf(90,20,99,21)).apply { position(1) }
    val packed = ByteArray(12)
    val scratch = ByteArray(8)
    YuvPacking.copyPlane(y, 6, 1, 4, 2, packed, 0, 4, 1, scratch)
    YuvPacking.copyPlane(u, 5, 2, 2, 1, packed, 9, 4, 2, scratch)
    YuvPacking.copyPlane(v, 5, 2, 2, 1, packed, 8, 4, 2, scratch)
    check(packed.contentEquals(byteArrayOf(0,1,2,3,4,5,6,7,20,10,21,11))) { "Padded/interleaved NV21 changed" }
    check(y.position() == 1 && u.position() == 1 && v.position() == 1) { "Camera buffer position modified" }
    val contiguous = ByteArray(8)
    YuvPacking.copyPlane(ByteBuffer.wrap(byteArrayOf(0,1,2,3,4,5,6,7)),4,1,4,2,contiguous,0,4,1,scratch)
    check(contiguous.contentEquals(byteArrayOf(0,1,2,3,4,5,6,7)))
    println("PASS: bulk YUV copy preserves padded rows, interleaved chroma, truncated final padding, NV21 order and original buffer positions.")
}
