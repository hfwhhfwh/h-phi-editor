shader_type canvas_item;

uniform float blur_size : hint_range(0.0, 10.0) = 5.0;  // 模糊半径（像素）
uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap; // 可选：用于屏幕纹理

void fragment() {
    vec2 uv = UV;
    vec4 color = vec4(0.0);
    float total = 0.0;
    
    // 简单的盒式模糊（可替换为更高效的高斯模糊）
    int size = int(blur_size);
    for (int x = -size; x <= size; x++) {
        for (int y = -size; y <= size; y++) {
            vec2 offset = vec2(float(x), float(y)) * TEXTURE_PIXEL_SIZE; // 使用纹理像素大小
            color += texture(TEXTURE, uv + offset);
            total += 1.0;
        }
    }
    COLOR = color / total;
	
	//vec3 result = textureLod(TEXTURE,UV,blur_size).rgb;
	//COLOR.rgb = result;
}