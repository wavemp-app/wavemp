// Helpers of the WebGL 2 renderers of the own visualizations (A-Force, BlackCap): shaders, programs
// and resources that fail loudly, so the controller shows the visualization as failed.

/** A linked program with a cache of its uniform locations. */
export interface Program {
    readonly program: WebGLProgram
    uniform(name: string): WebGLUniformLocation | null
}

/** The resource, or an error if WebGL could not create it (e.g. a lost context). */
export function requireResource<T>(value: T | null): T {
    if (value === null) {
        throw new Error('WebGL could not create a resource.')
    }

    return value
}

/** Compiles a shader; owner names the visualization in the error. */
export function compileShader(
    gl: WebGL2RenderingContext,
    type: number,
    source: string,
    owner: string,
): WebGLShader {
    const shader = gl.createShader(type)
    if (!shader) {
        throw new Error('WebGL could not create a shader.')
    }

    gl.shaderSource(shader, source)
    gl.compileShader(shader)
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS) && !gl.isContextLost()) {
        const log = gl.getShaderInfoLog(shader)
        gl.deleteShader(shader)
        throw new Error(`A shader of ${owner} does not compile: ${log ?? ''}`)
    }

    return shader
}

/** Compiles and links a program of a vertex and a fragment shader; owner names the visualization. */
export function createProgram(
    gl: WebGL2RenderingContext,
    vertex: string,
    fragment: string,
    owner: string,
): Program {
    const program = gl.createProgram()
    if (!program) {
        throw new Error('WebGL could not create a program.')
    }

    const vertexShader = compileShader(gl, gl.VERTEX_SHADER, vertex, owner)
    const fragmentShader = compileShader(gl, gl.FRAGMENT_SHADER, fragment, owner)
    gl.attachShader(program, vertexShader)
    gl.attachShader(program, fragmentShader)
    gl.linkProgram(program)
    gl.deleteShader(vertexShader)
    gl.deleteShader(fragmentShader)
    if (!gl.getProgramParameter(program, gl.LINK_STATUS) && !gl.isContextLost()) {
        const log = gl.getProgramInfoLog(program)
        gl.deleteProgram(program)
        throw new Error(`A program of ${owner} does not link: ${log ?? ''}`)
    }

    const uniforms = new Map<string, WebGLUniformLocation | null>()
    return {
        program,
        uniform: (name) => {
            if (!uniforms.has(name)) {
                uniforms.set(name, gl.getUniformLocation(program, name))
            }

            return uniforms.get(name) ?? null
        },
    }
}

/** A triangle that covers the whole target; needs no vertex buffer (draw 3 vertices). */
export const fullscreenVertex = `#version 300 es
out vec2 vUv;
void main() {
    vec2 position = vec2(float((gl_VertexID << 1) & 2), float(gl_VertexID & 2));
    vUv = position;
    gl_Position = vec4(position * 2.0 - 1.0, 0.0, 1.0);
}
`
