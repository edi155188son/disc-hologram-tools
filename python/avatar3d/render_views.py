# -*- coding: utf-8 -*-
"""Blender (5.x) headless renderer for the avatar relief.
   blender -b --factory-startup --python render_views.py -- [N_VIEWS] [HALF_ANGLE_DEG]
Writes views/view1..N.png (transparent background, horizontal orbit, left -> right) and preview_*.png.
"""
import bpy, sys, math, os
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
N = int(argv[0]) if len(argv) > 0 else 7
HALF = float(argv[1]) if len(argv) > 1 else 12.0
OUT = argv[2] if len(argv) > 2 else "views"
LIGHT = argv[3] if len(argv) > 3 else "STUDIO"        # STUDIO (shaded) or FLAT (pure texture colours)
PREVIEWS = (argv[4] != "0") if len(argv) > 4 else True
HERE = os.path.dirname(os.path.abspath(__file__))
os.makedirs(os.path.join(HERE, OUT), exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=os.path.join(HERE, "avatar_relief.obj"))   # OBJ +Y up -> Blender +Z, OBJ +Z -> Blender -Y
obj = bpy.context.selected_objects[0]
bpy.ops.object.shade_smooth()

# centre of the front surface
bb = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
ctr = sum(bb, Vector()) / 8.0
size = max((max(v[i] for v in bb) - min(v[i] for v in bb)) for i in range(3))

scn = bpy.context.scene
scn.render.engine = "BLENDER_WORKBENCH"
scn.display.shading.light = LIGHT
scn.display.shading.color_type = "TEXTURE"
scn.display.shading.show_cavity = LIGHT != "FLAT"
scn.display.shading.cavity_type = "WORLD"
scn.render.film_transparent = True
scn.view_settings.view_transform = "Standard"     # AgX (default) darkens/desaturates the texture
scn.view_settings.look = "None"
scn.render.resolution_x = scn.render.resolution_y = 1079
scn.render.image_settings.file_format = "PNG"
scn.render.image_settings.color_mode = "RGBA"

cam_data = bpy.data.cameras.new("cam")
cam = bpy.data.objects.new("cam", cam_data)
scn.collection.objects.link(cam)
scn.camera = cam
DIST = 300.0                                     # mm viewing distance
cam_data.lens_unit = "FOV"
cam_data.angle = 2 * math.atan((size * 0.56) / DIST)
cam_data.clip_end = 5000


def shoot(az_deg, el_deg, path, dist=DIST):
    az, el = math.radians(az_deg), math.radians(el_deg)
    # front view looks along +Y (camera at -Y); positive azimuth = camera moves to +X (viewer's right)
    pos = ctr + Vector((dist * math.sin(az) * math.cos(el), -dist * math.cos(az) * math.cos(el), dist * math.sin(el)))
    cam.location = pos
    cam.rotation_euler = (ctr - pos).to_track_quat("-Z", "Y").to_euler()
    scn.render.filepath = path
    bpy.ops.render.render(write_still=True)


angles = [(-HALF + 2 * HALF * k / (N - 1)) if N > 1 else 0 for k in range(N)]
for k, a in enumerate(angles):
    shoot(a, 0, os.path.join(HERE, OUT, f"view{k+1}.png"))
with open(os.path.join(HERE, OUT, "angles.txt"), "w") as fh:
    fh.write(f"view,azimuth_deg (+ = viewer moves right), light={LIGHT}\n")
    for k, a in enumerate(angles):
        fh.write(f"view{k+1},{a:.2f}\n")
if not PREVIEWS:
    print("done", angles)
    sys.exit(0)

# previews that show the depth (oblique)
scn.render.film_transparent = False
w = bpy.data.worlds.new("w"); scn.world = w
scn.display.shading.background_type = "VIEWPORT"
shoot(0, 0, os.path.join(HERE, "preview_front.png"))
shoot(45, 15, os.path.join(HERE, "preview_right45.png"))
shoot(-70, 10, os.path.join(HERE, "preview_left70.png"))
scn.display.shading.color_type = "SINGLE"
shoot(35, 20, os.path.join(HERE, "preview_clay.png"))
print("done", angles)
