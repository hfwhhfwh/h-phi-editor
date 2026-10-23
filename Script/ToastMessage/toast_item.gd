@tool
class_name ToastItem
extends PanelContainer

@onready var _label: Label = %TextLabel3yb7r
@onready var _color_rect_icon: PanelContainer = %PanelContainer8dnfj
@onready var _color_rect_text: PanelContainer = %PanelContainer2ml0s
@onready var icon_node: TextureRect = %Icon8sn3k

var _text: String = ""
var _color_icon: Color = Color("6eff9e")
var _color_text: Color = Color("45e66a")
var _icon: Texture

@export var text: String:
	get:
		return _text
	set(value):
		_text = value
		# 确保节点已初始化，防止在 _ready 之前赋值报错
		if is_instance_valid(_label):
			_label.text = _text

@export var color_icon: Color = Color("6eff9e"):
	get:
		return _color_icon
	set(value):
		_color_icon = value
		if is_instance_valid(_color_rect_icon):
			set_stylebox_color(_color_rect_icon, _color_icon)

@export var color_text: Color = Color("45e66a"):
	get:
		return _color_text
	set(value):
		_color_text = value
		if is_instance_valid(_color_rect_text):
			set_stylebox_color(_color_rect_text, _color_text)

@export var icon: Texture:
	get:
		return _icon
	set(value):
		_icon = value
		if is_instance_valid(icon_node):
			icon_node.texture = value
		

func _ready() -> void:
	# 使用 % 获取场景唯一节点，确保即使节点改名或移动层级也能找到
	# _label = get_node_or_null("%TextLabel3yb7r")
	
	# 同步一次初始文本
	if is_instance_valid(_label):
		_label.text = _text
		set_stylebox_color(_color_rect_icon, _color_icon)
		set_stylebox_color(_color_rect_text, _color_text)
	# 同步图标
	if is_instance_valid(icon_node) and _icon != null:
		icon_node.texture = _icon
		

func set_stylebox_color(panel_container: PanelContainer, new_color: Color):
	var style = panel_container.get_theme_stylebox("panel").duplicate()
	if style is StyleBoxFlat:
		style.bg_color = new_color
		panel_container.add_theme_stylebox_override("panel", style)
