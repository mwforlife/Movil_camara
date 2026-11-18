package com.wilkenstech.movilcamara.camera

import android.content.Context
import android.util.AttributeSet
import android.view.View
import android.view.ViewGroup
import kotlin.math.max
import kotlin.math.min

/** Three XML-defined sections: header, viewfinder, controls.
 * Moves controls beside the viewfinder in wide windows without reinflating the camera surface.
 */
class CameraScreenLayout @JvmOverloads constructor(
    context: Context, attrs: AttributeSet? = null
) : ViewGroup(context, attrs) {
    private val gap = (8 * resources.displayMetrics.density).toInt()
    private var sidebar = 0
    private var wide = false

    override fun onMeasure(widthMeasureSpec: Int, heightMeasureSpec: Int) {
        val width = MeasureSpec.getSize(widthMeasureSpec)
        val height = MeasureSpec.getSize(heightMeasureSpec)
        setMeasuredDimension(width, height)
        if (childCount != 3) return
        val availableWidth = max(0, width - paddingLeft - paddingRight)
        val availableHeight = max(0, height - paddingTop - paddingBottom)
        wide = availableWidth > availableHeight
        val header = getChildAt(0)
        val viewfinder = getChildAt(1)
        val controls = getChildAt(2)
        if (wide) {
            sidebar = min((200 * resources.displayMetrics.density).toInt(), availableWidth / 3)
            val sideSpec = MeasureSpec.makeMeasureSpec(sidebar, MeasureSpec.EXACTLY)
            val contentSpec = MeasureSpec.makeMeasureSpec(availableHeight, MeasureSpec.AT_MOST)
            header.measure(sideSpec, contentSpec)
            controls.measure(sideSpec, MeasureSpec.makeMeasureSpec(max(0, availableHeight - header.measuredHeight - gap), MeasureSpec.AT_MOST))
            viewfinder.measure(
                MeasureSpec.makeMeasureSpec(max(0, availableWidth - sidebar - gap), MeasureSpec.EXACTLY),
                MeasureSpec.makeMeasureSpec(availableHeight, MeasureSpec.EXACTLY)
            )
        } else {
            val fullWidth = MeasureSpec.makeMeasureSpec(availableWidth, MeasureSpec.EXACTLY)
            val wrapHeight = MeasureSpec.makeMeasureSpec(availableHeight, MeasureSpec.AT_MOST)
            header.measure(fullWidth, wrapHeight)
            controls.measure(fullWidth, MeasureSpec.makeMeasureSpec((availableHeight * .52).toInt(), MeasureSpec.AT_MOST))
            viewfinder.measure(fullWidth, MeasureSpec.makeMeasureSpec(
                max(0, availableHeight - header.measuredHeight - controls.measuredHeight - 2 * gap),
                MeasureSpec.EXACTLY
            ))
        }
    }
    override fun onLayout(changed: Boolean, left: Int, top: Int, right: Int, bottom: Int) {
        if (childCount != 3) return
        val header = getChildAt(0)
        val viewfinder = getChildAt(1)
        val controls = getChildAt(2)
        if (wide) {
            place(viewfinder, paddingLeft, paddingTop)
            val sideLeft = width - paddingRight - sidebar
            place(header, sideLeft, paddingTop)
            place(controls, sideLeft, max(paddingTop + header.measuredHeight + gap, height - paddingBottom - controls.measuredHeight))
        } else {
            place(header, paddingLeft, paddingTop)
            place(viewfinder, paddingLeft, paddingTop + header.measuredHeight + gap)
            place(controls, paddingLeft, paddingTop + header.measuredHeight + gap + viewfinder.measuredHeight + gap)
        }
    }
    private fun place(view: View, x: Int, y: Int) = view.layout(x, y, x + view.measuredWidth, y + view.measuredHeight)
    override fun generateDefaultLayoutParams() = LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.WRAP_CONTENT)
    override fun generateLayoutParams(attrs: AttributeSet) = LayoutParams(context, attrs)
}
