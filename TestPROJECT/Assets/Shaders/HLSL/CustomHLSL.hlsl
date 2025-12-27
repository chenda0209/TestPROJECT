inline float RandomValue (float2 uv) {
    return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
}

// 2. 差值平滑函数
inline float NoiseInterpolate (float a, float b, float t) {
    return (1.0-t)*a + (t*b);
}

// 3. 基础 Value Noise 实现
inline float ValueNoise (float2 uv) {
    float2 i = floor(uv);
    float2 f = frac(uv);
    f = f * f * (3.0 - 2.0 * f);

    uv = abs(frac(uv) - 0.5);
    float2 c0 = i + float2(0.0, 0.0);
    float2 c1 = i + float2(1.0, 0.0);
    float2 c2 = i + float2(0.0, 1.0);
    float2 c3 = i + float2(1.0, 1.0);
    
    float r0 = RandomValue(c0);
    float r1 = RandomValue(c1);
    float r2 = RandomValue(c2);
    float r3 = RandomValue(c3);

    float bottomOfGrid = NoiseInterpolate(r0, r1, f.x);
    float topOfGrid = NoiseInterpolate(r2, r3, f.x);
    return NoiseInterpolate(bottomOfGrid, topOfGrid, f.y);
}

// 4. 最终调用的 Simple Noise (叠加了 3 层不同频率的噪声)
void SimpleNoise(float2 UV, float Scale, out float Out) {
    float t = 0.0;
    for(int i = 0; i < 3; i++) {
        float freq = pow(2.0, float(i));
        float amp = pow(0.5, float(3-i));
        t += ValueNoise(UV * Scale / freq) * amp;
    }
    Out = t;
}