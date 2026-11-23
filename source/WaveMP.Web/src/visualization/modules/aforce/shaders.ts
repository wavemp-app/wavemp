// The shaders of A-Force (GLSL ES 3.00 for WebGL 2). The scene (the layers of the mode with their
// trails) is drawn into a texture; the composite pass lays it over the background with a glow into
// the input of the flash guard. The reaction to the music arrives as finished numbers (frame.ts):
// the shaders only draw them. Every layer is multiplied by its weight of the mode and by the
// brightness of the frame.

export { fullscreenVertex } from '../../webgl'

/** The previous frame, a little larger and turned, fading out: the trails. */
export const trailFragment = `#version 300 es
precision highp float;
in vec2 vUv;
uniform sampler2D uPrevious;
uniform float uKeep;
uniform float uZoom;
uniform float uTurn;
uniform float uAspect;
out vec4 outColor;
void main() {
    vec2 c = vUv - 0.5;
    c.x *= uAspect;
    float s = sin(uTurn);
    float k = cos(uTurn);
    c = mat2(k, s, -s, k) * c / uZoom;
    c.x /= uAspect;
    vec2 uv = c + 0.5;
    vec3 previous = texture(uPrevious, uv).rgb;
    float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
    // The small subtraction lets dark trails reach black despite the 8 bits of the texture.
    outColor = vec4(max(previous * uKeep - 1.5 / 255.0, 0.0) * inside, 1.0);
}
`

/** Particles flying through a swirling tunnel towards the viewer. */
export const particleVertex = `#version 300 es
precision highp float;
layout(location = 0) in vec4 aSeed;
uniform mat4 uViewProjection;
uniform float uTime;
uniform float uFlow;
uniform float uScale;
uniform float uSway;
uniform float uSparkle;
uniform float uBeat;
uniform float uSpread;
uniform float uSwirl;
uniform float uSize;
uniform float uPixelScale;
uniform float uWeight;
uniform float uBrightness;
uniform vec3 uColorA;
uniform vec3 uColorB;
uniform vec3 uSpark;
out vec3 vColor;
out float vAlpha;
const float depth = 22.0;
void main() {
    float life = fract(aSeed.x + uFlow * (0.6 + aSeed.w * 0.8) / depth);
    float z = mix(-depth, 3.0, life);
    float angle = aSeed.y * 6.2831853 + uTime * uSwirl * (0.2 + aSeed.z * 0.6) + life * uSwirl * 2.5;
    float radius = mix(0.35, uSpread, aSeed.z) * uScale;
    vec3 position = vec3(cos(angle) * radius, sin(angle) * radius * 0.62, z);
    position.y += sin(position.x * 0.8 + uTime * 0.7 + aSeed.w * 6.0) * 0.25 * (0.4 + uSway);
    vec4 clip = uViewProjection * vec4(position, 1.0);
    gl_Position = clip;
    float spark = step(0.88, aSeed.w);
    float size = uSize * (0.5 + aSeed.w) * (1.0 + uSparkle * 1.2 * spark + uBeat * 0.6);
    gl_PointSize = clamp(size * uPixelScale / max(clip.w, 0.2), 1.0, 40.0);
    float fade = smoothstep(0.0, 0.12, life) * (1.0 - smoothstep(0.9, 1.0, life));
    vColor = mix(mix(uColorA, uColorB, aSeed.z), uSpark, spark * min(1.0, 0.3 + uSparkle * 0.7));
    vAlpha = fade * (0.3 + 0.4 * uSway * aSeed.z + 0.9 * uSparkle * spark + 0.3 * uBeat)
        * uWeight * uBrightness;
}
`

export const particleFragment = `#version 300 es
precision highp float;
in vec3 vColor;
in float vAlpha;
out vec4 outColor;
void main() {
    vec2 d = gl_PointCoord * 2.0 - 1.0;
    float r = dot(d, d);
    if (r > 1.0) {
        discard;
    }
    float a = 1.0 - r;
    outColor = vec4(vColor * a * a * vAlpha, 1.0);
}
`

/** Reads a point of a row of the waveform texture, linearly between its texels. */
const sampleWave = `
float sampleWave(int row, float u) {
    float x = clamp(u, 0.0, 1.0) * float(uPoints - 1);
    int index = int(floor(x));
    float a = texelFetch(uWaves, ivec2(index, row), 0).r;
    float b = texelFetch(uWaves, ivec2(min(index + 1, uPoints - 1), row), 0).r;
    return mix(a, b, fract(x));
}
`

/** Reads a band of a row of the spectrum texture (0: lowest, 1: highest), linearly. */
const sampleSpectrum = `
float sampleSpectrum(int row, float x) {
    float f = clamp(x, 0.0, 1.0) * float(uBands - 1);
    int index = int(floor(f));
    float a = texelFetch(uSpectrum, ivec2(index, row), 0).r;
    float b = texelFetch(uSpectrum, ivec2(min(index + 1, uBands - 1), row), 0).r;
    return mix(a, b, fract(f));
}
`

/** Fragment of the line layers (waveforms, ridges, helix): a strip with a bright core. */
export const lineFragment = `#version 300 es
precision highp float;
in vec3 vColor;
in float vEdge;
in float vFade;
out vec4 outColor;
void main() {
    float a = 1.0 - abs(vEdge);
    float core = smoothstep(0.55, 1.0, a);
    vec3 color = vColor * a * a * 0.9 + vec3(core * 0.45);
    outColor = vec4(color * vFade, 1.0);
}
`

/**
 * The waveforms: one instance per waveform, each a strip along the points of a row of the
 * waveform texture. Farther waveforms show older rows, so the sound flows into the depth.
 */
export const waveVertex = `#version 300 es
precision highp float;
layout(location = 0) in vec2 aVertex;
uniform sampler2D uWaves;
uniform int uPoints;
uniform int uRows;
uniform int uHead;
uniform int uLag;
uniform int uCount;
uniform mat4 uViewProjection;
uniform float uTime;
uniform float uFlow;
uniform float uAmplitude;
uniform float uSwing;
uniform float uSpread;
uniform float uThickness;
uniform float uWaveFlow;
uniform float uWidth;
uniform float uWeight;
uniform float uBrightness;
uniform vec3 uColorStart;
uniform vec3 uColorEnd;
uniform vec3 uColorFar;
out vec3 vColor;
out float vEdge;
out float vFade;
${sampleWave}
void main() {
    int wave = gl_InstanceID;
    float far = float(wave) / max(float(uCount - 1), 1.0);
    int row = (uHead - wave * uLag + uRows * 8) % uRows;
    float u = aVertex.x;
    float envelope = sin(u * 3.14159265);
    float sound = sampleWave(row, u);
    float flowing = sin(u * 6.0 + uFlow * uWaveFlow + far * 2.4) * 0.35
        + sin(u * 13.0 - uTime * 0.9 + far * 5.0) * 0.12;
    float y = (sound * uAmplitude + flowing * uSwing) * envelope;
    y += (0.5 - far) * uSpread;
    float z = -far * 3.5 + sin(u * 4.0 + uTime * 0.5 + far) * 0.4;
    float thickness = uThickness * (0.35 + 0.65 * envelope);
    vec3 position = vec3((u - 0.5) * uWidth, y + aVertex.y * thickness, z);
    gl_Position = uViewProjection * vec4(position, 1.0);
    vEdge = aVertex.y;
    vFade = envelope * (1.0 - far * 0.55) * uWeight * uBrightness;
    vColor = mix(mix(uColorStart, uColorEnd, u), uColorFar, far);
}
`

/**
 * The rings: concentric rings of the newest spectrum around the middle, seen head on. Each ring is
 * a band (low inside, high outside); the bass swells the inner rings, the highs ripple the outer
 * ones with fine teeth, and a beat sends a ring of light outwards.
 */
export const ringFragment = `#version 300 es
precision highp float;
in vec2 vUv;
uniform sampler2D uSpectrum;
uniform int uBands;
uniform int uHead;
uniform float uCount;
uniform float uRadius;
uniform float uAspect;
uniform float uTime;
uniform float uSpin;
uniform float uSwell;
uniform float uSparkle;
uniform float uBeat;
uniform float uWeight;
uniform float uBrightness;
uniform vec3 uInner;
uniform vec3 uOuter;
uniform vec3 uSpark;
out vec4 outColor;
${sampleSpectrum}
void main() {
    vec2 c = (vUv - 0.5) * vec2(uAspect, 1.0);
    float r = length(c);
    float angle = atan(c.y, c.x);
    float spacing = uRadius / uCount;
    vec3 color = vec3(0.0);
    float nearest = floor(r / spacing);
    for (int offset = -2; offset <= 1; offset++) {
        float ring = nearest + float(offset);
        if (ring < 0.0 || ring >= uCount) {
            continue;
        }
        float t = (ring + 0.5) / uCount;
        float level = sampleSpectrum(uHead, t);
        float swell = level * spacing * (0.25 + 0.6 * uSwell * (1.0 - t));
        float teeth = sin(angle * (10.0 + floor(t * 4.0) * 6.0) + uSpin * (mod(ring, 2.0) * 2.0 - 1.0)
            + uTime * 0.6) * uSparkle * level * spacing * 0.45 * t;
        float radius = (ring + 0.5) * spacing + swell + teeth;
        float width = spacing * (0.07 + 0.12 * level);
        float d = (r - radius) / width;
        float line = exp(-d * d);
        vec3 tint = mix(uInner, uOuter, t);
        tint = mix(tint, uSpark, clamp(uSparkle * t * level, 0.0, 0.8));
        color += tint * line * (0.25 + 0.9 * level);
    }
    float shock = (r - (1.0 - uBeat) * uRadius * 1.3) / (spacing * 0.35);
    color += uSpark * exp(-shock * shock) * uBeat * 0.7;
    outColor = vec4(color * uWeight * uBrightness, 1.0);
}
`

/** Particles as streaks flying at the viewer: per instance a strip from the tail to the head. */
export const streakVertex = `#version 300 es
precision highp float;
layout(location = 0) in vec4 aSeed;
uniform mat4 uViewProjection;
uniform float uTime;
uniform float uFlow;
uniform float uLength;
uniform float uSpread;
uniform float uSparkle;
uniform float uBeat;
uniform float uPixelWidth;
uniform float uAspect;
uniform float uWeight;
uniform float uBrightness;
uniform vec3 uColorA;
uniform vec3 uColorB;
uniform vec3 uSpark;
out vec3 vColor;
out float vEdge;
out float vAlpha;
const float depth = 30.0;
vec3 place(float life) {
    float angle = aSeed.y * 6.2831853 + uTime * 0.04 * (aSeed.z - 0.5);
    float radius = mix(0.25, uSpread, aSeed.z * aSeed.z);
    return vec3(cos(angle) * radius, sin(angle) * radius * 0.7, mix(-depth, 2.5, life));
}
void main() {
    float life = fract(aSeed.x + uFlow * (0.7 + aSeed.w * 0.6) / depth);
    float tail = max(0.0, life - uLength * (0.015 + aSeed.w * 0.02));
    bool atHead = gl_VertexID >= 2;
    float side = (gl_VertexID % 2 == 0) ? -1.0 : 1.0;
    vec4 head = uViewProjection * vec4(place(life), 1.0);
    vec4 back = uViewProjection * vec4(place(tail), 1.0);
    vec4 clip = atHead ? head : back;
    if (head.w < 0.05 || back.w < 0.05) {
        gl_Position = vec4(2.0, 2.0, 2.0, 1.0);
        vAlpha = 0.0;
        vEdge = 0.0;
        vColor = vec3(0.0);
        return;
    }
    vec2 direction = head.xy / head.w - back.xy / back.w;
    direction.x *= uAspect;
    vec2 normal = length(direction) > 1e-5 ? normalize(vec2(-direction.y, direction.x)) : vec2(0.0, 1.0);
    normal.x /= uAspect;
    float spark = step(0.85, aSeed.w);
    float width = uPixelWidth * (0.6 + aSeed.w) * (1.0 + uSparkle * spark * 1.5 + uBeat * 0.5);
    clip.xy += normal * side * width * clip.w;
    gl_Position = clip;
    vEdge = side;
    float fade = smoothstep(0.0, 0.2, life) * (1.0 - smoothstep(0.92, 1.0, life));
    vColor = mix(mix(uColorA, uColorB, aSeed.z), uSpark, spark * min(1.0, 0.25 + uSparkle * 0.75));
    vAlpha = fade * (atHead ? 1.0 : 0.15) * (0.4 + 0.3 * uBeat + 0.8 * uSparkle * spark)
        * uWeight * uBrightness;
}
`

export const streakFragment = `#version 300 es
precision highp float;
in vec3 vColor;
in float vEdge;
in float vAlpha;
out vec4 outColor;
void main() {
    float a = 1.0 - abs(vEdge);
    outColor = vec4(vColor * a * vAlpha, 1.0);
}
`

/**
 * The ridges: one instance per row of the spectrum history, the newest in front, each a line
 * across the landscape with the low bands in the middle and the high ones at the sides. The rows
 * glide towards the viewer between two new rows (uPhase); a beat lifts the front rows, the highs
 * glitter on the crests.
 */
export const terrainVertex = `#version 300 es
precision highp float;
layout(location = 0) in vec2 aVertex;
uniform sampler2D uSpectrum;
uniform int uBands;
uniform int uRows;
uniform int uHead;
uniform int uCount;
uniform mat4 uViewProjection;
uniform float uPhase;
uniform float uLift;
uniform float uFront;
uniform float uSparkle;
uniform float uSway;
uniform float uTime;
uniform float uThickness;
uniform float uWeight;
uniform float uBrightness;
uniform vec3 uNear;
uniform vec3 uFar;
uniform vec3 uSpark;
out vec3 vColor;
out float vEdge;
out float vFade;
${sampleSpectrum}
const float spacing = 0.42;
const float front = 2.2;
const float floorY = -1.55;
void main() {
    int index = gl_InstanceID;
    int row = (uHead - index + uRows * 8) % uRows;
    float x = aVertex.x * 2.0 - 1.0;
    float level = sampleSpectrum(row, abs(x));
    float along = float(index) - uPhase;
    float far = clamp(along / float(uCount), 0.0, 1.0);
    float frontLift = uFront * max(0.0, 1.0 - along / 3.0) * 0.6;
    float height = level * uLift * (0.6 + 0.6 * (1.0 - abs(x))) + frontLift * (1.0 - abs(x) * 0.5);
    float bend = sin(x * 2.0 + uTime * 0.3 + float(row) * 0.2) * 0.15 * uSway;
    vec3 position = vec3(x * 7.0, floorY + height + bend + aVertex.y * uThickness, front - along * spacing);
    gl_Position = uViewProjection * vec4(position, 1.0);
    vEdge = aVertex.y;
    float glitter = uSparkle * level * level * step(0.5, fract(x * 23.0 + float(row) * 0.37));
    vColor = mix(mix(uNear, uFar, far), uSpark, clamp(glitter, 0.0, 0.9));
    vFade = smoothstep(0.0, 0.08, far + 0.02) * (1.0 - far) * (1.0 - smoothstep(0.75, 1.0, abs(x)))
        * (0.45 + 0.8 * level) * uWeight * uBrightness;
}
`

/**
 * The helix: two strands of the waveform twisted around the depth axis, turning with the mids; a
 * beat tightens the twist and the highs send glints along the strands.
 */
export const helixVertex = `#version 300 es
precision highp float;
layout(location = 0) in vec2 aVertex;
uniform sampler2D uWaves;
uniform int uPoints;
uniform int uRows;
uniform int uHead;
uniform int uLag;
uniform mat4 uViewProjection;
uniform float uSpin;
uniform float uTurns;
uniform float uRadius;
uniform float uAmplitude;
uniform float uThickness;
uniform float uSparkle;
uniform float uFlow;
uniform float uWeight;
uniform float uBrightness;
uniform vec3 uColorStart;
uniform vec3 uColorEnd;
uniform vec3 uSpark;
out vec3 vColor;
out float vEdge;
out float vFade;
${sampleWave}
const float helixLength = 16.0;
void main() {
    int strand = gl_InstanceID;
    int row = (uHead - strand * uLag + uRows * 8) % uRows;
    float u = aVertex.x;
    float sound = sampleWave(row, u);
    float angle = u * uTurns * 6.2831853 + uSpin + float(strand) * 3.14159265;
    float radius = uRadius * (1.0 + sound * 0.5 * uAmplitude) * mix(1.0, 0.55, u);
    vec3 radial = vec3(cos(angle), sin(angle) * 0.8, 0.0);
    vec3 position = radial * radius + vec3(0.0, 0.0, 2.0 - u * helixLength);
    position += normalize(radial) * aVertex.y * uThickness * mix(1.0, 0.5, u);
    gl_Position = uViewProjection * vec4(position, 1.0);
    vEdge = aVertex.y;
    float glint = uSparkle * step(0.9, fract(u * 18.0 - uFlow * 0.2 + float(strand) * 0.5));
    vColor = mix(strand == 0 ? uColorStart : uColorEnd, uSpark, clamp(glint, 0.0, 0.9));
    vFade = smoothstep(0.0, 0.06, u) * (1.0 - smoothstep(0.7, 1.0, u)) * (0.8 + glint)
        * uWeight * uBrightness;
}
`

/** Background, scene, glow, the light of a beat and vignette, into the input of the guard. */
export const compositeFragment = `#version 300 es
precision highp float;
in vec2 vUv;
uniform sampler2D uScene;
uniform vec3 uTop;
uniform vec3 uBottom;
uniform vec3 uGlow;
uniform float uTime;
uniform float uMass;
uniform float uBeat;
uniform float uGlowStrength;
uniform float uBrightness;
uniform float uBloom;
uniform float uAspect;
out vec4 outColor;
void main() {
    vec2 c = (vUv - 0.5) * vec2(uAspect, 1.0);
    vec3 color = mix(uBottom, uTop, clamp(vUv.y + 0.08 * sin(uTime * 0.2 + vUv.x * 3.0), 0.0, 1.0));
    color *= 0.6 + 0.4 * uBrightness;
    float radial = exp(-dot(c, c) * (3.4 - clamp(uMass - 1.0, 0.0, 1.0) * 1.6));
    color += uGlow * radial * uGlowStrength;
    color += texture(uScene, vUv).rgb;
    if (uBloom > 0.0) {
        vec3 glow = textureLod(uScene, vUv, 2.5).rgb * 0.6 + textureLod(uScene, vUv, 4.5).rgb * 0.8;
        color += glow * uBloom;
    }
    color += uGlow * uBeat * 0.05;
    color *= mix(1.0, 0.55, smoothstep(0.3, 1.15, length(c)));
    outColor = vec4(1.0 - exp(-color * 1.4), 1.0);
}
`
