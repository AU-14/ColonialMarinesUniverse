"""Unit solid meshes: outward winding, smooth sphere normals, bounds +/- .5."""
import math


def ellipsoid_geometry(segments=16, rings=8):
    positions = [(0, 0, .5)]
    for ring in range(1, rings):
        phi = math.pi * ring / rings
        for segment in range(segments):
            theta = math.tau * segment / segments
            positions.append((.5 * math.sin(phi) * math.cos(theta),
                              .5 * math.sin(phi) * math.sin(theta), .5 * math.cos(phi)))
    positions.append((0, 0, -.5))
    normals = [tuple(2 * value for value in point) for point in positions]
    indices = []
    for segment in range(segments):
        next_segment = (segment + 1) % segments
        indices.extend((0, 1 + segment, 1 + next_segment))
        for ring in range(rings - 2):
            a = 1 + ring * segments + segment
            b = 1 + ring * segments + next_segment
            c, d = a + segments, b + segments
            indices.extend((a, c, b, b, c, d))
        bottom = len(positions) - 1
        a = 1 + (rings - 2) * segments + segment
        b = 1 + (rings - 2) * segments + next_segment
        indices.extend((a, bottom, b))
    return positions, normals, indices


def triangle_count(parts):
    return sum(912 if part.get('shape') == 'Foliage' else 224 if part.get('shape') == 'Ellipsoid' or part.get('shape', '').startswith('Slanted') else 64 if part.get('shape', '').startswith('Cylinder') else 8 if part.get('shape') in ('WedgeY', 'WedgeYReverse') else 12 for part in parts)


def slanted_geometry(axis='X', reverse=False):
    """Continuous tapered leaf/stem; sheared ellipsoid inscribed in the same bounds.

    Correlation +/- .85 tilts the selected horizontal axis with Z. Transforming
    normals by the inverse transpose keeps lighting consistent with the ray solid.
    """
    if axis not in ('X', 'Y'):
        raise ValueError('Slanted axis must be X or Y')
    index = 'XY'.index(axis)
    shear = -.85 if reverse else .85
    scale = math.sqrt(1 - shear * shear)
    points, normals, indices = ellipsoid_geometry()
    positions, transformed_normals = [], []
    for point, normal in zip(points, normals):
        p, n = list(point), list(normal)
        p[index] = scale * point[index] + shear * point[2]
        n[index] = normal[index] / scale
        n[2] = normal[2] - shear * normal[index] / scale
        length = math.sqrt(sum(v*v for v in n))
        positions.append(tuple(p))
        transformed_normals.append(tuple(v/length for v in n))
    return positions, transformed_normals, indices


def wedge_geometry(reverse=False):
    """Triangular prism rising from -Y to +Y; normalized solid satisfies z <= y."""
    a,b,c,d,e,f = (-.5,-.5,-.5),(.5,-.5,-.5),(-.5,.5,-.5),(.5,.5,-.5),(-.5,.5,.5),(.5,.5,.5)
    faces = [((0,0,-1),(a,c,d,b)),((0,1,0),(c,e,f,d)),
             ((-1,0,0),(a,e,c)),((1,0,0),(b,d,f)),
             ((0,-2**-.5,2**-.5),(a,b,f,e))]
    positions,normals,indices = [],[],[]
    for normal,points in faces:
        start=len(positions);positions.extend(points);normals.extend([normal]*len(points))
        indices.extend((start,start+1,start+2))
        if len(points)==4:indices.extend((start,start+2,start+3))
    if reverse:
        positions=[(x,-y,z) for x,y,z in positions]
        normals=[(x,-y,z) for x,y,z in normals]
        indices=[v for i in range(0,len(indices),3) for v in (indices[i],indices[i+2],indices[i+1])]
    return positions,normals,indices


def cylinder_geometry(axis='Z', segments=16):
    """Capped elliptical cylinder after bounds scaling; separate cap and side normals."""
    if axis not in ('X', 'Y', 'Z'):
        raise ValueError('Cylinder axis must be X, Y or Z')
    def orient(p):
        x,y,z=p
        return (z,x,y) if axis=='X' else (y,z,x) if axis=='Y' else p
    positions, normals, indices = [], [], []
    def vertex(p,n):
        positions.append(orient(p)); normals.append(orient(n))
        return len(positions)-1
    for i in range(segments):
        a,b=math.tau*i/segments,math.tau*(i+1)/segments
        ca,sa,cb,sb=math.cos(a),math.sin(a),math.cos(b),math.sin(b)
        start=len(positions)
        for p,n in [((.5*ca,.5*sa,-.5),(ca,sa,0)),((.5*cb,.5*sb,-.5),(cb,sb,0)),
                    ((.5*cb,.5*sb,.5),(cb,sb,0)),((.5*ca,.5*sa,.5),(ca,sa,0))]: vertex(p,n)
        indices.extend((start,start+1,start+2,start,start+2,start+3))
        for z,normal in ((.5,(0,0,1)),(-.5,(0,0,-1))):
            center=vertex((0,0,z),normal)
            va=vertex((.5*ca,.5*sa,z),normal);vb=vertex((.5*cb,.5*sb,z),normal)
            indices.extend((center,va,vb) if z>0 else (center,vb,va))
    return positions,normals,indices


def solid_geometry(shape):
    if shape == 'Foliage':
        from foliage import foliage_geometry
        return foliage_geometry()
    if shape in ('SlantedX', 'SlantedXReverse', 'SlantedY', 'SlantedYReverse'):
        return slanted_geometry(shape[7], shape.endswith('Reverse'))
    if shape in ('WedgeY', 'WedgeYReverse'):
        return wedge_geometry(shape == 'WedgeYReverse')
    if shape == 'Ellipsoid':
        return ellipsoid_geometry()
    if shape in ('CylinderX','CylinderY','CylinderZ'):
        return cylinder_geometry(shape[-1])
    raise ValueError(f'Unsupported solid shape: {shape}')
