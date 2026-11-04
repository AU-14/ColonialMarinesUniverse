import test from 'node:test';
import assert from 'node:assert/strict';
import {OrbitCamera, multiply, lookAt, perspective, color} from './math.js';
import {appendBox, INSTANCE_STRIDE} from './renderer.js';
import {tileUvs, appendTile} from './terrain.js';
import {referenceFrame} from './reference.js';
import {ellipsoidGeometry, cylinderGeometry, wedgeGeometry, slantedGeometry, foliageGeometry} from './primitives.js';
import {cross, subtract, dot} from './math.js';

test('stacked floors render their underside while stair apertures stay open', () => {
    const vertices = [];
    appendTile(vertices, {x: 0, y: 0, z: 3, solid: true}, null, [1, 1, 1]);
    const normals = [];
    for (let i = 0; i < vertices.length; i += 24) {
        const a = vertices.slice(i, i+3), b = vertices.slice(i+8, i+11), c = vertices.slice(i+16, i+19);
        normals.push(cross(subtract(b,a), subtract(c,a)));
    }
    assert.ok(normals.some(normal => normal[2] > 0));
    assert.ok(normals.some(normal => normal[2] < 0));
    const aperture = [];
    appendTile(aperture, {x: 0, y: 0, z: 3, solid: true, floorFragments: []}, null, [1, 1, 1]);
    assert.equal(aperture.length, 0);
});

test('a rotated terrain ramp meets both landing heights and has closed foundations', () => {
    const vertices = [];
    appendTile(vertices, {x:10,y:20,z:2,yaw:Math.PI/2,elevation:.39,foundationDepth:.07,
        elevationRamp:{bottom:.39,top:.78,direction:[1,0]}}, null, [1,1,1]);
    const edges = new Map();
    const points = [];
    for (let i=0; i<vertices.length; i+=8) points.push(vertices.slice(i,i+3).map(v=>Number(v.toFixed(6))));
    for (let i=0; i<points.length; i+=3) for (let j=0; j<3; j++) {
        const key = [points[i+j].join(','),points[i+(j+1)%3].join(',')].sort().join('|');
        edges.set(key,(edges.get(key)||0)+1);
    }
    assert.deepEqual(new Set(edges.values()),new Set([2]));
    assert.ok(points.some(([x,y,z])=>x===10 && y===20 && z===2.385));
    assert.ok(points.some(([x,y,z])=>x===10 && y===21 && z===2.775));
    assert.equal(Math.min(...points.map(p=>p[2])),2.315);
});

for (const axis of ['X','Y']) for (const reverse of [false,true])
test(`continuous leaf ${axis} reverse=${reverse} has closed outward geometry and gradient normals`, () => {
    const geometry=slantedGeometry(axis,reverse), ai=axis==='X'?0:1;
    const k=reverse?-.85:.85,s=Math.sqrt(1-k*k),edges=new Map();
    assert.equal(geometry.length,224*18);
    for(let i=0;i<geometry.length;i+=18) {
        const points=[0,6,12].map(j=>[...geometry.slice(i+j,i+j+3)]);
        const [a,b,c]=points;
        assert.ok(dot(cross(subtract(b,a),subtract(c,a)),a)>0);
        for (let j=0;j<3;j++) {
            const p=points[j], n=[...geometry.slice(i+j*6+3,i+j*6+6)],u=[...p];
            u[ai]=(p[ai]-k*p[2])/s;
            near(Math.hypot(...u),.5);near(Math.hypot(...n),1);
            assert.ok(Math.max(...p.map(Math.abs))<=.500001);
            const g=[...p];g[ai]=(p[ai]-k*p[2])/(s*s);g[2]=p[2]-k*g[ai];
            const length=Math.hypot(...g);g.forEach((v,d)=>near(n[d],v/length));
            const key=[p.join(','),points[(j+1)%3].join(',')].sort().join('|');
            edges.set(key,(edges.get(key)||0)+1);
        }
    }
    assert.deepEqual(new Set(edges.values()),new Set([2]));
});

for (const reverse of [false,true]) test(`wedge reverse=${reverse} has outward faces and half a box of volume`, () => {
    const geometry=wedgeGeometry(reverse);let volume=0;
    assert.equal(geometry.length,8*3*6);
    for(let i=0;i<geometry.length;i+=18){
        const a=[...geometry.slice(i,i+3)],b=[...geometry.slice(i+6,i+9)],c=[...geometry.slice(i+12,i+15)];
        const normal=[...geometry.slice(i+3,i+6)];
        assert.ok(dot(cross(subtract(b,a),subtract(c,a)),normal)>0);
        for(const p of [a,b,c])assert.ok(p[2]<=p[1]*(reverse?-1:1));
        volume+=dot(a,cross(b,c))/6;
    }
    assert.ok(Math.abs(volume-.5)<.000001);
});

test('cylinders have closed circular caps, outward triangles and axis-specific normals', () => {
    for (const axis of ['X','Y','Z']) {
        const vertices=cylinderGeometry(axis),ai='XYZ'.indexOf(axis);
        assert.equal(vertices.length,64*18);
        let caps=0,sides=0;
        for (let i=0;i<vertices.length;i+=18) {
            const a=Array.from(vertices.slice(i,i+3)),b=Array.from(vertices.slice(i+6,i+9)),c=Array.from(vertices.slice(i+12,i+15));
            assert.ok(dot(cross(subtract(b,a),subtract(c,a)),a)>0);
            for (let j=0;j<18;j+=6) {
                const p=vertices.slice(i+j,i+j+3),n=vertices.slice(i+j+3,i+j+6);
                near(Math.hypot(...n),1);
                assert.ok(Math.max(...Array.from(p,Math.abs))<=.500001);
                if (Math.abs(n[ai])>.9) caps++; else sides++;
            }
        }
        assert.equal(caps,96); assert.equal(sides,96);
    }
});

test('surface rectangles and cutaway UV scale retain the original printed artwork', () => {
    const values = [];
    appendBox(values, [-1,-.02,1], [1,.02,3], '#FFFFFF', [0,0,0], Math.PI/2, 1,
        {surface: {rect:[256,512,32,64]}, surfaceAxis:'XZ', maxHeight:2});
    assert.equal(values.length, INSTANCE_STRIDE);
    assert.deepEqual(values.slice(13,17), [256,512,32,64]);
    assert.equal(values[17], 1);
    assert.equal(values[18], .5);
    near(values[5], 1);
    near(values[6], 0); near(values[7], 1);
});

test('rounded mesh has outward triangles and unit normals instead of box corners', () => {
    const vertices = ellipsoidGeometry();
    assert.equal(vertices.length, 224 * 3 * 6);
    for (let i = 0; i < vertices.length; i += 18) {
        const a = Array.from(vertices.slice(i, i+3)), b = Array.from(vertices.slice(i+6, i+9)), c = Array.from(vertices.slice(i+12, i+15));
        assert.ok(dot(cross(subtract(b,a), subtract(c,a)), a) > 0);
        for (let j = 0; j < 18; j += 6) {
            near(Math.hypot(...vertices.slice(i+j,i+j+3)), .5);
            near(Math.hypot(...vertices.slice(i+j+3,i+j+6)), 1);
        }
    }
});

test('room-side surfaces encode the horizontal flip and reject invalid atlas geometry', () => {
    const values=[];
    appendBox(values,[-1,-1,0],[1,1,2],'#FFFFFF',[0,0,0],0,1,
        {surface:{rect:[0,256,2,2]},surfaceAxis:'XZ',surfaceFlipU:true});
    assert.equal(values[17],5);
    for (const rect of [[0,0,NaN,2],[0,0,0,2],[-1,0,2,2],[0,0,2]]) {
        const rejected=[];
        appendBox(rejected,[-1,-1,0],[1,1,2],'#FFFFFF',[0,0,0],0,1,{surface:{rect}});
        assert.equal(rejected.length,0);
    }
});

test('source comparison selects actual map direction and skips earlier direction animation frames', () => {
    const meta={size:{x:32,y:32},states:[{name:'door',directions:4}]};
    assert.deepEqual(referenceFrame(meta,'door',0,64),[0,0,32,32]);
    assert.deepEqual(referenceFrame(meta,'door',Math.PI,64),[32,0,32,32]);
    assert.deepEqual(referenceFrame(meta,'door',Math.PI/2,64),[0,32,32,32]);
    assert.deepEqual(referenceFrame(meta,'door',-Math.PI/2,64),[32,32,32,32]);
    meta.states[0].delays=[[.1,.1],[.2,.2,.2],[.1],[.1]];
    assert.deepEqual(referenceFrame(meta,'door',Math.PI/2,96),[64,32,32,32]);
});

test('source tile corners follow engine rotation and mirror ordering', () => {
    assert.deepEqual(tileUvs(0), [[0,1],[1,1],[1,0],[0,0]]);
    assert.deepEqual(tileUvs(1), [[0,0],[0,1],[1,1],[1,0]]);
    assert.deepEqual(tileUvs(4), [[1,1],[0,1],[0,0],[1,0]]);
    assert.deepEqual(tileUvs(5), [[0,1],[0,0],[1,0],[1,1]]);
    assert.equal(new Set(Array.from({length:8},(_,i)=>JSON.stringify(tileUvs(i)))).size,8);
});

test('textured tile keeps rotated world geometry and variant atlas coordinates', () => {
    const values=[];
    appendTile(values,{x:10,y:20,z:2,yaw:Math.PI/2,rotationMirroring:1},[.2,.3,.1,.1],[1,1,1],true);
    assert.equal(values.length,48);
    near(values[0],10); near(values[1],20); near(values[2],1.995);
    near(values[3],.2); near(values[4],.3);
    near(values[8],10); near(values[9],21);
    near(values[19],.3); near(values[20],.4);
});

test('tile definitions that disallow mirroring keep their original orientation', () => {
    const normal=[], mirrored=[];
    const tile={x:0,y:0,z:0,rotationMirroring:7};
    appendTile(normal,tile,[0,0,1,1],[1,1,1],false);
    appendTile(mirrored,{...tile,rotationMirroring:0},[0,0,1,1],[1,1,1],true);
    assert.deepEqual(normal,mirrored);
});

test('floor aperture leaves empty space and preserves cropped UVs for every tile orientation', () => {
    for(let rotationMirroring=0;rotationMirroring<8;rotationMirroring++) {
        const values=[], fragments=[[0,0,.25,1],[.75,0,1,1],[.25,0,.75,.125],[.25,.5,.75,1]];
        appendTile(values,{x:0,y:0,z:0,rotationMirroring,floorFragments:fragments},[0,0,1,1],[1,1,1],true);
        assert.equal(values.length,4*6*8);
        const original=tileUvs(rotationMirroring);
        for(let i=0;i<values.length;i+=8) {
            const [x,y]=values.slice(i,i+2);
            assert.ok(!(x>.25&&x<.75&&y>.125&&y<.5));
            for(let channel=0;channel<2;channel++)
                near(values[i+3+channel],original[0][channel]+x*(original[1][channel]-original[0][channel])+y*(original[3][channel]-original[0][channel]));
        }
    }
});

const near = (actual, expected, tolerance = 0.00001) => assert.ok(Math.abs(actual - expected) < tolerance,
    `Expected ${actual} to be within ${tolerance} of ${expected}`);
function transform(matrix, vector) {
    return Array.from({length: 4}, (_, row) => vector.reduce((sum, value, column) => sum + value * matrix[column * 4 + row], 0));
}
function projected(matrix, point) {
    const value = transform(matrix, [...point, 1]);
    return value.slice(0, 3).map(channel => channel / value[3]);
}

test('orbit camera keeps its target centered through full yaw and near-vertical pitch', () => {
    const camera = new OrbitCamera();
    camera.target = [228.5, -159.5, 0.6];
    for (const yaw of [-Math.PI, -Math.PI / 2, 0, Math.PI / 2, Math.PI])
        for (const pitch of [0.08, 0.8, 1.53]) {
            camera.yaw = yaw; camera.pitch = pitch;
            const matrix = camera.matrix(1.6), screen = projected(matrix, camera.target);
            assert.ok([...matrix].every(Number.isFinite));
            near(screen[0], 0); near(screen[1], 0);
            assert.ok(screen[2] > -1 && screen[2] < 1);
        }
});

test('Z-up view puts east right and altitude up when viewed from game south', () => {
    const matrix = multiply(perspective(Math.PI / 4, 1, 0.1, 100), lookAt([0, -5, 0], [0, 0, 0]));
    assert.ok(projected(matrix, [1, 0, 0])[0] > 0);
    assert.ok(projected(matrix, [0, 0, 1])[1] > 0);
    near(projected(matrix, [0, -4.9, 0])[2], -1, 0.0001);
    near(projected(matrix, [0, 95, 0])[2], 1, 0.0001);
});

test('pan follows camera axes, scales with zoom, and preserves the floor', () => {
    const camera = new OrbitCamera();
    camera.target = [0, 0, 0.6]; camera.yaw = -Math.PI / 2; camera.distance = 10;
    camera.pan(-100, -100, 1000);
    near(camera.target[0], 0.83); near(camera.target[1], 0.83); near(camera.target[2], 0.6);
    camera.target = [0, 0, 0.6]; camera.yaw = 0; camera.distance = 20;
    camera.pan(-100, 0, 1000);
    near(camera.target[0], 0); near(camera.target[1], 1.66); near(camera.target[2], 0.6);
});

test('zoom and pitch remain bounded under extreme wheel and drag input', () => {
    const camera = new OrbitCamera();
    camera.zoom(-100000); assert.equal(camera.distance, 3);
    camera.zoom(100000); assert.equal(camera.distance, 220);
    camera.orbit(0, 100000); assert.equal(camera.pitch, 1.53);
    camera.orbit(0, -100000); assert.equal(camera.pitch, 0.08);
    assert.ok([...camera.matrix(1)].every(Number.isFinite));
});

test('instanced box rotates its local center around the map pivot, with Z preserved', () => {
    const values = [];
    appendBox(values, [0, 0, 0], [2, 4, 6], '#808040', [10, 20, 3], Math.PI / 2, 17);
    assert.equal(values.length, INSTANCE_STRIDE);
    near(values[0], 8); near(values[1], 21); near(values[2], 6);
    assert.deepEqual(values.slice(3, 6), [2, 4, 6]);
    near(values[6], 0); near(values[7], 1);
    assert.equal(values[12], 17);
});

test('cutaway clips local height before translation and omits completely clipped parts', () => {
    const values = [];
    appendBox(values, [-1, -1, 0], [1, 1, 3], '#FFFFFF', [0, 0, 5], 0, 1, {maxHeight: 0.9});
    near(values[2], 5.45); near(values[5], 0.9);
    const size = values.length;
    appendBox(values, [-1, -1, 1], [1, 1, 2], '#FFFFFF', [0, 0, 5], 0, 1, {maxHeight: 0.9});
    assert.equal(values.length, size);
});

test('material alpha multiplies the review marker opacity without changing RGB', () => {
    const values = [];
    appendBox(values, [0, 0, 0], [1, 1, 1], '#3388CC80', [0, 0, 0], 0, 42, {alpha: 0.5});
    near(values[8], 0x33 / 255); near(values[9], 0x88 / 255); near(values[10], 0xCC / 255);
    near(values[11], 128 / 255 * 0.5);
    assert.deepEqual(color('not a color'), [0.6, 0.65, 0.65, 1]);
});

test('malformed, nonfinite, reversed, and flat geometry never enters the GPU buffer', () => {
    const valid = {min: [0, 0, 0], max: [1, 1, 1], position: [0, 0, 0], yaw: 0, id: 1, options: {}};
    for (const change of [
        {min: [0, 0]}, {max: [1, 1, NaN]}, {position: [0, Infinity, 0]}, {position: null},
        {min: [2, 0, 0]}, {max: [0, 1, 1]}, {yaw: NaN}, {id: 16777216}, {id: -1},
        {options: {maxHeight: NaN}}, {options: {alpha: Infinity}}, {options: {alpha: -0.1}},
    ]) {
        const sample = {...valid, ...change}, values = [];
        assert.doesNotThrow(() => appendBox(values, sample.min, sample.max, '#FFFFFF', sample.position, sample.yaw, sample.id, sample.options));
        assert.equal(values.length, 0, JSON.stringify(change));
    }
});


test('irregular foliage has closed outward leaflets inside the original ellipsoid', () => {
    const geometry=foliageGeometry(), edges=new Map();
    assert.equal(geometry.length, 912*18);
    for(let i=0;i<geometry.length;i+=18) {
        const points=[0,6,12].map(j=>[...geometry.slice(i+j,i+j+3)]);
        const [a,b,c]=points, n=[...geometry.slice(i+3,i+6)];
        assert.ok(dot(cross(subtract(b,a),subtract(c,a)),n)>0);
        for(let j=0;j<3;j++) {
            assert.ok(Math.hypot(...points[j])<.5);
            near(Math.hypot(...geometry.slice(i+j*6+3,i+j*6+6)),1);
            const key=[points[j].join(','),points[(j+1)%3].join(',')].sort().join('|');
            edges.set(key,(edges.get(key)||0)+1);
        }
    }
    assert.deepEqual(new Set(edges.values()),new Set([2]));
});


test('part yaw composes orientations without orbiting an off-center part around its entity', () => {
    const data=[];
    appendBox(data,[1,2,0],[3,2.2,1],'#FFFFFF',[10,20,0],Math.PI/2,9,{partYaw:90});
    near(data[0],7.9);near(data[1],22);near(data[2],.5);
    near(data[6],-1);near(data[7],0);
    assert.equal(data[12],9);
    for(const partYaw of [NaN,Infinity,361]) {
        const bad=[];appendBox(bad,[0,0,0],[1,1,1],'#FFFFFF',[0,0,0],0,0,{partYaw});
        assert.equal(bad.length,0);
    }
});

test('part tilt preserves the part center and thin local stem dimensions', () => {
    const data=[];
    appendBox(data,[1,2,3],[3,2.12,3.12],'#FFFFFF',[10,20,0],Math.PI/2,9,{partYaw:37,partPitch:63});
    assert.equal(data.length,INSTANCE_STRIDE);
    near(data[0],7.94);near(data[1],22);near(data[2],3.06);
    near(data[3],2);near(data[4],.12);near(data[5],.12);
    near(data[19],Math.cos(63*Math.PI/180));near(data[20],Math.sin(63*Math.PI/180));
    const tip=[data[0]+data[6]*data[19],data[1]+data[7]*data[19],data[2]+data[20]];
    near(Math.hypot(tip[0]-data[0],tip[1]-data[1],tip[2]-data[2]),1);
    for(const options of [{partPitch:NaN},{partPitch:91},{partPitch:15,surface:{rect:[0,0,32,32]}}]) {
        const bad=[];appendBox(bad,[0,0,0],[1,1,1],'#FFFFFF',[0,0,0],0,0,options);
        assert.equal(bad.length,0);
    }
});
