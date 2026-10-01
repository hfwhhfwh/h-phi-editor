shader_type canvas_item;

// Godot 4 的 shader for 循环边界必须是编译期常量，所以用常量上限
const int MAX_SAMPLES = 64;

uniform float radius : hint_range(0.0, 1024.0) = 8.0;   // 模糊半径（像素）
uniform vec2 direction = vec2(1.0, 0.0);               // (1,0) 横向 / (0,1) 纵向

void fragment() {
    vec2 texel = direction / vec2(textureSize(TEXTURE, 0));
    vec4 sum = vec4(0.0);
    float weight_sum = 0.0;
    float sigma = max(radius * 0.5, 0.001);

    for (int i = -MAX_SAMPLES; i <= MAX_SAMPLES; i++) {
        // 关键改动：采样间距随 radius 放大，65 个采样点均匀覆盖 ±radius
        float offset = float(i) * (radius / float(MAX_SAMPLES));
        float w = exp(-0.5 * offset * offset / (sigma * sigma));
        sum += texture(TEXTURE, UV + texel * offset) * w;
        weight_sum += w;
    }
    COLOR = sum / weight_sum;
}