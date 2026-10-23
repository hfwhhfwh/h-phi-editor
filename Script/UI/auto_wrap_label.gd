@tool
class_name AutoWrapLabel
extends Label

@export var max_width: float = 300.0

func _ready() -> void:
	# 监听尺寸变化
	item_rect_changed.connect(_on_layout_changed)
	# 初始时延迟执行一次布局计算
	call_deferred("update_label_layout")

# GDScript 中重写 _set，返回 false 表示让引擎继续处理原本的属性赋值
func _set(property: StringName, value: Variant) -> bool:
	if property == "text":
		call_deferred("update_label_layout")
	return false 

func _on_layout_changed() -> void:
	update_label_layout()

func update_label_layout() -> void:
	if not is_inside_tree() or text.is_empty():
		# 空文本时重置状态
		autowrap_mode = TextServer.AUTOWRAP_OFF
		custom_minimum_size = Vector2.ZERO
		return

	var font := get_theme_font("font")
	var font_size := get_theme_font_size("font_size")
	if font == null:
		return

	# 1. 关闭换行，测量文本在“理想状态”下的真实宽度
	autowrap_mode = TextServer.AUTOWRAP_OFF
	custom_minimum_size = Vector2.ZERO
	
	# 使用 Font 直接测量真实宽度
	var text_size := font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size)

	# 2. 根据真实宽度与上限的对比，决定最终状态
	if text_size.x > max_width:
		# 超长文本：开启智能换行，并锁定最大宽度
		autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		custom_minimum_size = Vector2(max_width, 0)
	else:
		# 短文本：保持不换行，让 Label 自然包裹内容
		autowrap_mode = TextServer.AUTOWRAP_OFF
		custom_minimum_size = Vector2.ZERO
