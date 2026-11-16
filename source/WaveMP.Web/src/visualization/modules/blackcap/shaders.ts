// The shaders of BlackCap (GLSL ES 3.00 for WebGL 2). The scene (background with floating lights,
// then the shapes with depth) is drawn into a texture; the composite pass lays it onto the canvas
// with a soft glow and a vignette.

/** Number of bands of the spectrum the surfaces follow. */
export const spectrumBands = 16

/** Most floating lights of the background the shader draws. */
export const shaderLights = 12

/**
 * Smooth gradient noise in 3D, from about -1 to 1: random gradients on a lattice, hashed with
 * integer arithmetic, blended with a quintic curve so the surfaces stay smooth.
 */
const noise = `
uint hashCell(uvec3 v) {
    v = v * 1664525u + 1013904223u;
    v.x += v.y * v.z;
    v.y += v.z * v.x;
    v.z += v.x * v.y;
    v ^= v >> 16u;
    v.x += v.y * v.z;
    v.y += v.z * v.x;
    v.z += v.x * v.y;
    return v.x ^ v.y ^ v.z;
}
float corner(ivec3 cell, vec3 offset) {
    uint h = hashCell(uvec3(cell + ivec3(32768)));
    vec3 g = vec3(float(h & 1023u), float((h >> 10u) & 1023u), float((h >> 20u) & 1023u)) / 511.5 - 1.0;
    return dot(g, offset);
}
float noise(vec3 p) {
    vec3 base = floor(p);
    ivec3 i = ivec3(base);
    vec3 f = p - base;
    vec3 w = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float x00 = mix(corner(i, f), corner(i + ivec3(1, 0, 0), f - vec3(1.0, 0.0, 0.0)), w.x);
    float x10 = mix(corner(i + ivec3(0, 1, 0), f - vec3(0.0, 1.0, 0.0)),
        corner(i + ivec3(1, 1, 0), f - vec3(1.0, 1.0, 0.0)), w.x);
    float x01 = mix(corner(i + ivec3(0, 0, 1), f - vec3(0.0, 0.0, 1.0)),
        corner(i + ivec3(1, 0, 1), f - vec3(1.0, 0.0, 1.0)), w.x);
    float x11 = mix(corner(i + ivec3(0, 1, 1), f - vec3(0.0, 1.0, 1.0)),
        corner(i + ivec3(1, 1, 1), f - vec3(1.0, 1.0, 1.0)), w.x);
    return mix(mix(x00, x10, w.y), mix(x01, x11, w.y), w.z);
}
`

/** The background of the scene: a gradient, a glow that breathes with the bass and floating lights. */
export const backgroundFragment = `#version 300 es
precision highp float;
in vec2 vUv;
uniform vec3 uTop;
uniform vec3 uBottom;
uniform vec3 uLight;
uniform vec3 uGlow;
uniform float uTime;
uniform float uAspect;
uniform float uLights;
uniform int uLightCount;
uniform float uBass;
uniform float uMid;
uniform float uPulse;
out vec4 outColor;
void main() {
    vec2 c = (vUv - 0.5) * vec2(uAspect, 1.0);
    vec3 color = mix(uBottom, uTop, smoothstep(0.0, 1.0, vUv.y + 0.06 * sin(uTime * 0.11 + vUv.x * 2.0)));
    color += uGlow * exp(-dot(c, c) * 2.5) * (0.05 + 0.1 * uBass + 0.05 * uPulse);
    for (int i = 0; i < ${shaderLights}; i++) {
        if (i >= uLightCount) {
            break;
        }
        float k = float(i);
        vec2 at = vec2(sin(uTime * (0.031 + 0.007 * k) + k * 2.4) * 0.48 * uAspect,
            sin(uTime * (0.023 + 0.005 * k) + k * 1.7) * 0.42);
        float size = 0.05 + 0.07 * fract(k * 0.618);
        float d = length(c - at) / size;
        color += uLight * exp(-d * d) * uLights * (0.05 + 0.05 * fract(k * 0.37)) * (0.7 + 0.6 * uMid);
    }
    float ray = pow(max(0.0, sin(c.x * 5.0 + c.y * 1.5 + uTime * 0.05)), 12.0) * smoothstep(-0.2, 0.5, c.y);
    color += uLight * ray * 0.03 * uLights;
    outColor = vec4(color, 1.0);
}
`

/**
 * A shape: the directions of a sphere pushed out into an organic surface by turning lobes, flowing
 * waves of noise and the spectrum (the bass at the bottom, the highs at the top). The normal comes
 * from two neighbouring points of the surface, so the light follows every curve.
 */
export const shapeVertex = `#version 300 es
precision highp float;
precision highp int;
layout(location = 0) in vec3 aDirection;
uniform mat4 uModel;
uniform mat4 uViewProjection;
uniform vec4 uLobes;
uniform float uLobePhase;
uniform float uWaves;
uniform float uWaveScale;
uniform float uSpectrumAmount;
uniform float uSpectrum[${spectrumBands}];
uniform vec3 uStretch;
uniform float uFlow;
uniform float uSeed;
uniform int uDetail;
out vec3 vNormal;
out vec3 vPosition;
out vec3 vDirection;
${noise}
vec2 cmul(vec2 a, vec2 b) {
    return vec2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x);
}
vec2 turn(float angle) {
    return vec2(cos(angle), sin(angle));
}
float spectrumAt(float y) {
    float x = clamp(y * 0.5 + 0.5, 0.0, 1.0) * float(${spectrumBands - 1});
    int index = int(floor(x));
    int next = min(index + 1, ${spectrumBands - 1});
    return mix(uSpectrum[index], uSpectrum[next], smoothstep(0.0, 1.0, fract(x)));
}
float radius(vec3 d) {
    vec2 c = d.xz;
    vec2 c2 = cmul(c, c);
    vec2 c3 = cmul(c2, c);
    vec2 c5 = cmul(c3, c2);
    float lobes = uLobes.x * cmul(c2, turn(uLobePhase)).x
        + uLobes.y * cmul(c3, turn(d.y * 2.0 - uLobePhase * 0.7)).x * 1.6
        + uLobes.z * cmul(c5, turn(d.y * 4.0 + uLobePhase * 0.5)).x * 2.5
        + uLobes.w * (4.0 * d.y * d.y * d.y - 3.0 * d.y) * cos(uLobePhase * 0.6 + uSeed);
    vec3 p = d * uWaveScale + vec3(uSeed * 11.3, uFlow * 0.31, uFlow * 0.23);
    float waves = noise(p);
    if (uDetail > 1) {
        waves += 0.25 * noise(p * 1.9 + vec3(5.2, 1.3, uFlow * 0.4));
    }
    float lift = spectrumAt(d.y) * (0.55 + 0.6 * noise(d * 2.0 + vec3(uSeed, 0.0, uFlow * 0.5)));
    return max(0.3, 1.0 + lobes + uWaves * waves + uSpectrumAmount * lift);
}
vec3 surface(vec3 d) {
    return d * radius(d) * uStretch;
}
void main() {
    vec3 d = normalize(aDirection);
    vec3 helper = abs(d.y) < 0.9 ? vec3(0.0, 1.0, 0.0) : vec3(1.0, 0.0, 0.0);
    vec3 t1 = normalize(cross(d, helper));
    vec3 t2 = cross(d, t1);
    vec3 p = surface(d);
    vec3 p1 = surface(normalize(d + t1 * 0.015));
    vec3 p2 = surface(normalize(d + t2 * 0.015));
    vec3 n = normalize(cross(p1 - p, p2 - p));
    if (dot(n, p) < 0.0) {
        n = -n;
    }
    vec4 world = uModel * vec4(p, 1.0);
    vPosition = world.xyz;
    vNormal = mat3(uModel) * n;
    vDirection = d;
    gl_Position = uViewProjection * world;
}
`

/**
 * The light of a shape: a color gradient across the surface, a key and a fill light with their
 * reflections, the mirrored surroundings (stronger at grazing angles) and light at the edges.
 */
export const shapeFragment = `#version 300 es
precision highp float;
in vec3 vNormal;
in vec3 vPosition;
in vec3 vDirection;
uniform vec3 uEye;
uniform vec3 uTop;
uniform vec3 uBottom;
uniform vec3 uSurfaceA;
uniform vec3 uSurfaceB;
uniform vec3 uRim;
uniform vec3 uHighlight;
uniform vec3 uGlow;
uniform float uGloss;
uniform float uMirror;
uniform float uRimStrength;
uniform float uShade;
uniform float uTime;
uniform float uSeed;
uniform float uMid;
uniform float uTreble;
uniform float uPulse;
out vec4 outColor;
vec3 surroundings(vec3 r) {
    float up = r.y * 0.5 + 0.5;
    vec3 sky = mix(uBottom, uTop, up) * 2.5 + uGlow * 0.15 * up;
    float panel = smoothstep(0.35, 1.0, sin(r.y * 4.0 + atan(r.z, r.x) * 2.0 + uTime * 0.07) * 0.5 + 0.5);
    float top = smoothstep(0.45, 1.0, r.y);
    return sky + uHighlight * (panel * 0.35 + top * 0.8);
}
void main() {
    vec3 n = normalize(vNormal);
    vec3 v = normalize(uEye - vPosition);
    float facing = clamp(dot(n, v), 0.0, 1.0);
    float fresnel = pow(1.0 - facing, 3.0);
    float g = 0.5 + 0.5 * sin(vDirection.y * 2.4 + vDirection.x * 1.3 + uShade + uSeed * 1.7);
    vec3 base = mix(uSurfaceA, uSurfaceB, g);
    vec3 key = normalize(vec3(0.45, 0.8, 0.55));
    vec3 fill = normalize(vec3(-0.75, -0.15, 0.4));
    float diffuse = 0.18 + 0.7 * max(dot(n, key), 0.0) + 0.3 * max(dot(n, fill), 0.0);
    float shine = pow(max(dot(n, normalize(key + v)), 0.0), uGloss)
        + 0.5 * pow(max(dot(n, normalize(fill + v)), 0.0), uGloss * 0.6);
    vec3 color = base * diffuse;
    vec3 mirrored = surroundings(reflect(-v, n)) * mix(vec3(1.0), base * 1.5, 0.35);
    color = mix(color, mirrored, uMirror * (0.3 + 0.7 * fresnel));
    color += uHighlight * shine * (0.7 + 0.6 * uTreble);
    color += uRim * fresnel * uRimStrength * (0.55 + 0.6 * uMid);
    color += uGlow * uPulse * 0.12;
    outColor = vec4(color, 1.0);
}
`

/** The scene onto the canvas: a soft glow of bright parts, a vignette and a gentle tone curve. */
export const compositeFragment = `#version 300 es
precision highp float;
in vec2 vUv;
uniform sampler2D uScene;
uniform vec3 uGlow;
uniform float uBloom;
uniform float uAspect;
uniform float uPulse;
out vec4 outColor;
void main() {
    vec2 c = (vUv - 0.5) * vec2(uAspect, 1.0);
    vec3 color = texture(uScene, vUv).rgb;
    if (uBloom > 0.0) {
        vec3 glow = textureLod(uScene, vUv, 2.0).rgb * 0.35 + textureLod(uScene, vUv, 4.0).rgb * 0.45
            + textureLod(uScene, vUv, 5.5).rgb * 0.35;
        color += max(glow - 0.08, 0.0) * uBloom;
    }
    color += uGlow * uPulse * 0.03;
    color *= mix(1.0, 0.6, smoothstep(0.35, 1.2, length(c)));
    outColor = vec4(1.0 - exp(-color * 1.3), 1.0);
}
`
