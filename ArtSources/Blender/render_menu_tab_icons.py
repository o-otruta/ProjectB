"""
ProjectB — 3D Voxel Main Menu Tab Icon Generator (Blender 5.3 + BLENDER_EEVEE)
Builds 5 cohesive 3D voxel sculptures (Upgrades, Abilities, Play, Achievements, Bonus),
renders each solo at 512x512 RGBA under unified 3-point studio lighting, post-processes
with a #16132A contour + soft drop shadow into crisp 256x256 sprites, saves
ArtSources/Blender/MenuTabIcons.blend, and arranges a 5-icon showcase in the viewport.
"""

import math
import os
import tempfile

import bpy
import bmesh
from mathutils import Vector, Matrix
import numpy as np
from PIL import Image, ImageFilter

PROJECT_ROOT = r"d:\UnityProjects\ProjectB"
ICONS_OUT_DIR = os.path.join(PROJECT_ROOT, "Assets", "_UI", "Kit", "Icons")
BLEND_OUT_PATH = os.path.join(PROJECT_ROOT, "ArtSources", "Blender", "MenuTabIcons.blend")

VOXEL_SIZE = 0.10
RENDER_RES = 512
FINAL_RES = 256
OPTICAL_MAX_DIM = 426  # out of 512, leaving ~43px margin for outline + drop shadow
OUTLINE_RADIUS_512 = 10.5
OUTLINE_RGB = (22, 19, 42)  # #16132A matching PixelCanvas.OutlineColor
SHADOW_RGB = (14, 11, 30)
SHADOW_OPACITY = 0.34
SHADOW_OFFSET_Y_512 = 9
SHADOW_BLUR_512 = 7.0

MAT_MATTE = 0
MAT_METAL = 1
MAT_GLOW = 2


def hex_to_linear_rgba(hex_str: str) -> tuple[float, float, float, float]:
    h = hex_str.lstrip("#")
    sr = int(h[0:2], 16) / 255.0
    sg = int(h[2:4], 16) / 255.0
    sb = int(h[4:6], 16) / 255.0

    def srgb_to_lin(c: float) -> float:
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

    return (srgb_to_lin(sr), srgb_to_lin(sg), srgb_to_lin(sb), 1.0)


def clear_scene():
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")

    scene = bpy.context.scene
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for col in list(scene.collection.children):
        scene.collection.children.unlink(col)
    for col in list(bpy.data.collections):
        bpy.data.collections.remove(col)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)
    for mat in list(bpy.data.materials):
        bpy.data.materials.remove(mat)
    for cam in list(bpy.data.cameras):
        bpy.data.cameras.remove(cam)
    for light in list(bpy.data.lights):
        bpy.data.lights.remove(light)


def create_voxel_materials() -> list[bpy.types.Material]:
    specs = [
        ("Voxel_Matte", 0.65, 0.00, 0.20, 0.0),
        ("Voxel_Metal", 0.30, 0.16, 0.45, 0.0),
        ("Voxel_Glow", 0.35, 0.00, 0.15, 0.70),
    ]
    mats = []
    for name, roughness, metallic, spec, emission_strength in specs:
        mat = bpy.data.materials.new(name=name)
        mat.use_nodes = True
        nt = mat.node_tree
        nt.nodes.clear()

        out = nt.nodes.new("ShaderNodeOutputMaterial")
        out.location = (400, 0)

        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        bsdf.location = (100, 0)
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Metallic"].default_value = metallic
        if "Specular IOR Level" in bsdf.inputs:
            bsdf.inputs["Specular IOR Level"].default_value = spec
        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = emission_strength

        vcol = nt.nodes.new("ShaderNodeVertexColor")
        vcol.location = (-200, 0)
        vcol.layer_name = "Col"

        nt.links.new(vcol.outputs["Color"], bsdf.inputs["Base Color"])
        if emission_strength > 0.0 and "Emission Color" in bsdf.inputs:
            nt.links.new(vcol.outputs["Color"], bsdf.inputs["Emission Color"])
        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
        mats.append(mat)
    return mats


# Face definitions: (neighbor_offset, 4 corner offsets in unit cube [0..1]^3 ordered CCW from outside)
FACES = [
    ((1, 0, 0), ((1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1))),
    ((-1, 0, 0), ((0, 1, 0), (0, 0, 0), (0, 0, 1), (0, 1, 1))),
    ((0, 1, 0), ((1, 1, 0), (0, 1, 0), (0, 1, 1), (1, 1, 1))),
    ((0, -1, 0), ((0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1))),
    ((0, 0, 1), ((0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1))),
    ((0, 0, -1), ((0, 1, 0), (1, 1, 0), (1, 0, 0), (0, 0, 0))),
]


def build_voxel_object(
    name: str,
    voxels: dict[tuple[int, int, int], tuple[int, str]],
    collection: bpy.types.Collection,
    materials: list[bpy.types.Material],
    voxel_size: float = VOXEL_SIZE,
    center_offset: Vector = Vector((0.0, 0.0, 0.0)),
) -> bpy.types.Object:
    if not voxels:
        raise ValueError(f"Empty voxel dict for {name}")

    xs = [p[0] for p in voxels]
    ys = [p[1] for p in voxels]
    zs = [p[2] for p in voxels]
    mid_x = (min(xs) + max(xs) + 1) * 0.5
    mid_y = (min(ys) + max(ys) + 1) * 0.5
    mid_z = (min(zs) + max(zs) + 1) * 0.5

    bm = bmesh.new()
    vert_cache: dict[tuple[int, int, int], bmesh.types.BMVert] = {}

    def get_vert(gx: int, gy: int, gz: int) -> bmesh.types.BMVert:
        key = (gx, gy, gz)
        v = vert_cache.get(key)
        if v is None:
            v = bm.verts.new((
                (gx - mid_x) * voxel_size,
                (gy - mid_y) * voxel_size,
                (gz - mid_z) * voxel_size,
            ))
            vert_cache[key] = v
        return v

    face_colors: list[tuple[float, float, float, float]] = []
    color_cache: dict[str, tuple[float, float, float, float]] = {}

    for (vx, vy, vz), (mat_idx, hex_col) in voxels.items():
        rgba = color_cache.get(hex_col)
        if rgba is None:
            rgba = hex_to_linear_rgba(hex_col)
            color_cache[hex_col] = rgba

        for (dx, dy, dz), corners in FACES:
            if (vx + dx, vy + dy, vz + dz) in voxels:
                continue
            fverts = [get_vert(vx + cx, vy + cy, vz + cz) for cx, cy, cz in corners]
            face = bm.faces.new(fverts)
            face.material_index = mat_idx
            face.smooth = False
            face_colors.append(rgba)

    bm.normal_update()

    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm.to_mesh(mesh)
    bm.free()

    for mat in materials:
        mesh.materials.append(mat)

    col_attr = mesh.color_attributes.new(name="Col", type="FLOAT_COLOR", domain="CORNER")
    for poly, rgba in zip(mesh.polygons, face_colors):
        poly.use_smooth = False
        for li in poly.loop_indices:
            col_attr.data[li].color = rgba

    obj = bpy.data.objects.new(name, mesh)
    obj.location = center_offset
    collection.objects.link(obj)
    return obj


# ---------------------------------------------------------------------------
# Voxel Sculpture Helpers
# ---------------------------------------------------------------------------

def set_box(
    vox: dict,
    x0: int, x1: int,
    y0: int, y1: int,
    z0: int, z1: int,
    mat: int,
    color: str,
):
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            for z in range(z0, z1 + 1):
                vox[(x, y, z)] = (mat, color)


def set_vox(vox: dict, x: int, y: int, z: int, mat: int, color: str):
    vox[(x, y, z)] = (mat, color)


# ---------------------------------------------------------------------------
# 1. Tab_Upgrades — Chunky 3D Emerald Up-Arrow + Steel/Gold Collar + Sparkles
# ---------------------------------------------------------------------------
def build_upgrades_voxels() -> dict:
    v: dict = {}
    EM = "#28CF72"
    EM_HI = "#70F5A8"
    EM_MID = "#47E38B"
    EM_DK = "#147D44"
    STEEL = "#38416B"
    STEEL_HI = "#57659C"
    GOLD = "#FFC019"
    GOLD_HI = "#FFE66E"
    GOLD_DK = "#CC740A"
    GLOW_Y = "#FFD93D"
    GLOW_W = "#FFF4A3"

    # Shaft: x in [-3..3] (7 wide), y in [-2..2] (5 deep), z in [-8..-1]
    for z in range(-8, 0):
        for y in range(-2, 3):
            for x in range(-3, 4):
                if z in (-7, -6):
                    col = STEEL_HI if (x <= -2 or y == -2) else STEEL
                    set_vox(v, x, y, z, MAT_MATTE, col)
                else:
                    if x >= 2 or y >= 2 or z == -8:
                        col = EM_DK
                    elif x <= -2:
                        col = EM_HI
                    else:
                        col = EM
                    set_vox(v, x, y, z, MAT_MATTE, col)

    # Gold clasp / stud on front of steel collar (y = -3)
    for x in range(-1, 2):
        for z in range(-7, -5):
            set_vox(v, x, -3, z, MAT_METAL, GOLD_HI if (x == 0 and z == -6) else GOLD)

    # Arrow head: z in [0..8], y in [-2..2], stepped pyramid width
    half_widths = {
        0: 7,
        1: 6,
        2: 6,
        3: 5,
        4: 4,
        5: 3,
        6: 2,
        7: 1,
        8: 0,
    }
    for z, hw in half_widths.items():
        for y in range(-2, 3):
            for x in range(-hw, hw + 1):
                if z == 0 and abs(x) >= 4:
                    col = EM_DK
                elif (x >= hw - 1 and hw >= 2) or y >= 2:
                    col = EM_DK
                elif x <= -hw + 1 or z >= 7:
                    col = EM_HI
                else:
                    col = EM
                set_vox(v, x, y, z, MAT_MATTE, col)

    # Raised 3D front chevron ridge (y = -3) on arrowhead + shaft center spine
    for z in range(-4, 0):
        for x in range(-1, 2):
            set_vox(v, x, -3, z, MAT_MATTE, EM_HI if x == -1 else EM_MID)
    chevron_rows = [(1, 4), (2, 4), (3, 3), (4, 2), (5, 1), (6, 0)]
    for z, hw in chevron_rows:
        for x in range(-hw, hw + 1):
            if abs(x) >= max(0, hw - 1) or z >= 5:
                col = EM_HI if x <= 0 else EM_MID
                set_vox(v, x, -3, z, MAT_MATTE, col)

    # Back bevel ridge (y = +3) for 3D symmetry
    for z in range(1, 5):
        hw = 5 - z
        for x in range(-hw, hw + 1):
            set_vox(v, x, 3, z, MAT_MATTE, EM_DK)

    # Floating 3D golden 4-point plus-star sparkle at upper-right (cleanly separated from arrow)
    sx, sy, sz = 8, -2, 6
    set_vox(v, sx, sy, sz, MAT_GLOW, GLOW_W)
    for dx, dz in ((-1, 0), (1, 0), (0, -1), (0, 1)):
        set_vox(v, sx + dx, sy, sz + dz, MAT_GLOW, GLOW_Y)
    # Smaller 2x2 diamond accent at lower-right
    set_vox(v, 7, -2, -4, MAT_GLOW, GLOW_W)
    set_vox(v, 8, -2, -4, MAT_GLOW, GLOW_Y)
    set_vox(v, 7, -2, -5, MAT_GLOW, GLOW_Y)

    return v


# ---------------------------------------------------------------------------
# 2. Tab_Abilities — Standing 3D Arcane Grimoire with Symmetric Gold Star & Cyan Gem
# ---------------------------------------------------------------------------
def build_abilities_voxels() -> dict:
    v: dict = {}
    PURP = "#8443F6"
    PURP_HI = "#B080FF"
    PURP_DK = "#531FB5"
    SPINE = "#3F138F"
    PAGE = "#FAF3E0"
    PAGE_SH = "#DFD2B2"
    GOLD = "#FFC019"
    GOLD_HI = "#FFE66E"
    GOLD_DK = "#C76E08"
    CYAN = "#38E4FF"
    CYAN_W = "#B8F8FF"

    # Book spans x in [-7..6] (14 wide), y in [-3..3] (7 thick), z in [-9..9] (19 tall)
    for z in range(-9, 10):
        for y in range(-3, 4):
            for x in range(-7, 7):
                is_spine = x <= -6
                is_front = y == -3
                is_back = y == 3

                if is_spine:
                    if abs(y) == 3 and x == -7:
                        continue
                    col = PURP_HI if (y == -2 and z >= 6) else SPINE
                    set_vox(v, x, y, z, MAT_MATTE, col)
                elif is_front or is_back:
                    if x == 6 and abs(z) == 9:
                        continue
                    if is_front:
                        if z == 9 or x == -5:
                            col = PURP_HI
                        elif z == -9 or x == 6:
                            col = PURP_DK
                        elif -4 <= x <= 4 and -7 <= z <= 7 and (abs(x) == 4 or abs(z) == 7):
                            col = PURP_DK
                        else:
                            col = PURP
                    else:
                        col = PURP_DK
                    set_vox(v, x, y, z, MAT_MATTE, col)
                else:
                    if x <= 5 and abs(z) <= 8:
                        col = PAGE_SH if (z % 2 == 0 or x == 5) else PAGE
                        set_vox(v, x, y, z, MAT_MATTE, col)

    # Gold spine ribs (3 horizontal bands wrapping spine)
    for rz in (-6, 0, 6):
        for y in range(-3, 4):
            for x in (-7, -6):
                if abs(y) == 3 and x == -7:
                    continue
                set_vox(v, x, y, rz, MAT_METAL, GOLD_HI if y <= -1 else GOLD)

    # Gold corner clasps on outer front cover corners (y in [-4..-3])
    for sz in (-1, 1):
        z_base = 8 if sz > 0 else -8
        for dx in (4, 5, 6):
            for dz in (0, -sz):
                if dx == 6 and abs(z_base + dz) == 9:
                    continue
                set_vox(v, dx, -3, z_base + dz, MAT_METAL, GOLD)
        set_vox(v, 5, -4, z_base, MAT_METAL, GOLD_HI if sz > 0 else GOLD)
        set_vox(v, 6, -4, z_base - sz, MAT_METAL, GOLD_DK)

    # Crisp symmetric 9x10 5-point Gold Star on front cover (y = -4, centered at x=0, z=0)
    star_rows = {
        5:  [0],
        4:  [0],
        3:  [-1, 0, 1],
        2:  [-4, -3, -2, -1, 0, 1, 2, 3, 4],
        1:  [-3, -2, -1, 0, 1, 2, 3],
        0:  [-2, -1, 0, 1, 2],
        -1: [-2, -1, 0, 1, 2],
        -2: [-3, -2, -1, 1, 2, 3],
        -3: [-3, -2, 2, 3],
        -4: [-4, 4],
    }
    for sz, xs_list in star_rows.items():
        for sx in xs_list:
            col = GOLD_HI if (sz >= 3 or (sz == 2 and sx <= -2)) else GOLD
            set_vox(v, sx, -4, sz, MAT_METAL, col)

    # Glowing Cyan Arcane Gem protruding from star center (y = -5..-4)
    set_vox(v, 0, -4, 1, MAT_GLOW, CYAN)
    set_vox(v, 0, -5, 1, MAT_GLOW, CYAN_W)

    # Gold bookmark ribbon hanging out the bottom page block
    for z in range(-11, -8):
        set_vox(v, 2, -1, z, MAT_METAL, GOLD)
        set_vox(v, 2, 0, z, MAT_METAL, GOLD_DK)

    return v


# ---------------------------------------------------------------------------
# 3. Tab_Play — Crossed 3D Heroic Voxel Broadswords (Solid Blades, Depth-Offset X)
# ---------------------------------------------------------------------------
def build_play_voxels() -> dict:
    v: dict = {}
    STL_HI = "#F5F9FF"
    STL = "#C4D6F0"
    STL_SH = "#7E96BD"
    STL_DK = "#52678C"
    GOLD_HI = "#FFE66E"
    GOLD = "#FFC019"
    GOLD_DK = "#BA6C0B"
    GRIP = "#784220"
    GRIP_DK = "#4E2710"
    GEM = "#28B8FF"
    GEM_HI = "#6EE0FF"

    def draw_sword(flip_x: bool, y_center: int):
        sx = -1 if flip_x else 1

        # Solid 2-diagonal-thick staircase blade from u = -2 .. +8
        # Filling (u, u), (u-1, u), (u, u-1), plus sharp outer bevels so ZERO holes appear!
        for u in range(-2, 9):
            cx = sx * u
            cz = u
            # Core spine (3 voxels deep in Y)
            for dy in (-1, 0, 1):
                col = STL_HI if (dy == -1 and u >= 1) else (STL_SH if dy == 1 else STL)
                set_vox(v, cx, y_center + dy, cz, MAT_METAL, col)

            if u <= 7:
                # Step-fill upper-left neighbor (cx - sx, cz) and lower-right (cx, cz - 1)
                # so the diagonal blade forms a completely solid voxel ribbon!
                for dy in (-1, 0, 1):
                    col_u = STL_HI if dy <= 0 else STL
                    col_l = STL_SH if dy >= 0 else STL
                    set_vox(v, cx - sx, y_center + dy, cz, MAT_METAL, col_u)
                    set_vox(v, cx, y_center + dy, cz - 1, MAT_METAL, col_l)

            if u <= 6:
                # Cutting edges (1 voxel deep at y_center for crisp tapered diamond cross-section)
                set_vox(v, cx - sx, y_center, cz + 1, MAT_METAL, STL_HI)
                set_vox(v, cx + sx, y_center, cz - 1, MAT_METAL, STL_DK if not flip_x else STL_SH)

        # Sharp point at tip (sx*9, y_center, 9)
        set_vox(v, sx * 9, y_center, 9, MAT_METAL, STL_HI)
        set_vox(v, sx * 8, y_center, 9, MAT_METAL, STL_HI)
        set_vox(v, sx * 9, y_center, 8, MAT_METAL, STL)

        # Solid stepped crossguard around u = -3 (perpendicular: x = sx*(-3 + t), z = -3 - t)
        for t in range(-3, 4):
            gx = sx * (-3 + t)
            gz = -3 - t
            for dx_step, dz_step in ((0, 0), (-sx, 0), (0, -1)):
                for dy in (-1, 0, 1):
                    col = GOLD_HI if (dy == -1 and abs(t) <= 1) else (GOLD_DK if dy == 1 else GOLD)
                    set_vox(v, gx + dx_step, y_center + dy, gz + dz_step, MAT_METAL, col)
        # Upturned crossguard tips
        set_vox(v, sx * (-6), y_center, 1, MAT_METAL, GOLD_HI)
        set_vox(v, sx * 0, y_center, -7, MAT_METAL, GOLD_DK)
        # Crossguard center gem on front
        set_vox(v, sx * (-3), y_center - 2, -3, MAT_GLOW, GEM_HI if not flip_x else GEM)

        # Solid leather grip u = -4 .. -6
        for u in range(-6, -3):
            gx = sx * u
            gz = u
            for dx_step, dz_step in ((0, 0), (-sx, 0), (0, -1)):
                for dy in (-1, 0, 1):
                    col = GRIP_DK if dy == 1 else GRIP
                    set_vox(v, gx + dx_step, y_center + dy, gz + dz_step, MAT_MATTE, col)

        # Chunky gold pommel at u = -7 .. -8
        for px, pz in ((sx * (-7), -7), (sx * (-8), -8), (sx * (-7), -8), (sx * (-8), -7)):
            for dy in (-1, 0, 1):
                col = GOLD_HI if dy == -1 else (GOLD_DK if dy == 1 else GOLD)
                set_vox(v, px, y_center + dy, pz, MAT_METAL, col)

    # Background sword (y_center = +2) & foreground sword (y_center = -1)
    draw_sword(flip_x=True, y_center=2)
    draw_sword(flip_x=False, y_center=-1)

    return v


# ---------------------------------------------------------------------------
# 4. Tab_Achievements — 3D Hollow Gold Trophy Cup + Ruby Gem + Walnut Base
# ---------------------------------------------------------------------------
def build_achievements_voxels() -> dict:
    v: dict = {}
    GOLD = "#FFC019"
    GOLD_HI = "#FFE875"
    GOLD_SH = "#D97C10"
    GOLD_IN = "#A65108"
    GOLD_DEEP = "#753304"
    WOOD = "#6E3B1E"
    WOOD_HI = "#94552E"
    WOOD_DK = "#47220F"
    RUBY = "#FF3B5C"
    RUBY_HI = "#FF8FA3"

    # Tier 1 bottom plinth: z in [-9..-7], x in [-5..5], y in [-4..4]
    for z in range(-9, -6):
        for y in range(-4, 5):
            for x in range(-5, 6):
                if abs(x) == 5 and abs(y) == 4:
                    continue
                if z == -7 and (abs(x) <= 2 and y == -4):
                    col = WOOD_HI
                elif y == -4 or x <= -4:
                    col = WOOD_HI if z == -7 else WOOD
                else:
                    col = WOOD_DK if (z == -9 or y >= 3) else WOOD
                set_vox(v, x, y, z, MAT_MATTE, col)

    # Tier 2 upper plinth: z in [-6..-5], x in [-4..4], y in [-3..3]
    for z in (-6, -5):
        for y in range(-3, 4):
            for x in range(-4, 5):
                if abs(x) == 4 and abs(y) == 3:
                    continue
                col = WOOD_HI if (z == -5 and y == -3) else (WOOD_DK if y >= 2 else WOOD)
                set_vox(v, x, y, z, MAT_MATTE, col)

    # Gold front nameplate on plinth (y = -4, z = -8, x in [-2..2])
    for x in range(-2, 3):
        set_vox(v, x, -4, -8, MAT_METAL, GOLD_HI if x == -1 else GOLD)

    # Gold stem foot: z = -4, x in [-3..3], y in [-3..3]
    for y in range(-3, 4):
        for x in range(-3, 4):
            if abs(x) == 3 and abs(y) == 3:
                continue
            set_vox(v, x, y, -4, MAT_METAL, GOLD_HI if y == -3 else GOLD_SH)

    # Stem column: z in [-3..-1], x in [-1..1], y in [-1..1]
    for z in range(-3, 0):
        for y in range(-1, 2):
            for x in range(-1, 2):
                set_vox(v, x, y, z, MAT_METAL, GOLD_HI if x == -1 else (GOLD_SH if x == 1 else GOLD))
    for x, y in ((-2, 0), (2, 0), (0, -2), (0, 2)):
        set_vox(v, x, y, -2, MAT_METAL, GOLD)

    # Trophy bowl: z in [0..7], hollow inside
    bowl_profile = {
        0: (3, 3, False),
        1: (4, 4, False),
        2: (5, 4, True),
        3: (5, 5, True),
        4: (5, 5, True),
        5: (5, 5, True),
        6: (6, 5, True),
        7: (6, 5, True),
    }
    for z, (rx, ry, hollow) in bowl_profile.items():
        for y in range(-ry, ry + 1):
            for x in range(-rx, rx + 1):
                if abs(x) == rx and abs(y) == ry:
                    continue
                if rx >= 5 and abs(x) + abs(y) >= rx + ry - 1:
                    continue

                inner_rx = rx - 1
                inner_ry = ry - 1
                is_inner = (
                    hollow
                    and abs(x) < inner_rx
                    and abs(y) < inner_ry
                    and (abs(x) + abs(y) < inner_rx + inner_ry - 1)
                )
                if is_inner:
                    if z == 2:
                        set_vox(v, x, y, z, MAT_METAL, GOLD_DEEP)
                    continue

                if z == 7:
                    col = GOLD_HI
                elif x <= -rx + 1 and y <= 0:
                    col = GOLD_HI
                elif x >= rx - 1 or y >= ry - 1 or z <= 1:
                    col = GOLD_SH
                elif abs(x) <= inner_rx and abs(y) <= inner_ry and z >= 4:
                    col = GOLD_IN if (x > -inner_rx and y > -inner_ry) else GOLD
                else:
                    col = GOLD
                set_vox(v, x, y, z, MAT_METAL, col)

    # Ruby Star/Gem emblem on front belly of cup (y = -5..-6, z = 3..5)
    for dx, dz in ((0, 4), (-1, 4), (1, 4), (0, 5), (0, 3)):
        set_vox(v, dx, -6, dz, MAT_GLOW, RUBY_HI if (dx == 0 and dz == 4) else RUBY)
    for dx, dz in ((-1, 5), (1, 5), (-1, 3), (1, 3)):
        set_vox(v, dx, -5, dz, MAT_METAL, GOLD_SH)

    # Twin 3D curved handles (x = -8..-6 and +6..+8, z = 1..6), 2 voxels deep for crisp silhouette
    for side in (-1, 1):
        hx_in = side * 6
        hx_mid = side * 7
        hx_out = side * 8
        for y in (-1, 0):
            set_vox(v, hx_in, y, 6, MAT_METAL, GOLD_HI if y == -1 else GOLD)
            set_vox(v, hx_mid, y, 6, MAT_METAL, GOLD_HI if side < 0 else GOLD)
            for z in (3, 4, 5):
                set_vox(v, hx_out, y, z, MAT_METAL, GOLD_HI if (side < 0 and y == -1) else GOLD)
            set_vox(v, hx_mid, y, 2, MAT_METAL, GOLD_SH)
            set_vox(v, hx_in, y, 1, MAT_METAL, GOLD_SH)

    return v


# ---------------------------------------------------------------------------
# 5. Tab_Bonus — 3D Crimson Gift Box + Overhanging Lid + Gold Ribbon & Bow
# ---------------------------------------------------------------------------
def build_bonus_voxels() -> dict:
    v: dict = {}
    RED = "#DE2644"
    RED_HI = "#FF637B"
    RED_LID = "#ED3452"
    RED_DK = "#991229"
    GOLD = "#FFC019"
    GOLD_HI = "#FFE66E"
    GOLD_DK = "#C76E08"

    # Box body: x in [-6..6], y in [-6..6], z in [-8..0]
    for z in range(-8, 1):
        for y in range(-6, 7):
            for x in range(-6, 7):
                if z == -8 or x >= 5 or y >= 5:
                    col = RED_DK
                elif x <= -5 and y <= 0:
                    col = RED_HI
                else:
                    col = RED
                set_vox(v, x, y, z, MAT_MATTE, col)

    # Overhanging lid: x in [-7..7], y in [-7..7], z in [1..4]
    for z in range(1, 5):
        for y in range(-7, 8):
            for x in range(-7, 8):
                if abs(x) == 7 and abs(y) == 7:
                    continue
                if z == 4 and (x <= -3 or y <= -3):
                    col = RED_HI
                elif z == 4:
                    col = RED_LID
                elif z == 1 or x >= 6 or y >= 6:
                    col = RED_DK
                elif x <= -6:
                    col = RED_HI
                else:
                    col = RED_LID
                set_vox(v, x, y, z, MAT_MATTE, col)

    # Gold ribbon bands proud by 1 voxel around body (x/y in [-1..1]) and lid
    for z in range(-8, 1):
        for y in range(-7, 8):
            for x in range(-1, 2):
                if abs(y) == 7:
                    col = GOLD_HI if (y == -7 and x <= 0) else (GOLD_DK if z == -8 else GOLD)
                    set_vox(v, x, y, z, MAT_METAL, col)
        for x in range(-7, 8):
            for y in range(-1, 2):
                if abs(x) == 7:
                    col = GOLD_HI if x == -7 else (GOLD_DK if z == -8 else GOLD)
                    set_vox(v, x, y, z, MAT_METAL, col)

    # Lid ribbon bands proud by 1 voxel (x/y in [-8..8], z in [1..5])
    for z in range(1, 6):
        for y in range(-8, 9):
            for x in range(-1, 2):
                if z == 5 or abs(y) == 8:
                    col = GOLD_HI if (z == 5 and (y <= 0 or x <= 0)) else (GOLD_DK if z == 1 else GOLD)
                    set_vox(v, x, y, z, MAT_METAL, col)
        for x in range(-8, 9):
            for y in range(-1, 2):
                if z == 5 or abs(x) == 8:
                    col = GOLD_HI if (z == 5 or x == -8) else (GOLD_DK if z == 1 else GOLD)
                    set_vox(v, x, y, z, MAT_METAL, col)

    # Central bow knot: x in [-1..1], y in [-1..1], z in [6..7]
    set_box(v, -1, 1, -1, 1, 6, 7, MAT_METAL, GOLD_HI)
    set_vox(v, 1, 1, 6, MAT_METAL, GOLD_DK)

    # Two sculpted 3D hollow bow loops along X (left and right)
    for side in (-1, 1):
        for ix in range(2, 6):
            for y in range(-1, 2):
                z_top = 9 if ix in (2, 5) else 10
                set_vox(v, side * ix, y, z_top, MAT_METAL, GOLD_HI if y <= 0 else GOLD)
                if ix in (3, 4):
                    set_vox(v, side * ix, y, z_top - 1, MAT_METAL, GOLD)
        for z in (6, 7, 8):
            for y in range(-1, 2):
                set_vox(v, side * 6, y, z, MAT_METAL, GOLD_HI if (side < 0 and y <= 0) else GOLD)
        for ix in (2, 3, 4, 5):
            for y in range(-1, 2):
                z_bot = 6 if ix in (2, 5) else 5
                set_vox(v, side * ix, y, z_bot, MAT_METAL, GOLD_DK if y == 1 else GOLD)

        # Diagonal ribbon tails resting on lid front-diagonal
        set_vox(v, side * 3, -3, 5, MAT_METAL, GOLD)
        set_vox(v, side * 4, -4, 5, MAT_METAL, GOLD_HI if side < 0 else GOLD)
        set_vox(v, side * 5, -4, 5, MAT_METAL, GOLD_DK)

    return v


# ---------------------------------------------------------------------------
# 6. Tab_Constellations — 3D Constellation Star Map (Gold Stars + Cyan Core Shard)
# ---------------------------------------------------------------------------
def build_constellations_voxels() -> dict:
    v: dict = {}
    IND = "#363E70"
    IND_HI = "#5765A3"
    IND_DK = "#21264A"
    GOLD = "#FFC019"
    GOLD_HI = "#FFE66E"
    GOLD_DK = "#BA6C0B"
    CYAN = "#38E4FF"
    CYAN_HI = "#B8F8FF"
    CYAN_DK = "#1898B8"
    GLOW_Y = "#FFF4A3"

    def draw_beam(x0: int, z0: int, x1: int, z1: int, y_min: int, y_max: int, mat: int, col_hi: str, col_mid: str, col_dk: str):
        steps = max(abs(x1 - x0), abs(z1 - z0)) * 2
        if steps == 0:
            return
        for s in range(steps + 1):
            t = s / float(steps)
            cx = int(round(x0 + (x1 - x0) * t))
            cz = int(round(z0 + (z1 - z0) * t))
            for dx, dz in ((0, 0), (1, 0), (0, 1)):
                for y in range(y_min, y_max + 1):
                    col = col_hi if y == y_min else (col_dk if y == y_max else col_mid)
                    set_vox(v, cx + dx, y, cz + dz, mat, col)

    # Outer indigo astrolabe ring (r ~ 6.6) backing the constellation so the silhouette is chunky
    for z in range(-8, 9):
        for x in range(-8, 9):
            dist = math.hypot(x, z - 0.5)
            if 5.5 <= dist <= 7.5:
                for y in (0, 1):
                    if y == 0 and x <= 0 and z >= 0:
                        col = IND_HI
                    elif y == 1 or x >= 4 or z <= -4:
                        col = IND_DK
                    else:
                        col = IND
                    set_vox(v, x, y, z, MAT_MATTE, col)

    # Constellation star positions (x, z)
    nodes = [
        (0, 7),    # Top star
        (-6, -5),  # Bottom-left star
        (6, -5),   # Bottom-right star
    ]

    # Golden beams from central core to each star + subtle cross-links
    for nx, nz in nodes:
        draw_beam(0, 0, nx, nz, -1, 1, MAT_METAL, GOLD_HI, GOLD, GOLD_DK)

    # Central Gold Ring Frame around the Cyan Boss Shard Core
    for z in range(-3, 4):
        for x in range(-3, 4):
            if abs(x) + abs(z) <= 4 and abs(x) + abs(z) >= 2:
                for y in (-1, 0, 1):
                    col = GOLD_HI if (y == -1 and x <= 0) else (GOLD_DK if y == 1 else GOLD)
                    set_vox(v, x, y, z, MAT_METAL, col)

    # Central Glowing Cyan Boss Shard (y in [-2..1], tall diamond)
    shard_pts = [
        (0, 3), (-1, 2), (0, 2), (1, 2),
        (-2, 1), (-1, 1), (0, 1), (1, 1), (2, 1),
        (-2, 0), (-1, 0), (0, 0), (1, 0), (2, 0),
        (-2, -1), (-1, -1), (0, -1), (1, -1), (2, -1),
        (-1, -2), (0, -2), (1, -2), (0, -3),
    ]
    for sx, sz in shard_pts:
        for y in (-2, 0, -1):
            if abs(sx) == 2 and y == -2:
                continue
            col = CYAN_HI if (sx <= 0 and sz >= 0 and y <= -1) else (CYAN_DK if (sx > 0 and sz < 0) else CYAN)
            set_vox(v, sx, y, sz, MAT_GLOW, col)
    set_vox(v, 0, -3, 0, MAT_GLOW, CYAN_HI)
    set_vox(v, 0, -3, 1, MAT_GLOW, CYAN_HI)

    # 3 Chunky Gold Star Nodes at the constellation tips
    star_offsets = [
        (0, 2),
        (-1, 1), (0, 1), (1, 1),
        (-2, 0), (-1, 0), (0, 0), (1, 0), (2, 0),
        (-1, -1), (0, -1), (1, -1),
        (0, -2),
    ]
    for nx, nz in nodes:
        for dx, dz in star_offsets:
            for y in (-2, -1, 0, 1):
                if abs(dx) + abs(dz) == 2 and y in (-2, 1):
                    continue
                if dx == 0 and dz == 0 and y == -2:
                    set_vox(v, nx + dx, y, nz + dz, MAT_GLOW, GLOW_Y)
                else:
                    col = GOLD_HI if (dx <= 0 and dz >= 0 and y <= -1) else (GOLD_DK if y >= 1 else GOLD)
                    set_vox(v, nx + dx, y, nz + dz, MAT_METAL, col)

    return v


# ---------------------------------------------------------------------------
# Studio Lighting & Camera Framing
# ---------------------------------------------------------------------------

def setup_studio(scene: bpy.types.Scene) -> tuple[bpy.types.Collection, bpy.types.Object]:
    studio_col = bpy.data.collections.new("Studio")
    scene.collection.children.link(studio_col)

    # Render settings
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = RENDER_RES
    scene.render.resolution_y = RENDER_RES
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"

    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0

    if hasattr(scene, "eevee") and hasattr(scene.eevee, "taa_render_samples"):
        scene.eevee.taa_render_samples = 32

    # World soft cool-indigo ambient
    if scene.world is None:
        scene.world = bpy.data.worlds.new("IconWorld")
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.30, 0.32, 0.46, 1.0)
        bg.inputs[1].default_value = 0.65

    # 3-Point Studio Sun Lights (based on SecretChest.blend)
    def add_sun(name: str, energy: float, color: tuple[float, float, float], rot_deg: tuple[float, float, float]):
        ldata = bpy.data.lights.new(name=name, type="SUN")
        ldata.energy = energy
        ldata.color = color
        if hasattr(ldata, "angle"):
            ldata.angle = math.radians(12.0)
        lobj = bpy.data.objects.new(name, ldata)
        lobj.rotation_euler = tuple(math.radians(a) for a in rot_deg)
        studio_col.objects.link(lobj)
        return lobj

    add_sun("KeyLight", 3.1, (1.0, 0.96, 0.88), (48.0, 12.0, -32.0))
    add_sun("FillLight", 1.35, (0.74, 0.86, 1.0), (58.0, -15.0, 132.0))
    add_sun("RimLight", 1.35, (0.95, 0.98, 1.0), (36.0, 0.0, 174.0))

    # Camera
    cam_data = bpy.data.cameras.new("IconCam")
    cam_data.type = "PERSP"
    cam_data.lens = 85.0
    cam_data.sensor_width = 36.0
    cam_data.clip_start = 0.1
    cam_data.clip_end = 100.0
    cam_obj = bpy.data.objects.new("IconCam", cam_data)
    studio_col.objects.link(cam_obj)
    scene.camera = cam_obj

    return studio_col, cam_obj


def frame_camera_on_object(
    cam_obj: bpy.types.Object,
    target_obj: bpy.types.Object,
    azimuth_deg: float,
    elevation_deg: float,
    fill_ratio: float = 0.80,
):
    """Places telephoto camera at (azimuth, elevation) around target_obj and solves exact distance so
    the projected 3D bounding box fits inside fill_ratio of the square render frame."""
    target_center = target_obj.location.copy()
    az = math.radians(azimuth_deg)
    el = math.radians(elevation_deg)

    # Direction from target toward camera (-Y is front at az=0)
    dir_to_cam = Vector((
        math.sin(az) * math.cos(el),
        -math.cos(az) * math.cos(el),
        math.sin(el),
    )).normalized()

    # Build camera basis (camera -Z points along -dir_to_cam, camera +Y up)
    forward = -dir_to_cam
    world_up = Vector((0.0, 0.0, 1.0))
    right = forward.cross(world_up).normalized()
    up = right.cross(forward).normalized()

    rot_mat = Matrix((right, up, -forward)).transposed().to_4x4()
    cam_obj.rotation_euler = rot_mat.to_euler()

    # Compute world-space bbox corners
    depsgraph = bpy.context.evaluated_depsgraph_get()
    eval_obj = target_obj.evaluated_get(depsgraph)
    corners = [target_obj.matrix_world @ Vector(c) for c in eval_obj.bound_box]

    # Half-FOV for square sensor (sensor_width = 36, lens = 85)
    half_fov = math.atan((cam_obj.data.sensor_width * 0.5) / cam_obj.data.lens)
    tan_half = math.tan(half_fov) * fill_ratio

    # Solve minimum distance d so |proj_x| / (d - depth) <= tan_half and |proj_y| / (d - depth) <= tan_half
    req_dist = 1.0
    for c in corners:
        rel = c - target_center
        px = abs(rel.dot(right))
        py = abs(rel.dot(up))
        depth_toward_cam = rel.dot(dir_to_cam)  # positive = closer to camera
        req_x = px / tan_half + depth_toward_cam
        req_y = py / tan_half + depth_toward_cam
        req_dist = max(req_dist, req_x, req_y)

    cam_obj.location = target_center + dir_to_cam * req_dist


# ---------------------------------------------------------------------------
# Post-Process: Optical Centering + #16132A Contour + Soft Drop Shadow -> 256px
# ---------------------------------------------------------------------------

def postprocess_icon(raw_png_path: str, final_png_path: str):
    img = Image.open(raw_png_path).convert("RGBA")
    arr = np.array(img)
    alpha = arr[:, :, 3]

    ys, xs = np.where(alpha > 10)
    if len(xs) == 0:
        raise RuntimeError(f"Empty render in {raw_png_path}")

    x0, x1 = int(xs.min()), int(xs.max()) + 1
    y0, y1 = int(ys.min()), int(ys.max()) + 1
    cropped = img.crop((x0, y0, x1, y1))

    cw, ch = cropped.size
    scale = OPTICAL_MAX_DIM / float(max(cw, ch))
    nw = max(1, int(round(cw * scale)))
    nh = max(1, int(round(ch * scale)))
    resized = cropped.resize((nw, nh), Image.Resampling.LANCZOS)

    # Place centered horizontally, slightly above center vertically to leave room for bottom drop shadow
    canvas = Image.new("RGBA", (RENDER_RES, RENDER_RES), (0, 0, 0, 0))
    px = (RENDER_RES - nw) // 2
    py = (RENDER_RES - nh) // 2 - 4
    canvas.paste(resized, (px, py))

    icon_np = np.array(canvas).astype(np.float32) / 255.0
    icon_a = icon_np[:, :, 3]

    # Exact Euclidean distance-field dilation for razor-smooth circular outline
    h, w = icon_a.shape
    mask = icon_a > 0.35
    r_ceil = int(math.ceil(OUTLINE_RADIUS_512 + 1.5))

    # Build circular kernel offsets
    yy, xx = np.mgrid[-r_ceil:r_ceil + 1, -r_ceil:r_ceil + 1]
    dist_grid = np.sqrt(xx * xx + yy * yy)

    # Compute min distance from each pixel to any mask pixel via shifted max of smooth coverage
    # Actually: for crisp AA outline, take max of (shifted icon_a * radial falloff)
    outline_a = np.zeros_like(icon_a)
    for dy in range(-r_ceil, r_ceil + 1):
        for dx in range(-r_ceil, r_ceil + 1):
            d = math.hypot(dx, dy)
            if d > OUTLINE_RADIUS_512 + 1.2:
                continue
            # Radial weight: 1.0 inside radius-1, smooth AA ramp over [radius-1 .. radius+0.8]
            weight = np.clip((OUTLINE_RADIUS_512 + 0.6 - d) / 1.6, 0.0, 1.0)
            if weight <= 0.0:
                continue
            src_y0 = max(0, -dy)
            src_y1 = min(h, h - dy)
            src_x0 = max(0, -dx)
            src_x1 = min(w, w - dx)
            dst_y0 = max(0, dy)
            dst_y1 = min(h, h + dy)
            dst_x0 = max(0, dx)
            dst_x1 = min(w, w + dx)
            shifted = icon_a[src_y0:src_y1, src_x0:src_x1] * weight
            outline_a[dst_y0:dst_y1, dst_x0:dst_x1] = np.maximum(
                outline_a[dst_y0:dst_y1, dst_x0:dst_x1],
                shifted,
            )

    # Soft drop shadow under the outlined silhouette
    shadow_src = Image.fromarray(np.uint8(np.clip(outline_a * 255.0, 0, 255)), mode="L")
    shadow_shifted = Image.new("L", (RENDER_RES, RENDER_RES), 0)
    shadow_shifted.paste(shadow_src, (0, SHADOW_OFFSET_Y_512))
    shadow_blurred = shadow_shifted.filter(ImageFilter.GaussianBlur(radius=SHADOW_BLUR_512))
    shadow_a = (np.array(shadow_blurred).astype(np.float32) / 255.0) * SHADOW_OPACITY

    # Porter-Duff "over" compositing using premultiplied RGB:
    # Layer 1: shadow -> Layer 2: outline -> Layer 3: icon
    out_premul = np.zeros((RENDER_RES, RENDER_RES, 3), dtype=np.float32)
    out_a = np.zeros((RENDER_RES, RENDER_RES), dtype=np.float32)

    def over_layer(rgb_01: tuple[float, float, float], layer_a: np.ndarray, layer_rgb: np.ndarray = None):
        nonlocal out_premul, out_a
        if layer_rgb is None:
            l_premul = np.stack([
                layer_a * rgb_01[0],
                layer_a * rgb_01[1],
                layer_a * rgb_01[2],
            ], axis=-1)
        else:
            l_premul = layer_rgb * layer_a[:, :, None]
        out_premul = l_premul + out_premul * (1.0 - layer_a[:, :, None])
        out_a = layer_a + out_a * (1.0 - layer_a)

    over_layer((SHADOW_RGB[0] / 255.0, SHADOW_RGB[1] / 255.0, SHADOW_RGB[2] / 255.0), shadow_a)
    over_layer((OUTLINE_RGB[0] / 255.0, OUTLINE_RGB[1] / 255.0, OUTLINE_RGB[2] / 255.0), outline_a)
    over_layer((0.0, 0.0, 0.0), icon_a, icon_np[:, :, :3])

    safe_a = np.maximum(out_a, 1e-6)
    final_rgb = np.where(out_a[:, :, None] > 1e-5, out_premul / safe_a[:, :, None], 0.0)
    final_rgba = np.dstack([
        np.clip(final_rgb * 255.0, 0, 255),
        np.clip(out_a * 255.0, 0, 255),
    ]).astype(np.uint8)

    out_512 = Image.fromarray(final_rgba, mode="RGBA")
    out_256 = out_512.resize((FINAL_RES, FINAL_RES), Image.Resampling.LANCZOS)
    os.makedirs(os.path.dirname(final_png_path), exist_ok=True)
    out_256.save(final_png_path, format="PNG")


def frame_viewport_on_showcase(scene: bpy.types.Scene):
    for area in bpy.context.screen.areas:
        if area.type == "VIEW_3D":
            for space in area.spaces:
                if space.type == "VIEW_3D":
                    space.shading.type = "RENDERED"
            region_3d = area.spaces.active.region_3d
            if region_3d:
                region_3d.view_location = Vector((0.0, 0.0, 0.0))
                region_3d.view_distance = 12.5
                region_3d.view_rotation = Vector((1.0, 0.0, 0.0, 0.0))
                # Gentle 3/4 view looking from -Y/+Z
                from mathutils import Euler
                region_3d.view_rotation = Euler((math.radians(68.0), 0.0, math.radians(-12.0)), "XYZ").to_quaternion()


def main():
    clear_scene()
    scene = bpy.context.scene
    materials = create_voxel_materials()
    _, cam_obj = setup_studio(scene)

    icon_specs = [
        ("Tab_Upgrades", "icon_tab_upgrades.png", build_upgrades_voxels, -4.4, -22.0, 23.0, 0.80),
        ("Tab_Abilities", "icon_tab_abilities.png", build_abilities_voxels, -2.2, -25.0, 22.0, 0.80),
        ("Tab_Play", "icon_tab_play.png", build_play_voxels, 0.0, -16.0, 18.0, 0.82),
        ("Tab_Achievements", "icon_tab_achievements.png", build_achievements_voxels, 2.2, -21.0, 25.0, 0.81),
        ("Tab_Bonus", "icon_tab_bonus.png", build_bonus_voxels, 4.4, -24.0, 25.0, 0.81),
        ("Tab_Constellations", "icon_tab_constellations.png", build_constellations_voxels, 6.6, -18.0, 22.0, 0.82),
    ]

    collections: list[bpy.types.Collection] = []
    objects: list[bpy.types.Object] = []

    for col_name, _, builder, x_pos, _, _, _ in icon_specs:
        col = bpy.data.collections.new(col_name)
        scene.collection.children.link(col)
        vox = builder()
        obj = build_voxel_object(col_name, vox, col, materials, VOXEL_SIZE, Vector((x_pos, 0.0, 0.0)))
        collections.append(col)
        objects.append(obj)

    tmp_dir = tempfile.mkdtemp(prefix="projectb_tab_icons_")
    rendered_files = []

    for idx, (col_name, out_name, _, _, az, el, fill_ratio) in enumerate(icon_specs):
        for j, col in enumerate(collections):
            col.hide_render = (j != idx)

        frame_camera_on_object(cam_obj, objects[idx], az, el, fill_ratio)
        raw_path = os.path.join(tmp_dir, f"raw_{out_name}")
        final_path = os.path.join(ICONS_OUT_DIR, out_name)

        scene.render.filepath = raw_path
        bpy.ops.render.render(write_still=True)
        postprocess_icon(raw_path, final_path)
        rendered_files.append(final_path)

    # Restore all collections visible for viewport showcase & save .blend
    for col in collections:
        col.hide_render = False
        col.hide_viewport = False

    # Position camera nicely on Play icon for convenience
    frame_camera_on_object(cam_obj, objects[2], -16.0, 18.0, 0.82)
    frame_viewport_on_showcase(scene)

    os.makedirs(os.path.dirname(BLEND_OUT_PATH), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT_PATH)

    return {
        "blend": BLEND_OUT_PATH,
        "rendered": rendered_files,
        "temp_dir": tmp_dir,
    }


result = main()
