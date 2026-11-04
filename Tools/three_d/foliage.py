"""Deterministic open foliage spray, contained by the unit ellipsoid it replaces.

The nineteen tapered leaves have distinct rotations and sizes. Each analytic
ellipsoid is contained in the radius-.5 sphere by the triangle inequality;
part scaling therefore cannot expand the previous ellipsoid's occupied volume.
Hidden leaf arrangement is inferred, not reconstructed from sprite depth.
"""
import math
from functools import cache


def lobes():
    """Yield center, orthonormal basis columns and diameters in +/- .5 space."""
    for i in range(19):
        phi = i * 2.399963229728653
        z = .68 * (i / 18 - .5)
        radius = .34 * math.sqrt(max(0, 1 - (z / .39) ** 2))
        center = (radius * math.cos(phi), radius * math.sin(phi), z)
        tilt = .55 + .25 * math.sin(i * 1.7)
        c, s, ct, st = math.cos(phi), math.sin(phi), math.cos(tilt), math.sin(tilt)
        axes = ((c * ct, s * ct, -st), (-s, c, 0), (c * st, s * st, ct))
        size = (.24 + .045 * math.sin(i * 2), .10, .39 + .05 * math.cos(i))
        factor = min(1, .98 * (.5 - math.sqrt(sum(v*v for v in center))) / (max(size) / 2))
        yield center, axes, tuple(v * factor for v in size)


@cache
def foliage_geometry():
    from primitives import ellipsoid_geometry
    vertices, normals, indices = ellipsoid_geometry(8, 4)
    positions, transformed, triangles = [], [], []
    for center, axes, size in lobes():
        start = len(positions)
        for point, normal in zip(vertices, normals):
            positions.append(tuple(center[k] + sum(axes[j][k]*size[j]*point[j] for j in range(3)) for k in range(3)))
            n = tuple(sum(axes[j][k]*normal[j]/size[j] for j in range(3)) for k in range(3))
            length = math.sqrt(sum(v*v for v in n))
            transformed.append(tuple(v/length for v in n))
        triangles.extend(start + index for index in indices)
    return tuple(positions), tuple(transformed), tuple(triangles)
