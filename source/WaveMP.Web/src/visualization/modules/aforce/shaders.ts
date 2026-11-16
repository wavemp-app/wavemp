// The shaders of A-Force (GLSL ES 3.00 for WebGL 2). The scene (waveforms and particles with their
// trails) is drawn into a texture; the composite pass lays it over the background with a glow.

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
uniform float uBass;
uniform float uMid;
uniform float uTreble;
uniform float uPulse;
uniform float uSpread;
uniform float uSwirl;
uniform float uSize;
uniform float uPixelScale;
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
    float radius = mix(0.35, uSpread, aSeed.z) * (1.0 + uBass * 0.3 + uPulse * 0.25);
    vec3 position = vec3(cos(angle) * radius, sin(angle) * radius * 0.62, z);
    position.y += sin(position.x * 0.8 + uTime * 0.7 + aSeed.w * 6.0) * 0.25 * (0.4 + uMid);
    vec4 clip = uViewProjection * vec4(position, 1.0);
    gl_Position = clip;
    float spark = step(0.88, aSeed.w);
    float size = uSize * (0.5 + aSeed.w) * (1.0 + uTreble * 0.8 * spark + uPulse * 0.6);
    gl_PointSize = clamp(size * uPixelScale / max(clip.w, 0.2), 1.0, 40.0);
    float fade = smoothstep(0.0, 0.12, life) * (1.0 - smoothstep(0.9, 1.0, life));
    vColor = mix(mix(uColorA, uColorB, aSeed.z), uSpark, spark * (0.3 + uTreble * 0.7));
    vAlpha = fade * (0.3 + 0.5 * uMid * aSeed.z + 0.8 * uTreble * spark + 0.3 * uPulse);
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
uniform float uBass;
uniform float uMid;
uniform float uTreble;
uniform float uPulse;
uniform float uAmplitude;
uniform float uSpread;
uniform float uThickness;
uniform float uWaveFlow;
uniform float uWidth;
uniform vec3 uColorStart;
uniform vec3 uColorEnd;
uniform vec3 uColorFar;
out vec3 vColor;
out float vEdge;
out float vFade;
float sampleWave(int row, float u) {
    float x = u * float(uPoints - 1);
    int index = int(floor(x));
    float a = texelFetch(uWaves, ivec2(index, row), 0).r;
    float b = texelFetch(uWaves, ivec2(min(index + 1, uPoints - 1), row), 0).r;
    return mix(a, b, fract(x));
}
void main() {
    int wave = gl_InstanceID;
    float far = float(wave) / max(float(uCount - 1), 1.0);
    int row = (uHead - wave * uLag + uRows * 8) % uRows;
    float u = aVertex.x;
    float envelope = sin(u * 3.14159265);
    float sound = sampleWave(row, u);
    float flowing = sin(u * 6.0 + uFlow * uWaveFlow + far * 2.4) * 0.35
        + sin(u * 13.0 - uTime * 0.9 + far * 5.0) * 0.12;
    float y = (sound * uAmplitude * (0.7 + uBass * 0.9 + uPulse * 0.6) + flowing * (0.35 + uMid * 0.7))
        * envelope;
    y += (0.5 - far) * uSpread;
    float z = -far * 3.5 + sin(u * 4.0 + uTime * 0.5 + far) * 0.4;
    float thickness = uThickness * (0.7 + uTreble * 0.9 + uPulse * 0.5) * (0.35 + 0.65 * envelope);
    vec3 position = vec3((u - 0.5) * uWidth, y + aVertex.y * thickness, z);
    gl_Position = uViewProjection * vec4(position, 1.0);
    vEdge = aVertex.y;
    vFade = envelope * (1.0 - far * 0.55);
    vColor = mix(mix(uColorStart, uColorEnd, u), uColorFar, far);
}
`

export const waveFragment = `#version 300 es
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

/** Background, scene, glow, flash of a beat and vignette, onto the canvas. */
export const compositeFragment = `#version 300 es
precision highp float;
in vec2 vUv;
uniform sampler2D uScene;
uniform vec3 uTop;
uniform vec3 uBottom;
uniform vec3 uGlow;
uniform float uTime;
uniform float uBass;
uniform float uPulse;
uniform float uBloom;
uniform float uAspect;
out vec4 outColor;
void main() {
    vec2 c = (vUv - 0.5) * vec2(uAspect, 1.0);
    vec3 color = mix(uBottom, uTop, clamp(vUv.y + 0.08 * sin(uTime * 0.2 + vUv.x * 3.0), 0.0, 1.0));
    float radial = exp(-dot(c, c) * (3.2 - uBass * 1.4));
    color += uGlow * radial * (0.12 + 0.3 * uBass + 0.25 * uPulse);
    color += texture(uScene, vUv).rgb;
    if (uBloom > 0.0) {
        vec3 glow = textureLod(uScene, vUv, 2.5).rgb * 0.6 + textureLod(uScene, vUv, 4.5).rgb * 0.8;
        color += glow * uBloom;
    }
    color += uGlow * uPulse * 0.06;
    color *= mix(1.0, 0.55, smoothstep(0.3, 1.15, length(c)));
    outColor = vec4(1.0 - exp(-color * 1.4), 1.0);
}
`
