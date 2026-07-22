Texture2D CaptureTexture : register(t0);
SamplerState LinearSampler : register(s0);

cbuffer EffectSettings : register(b0)
{
    float EffectMode;
    float EffectTime;
    float SourceWidth;
    float SourceHeight;
};

struct VertexOutput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

float RandomNoise(float2 seed)
{
    return frac(sin(dot(seed, float2(12.9898, 78.233))) * 43758.5453);
}

VertexOutput VSMain(uint vertexId : SV_VertexID)
{
    VertexOutput output;
    float2 position;
    float2 uv;

    if (vertexId == 0) { position = float2(-1.0,  1.0); uv = float2(0.0, 0.0); }
    else if (vertexId == 1) { position = float2( 3.0,  1.0); uv = float2(2.0, 0.0); }
    else { position = float2(-1.0, -3.0); uv = float2(0.0, 2.0); }

    output.Position = float4(position, 0.0, 1.0);
    output.TexCoord = uv;
    return output;
}

float2 ApplyTransform(float2 uv, int mode)
{
    if (mode == 1) return float2(uv.y, 1.0 - uv.x);             // 90 clockwise
    if (mode == 2) return float2(1.0 - uv.y, uv.x);             // 90 counterclockwise
    if (mode == 3) return 1.0 - uv;                             // 180
    if (mode == 4) return float2(1.0 - uv.x, uv.y);             // mirror
    if (mode == 5) return float2(uv.x, 1.0 - uv.y);             // vertical flip
    return uv;
}

float4 PSMain(VertexOutput input) : SV_TARGET
{
    int mode = (int)(EffectMode + 0.5);
    float2 uv = ApplyTransform(input.TexCoord, mode);

    if (mode == 8)                                               // pixelate
    {
        float blockSize = 22.0;
        float2 blocks = max(float2(SourceWidth, SourceHeight) / blockSize, 1.0);
        uv = (floor(uv * blocks) + 0.5) / blocks;
    }
    else if (mode == 10)                                         // wave
    {
        uv.x += sin(uv.y * 32.0 + EffectTime * 7.0) * 0.018;
        uv.y += cos(uv.x * 24.0 + EffectTime * 5.0) * 0.010;
    }
    else if (mode == 11)                                         // kaleidoscope
    {
        float2 centered = uv - 0.5;
        float radius = length(centered);
        float angle = atan2(centered.y, centered.x) + EffectTime * 0.35;
        const float segment = 1.0471975512;
        angle = abs(fmod(angle + segment * 0.5, segment) - segment * 0.5);
        uv = 0.5 + radius * float2(cos(angle), sin(angle));
    }
    else if (mode == 16)                                         // breathing zoom
    {
        float zoom = 1.04 + (sin(EffectTime * 3.0) * 0.5 + 0.5) * 0.10;
        uv = (uv - 0.5) / zoom + 0.5;
    }
    else if (mode == 17)                                         // digital glitch
    {
        float slice = floor(uv.y * 42.0);
        float noise = frac(sin(slice * 91.7 + floor(EffectTime * 14.0)) * 43758.5453);
        if (noise > 0.84)
            uv.x += (noise - 0.84) * 0.28 * sin(EffectTime * 31.0 + slice);
    }
    else if (mode == 18)                                         // lens warp
    {
        float2 centered = uv - 0.5;
        float radiusSquared = dot(centered, centered);
        uv = 0.5 + centered * (1.0 + radiusSquared * 0.72);
    }
    else if (mode == 21)                                         // screen shake
    {
        float beat = floor(EffectTime * 9.0);
        float strength = step(0.38, RandomNoise(float2(beat, beat + 4.0)));
        float2 shake = float2(
            sin(EffectTime * 47.0 + beat),
            cos(EffectTime * 39.0 + beat * 1.7));
        uv += shake * 0.012 * strength;
    }
    else if (mode == 22)                                         // mirror tiles
    {
        float2 tiled = uv * 3.0;
        float2 tileIndex = floor(tiled);
        float2 localUv = frac(tiled);
        if (fmod(tileIndex.x, 2.0) >= 1.0) localUv.x = 1.0 - localUv.x;
        if (fmod(tileIndex.y, 2.0) >= 1.0) localUv.y = 1.0 - localUv.y;
        uv = localUv;
    }

    if (any(uv < 0.0) || any(uv > 1.0))
        return float4(0.0, 0.0, 0.0, 1.0);

    if (mode == 9)                                               // chromatic aberration
    {
        float pulse = 0.006 + sin(EffectTime * 8.0) * 0.002;
        float2 direction = normalize((uv - 0.5) + float2(0.0001, 0.0001));
        float r = CaptureTexture.Sample(LinearSampler, uv + direction * pulse).r;
        float g = CaptureTexture.Sample(LinearSampler, uv).g;
        float b = CaptureTexture.Sample(LinearSampler, uv - direction * pulse).b;
        return float4(r, g, b, 1.0);
    }

    float4 color = CaptureTexture.Sample(LinearSampler, uv);

    if (mode == 23)                                               // dream blur
    {
        float2 pixel = float2(
            1.0 / max(SourceWidth, 1.0),
            1.0 / max(SourceHeight, 1.0));
        float radius = 3.0 + 1.5 * (sin(EffectTime * 2.2) * 0.5 + 0.5);
        float2 offset = pixel * radius;
        color = color * 0.40;
        color += CaptureTexture.Sample(LinearSampler, uv + float2(offset.x, 0.0)) * 0.15;
        color += CaptureTexture.Sample(LinearSampler, uv - float2(offset.x, 0.0)) * 0.15;
        color += CaptureTexture.Sample(LinearSampler, uv + float2(0.0, offset.y)) * 0.15;
        color += CaptureTexture.Sample(LinearSampler, uv - float2(0.0, offset.y)) * 0.15;
        color.rgb = lerp(color.rgb, color.rgb * float3(1.08, 0.96, 1.12), 0.45);
    }

    if (mode == 6)                                               // grayscale
    {
        float luminance = dot(color.rgb, float3(0.2126, 0.7152, 0.0722));
        color.rgb = luminance.xxx;
    }
    else if (mode == 7)                                          // invert
    {
        color.rgb = 1.0 - color.rgb;
    }
    else if (mode == 12)                                         // sepia
    {
        float3 original = color.rgb;
        color.r = dot(original, float3(0.393, 0.769, 0.189));
        color.g = dot(original, float3(0.349, 0.686, 0.168));
        color.b = dot(original, float3(0.272, 0.534, 0.131));
        color.rgb = saturate(color.rgb);
    }
    else if (mode == 13)                                         // posterize
    {
        const float levels = 5.0;
        color.rgb = floor(color.rgb * levels + 0.5) / levels;
    }
    else if (mode == 14)                                         // scanlines
    {
        float scanlineMask = fmod(floor(input.Position.y), 4.0) < 2.0 ? 1.0 : 0.58;
        float flicker = 0.94 + 0.06 * sin(EffectTime * 25.0);
        color.rgb *= scanlineMask * flicker;
    }
    else if (mode == 15)                                         // pulsing vignette
    {
        float radius = length(uv - 0.5);
        float pulse = 0.56 + 0.08 * sin(EffectTime * 4.0);
        float vignette = 1.0 - smoothstep(0.24, pulse, radius);
        color.rgb *= lerp(0.12, 1.0, vignette);
    }
    else if (mode == 17)                                         // glitch color breakup
    {
        float shift = 0.004 + 0.004 * abs(sin(EffectTime * 19.0));
        color.r = CaptureTexture.Sample(LinearSampler, uv + float2(shift, 0)).r;
        color.b = CaptureTexture.Sample(LinearSampler, uv - float2(shift, 0)).b;
    }
    else if (mode == 19)                                         // heat vision
    {
        float luminance = dot(color.rgb, float3(0.2126, 0.7152, 0.0722));
        float3 cold = float3(0.02, 0.04, 0.28);
        float3 warm = float3(0.95, 0.12, 0.02);
        float3 hot = float3(1.0, 0.95, 0.28);
        color.rgb = luminance < 0.55
            ? lerp(cold, warm, smoothstep(0.05, 0.55, luminance))
            : lerp(warm, hot, smoothstep(0.55, 1.0, luminance));
    }
    else if (mode == 20)                                         // color cycle
    {
        float angle = EffectTime * 1.7;
        float sine = sin(angle);
        float cosine = cos(angle);
        const float3 axis = float3(0.57735027, 0.57735027, 0.57735027);
        color.rgb = saturate(
            color.rgb * cosine +
            cross(axis, color.rgb) * sine +
            axis * dot(axis, color.rgb) * (1.0 - cosine));
    }
    else if (mode == 24)                                         // night vision
    {
        float luminance = dot(color.rgb, float3(0.2126, 0.7152, 0.0722));
        float noise = RandomNoise(input.Position.xy + floor(EffectTime * 24.0));
        float scan = 0.94 + 0.06 * sin(input.Position.y * 1.8);
        float green = saturate(luminance * 1.35 + (noise - 0.5) * 0.10) * scan;
        float vignette = 1.0 - smoothstep(0.30, 0.72, length(uv - 0.5));
        color.rgb = float3(green * 0.08, green, green * 0.18) * vignette;
    }

    return float4(color.rgb, 1.0);
}
