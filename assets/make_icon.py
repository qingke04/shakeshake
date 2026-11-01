"""Generate assets/app.ico (multi-size) and preview PNGs from a master PNG.

The master artwork lives in assets/icon_master.png; this script only scales it
down, it never invents new artwork.

Usage:
    python assets/make_icon.py              # rebuild from assets/icon_master.png
    python assets/make_icon.py new.png      # replace the master, then rebuild
"""
from PIL import Image
import os
import shutil
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
MASTER = os.path.join(HERE, "icon_master.png")

# Sizes Windows asks for: shell/taskbar/tray (16-64), Alt-Tab and file
# properties (128), and the 256px "extra large" view.
SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)


def load_master():
    im = Image.open(MASTER)
    if im.mode != "RGBA":
        im = im.convert("RGBA")
    return im


def fix_ico_planes(path):
    """Pillow writes wPlanes=0 in every ICONDIRENTRY; the spec says 1.

    Windows ignores the field, but stricter icon tools and validators expect
    1, so patch the header after Pillow has written the file.
    """
    with open(path, "r+b") as f:
        data = bytearray(f.read())
        count = struct.unpack_from("<H", data, 4)[0]
        for i in range(count):
            struct.pack_into("<H", data, 6 + i * 16 + 4, 1)
        f.seek(0)
        f.write(data)


def main():
    if len(sys.argv) > 1:
        src = os.path.abspath(sys.argv[1])
        if not os.path.isfile(src):
            raise SystemExit("master not found: %s" % src)
        if src.lower() != MASTER.lower():
            shutil.copyfile(src, MASTER)
        print("master <-", src)

    if not os.path.isfile(MASTER):
        raise SystemExit("master not found: %s" % MASTER)

    master = load_master()
    print("master:", master.size, master.mode)

    # Pre-scale every frame ourselves so each size gets a clean LANCZOS pass
    # from the full-resolution master (no compounding resizes).
    frames = [master.resize((s, s), Image.LANCZOS) for s in SIZES]

    ico = os.path.join(HERE, "app.ico")
    frames[-1].save(
        ico,
        format="ICO",
        sizes=[(s, s) for s in SIZES],
        append_images=frames[:-1],
    )
    fix_ico_planes(ico)

    # Preview PNGs for docs / eyeballing: 256, 32, 16 and an 8x zoom of 16.
    frames[SIZES.index(256)].save(os.path.join(HERE, "preview_256.png"))
    frames[SIZES.index(32)].save(os.path.join(HERE, "preview_32.png"))
    preview16 = frames[SIZES.index(16)]
    preview16.save(os.path.join(HERE, "preview_16.png"))
    preview16.resize((256, 256), Image.NEAREST).save(
        os.path.join(HERE, "preview_16_zoom.png")
    )

    print("wrote:", ico, os.path.getsize(ico), "bytes")


if __name__ == "__main__":
    main()
