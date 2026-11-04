import {FOLIAGE_LOBES} from './foliage-data.js';

// Same closed, tapered leaf meshes and transforms as the portable exporter.
export function foliageGeometry() {
    const sphere = ellipsoidGeometry(8, 4), output = [];
    for (const [center, axes, size] of FOLIAGE_LOBES) {
        for (let i = 0; i < sphere.length; i += 6) {
            const point = center.map((value, k) => value + axes.reduce((sum, axis, j) => sum + axis[k] * size[j] * sphere[i+j], 0));
            const normal = center.map((_, k) => axes.reduce((sum, axis, j) => sum + axis[k] * sphere[i+j+3] / size[j], 0));
            const length = Math.hypot(...normal);
            output.push(...point, ...normal.map(value => value / length));
        }
    }
    return new Float32Array(output);
}

// Same 16-segment / 8-ring unit sphere as the portable exporter by default.
export function wedgeGeometry(reverse = false) {
    const a=[-.5,-.5,-.5],b=[.5,-.5,-.5],c=[-.5,.5,-.5],d=[.5,.5,-.5],e=[-.5,.5,.5],f=[.5,.5,.5];
    const faces=[[[0,0,-1],[a,c,d,b]],[[0,1,0],[c,e,f,d]],
        [[-1,0,0],[a,e,c]],[[1,0,0],[b,d,f]],[[0,-Math.SQRT1_2,Math.SQRT1_2],[a,b,f,e]]];
    const output=[];
    for(const [normal,points] of faces)for(const i of points.length===3?[0,1,2]:[0,1,2,0,2,3])output.push(...points[i],...normal);
    if (reverse) {
        for (let i=0;i<output.length;i+=6) { output[i+1]*=-1; output[i+4]*=-1; }
        for (let i=0;i<output.length;i+=18)
            for (let j=0;j<6;j++) [output[i+6+j],output[i+12+j]]=[output[i+12+j],output[i+6+j]];
    }
    return new Float32Array(output);
}

export function slantedGeometry(axis = 'X', reverse = false) {
    if (!['X', 'Y'].includes(axis)) throw new Error('Invalid slanted axis');
    const geometry = ellipsoidGeometry(), index = axis === 'X' ? 0 : 1;
    const shear = reverse ? -.85 : .85, scale = Math.sqrt(1-shear*shear);
    for (let i=0;i<geometry.length;i+=6) {
        geometry[i+index] = scale*geometry[i+index] + shear*geometry[i+2];
        const n = [geometry[i+3],geometry[i+4],geometry[i+5]];
        n[2] -= shear*n[index]/scale;
        n[index] /= scale;
        const length = Math.hypot(...n);
        for (let j=0;j<3;j++) geometry[i+3+j] = n[j]/length;
    }
    return geometry;
}

export function ellipsoidGeometry(segments = 16, rings = 8) {
    const vertices = [[0, 0, .5]], output = [];
    for (let ring = 1; ring < rings; ring++) {
        const phi = Math.PI * ring / rings;
        for (let segment = 0; segment < segments; segment++) {
            const theta = Math.PI * 2 * segment / segments;
            vertices.push([.5 * Math.sin(phi) * Math.cos(theta), .5 * Math.sin(phi) * Math.sin(theta), .5 * Math.cos(phi)]);
        }
    }
    vertices.push([0, 0, -.5]);
    const triangle = (...indices) => {
        for (const index of indices) output.push(...vertices[index], ...vertices[index].map(v => v * 2));
    };
    for (let segment = 0; segment < segments; segment++) {
        const next = (segment + 1) % segments;
        triangle(0, 1 + segment, 1 + next);
        for (let ring = 0; ring < rings - 2; ring++) {
            const a = 1 + ring * segments + segment, b = 1 + ring * segments + next;
            triangle(a, a + segments, b); triangle(b, a + segments, b + segments);
        }
        const bottomRing = 1 + (rings - 2) * segments;
        triangle(bottomRing + segment, vertices.length - 1, bottomRing + next);
    }
    return new Float32Array(output);
}

// Capped 16-segment cylinder, with separate flat cap and smooth radial normals.
export function cylinderGeometry(axis = 'Z') {
    if (!['X','Y','Z'].includes(axis)) throw new Error('Invalid cylinder axis');
    const output = [];
    const orient = ([x,y,z]) => axis === 'X' ? [z,x,y] : axis === 'Y' ? [y,z,x] : [x,y,z];
    const vertex = (p,n) => output.push(...orient(p),...orient(n));
    for (let i=0;i<16;i++) {
        const a=Math.PI*2*i/16,b=Math.PI*2*(i+1)/16;
        const ca=Math.cos(a),sa=Math.sin(a),cb=Math.cos(b),sb=Math.sin(b);
        const points=[[.5*ca,.5*sa,-.5],[.5*cb,.5*sb,-.5],[.5*cb,.5*sb,.5],[.5*ca,.5*sa,.5]];
        const normals=[[ca,sa,0],[cb,sb,0],[cb,sb,0],[ca,sa,0]];
        for (const j of [0,1,2,0,2,3]) vertex(points[j],normals[j]);
        for (const z of [.5,-.5]) {
            vertex([0,0,z],[0,0,Math.sign(z)]);
            for (const [c,s] of z>0 ? [[ca,sa],[cb,sb]] : [[cb,sb],[ca,sa]]) vertex([.5*c,.5*s,z],[0,0,Math.sign(z)]);
        }
    }
    return new Float32Array(output);
}
