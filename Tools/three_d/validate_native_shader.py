#!/usr/bin/env python3
"""Compile and link the actual Robust-generated native scene shaders in a hidden SDL GL context.

Requires a built Content.Client, PowerShell 7 on .NET matching that build, and its bundled SDL3.
Uses only Python's standard library; does not build, change engine files, or access a browser.
The validation covers desktop GLSL 140 with and without uniform buffers and native sRGB support,
including a 65x65 rendered-pixel smoke test against actual C# encoding and CPU picking.
Optional benchmarks measure isolated GPU draws and CPU picking, not full game frame rate.
Neither validation nor benchmarks establish complete scene accuracy or gameplay visibility.
"""
from __future__ import annotations

import argparse
import base64
import ctypes as ct
import json
from pathlib import Path
import platform
import shutil
import subprocess
import struct
import statistics
import sys


def render_smoke(gl, program, smoke, native_srgb, uniform_buffers):
    """Render the actual engine-generated shader with C# packed bytes and check known rays."""
    uint = ct.c_uint
    integer = ct.c_int
    pointer = ct.c_void_p
    use_program = gl('glUseProgram', None, uint)
    location = gl('glGetUniformLocation', integer, uint, ct.c_char_p)
    uniform1i = gl('glUniform1i', None, integer, integer)
    uniform1f = gl('glUniform1f', None, integer, ct.c_float)
    uniform2f = gl('glUniform2f', None, integer, ct.c_float, ct.c_float)
    uniform3f = gl('glUniform3f', None, integer, ct.c_float, ct.c_float, ct.c_float)
    uniform4f = gl('glUniform4f', None, integer, ct.c_float, ct.c_float, ct.c_float, ct.c_float)
    matrix3 = gl('glUniformMatrix3fv', None, integer, integer, ct.c_ubyte, pointer)
    texture_image = gl('glTexImage2D', None, uint, integer, integer, integer, integer, integer, uint, uint, pointer)
    texture_parameter = gl('glTexParameteri', None, uint, uint, integer)
    bind_texture = gl('glBindTexture', None, uint, uint)
    active_texture = gl('glActiveTexture', None, uint)
    bind_framebuffer = gl('glBindFramebuffer', None, uint, uint)
    attach_texture = gl('glFramebufferTexture2D', None, uint, uint, uint, uint, integer)
    framebuffer_status = gl('glCheckFramebufferStatus', uint, uint)
    bind_buffer = gl('glBindBuffer', None, uint, uint)
    buffer_data = gl('glBufferData', None, uint, ct.c_ssize_t, pointer, uint)
    bind_array = gl('glBindVertexArray', None, uint)
    attribute_location = gl('glGetAttribLocation', integer, uint, ct.c_char_p)
    attribute_pointer = gl('glVertexAttribPointer', None, uint, integer, uint, ct.c_ubyte, integer, pointer)
    enable_attribute = gl('glEnableVertexAttribArray', None, uint)
    viewport = gl('glViewport', None, integer, integer, integer, integer)
    draw = gl('glDrawArrays', None, uint, integer, integer)
    read_pixels = gl('glReadPixels', None, integer, integer, integer, integer, uint, uint, pointer)
    get_error = gl('glGetError', uint)
    enable = gl('glEnable', None, uint)
    disable = gl('glDisable', None, uint)
    allocations = []

    def allocate(kind):
        handle = uint()
        gl(f'glGen{kind}', None, integer, ct.POINTER(uint))(1, ct.byref(handle))
        allocations.append((kind, handle))
        return handle.value

    def upload_texture(width, height, data, internal=0x8058):
        handle = allocate('Textures')
        bind_texture(0x0DE1, handle)
        texture_parameter(0x0DE1, 0x2801, 0x2600)  # nearest, no mipmaps
        texture_parameter(0x0DE1, 0x2800, 0x2600)
        texture_parameter(0x0DE1, 0x2802, 0x812F)  # clamp-to-edge
        texture_parameter(0x0DE1, 0x2803, 0x812F)
        buffer = ct.create_string_buffer(data) if data is not None else None
        texture_image(0x0DE1, 0, internal, width, height, 0, 0x1908, 0x1401, buffer)
        return handle

    def uniform(name):
        return location(program, name.encode())

    def upload_block(name, binding, values):
        index = gl('glGetUniformBlockIndex', uint, uint, ct.c_char_p)(program, name.encode())
        if index == 0xFFFFFFFF:
            raise RuntimeError(f'Expected engine uniform block was optimized out: {name}')
        handle = allocate('Buffers')
        bind_buffer(0x8A11, handle)
        data = (ct.c_float * len(values))(*values)
        buffer_data(0x8A11, ct.sizeof(data), data, 0x88E4)
        gl('glBindBufferBase', None, uint, uint, uint)(0x8A11, binding, handle)
        gl('glUniformBlockBinding', None, uint, uint, uint)(program, index, binding)

    try:
        width, height = smoke['width'], smoke['height']
        target = upload_texture(width, height, None, 0x8C43 if native_srgb else 0x8058)
        framebuffer = allocate('Framebuffers')
        bind_framebuffer(0x8D40, framebuffer)
        attach_texture(0x8D40, 0x8CE0, 0x0DE1, target, 0)
        if framebuffer_status(0x8D40) != 0x8CD5:
            raise RuntimeError('Smoke framebuffer is incomplete.')
        use_program(program)
        for unit, name, key, rows in ((0, 'sceneBoxes', 'boxPixels', smoke['boxRows']), (1, 'sceneGrid', 'gridPixels', smoke['gridRows'])):
            raw = base64.b64decode(smoke[key], validate=True)
            texture_width = smoke['gridWidth'] if key == 'gridPixels' else 256
            stride = texture_width * 4
            if len(raw) != stride * rows:
                raise RuntimeError(f'Unexpected actual encoder texture dimensions: {name}')
            # Clyde uploads image rows in reverse order. Reproduce that path before shader sampling.
            flipped = b''.join(raw[row * stride:(row + 1) * stride] for row in reversed(range(rows)))
            active_texture(0x84C0 + unit)
            upload_texture(texture_width, rows, flipped)
            uniform1i(uniform(name), unit)
        active_texture(0x84C2)
        upload_texture(1, 1, b'\xff\xff\xff\xff')
        uniform1i(uniform('lightMap'), 2)
        uniform1i(uniform('TEXTURE'), 2)
        # Use the actual C# atlas, while expected source colors remain independent fixture values.
        atlas_width, atlas_height = smoke.get('surfaceWidth', 1), smoke.get('surfaceHeight', 1)
        raw_atlas = base64.b64decode(smoke['surfacePixels'], validate=True) if 'surfacePixels' in smoke else b'\xff' * 4
        stride = atlas_width * 4
        if len(raw_atlas) != stride * atlas_height:
            raise RuntimeError('Unexpected actual surface atlas dimensions.')
        atlas = b''.join(raw_atlas[row*stride:(row+1)*stride] for row in reversed(range(atlas_height)))
        active_texture(0x84C3)
        upload_texture(atlas_width,atlas_height,bytes(atlas))
        uniform1i(uniform('sceneSurfaces'),3)
        uniform2f(uniform('surfaceAtlasSize'),atlas_width,atlas_height)
        info = base64.b64decode(smoke['surfaceInformation'], validate=True) if 'surfaceInformation' in smoke else bytes(4096*4)
        if len(info) != 4096 * 4:
            raise RuntimeError('Unexpected actual surface information dimensions.')
        active_texture(0x84C4)
        upload_texture(4096,1,bytes(info))
        uniform1i(uniform('sceneSurfaceInfo'),4)
        for name, key in (('cameraOrigin', 'origin'), ('cameraForward', 'forward'), ('cameraRight', 'right'), ('cameraUp', 'up')):
            uniform3f(uniform(name), *smoke[key])
        uniform2f(uniform('sceneZ'), *smoke['sceneZ'])
        uniform1f(uniform('aspect'), width / height)
        uniform1f(uniform('tanHalfFov'), smoke.get('tanHalfFov', 0.414213562))
        uniform4f(uniform('gridLayout'), *smoke['gridLayout'])
        uniform3f(uniform('gridZ'), *smoke.get('gridZ', (-80, 160, 1)))
        uniform1f(uniform('sceneRadius'), smoke.get('radius', 8))
        uniform3f(uniform('sceneView'), *smoke.get('view', [0, 0, smoke.get('radius', 8)]))
        uniform1f(uniform('useLiveLight'), 0)
        uniform1f(uniform('brightness'), 1)
        uniform1f(uniform('billboardCount'), 0)
        if 'billboard' in smoke:
            billboard = smoke['billboard']
            packed = struct.pack('<8H', *(round(v * 1024 + 32768) for v in (billboard.get('x', 0), billboard['y'], billboard.get('z', 1.3), 1, 2,
                                                                       billboard.get('yaw', 0), billboard.get('tilt', 0), billboard.get('emissive', 0))))
            slot = billboard.get('slot', 0)
            columns = 16 if slot >= 64 else 8
            capacity = columns * columns
            data = bytearray(capacity * 4 * 4)
            data[slot * 16:slot * 16 + len(packed)] = packed
            active_texture(0x84C5)
            upload_texture(capacity * 4, 1, bytes(data))
            uniform2f(uniform('billboardLayout'), columns, capacity * 4)
            uniform1i(uniform('billboardData'), 5)
            atlas_size = columns * 2
            atlas = bytearray(atlas_size * atlas_size * 4)
            for y in range(2):
                for x in range(2):
                    if billboard.get('cutout') and x == 1:
                        continue
                    offset = ((atlas_size - 1 - (slot // columns) * 2 - y) * atlas_size + (slot % columns) * 2 + x) * 4
                    atlas[offset:offset + 4] = bytes((0, 255, 0, billboard.get('alpha', 255)))
            active_texture(0x84C6)
            upload_texture(atlas_size, atlas_size, bytes(atlas))
            uniform1i(uniform('spriteAtlas'), 6)
            uniform1f(uniform('billboardCount'), slot + 1)
        if smoke.get('darkLight'):
            active_texture(0x84C7)
            upload_texture(1, 1, bytes((0, 0, 0, 255)))
            uniform1i(uniform('liveLight'), 7)
            uniform1f(uniform('useLiveLight'), 1)
        uniform1f(uniform('selectedBox'), 0)
        uniform4f(uniform('modifyUV'), 0, 0, 1, 1)
        uniform4f(uniform('SCREEN_UV_RECT'), 0, 0, 1, 1)
        identity = (ct.c_float * 9)(1, 0, 0, 0, 1, 0, 0, 0, 1)
        matrix3(uniform('modelMatrix'), 1, 0, identity)
        if uniform_buffers:
            upload_block('projectionViewMatrices', 0, [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0] * 2)
            upload_block('uniformConstants', 1, [1 / width, 1 / height, 0, 0])
        else:
            matrix3(uniform('projectionMatrix'), 1, 0, identity)
            matrix3(uniform('viewMatrix'), 1, 0, identity)
            uniform2f(uniform('SCREEN_PIXEL_SIZE'), 1 / width, 1 / height)
        if native_srgb:
            enable(0x8DB9)  # GL_FRAMEBUFFER_SRGB
        else:
            disable(0x8DB9)
            uniform2f(uniform('SRGB_EMU_CONFIG'), 0, 1)
        bind_array(allocate('VertexArrays'))
        bind_buffer(0x8892, allocate('Buffers'))
        # Full-screen triangle: position, unused UV, quad UV, unshaded white modulation.
        vertices = (ct.c_float * 30)(-1, -1, 0, 0, 0, 0, -2, -2, -2, -2,
                                     3, -1, 2, 0, 2, 0, -2, -2, -2, -2,
                                    -1, 3, 0, 2, 0, 2, -2, -2, -2, -2)
        buffer_data(0x8892, ct.sizeof(vertices), vertices, 0x88E4)
        for name, count, offset in ((b'aPos', 2, 0), (b'tCoord', 2, 8), (b'tCoord2', 2, 16), (b'modulate', 4, 24)):
            index = attribute_location(program, name)
            if index >= 0:
                enable_attribute(index)
                attribute_pointer(index, count, 0x1406, 0, 40, pointer(offset))
        viewport(0, 0, width, height)
        draw(0x0004, 0, 3)
        timing = {key: smoke[key] for key in ('cpuPickMilliseconds', 'cpuPickSamples') if key in smoke}
        if smoke.get('benchmark'):
            query = allocate('Queries')
            begin = gl('glBeginQuery', None, uint, uint)
            end = gl('glEndQuery', None, uint)
            result = gl('glGetQueryObjectui64v', None, uint, uint, ct.POINTER(ct.c_uint64))
            elapsed = []
            for iteration in range(15):
                begin(0x88BF, query)  # GL_TIME_ELAPSED: GPU execution, not command submission time.
                draw(0x0004, 0, 3)
                end(0x88BF)
                nanoseconds = ct.c_uint64()
                result(query, 0x8866, ct.byref(nanoseconds))
                if iteration >= 3:
                    elapsed.append(nanoseconds.value / 1_000_000)
            timing.update({'gpuMillisecondsMedian': statistics.median(elapsed), 'gpuMillisecondsMax': max(elapsed),
                           'gpuSamples': len(elapsed)})
        results = []
        for sample in smoke['samples']:
            color = (ct.c_ubyte * 4)()
            read_pixels(*sample['pixel'], 1, 1, 0x1908, 0x1401, color)
            rgba = list(color)
            expected = sample['cpuBoxIndex']
            passed = (rgba[0] > 80 and max(rgba[1:3]) < 8) if expected == 1 else (
                rgba[2] > 80 and max(rgba[:2]) < 8) if expected == 0 else max(rgba[:3]) < 60
            if 'expectedRGB' in sample:
                passed = all(actual > 80 if wanted else actual < 8
                             for actual,wanted in zip(rgba[:3],sample['expectedRGB']))
            if not passed or rgba[3] != 255:
                raise RuntimeError(f'GPU/CPU smoke mismatch at {sample["name"]}: CPU box {expected}, GPU RGBA {rgba}')
            results.append({**sample, 'rgba': rgba, 'passed': True})
        error = get_error()
        if error:
            raise RuntimeError(f'OpenGL error after smoke render: 0x{error:04x}')
        return {'name': smoke.get('name'), 'targetSize': [width, height], 'surfaceFlipU': smoke.get('flipU'),
                'surfaceVScale': smoke.get('vScale'), 'surfaceSlot': smoke.get('surfaceSlot'),
                **{key: smoke[key] for key in ('cellCapacity', 'acceptedBoxes', 'omittedBoxes') if key in smoke},
                'samples': results, **timing}
    finally:
        bind_framebuffer(0x8D40, 0)
        bind_array(0)
        use_program(0)
        for kind, handle in reversed(allocations):
            gl(f'glDelete{kind}', None, integer, ct.POINTER(uint))(1, ct.byref(handle))


def validate(root: Path, powershell: str, sdl_path: Path, benchmark: bool = False,
             scene_path: Path | None = None, shader: str | None = None) -> dict:
    command = [powershell, '-NoLogo', '-NoProfile', '-File',
               str(root / 'Tools/three_d/parse_native_shader.ps1'), '-Root', str(root)]
    if benchmark:
        command.append('-Benchmark')
    if shader:
        command.extend(['-Shader', shader])
    process = subprocess.run(command, capture_output=True, text=True, timeout=120)
    if process.returncode:
        raise RuntimeError(f'Robust shader parsing/code generation failed:\n{process.stderr}')
    parsed = json.loads(process.stdout)
    if scene_path:
        scene = json.loads(scene_path.read_text(encoding='utf-8'))
        scene['benchmark'] = True
        parsed['benchmarkScenes'] = [scene]
    sdl = ct.CDLL(str(sdl_path))

    def sdl_function(name, result, *arguments):
        function = getattr(sdl, name)
        function.restype = result
        function.argtypes = list(arguments)
        return function

    error = sdl_function('SDL_GetError', ct.c_char_p)
    initialize = sdl_function('SDL_Init', ct.c_bool, ct.c_uint32)
    quit_sdl = sdl_function('SDL_Quit', None)
    attribute = sdl_function('SDL_GL_SetAttribute', ct.c_bool, ct.c_int, ct.c_int)
    create_window = sdl_function('SDL_CreateWindow', ct.c_void_p, ct.c_char_p, ct.c_int, ct.c_int, ct.c_uint64)
    destroy_window = sdl_function('SDL_DestroyWindow', None, ct.c_void_p)
    create_context = sdl_function('SDL_GL_CreateContext', ct.c_void_p, ct.c_void_p)
    destroy_context = sdl_function('SDL_GL_DestroyContext', ct.c_bool, ct.c_void_p)
    proc_address = sdl_function('SDL_GL_GetProcAddress', ct.c_void_p, ct.c_char_p)
    if not initialize(0x20):  # SDL_INIT_VIDEO
        raise RuntimeError(f'SDL video initialization failed: {error().decode()}')
    window = context = None
    try:
        for key, value in ((17, 3), (18, 3), (21, 1)):  # OpenGL 3.3 core profile
            if not attribute(key, value):
                raise RuntimeError(f'SDL GL attribute failed: {error().decode()}')
        window = create_window(b'CMU native shader validation (hidden)', 16, 16, 0x2 | 0x8)
        if not window:
            raise RuntimeError(f'Hidden SDL window failed: {error().decode()}')
        context = create_context(window)
        if not context:
            raise RuntimeError(f'Native OpenGL context failed: {error().decode()}')

        def gl(name, result, *arguments):
            address = proc_address(name.encode())
            if not address:
                raise RuntimeError(f'OpenGL entry point unavailable: {name}')
            convention = ct.WINFUNCTYPE if sys.platform == 'win32' else ct.CFUNCTYPE
            return convention(result, *arguments)(address)

        get_string = gl('glGetString', ct.c_char_p, ct.c_uint)
        create_shader = gl('glCreateShader', ct.c_uint, ct.c_uint)
        shader_source = gl('glShaderSource', None, ct.c_uint, ct.c_int, ct.POINTER(ct.c_char_p), ct.POINTER(ct.c_int))
        compile_shader = gl('glCompileShader', None, ct.c_uint)
        shader_status = gl('glGetShaderiv', None, ct.c_uint, ct.c_uint, ct.POINTER(ct.c_int))
        shader_log = gl('glGetShaderInfoLog', None, ct.c_uint, ct.c_int, ct.POINTER(ct.c_int), ct.c_void_p)
        delete_shader = gl('glDeleteShader', None, ct.c_uint)
        create_program = gl('glCreateProgram', ct.c_uint)
        attach_shader = gl('glAttachShader', None, ct.c_uint, ct.c_uint)
        link_program = gl('glLinkProgram', None, ct.c_uint)
        program_status = gl('glGetProgramiv', None, ct.c_uint, ct.c_uint, ct.POINTER(ct.c_int))
        program_log = gl('glGetProgramInfoLog', None, ct.c_uint, ct.c_int, ct.POINTER(ct.c_int), ct.c_void_p)
        delete_program = gl('glDeleteProgram', None, ct.c_uint)

        def check(handle, status, log, flag, label):
            success = ct.c_int()
            status(handle, flag, ct.byref(success))
            length = ct.c_int()
            status(handle, 0x8B84, ct.byref(length))  # GL_INFO_LOG_LENGTH
            buffer = ct.create_string_buffer(max(1, length.value))
            log(handle, len(buffer), None, buffer)
            message = buffer.value.decode(errors='replace').strip()
            if not success.value:
                raise RuntimeError(f'{label} failed:\n{message}')
            return message

        variants = []
        for native_srgb, uniform_buffers in ((False, False), (True, True)):
            header = '#version 140\n#define HAS_MOD\n#define HAS_DFDX\n#define HAS_FLOAT_TEXTURES\n'
            if native_srgb:
                header += '#define HAS_SRGB\n'
            if uniform_buffers:
                header += '#define HAS_UNIFORM_BUFFERS\n'
            shaders = []
            program = 0
            warnings = []
            try:
                for name, kind, define in (('vertex', 0x8B31, 'VERTEX_SHADER'), ('fragment', 0x8B30, 'FRAGMENT_SHADER')):
                    source = header + f'#define {define}\n' + parsed['library'] + parsed[name]
                    handle = create_shader(kind)
                    shaders.append(handle)
                    text = ct.c_char_p(source.encode())
                    shader_source(handle, 1, ct.byref(text), None)
                    compile_shader(handle)
                    warning = check(handle, shader_status, shader_log, 0x8B81, f'{name} shader')
                    if warning:
                        warnings.append(warning)
                program = create_program()
                for handle in shaders:
                    attach_shader(program, handle)
                link_program(program)
                warning = check(program, program_status, program_log, 0x8B82, 'shader program link')
                if warning:
                    warnings.append(warning)
                if benchmark or scene_path:
                    variants.append({'glsl': '140', 'nativeSrgb': native_srgb, 'uniformBuffers': uniform_buffers,
                                     'benchmarks': [render_smoke(gl, program, scene, native_srgb, uniform_buffers)
                                                    for scene in parsed['benchmarkScenes']]})
                    continue
                variants.append({'glsl': '140', 'nativeSrgb': native_srgb,
                                 'uniformBuffers': uniform_buffers, 'compiled': True, 'linked': True, 'warnings': warnings,
                                 'pixelSmoke': render_smoke(gl, program, parsed['smoke'], native_srgb, uniform_buffers)})
                variants[-1]['surfaceSmokes'] = [render_smoke(gl, program, scene, native_srgb, uniform_buffers)
                                                for scene in parsed['surfaceSmokes']]
                variants[-1]['cylinderSmokes'] = [render_smoke(gl, program, scene, native_srgb, uniform_buffers)
                                                 for scene in parsed['cylinderSmokes']]
                variants[-1]['alphaSmokes'] = [render_smoke(gl, program, scene, native_srgb, uniform_buffers)
                                              for scene in parsed['alphaSmokes']]
                variants[-1]['wedgeSmokes'] = [render_smoke(gl, program, scene, native_srgb, uniform_buffers)
                                              for scene in parsed['wedgeSmokes']]
                variants[-1]['slantedSmokes'] = [render_smoke(gl, program, scene, native_srgb, uniform_buffers)
                                                for scene in parsed['slantedSmokes']]
                variants[-1]['foliageSmokes'] = [render_smoke(gl, program, scene, native_srgb, uniform_buffers)
                                                for scene in parsed['foliageSmokes']]
                variants[-1]['tiltSmokes'] = [render_smoke(gl, program, scene, native_srgb, uniform_buffers)
                                                for scene in parsed['tiltSmokes']]
                variants[-1]['extendedSmoke'] = render_smoke(gl, program, parsed['extendedSmoke'], native_srgb, uniform_buffers)
                # The camera can move inside a larger packed scene without recentering it.
                # Shifting the visible square away must hide already-packed geometry.
                buffered = dict(parsed['smoke'])
                buffered['view'] = [20, 0, 4]
                buffered['samples'] = [{'name': 'buffered-outside-view', 'pixel': [32, 32], 'cpuBoxIndex': -1}]
                variants[-1]['bufferedViewSmoke'] = render_smoke(gl, program, buffered, native_srgb, uniform_buffers)
                variants[-1]['cellCapacitySmokes'] = [render_smoke(gl, program, scene, native_srgb, uniform_buffers)
                                                     for scene in parsed['cellCapacitySmokes']]
                billboard_smokes = []
                for name, y, yaw, cutout, samples in (
                    ('sprite-in-front', -2, 0, False, [(32, (0, 255, 0))]),
                    ('sprite-fixed-edge-on', -2, 1.570796327, False, [(32, (255, 0, 0))]),
                    ('sprite-oblique', -2, .785398163, False, [(32, (0, 255, 0))]),
                    ('sprite-behind-solid', 1, 0, False, [(32, (255, 0, 0))]),
                    ('sprite-alpha-cutout', -2, 0, True, [(30, (0, 255, 0)), (32, (255, 0, 0))]),
                ):
                    scene = dict(parsed['smoke'], name=name, billboard={'x': .25 if name == 'sprite-fixed-edge-on' else 0, 'y': y, 'yaw': yaw, 'cutout': cutout},
                                 samples=[{'name': name, 'pixel': [x, 32], 'cpuBoxIndex': 1, 'expectedRGB': rgb} for x, rgb in samples])
                    billboard_smokes.append(render_smoke(gl, program, scene, native_srgb, uniform_buffers))
                late_sprite = dict(parsed['smoke'], name='sprite-beyond-old-budget', billboard={'y': -2, 'slot': 200},
                                   samples=[{'name': 'sprite-beyond-old-budget', 'pixel': [32, 32], 'cpuBoxIndex': 1, 'expectedRGB': (0, 255, 0)}])
                billboard_smokes.append(render_smoke(gl, program, late_sprite, native_srgb, uniform_buffers))
                for name, settings, dark in (
                    ('combat-tilted-plane', {'tilt': .785398163}, False),
                    ('combat-emissive-in-darkness', {'emissive': 1}, True),
                    ('combat-fade-below-half-alpha', {'alpha': 80}, False),
                ):
                    scene = dict(parsed['smoke'], name=name, billboard=dict(y=-2, **settings), darkLight=dark,
                                 samples=[{'name': name, 'pixel': [32, 32], 'cpuBoxIndex': 1, 'expectedRGB': (0, 255, 0)}])
                    billboard_smokes.append(render_smoke(gl, program, scene, native_srgb, uniform_buffers))
                variants[-1]['billboardSmokes'] = billboard_smokes
                ground_sprite = dict(parsed['smoke'], name='ground-sprite',
                                     origin=[0, -2, 2], forward=[0, 0, -1], right=[1, 0, 0], up=[0, 1, 0],
                                     billboard={'y': -2, 'z': .01, 'tilt': -1.570796327, 'cutout': True},
                                     samples=[{'name': 'opaque-ground-pixel', 'pixel': [25, 32], 'cpuBoxIndex': -1,
                                               'expectedRGB': (0, 255, 0)},
                                              {'name': 'transparent-ground-pixel', 'pixel': [39, 32], 'cpuBoxIndex': -1}])
                variants[-1]['groundSpriteSmoke'] = render_smoke(gl, program, ground_sprite, native_srgb, uniform_buffers)
                dark = dict(parsed['smoke'], name='live-light-darkness', darkLight=True,
                            samples=[{'name': 'dark-lit-surface', 'pixel': [32, 32], 'cpuBoxIndex': 1, 'expectedRGB': [0, 0, 0]}])
                variants[-1]['lightingSmoke'] = render_smoke(gl, program, dark, native_srgb, uniform_buffers)
            finally:
                if program:
                    delete_program(program)
                for handle in shaders:
                    delete_shader(handle)
        return {'parser': parsed['parser'], 'assemblyVersion': parsed['assemblyVersion'],
                'shaderSha256': parsed['shaderSha256'], 'uniforms': parsed['uniforms'],
                'vendor': get_string(0x1F00).decode(), 'renderer': get_string(0x1F01).decode(),
                'openGL': get_string(0x1F02).decode(), 'variants': variants}
    finally:
        if context:
            destroy_context(context)
        if window:
            destroy_window(window)
        quit_sdl()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--powershell', default=shutil.which('pwsh'))
    parser.add_argument('--sdl', type=Path, help='Override the built client SDL3 native library.')
    parser.add_argument('--benchmark', action='store_true', help='Measure the production shader on stacked scenes using GPU timer queries.')
    parser.add_argument('--scene', type=Path, help='Benchmark an exported native packed scene fixture.')
    parser.add_argument('--shader', help='Shader source path relative to root, for before/after comparisons.')
    arguments = parser.parse_args()
    if not arguments.powershell:
        parser.error('PowerShell 7 is required; pass --powershell with its executable path.')
    machine = platform.machine().lower()
    architecture = 'arm64' if machine in ('arm64', 'aarch64') else 'x64'
    system, name = ('win', 'SDL3.dll') if sys.platform == 'win32' else ('osx', 'libSDL3.0.dylib') if sys.platform == 'darwin' else ('linux', 'libSDL3.so.0')
    sdl = arguments.sdl or arguments.root / f'bin/Content.Client/runtimes/{system}-{architecture}/native/{name}'
    print(json.dumps(validate(arguments.root.resolve(), arguments.powershell, sdl.resolve(), arguments.benchmark,
                              arguments.scene, arguments.shader), indent=2))


if __name__ == '__main__':
    main()
