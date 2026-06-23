#!/usr/bin/env python3
"""
sprite2voxel.py  (v2)
=====================
Convierte sprites pixel-art (PNG con alpha) en modelos 3D estilo voxel,
de forma automatica y por lotes. Pensado para tandas de sprites (p.ej. 128x128).

Mejoras v2
----------
- Culling de caras VECTORIZADO con numpy (rapido en sprites grandes).
- GREEDY MESHING opcional (por defecto ON): fusiona caras coplanares del
  mismo color en rectangulos -> .obj con muchisimos menos triangulos.
- Procesa una carpeta entera de un tiron.

Salidas (por sprite)
--------------------
- .vox : MagicaVoxel, Z-up, modelo de pie. Paleta <= 255 colores.
- .obj (+ .mtl) : malla Y-up lista para Unity / Godot / libGDX / Blender,
                  una material por color.

Uso
---
    python3 sprite2voxel.py CARPETA --out modelos/ --depth 8
    python3 sprite2voxel.py CARPETA --no-greedy          # malla por-cara
    python3 sprite2voxel.py hero.png --depth 8 --depth-map
    python3 sprite2voxel.py sheet.png --sheet 8x8        # 1 modelo por frame

'CARPETA' procesa todos los .png que no terminen en _depth.png.
"""
from __future__ import annotations

import argparse
import struct
import sys
import time
from pathlib import Path

import numpy as np
from PIL import Image


# --------------------------------------------------------------------------- #
#  Carga / preparacion                                                        #
# --------------------------------------------------------------------------- #
def load_rgba(path: Path) -> np.ndarray:
    return np.asarray(Image.open(path).convert("RGBA"), dtype=np.uint8)


def iter_frames(rgba, sheet):
    if sheet is None:
        yield 0, rgba
        return
    cols, rows = sheet
    h, w = rgba.shape[:2]
    fw, fh = w // cols, h // rows
    idx = 0
    for r in range(rows):
        for c in range(cols):
            frame = rgba[r * fh:(r + 1) * fh, c * fw:(c + 1) * fw]
            if frame[..., 3].any():
                yield idx, frame
                idx += 1


def quantize(rgba, alpha_thr, max_colors=255):
    """Devuelve idx_grid (H,W) [0=vacio, 1..N=color] y palette [(r,g,b,a)]."""
    h, w = rgba.shape[:2]
    mask = rgba[..., 3] >= alpha_thr
    pal_img = Image.fromarray(rgba[..., :3], "RGB").quantize(
        colors=max_colors, method=Image.Quantize.MEDIANCUT)
    pal_idx = np.asarray(pal_img, dtype=np.int32)
    raw_pal = pal_img.getpalette()

    used = np.unique(pal_idx[mask])
    remap = {old: new + 1 for new, old in enumerate(used)}
    idx_grid = np.zeros((h, w), dtype=np.int32)
    for old, new in remap.items():
        idx_grid[(pal_idx == old) & mask] = new

    palette = [tuple(raw_pal[o * 3:o * 3 + 3]) + (255,) for o in used]
    return idx_grid, palette


def build_voxels(idx_grid, depth, depth_map=None):
    """vox[x, y, z]; X=columna, Y=fila invertida (vertical), Z=profundidad."""
    h, w = idx_grid.shape
    vox = np.zeros((w, h, depth), dtype=np.int32)
    ys, xs = np.nonzero(idx_grid)
    for py, px in zip(ys, xs):
        my = h - 1 - py
        if depth_map is None:
            vox[px, my, 0:depth] = idx_grid[py, px]
        else:
            d = max(1, round(int(depth_map[py, px]) / 255 * depth))
            z0 = max(0, depth // 2 - d // 2)
            vox[px, my, z0:min(depth, z0 + d)] = idx_grid[py, px]
    return vox


# --------------------------------------------------------------------------- #
#  Greedy meshing 2D                                                          #
# --------------------------------------------------------------------------- #
def _greedy_rects(plane):
    """plane[i,j] int (0=sin cara). Devuelve (i0,j0,di,dj,color)."""
    s0, s1 = plane.shape
    done = np.zeros_like(plane, dtype=bool)
    rects = []
    for i in range(s0):
        row = plane[i]
        drow = done[i]
        j = 0
        while j < s1:
            c = row[j]
            if c == 0 or drow[j]:
                j += 1
                continue
            dj = 1
            while j + dj < s1 and row[j + dj] == c and not drow[j + dj]:
                dj += 1
            di = 1
            while i + di < s0:
                seg = plane[i + di, j:j + dj]
                segd = done[i + di, j:j + dj]
                if np.all(seg == c) and not segd.any():
                    di += 1
                else:
                    break
            done[i:i + di, j:j + dj] = True
            rects.append((i, j, di, dj, int(c)))
            j += dj
    return rects


def _faces_greedy(vox):
    """dict color -> lista de quads (4 esquinas (x,y,z)) fusionados."""
    solid = vox > 0
    faces = {}
    for fa, sign in [(0, 1), (0, -1), (1, 1), (1, -1), (2, 1), (2, -1)]:
        other = [a for a in (0, 1, 2) if a != fa]
        n = vox.shape[fa]
        outward = np.zeros(3); outward[fa] = sign
        for L in range(n):
            solid_layer = np.take(solid, L, axis=fa)
            color_layer = np.take(vox, L, axis=fa)
            nb = L + sign
            neigh = (np.take(solid, nb, axis=fa) if 0 <= nb < n
                     else np.zeros_like(solid_layer))
            plane = np.where(solid_layer & ~neigh, color_layer, 0)
            if not plane.any():
                continue
            p = L + 1 if sign > 0 else L
            for (i0, j0, di, dj, c) in _greedy_rects(plane):
                corners = []
                for a, b in [(0, 0), (di, 0), (di, dj), (0, dj)]:
                    coord = [0, 0, 0]
                    coord[fa] = p
                    coord[other[0]] = i0 + a
                    coord[other[1]] = j0 + b
                    corners.append(coord)
                v0 = np.array(corners[0]); v1 = np.array(corners[1]); v2 = np.array(corners[2])
                if np.dot(np.cross(v1 - v0, v2 - v0), outward) < 0:
                    corners = corners[::-1]
                faces.setdefault(c, []).append([tuple(pt) for pt in corners])
    return faces


def _faces_per_voxel(vox):
    """Malla sin fusionar (1 quad por cara expuesta), vectorizada."""
    solid = vox > 0
    cube = {
        (1, 0, 0): [(1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)],
        (-1, 0, 0): [(0, 0, 1), (0, 1, 1), (0, 1, 0), (0, 0, 0)],
        (0, 1, 0): [(0, 1, 0), (0, 1, 1), (1, 1, 1), (1, 1, 0)],
        (0, -1, 0): [(0, 0, 1), (0, 0, 0), (1, 0, 0), (1, 0, 1)],
        (0, 0, 1): [(0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)],
        (0, 0, -1): [(0, 1, 0), (1, 1, 0), (1, 0, 0), (0, 0, 0)],
    }
    W, H, D = vox.shape
    faces = {}
    for (dx, dy, dz), corners in cube.items():
        shifted = np.zeros_like(solid)
        sx = slice(max(dx, 0), W + min(dx, 0)); tx = slice(max(-dx, 0), W + min(-dx, 0))
        sy = slice(max(dy, 0), H + min(dy, 0)); ty = slice(max(-dy, 0), H + min(-dy, 0))
        sz = slice(max(dz, 0), D + min(dz, 0)); tz = slice(max(-dz, 0), D + min(-dz, 0))
        shifted[tx, ty, tz] = solid[sx, sy, sz]
        for x, y, z in np.argwhere(solid & ~shifted):
            quad = [(x + cx, y + cy, z + cz) for cx, cy, cz in corners]
            faces.setdefault(int(vox[x, y, z]), []).append(quad)
    return faces


def export_obj(vox, palette, out, scale=1.0, greedy=True):
    faces = _faces_greedy(vox) if greedy else _faces_per_voxel(vox)
    verts, vlist = {}, []

    def vid(p):
        if p not in verts:
            vlist.append(p); verts[p] = len(vlist)
        return verts[p]

    mtl = out.with_suffix(".mtl")
    nfaces = 0
    face_lines = []
    for color, quads in faces.items():
        face_lines.append(f"usemtl color_{color}\n")
        for q in quads:
            ids = [vid(p) for p in q]
            face_lines.append(f"f {ids[0]} {ids[1]} {ids[2]} {ids[3]}\n")
            nfaces += 1
    with open(out, "w") as f:
        f.write(f"# sprite2voxel v2\nmtllib {mtl.name}\n")
        for vx, vy, vz in vlist:
            f.write(f"v {vx*scale:.4f} {vy*scale:.4f} {vz*scale:.4f}\n")
        f.writelines(face_lines)
    with open(mtl, "w") as f:
        for color in faces:
            r, g, b, _ = palette[color - 1]
            f.write(f"newmtl color_{color}\nKd {r/255:.4f} {g/255:.4f} {b/255:.4f}\nillum 1\n\n")
    return len(vlist), nfaces


# --------------------------------------------------------------------------- #
#  Export VOX (MagicaVoxel, Z-up)                                             #
# --------------------------------------------------------------------------- #
def _chunk(cid, content, children=b""):
    return cid + struct.pack("<ii", len(content), len(children)) + content + children


def export_vox(vox, palette, out):
    W, H, D = vox.shape
    if max(W, H, D) > 256:
        raise ValueError(f"MagicaVoxel limita a 256^3 (modelo {W}x{H}x{D})")
    body = bytearray()
    for x, y, z in np.argwhere(vox > 0):
        body += struct.pack("<BBBB", x, z, y, int(vox[x, y, z]))  # Y<->Z -> Z-up
    size = _chunk(b"SIZE", struct.pack("<iii", W, D, H))
    xyzi = _chunk(b"XYZI", struct.pack("<i", int((vox > 0).sum())) + bytes(body))
    pal = bytearray()
    for i in range(256):
        r, g, b, a = palette[i] if i < len(palette) else (0, 0, 0, 255)
        pal += struct.pack("<BBBB", r, g, b, a)
    main = _chunk(b"MAIN", b"", size + xyzi + _chunk(b"RGBA", bytes(pal)))
    out.write_bytes(b"VOX " + struct.pack("<i", 150) + main)


# --------------------------------------------------------------------------- #
#  Pipeline                                                                   #
# --------------------------------------------------------------------------- #
def process_file(path, out_dir, depth, formats, alpha_thr, scale, sheet, use_dmap, greedy):
    rgba = load_rgba(path)
    dmap = None
    if use_dmap:
        dp = path.with_name(path.stem + "_depth.png")
        if dp.exists():
            dmap = np.asarray(Image.open(dp).convert("L"), dtype=np.uint8)
    for fi, frame in iter_frames(rgba, sheet):
        idx_grid, palette = quantize(frame, alpha_thr)
        if not palette:
            continue
        vox = build_voxels(idx_grid, depth, dmap if sheet is None else None)
        base = out_dir / (path.stem + ("" if sheet is None else f"_{fi:03d}"))
        msg = f"  {base.name}: {int((vox>0).sum())} voxels, {len(palette)} colores"
        if "vox" in formats:
            export_vox(vox, palette, base.with_suffix(".vox"))
        if "obj" in formats:
            nv, nf = export_obj(vox, palette, base.with_suffix(".obj"), scale, greedy)
            msg += f", {nv} verts, {nf} caras"
        print(msg)


def main(argv=None):
    p = argparse.ArgumentParser(description="Sprites pixel-art -> modelos voxel 3D (lote)")
    p.add_argument("input", help="PNG o carpeta de PNGs")
    p.add_argument("--out", default="voxel_out")
    p.add_argument("--depth", type=int, default=8, help="profundidad de extrusion (def 8)")
    p.add_argument("--formats", default="vox,obj")
    p.add_argument("--alpha", type=int, default=128)
    p.add_argument("--scale", type=float, default=1.0)
    p.add_argument("--sheet", default=None, help="cortar spritesheet, ej 8x8")
    p.add_argument("--depth-map", action="store_true")
    p.add_argument("--no-greedy", action="store_true", help="malla 1 quad por cara")
    a = p.parse_args(argv)

    formats = {x.strip() for x in a.formats.split(",") if x.strip()}
    sheet = tuple(int(v) for v in a.sheet.lower().split("x")) if a.sheet else None
    in_path = Path(a.input)
    out_dir = Path(a.out); out_dir.mkdir(parents=True, exist_ok=True)

    files = (sorted(f for f in in_path.glob("*.png") if not f.stem.endswith("_depth"))
             if in_path.is_dir() else [in_path])
    if not files:
        print("No se encontraron PNGs."); return 1

    print(f"Procesando {len(files)} sprite(s) -> {out_dir}/  (depth={a.depth}, "
          f"greedy={'no' if a.no_greedy else 'si'})")
    t0 = time.time()
    for f in files:
        print(f"- {f.name}")
        process_file(f, out_dir, a.depth, formats, a.alpha, a.scale,
                     sheet, a.depth_map, not a.no_greedy)
    print(f"Listo en {time.time()-t0:.1f}s")
    return 0


if __name__ == "__main__":
    sys.exit(main())
