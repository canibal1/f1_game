// Converts (meshopt-compressed, quantized) CC0 GLB models into a tiny binary
// "mesh pack" that the Unity game loads at runtime without any import package.
// Triangles are grouped by flat color (sampled from the palette texture or the
// material base color). Coordinates are converted from glTF (RH) to Unity (LH).
//
// usage: node convert.mjs <in.glb> <out.bytes> [--bake]
import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { dequantize } from '@gltf-transform/functions';
import { MeshoptDecoder } from 'meshoptimizer';
import { PNG } from 'pngjs';
import fs from 'node:fs';

const [, , inPath, outPath, ...flags] = process.argv;
const bake = flags.includes('--bake') || flags.includes('--center');
const center = flags.includes('--center');

await MeshoptDecoder.ready;
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS).registerDependencies({ 'meshopt.decoder': MeshoptDecoder });
const doc = await io.read(inPath);
await doc.transform(dequantize());

const lin2srgb = (c) => (c <= 0.0031308 ? 12.92 * c : 1.055 * Math.pow(c, 1 / 2.4) - 0.055);

const textureCache = new Map();
function decodeTexture(tex) {
  if (!tex) return null;
  if (textureCache.has(tex)) return textureCache.get(tex);
  const png = PNG.sync.read(Buffer.from(tex.getImage()));
  textureCache.set(tex, png);
  return png;
}

function sample(png, u, v) {
  u = u - Math.floor(u);
  v = v - Math.floor(v);
  const x = Math.min(png.width - 1, Math.max(0, Math.floor(u * png.width)));
  const y = Math.min(png.height - 1, Math.max(0, Math.floor(v * png.height)));
  const i = (y * png.width + x) * 4;
  return [png.data[i], png.data[i + 1], png.data[i + 2], png.data[i + 3]];
}

// --- math helpers (column-major 4x4 like glTF) ---
function mul(a, b) {
  const o = new Array(16).fill(0);
  for (let c = 0; c < 4; c++) for (let r = 0; r < 4; r++) {
    let s = 0;
    for (let k = 0; k < 4; k++) s += a[k * 4 + r] * b[c * 4 + k];
    o[c * 4 + r] = s;
  }
  return o;
}
function xformPoint(m, p) {
  return [
    m[0] * p[0] + m[4] * p[1] + m[8] * p[2] + m[12],
    m[1] * p[0] + m[5] * p[1] + m[9] * p[2] + m[13],
    m[2] * p[0] + m[6] * p[1] + m[10] * p[2] + m[14],
  ];
}
function xformDir(m, p) {
  const v = [m[0] * p[0] + m[4] * p[1] + m[8] * p[2], m[1] * p[0] + m[5] * p[1] + m[9] * p[2], m[2] * p[0] + m[6] * p[1] + m[10] * p[2]];
  const l = Math.hypot(...v) || 1;
  return v.map((x) => x / l);
}

const parts = [];
const scene = doc.getRoot().getDefaultScene() || doc.getRoot().listScenes()[0];

function visit(node, parentWorld) {
  const world = mul(parentWorld, node.getMatrix());
  const mesh = node.getMesh();
  if (mesh) {
    // In bake mode geometry is put in model space; otherwise in node-local space
    // with the node's translation/rotation/scale written separately.
    const geomMatrix = bake ? world : [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
    const verts = [];
    const norms = [];
    const groups = new Map(); // colorKey -> indices
    for (const prim of mesh.listPrimitives()) {
      const pos = prim.getAttribute('POSITION');
      const nrm = prim.getAttribute('NORMAL');
      const uv = prim.getAttribute('TEXCOORD_0');
      const idx = prim.getIndices();
      const mat = prim.getMaterial();
      const base = mat ? mat.getBaseColorFactor() : [1, 1, 1, 1];
      const texInfo = mat ? mat.getBaseColorTextureInfo() : null;
      const png = mat ? decodeTexture(mat.getBaseColorTexture()) : null;
      let off = [0, 0], scl = [1, 1];
      if (texInfo) {
        const tt = texInfo.getExtension('KHR_texture_transform');
        if (tt) { off = tt.getOffset(); scl = tt.getScale(); }
      }
      const baseIndex = verts.length;
      const tmp = [0, 0, 0];
      for (let i = 0; i < pos.getCount(); i++) {
        const p = xformPoint(geomMatrix, pos.getElement(i, tmp));
        verts.push([-p[0], p[1], p[2]]); // glTF RH -> Unity LH (mirror X)
        const n = nrm ? xformDir(geomMatrix, nrm.getElement(i, [0, 0, 0])) : [0, 1, 0];
        norms.push([-n[0], n[1], n[2]]);
      }
      const indexCount = idx ? idx.getCount() : pos.getCount();
      const getIndex = (k) => (idx ? idx.getScalar(k) : k);
      for (let t = 0; t + 2 < indexCount; t += 3) {
        const a = getIndex(t), b = getIndex(t + 1), c = getIndex(t + 2);
        let col;
        if (png && uv) {
          const ua = uv.getElement(a, [0, 0]), ub = uv.getElement(b, [0, 0]), uc = uv.getElement(c, [0, 0]);
          const u = (ua[0] + ub[0] + uc[0]) / 3 * scl[0] + off[0];
          const v = (ua[1] + ub[1] + uc[1]) / 3 * scl[1] + off[1];
          const s = sample(png, u, v);
          col = [Math.round(s[0] * base[0]), Math.round(s[1] * base[1]), Math.round(s[2] * base[2]), 255];
        } else {
          // Kenney stores its sRGB palette values directly in baseColorFactor, so no linear->sRGB conversion here.
          col = [0, 1, 2].map((k) => Math.round(255 * Math.min(1, base[k])));
          col.push(255);
        }
        const key = col.join(',');
        if (!groups.has(key)) groups.set(key, []);
        // reverse winding because of the X mirror
        groups.get(key).push(baseIndex + a, baseIndex + c, baseIndex + b);
      }
    }
    // node local TRS (converted to Unity space)
    const t = bake ? [0, 0, 0] : node.getTranslation();
    const r = bake ? [0, 0, 0, 1] : node.getRotation();
    const s = bake ? [1, 1, 1] : node.getScale();
    parts.push({
      name: bake ? 'model' : node.getName(),
      t: [-t[0], t[1], t[2]],
      r: [r[0], -r[1], -r[2], r[3]],
      s,
      verts, norms, groups,
    });
  }
  for (const child of node.listChildren()) visit(child, bake ? world : world);
}

for (const n of scene.listChildren()) visit(n, [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);

// merge baked parts into one
let outParts = parts;
if (bake && parts.length > 1) {
  const merged = { name: 'model', t: [0, 0, 0], r: [0, 0, 0, 1], s: [1, 1, 1], verts: [], norms: [], groups: new Map() };
  for (const p of parts) {
    const off = merged.verts.length;
    merged.verts.push(...p.verts);
    merged.norms.push(...p.norms);
    for (const [k, list] of p.groups) {
      if (!merged.groups.has(k)) merged.groups.set(k, []);
      merged.groups.get(k).push(...list.map((i) => i + off));
    }
  }
  outParts = [merged];
}

// --- cluster near-identical palette shades so each part has only a few flat colors ---
function clusterGroups(groups, thr = 40) {
  const entries = [...groups.entries()].map(([k, list]) => ({ c: k.split(',').map(Number), list }))
    .sort((a, b) => b.list.length - a.list.length);
  const clusters = [];
  for (const e of entries) {
    let best = null, bd = 1e9;
    for (const cl of clusters) {
      const d = Math.hypot(cl.c[0] - e.c[0], cl.c[1] - e.c[1], cl.c[2] - e.c[2]);
      if (d < bd) { bd = d; best = cl; }
    }
    if (best && bd < thr) {
      const n0 = best.n, n1 = e.list.length / 3;
      best.sum = best.sum.map((s, i) => s + e.c[i] * n1);
      best.n += n1;
      best.list.push(...e.list);
    } else {
      clusters.push({ c: e.c.slice(), sum: e.c.map((x) => x * e.list.length / 3), n: e.list.length / 3, list: e.list.slice() });
    }
  }
  const out = new Map();
  for (const cl of clusters) {
    const avg = cl.sum.slice(0, 3).map((s) => Math.round(s / cl.n));
    out.set([...avg, 255].join(','), cl.list);
  }
  return out;
}
for (const p of outParts) p.groups = clusterGroups(p.groups);
if (center) {
  for (const p of outParts) {
    const xs = p.verts.map((v) => v[0]), ys = p.verts.map((v) => v[1]), zs = p.verts.map((v) => v[2]);
    const cx = (Math.min(...xs) + Math.max(...xs)) / 2, cz = (Math.min(...zs) + Math.max(...zs)) / 2, my = Math.min(...ys);
    p.verts = p.verts.map((v) => [v[0] - cx, v[1] - my, v[2] - cz]);
  }
}

// --- write binary ---
const chunks = [];
const u8 = (v) => { const b = Buffer.alloc(1); b.writeUInt8(v); chunks.push(b); };
const i32 = (v) => { const b = Buffer.alloc(4); b.writeInt32LE(v); chunks.push(b); };
const f32 = (v) => { const b = Buffer.alloc(4); b.writeFloatLE(v); chunks.push(b); };
const str = (s) => { const b = Buffer.from(s, 'utf8'); i32(b.length); chunks.push(b); };

chunks.push(Buffer.from('MPK1', 'ascii'));
i32(outParts.length);
const summary = [];
for (const p of outParts) {
  str(p.name);
  p.t.forEach(f32); p.r.forEach(f32); p.s.forEach(f32);
  i32(p.verts.length);
  for (const v of p.verts) v.forEach(f32);
  for (const n of p.norms) n.forEach(f32);
  i32(p.groups.size);
  const g = [];
  for (const [key, list] of p.groups) {
    const c = key.split(',').map(Number);
    c.forEach(u8);
    i32(list.length);
    list.forEach(i32);
    g.push(`${key}:${list.length / 3}`);
  }
  const xs = p.verts.map((v) => v[0]), ys = p.verts.map((v) => v[1]), zs = p.verts.map((v) => v[2]);
  summary.push({ name: p.name, t: p.t.map((x) => +x.toFixed(3)), s: p.s, verts: p.verts.length,
    min: [Math.min(...xs), Math.min(...ys), Math.min(...zs)].map((x) => +x.toFixed(3)),
    max: [Math.max(...xs), Math.max(...ys), Math.max(...zs)].map((x) => +x.toFixed(3)), colors: g });
}
fs.writeFileSync(outPath, Buffer.concat(chunks));
console.log(JSON.stringify({ in: inPath, out: outPath, parts: summary }));
