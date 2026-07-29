Texture2D CaptureTexture : register(t0);
Texture2D PreviousFrameTexture : register(t1);
SamplerState LinearSampler : register(s0);

cbuffer EffectSettings : register(b0)
{
    float EffectMode;
    float EffectTime;
    float SourceWidth;
    float SourceHeight;
    float Exposure;
    float Contrast;
    float Saturation;
    float HueRadians;
    float Temperature;
    float Tint;
    float Gamma;
    float Vignette;
    float RedMultiplier;
    float GreenMultiplier;
    float BlueMultiplier;
    float ApplyColorSettings;
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
    else if (mode == 25)                                         // screen tearing
    {
        float movingTear = frac(EffectTime * 0.72);
        float tearDistance = abs(uv.y - movingTear);
        float majorBand = 1.0 - step(0.075, tearDistance);
        float bandIndex = floor(uv.y * 30.0);
        float bandNoise = RandomNoise(float2(
            bandIndex,
            floor(EffectTime * 12.0)));
        float brokenBand = step(0.88, bandNoise);
        float direction = bandNoise > 0.94 ? -1.0 : 1.0;
        uv.x += direction * (majorBand * 0.045 + brokenBand * 0.018);
    }
    else if (mode == 29)                                         // VHS tracking
    {
        float trackingBand = smoothstep(0.10, 0.0, abs(uv.y - frac(EffectTime * 0.23)));
        float rowNoise = RandomNoise(float2(floor(uv.y * 180.0), floor(EffectTime * 18.0)));
        uv.x += (rowNoise - 0.5) * 0.010 + trackingBand * 0.055;
    }
    else if (mode == 31)                                         // radial rush
    {
        float2 centered = uv - 0.5;
        float pulse = 0.035 + 0.018 * sin(EffectTime * 5.0);
        uv = 0.5 + centered * (1.0 - pulse);
    }
    else if (mode == 32)                                         // water ripple
    {
        float2 centered = uv - 0.5;
        float distanceFromCenter = length(centered);
        uv += normalize(centered + 0.0001) *
            sin(distanceFromCenter * 58.0 - EffectTime * 7.0) * 0.012;
    }
    else if (mode == 36)                                         // rolling shutter
    {
        float rowPhase = sin(uv.y * 20.0 - EffectTime * 5.5);
        uv.x += rowPhase * 0.018 * (0.25 + uv.y);
    }
    else if (mode == 37)                                         // frosted glass
    {
        float2 cell = floor(uv * float2(95.0, 54.0));
        float2 jitter = float2(
            RandomNoise(cell + floor(EffectTime * 5.0)),
            RandomNoise(cell.yx + 19.0 + floor(EffectTime * 5.0))) - 0.5;
        uv += jitter * 0.007;
    }
    else if (mode == 39)                                         // CRT curvature
    {
        float2 centered = uv * 2.0 - 1.0;
        float2 curved = centered * (1.0 + 0.11 * centered.yx * centered.yx);
        uv = curved * 0.5 + 0.5;
    }
    else if (mode == 40)                                         // mosaic shuffle
    {
        const float2 tileCount = float2(8.0, 5.0);
        float2 tile = floor(uv * tileCount);
        float2 localUv = frac(uv * tileCount);
        float shift = floor(EffectTime * 1.8 + tile.y);
        tile.x = fmod(tile.x + shift, tileCount.x);
        uv = (tile + localUv) / tileCount;
    }
    else if (mode == 43)                                         // tunnel vision twist
    {
        float2 centered = uv - 0.5;
        float radius = length(centered);
        float angle = atan2(centered.y, centered.x) +
            smoothstep(0.18, 0.72, radius) * sin(EffectTime * 1.8) * 0.55;
        uv = 0.5 + radius * float2(cos(angle), sin(angle));
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

    if (mode == 30)                                               // double vision
    {
        float2 sway = float2(
            sin(EffectTime * 2.1) * 0.014,
            cos(EffectTime * 1.7) * 0.006);
        float3 ghost = CaptureTexture.Sample(
            LinearSampler,
            saturate(uv + sway)).rgb;
        color.rgb = lerp(color.rgb, ghost, 0.38);
    }
    else if (mode == 31)                                          // radial rush streaks
    {
        float2 direction = (uv - 0.5) * 0.018;
        color.rgb = color.rgb * 0.36;
        color.rgb += CaptureTexture.Sample(LinearSampler, saturate(uv - direction)).rgb * 0.25;
        color.rgb += CaptureTexture.Sample(LinearSampler, saturate(uv - direction * 2.0)).rgb * 0.21;
        color.rgb += CaptureTexture.Sample(LinearSampler, saturate(uv - direction * 3.0)).rgb * 0.18;
    }
    else if (mode == 35)                                          // prism
    {
        float2 centered = uv - 0.5;
        float2 direction = normalize(centered + 0.0001);
        float2 tangent = float2(-direction.y, direction.x);
        float shift = 0.010 + 0.004 * sin(EffectTime * 2.4);
        color.r = CaptureTexture.Sample(LinearSampler, saturate(uv + tangent * shift)).r;
        color.b = CaptureTexture.Sample(LinearSampler, saturate(uv - tangent * shift)).b;
    }
    else if (mode == 41)                                          // hyperspace trails
    {
        float2 direction = normalize((uv - 0.5) + 0.0001);
        float brightness = max(color.r, max(color.g, color.b));
        float3 trail = 0.0;
        trail += CaptureTexture.Sample(LinearSampler, saturate(uv - direction * 0.012)).rgb;
        trail += CaptureTexture.Sample(LinearSampler, saturate(uv - direction * 0.028)).rgb;
        trail += CaptureTexture.Sample(LinearSampler, saturate(uv - direction * 0.050)).rgb;
        color.rgb = saturate(color.rgb + trail * brightness * 0.22);
    }

    if (mode == 27)                                               // Source-style hall of mirrors
    {
        float2 centered = uv - 0.5;
        float2 feedbackUv = 0.5 + centered * 0.972;
        feedbackUv += float2(
            sin(EffectTime * 0.83),
            cos(EffectTime * 0.69)) * 0.0015;
        float3 previous = PreviousFrameTexture.Sample(
            LinearSampler,
            saturate(feedbackUv)).rgb;
        float edgeVoid = smoothstep(0.68, 0.40, length(centered));
        float3 recursiveTrail = saturate(
            previous * float3(0.985, 0.975, 1.0) +
            color.rgb * 0.20);
        recursiveTrail *= lerp(0.72, 1.0, edgeVoid);
        float feedbackAmount = smoothstep(0.08, 0.45, EffectTime);
        color.rgb = lerp(color.rgb, recursiveTrail, feedbackAmount * 0.88);
    }
    else if (mode == 28)                                          // uncleared framebuffer
    {
        float3 previous = PreviousFrameTexture.Sample(
            LinearSampler,
            uv).rgb;

        // Overlay the incoming frame onto history at the exact same pixel
        // coordinates. This creates smooth temporal persistence with no
        // block mask, no neighbor sampling, and no additive brightening.
        // A very low write opacity makes old frames remain visibly stacked.
        float writeOpacity = EffectTime < 0.12 ? 1.0 : 0.008;
        color.rgb = lerp(previous, color.rgb, writeOpacity);
    }
    else if (mode == 44)                                          // motion-triggered frame echoes
    {
        float2 pixel = float2(
            1.0 / max(SourceWidth, 1.0),
            1.0 / max(SourceHeight, 1.0));
        float3 previous = PreviousFrameTexture.Sample(
            LinearSampler,
            uv).rgb;

        // Compare the new capture with the retained output. Pixels that have
        // not changed keep the fresh image; changed pixels pull several
        // displaced copies out of history. Using lerp rather than addition
        // prevents the trail from becoming brighter every frame.
        float difference = length(color.rgb - previous);
        float motionMask = smoothstep(0.035, 0.24, difference);
        float2 drift = float2(
            8.0 + 5.0 * sin(EffectTime * 1.7),
            5.0 + 4.0 * cos(EffectTime * 1.3)) * pixel;
        float3 echoA = PreviousFrameTexture.Sample(
            LinearSampler,
            saturate(uv + drift)).rgb;
        float3 echoB = PreviousFrameTexture.Sample(
            LinearSampler,
            saturate(uv + drift * 2.35)).rgb;
        float3 echoC = PreviousFrameTexture.Sample(
            LinearSampler,
            saturate(uv - drift * 1.4)).rgb;
        float3 copiedHistory =
            previous * 0.40 +
            echoA * 0.27 +
            echoB * 0.19 +
            echoC * 0.14;

        float startup = smoothstep(0.08, 0.35, EffectTime);
        color.rgb = lerp(
            color.rgb,
            copiedHistory,
            motionMask * startup * 0.86);
    }

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
    else if (mode == 29)                                         // VHS tape finish
    {
        float grain = RandomNoise(input.Position.xy + floor(EffectTime * 30.0)) - 0.5;
        float scanMask = 0.86 + 0.14 * sin(input.Position.y * 2.2);
        color.r = CaptureTexture.Sample(LinearSampler, saturate(uv + float2(0.004, 0.0))).r;
        color.b = CaptureTexture.Sample(LinearSampler, saturate(uv - float2(0.004, 0.0))).b;
        color.rgb = saturate(color.rgb * scanMask + grain * 0.075);
    }
    else if (mode == 33)                                         // security camera
    {
        float luminance = dot(color.rgb, float3(0.2126, 0.7152, 0.0722));
        float noise = RandomNoise(input.Position.xy + floor(EffectTime * 20.0)) - 0.5;
        float scanMask = fmod(floor(input.Position.y), 3.0) < 1.0 ? 0.78 : 1.0;
        float surveillanceLevel = saturate(
            (luminance + noise * 0.11) * scanMask);
        color.rgb = surveillanceLevel.xxx * float3(0.76, 0.94, 0.82);
    }
    else if (mode == 34)                                         // comic ink
    {
        float2 pixel = float2(1.0 / max(SourceWidth, 1.0), 1.0 / max(SourceHeight, 1.0));
        float luminance = dot(color.rgb, float3(0.299, 0.587, 0.114));
        float rightLum = dot(CaptureTexture.Sample(LinearSampler, uv + float2(pixel.x * 2.0, 0)).rgb,
            float3(0.299, 0.587, 0.114));
        float downLum = dot(CaptureTexture.Sample(LinearSampler, uv + float2(0, pixel.y * 2.0)).rgb,
            float3(0.299, 0.587, 0.114));
        float edge = saturate((abs(luminance - rightLum) + abs(luminance - downLum)) * 8.0);
        color.rgb = floor(color.rgb * 5.0) / 5.0;
        color.rgb *= 1.0 - edge * 0.88;
    }
    else if (mode == 37)                                         // frosted finish
    {
        color.rgb = lerp(color.rgb, dot(color.rgb, float3(0.25, 0.62, 0.13)).xxx, 0.12);
    }
    else if (mode == 38)                                         // solarize
    {
        color.rgb = abs(color.rgb * 2.0 - 1.0);
        color.rgb = saturate(color.rgb * float3(1.08, 0.88, 1.16));
    }
    else if (mode == 39)                                         // CRT grille
    {
        float grille = 0.78 + 0.22 * sin(input.Position.x * 3.14159265);
        float scanMask = 0.84 + 0.16 * sin(input.Position.y * 3.14159265);
        color.rgb *= grille * scanMask;
        color.rgb *= 1.0 - smoothstep(0.42, 0.72, length(uv - 0.5)) * 0.45;
    }
    else if (mode == 42)                                         // channel roulette
    {
        float phase = fmod(floor(EffectTime * 1.6), 3.0);
        if (phase < 1.0) color.rgb = color.gbr;
        else if (phase < 2.0) color.rgb = color.brg;
        color.rgb = lerp(color.rgb, color.rgb * float3(1.08, 0.94, 1.04), 0.5);
    }
    else if (mode == 43)                                         // tunnel darkness
    {
        float radius = length(uv - 0.5);
        float mask = 1.0 - smoothstep(0.18, 0.68, radius);
        color.rgb *= lerp(0.10, 1.0, mask);
    }

    if (ApplyColorSettings > 0.5)
    {
        color.rgb *= exp2(Exposure);
        color.rgb = (color.rgb - 0.5) * Contrast + 0.5;

        float luminance = dot(color.rgb, float3(0.2126, 0.7152, 0.0722));
        color.rgb = lerp(luminance.xxx, color.rgb, Saturation);

        const float3 hueAxis = float3(0.57735027, 0.57735027, 0.57735027);
        float hueSine = sin(HueRadians);
        float hueCosine = cos(HueRadians);
        color.rgb =
            color.rgb * hueCosine +
            cross(hueAxis, color.rgb) * hueSine +
            hueAxis * dot(hueAxis, color.rgb) * (1.0 - hueCosine);

        color.rgb += float3(
            Temperature * 0.12 + Tint * 0.025,
            Tint * 0.10,
            -Temperature * 0.12 + Tint * 0.025);
        color.rgb *= float3(RedMultiplier, GreenMultiplier, BlueMultiplier);
        color.rgb = pow(max(color.rgb, 0.0), 1.0 / max(Gamma, 0.05));

        float edge = smoothstep(0.18, 0.72, length(uv - 0.5));
        color.rgb *= 1.0 - edge * Vignette * 0.82;
    }

    return float4(saturate(color.rgb), 1.0);
}
