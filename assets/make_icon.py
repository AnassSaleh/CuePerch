"""Generates assets/icon.ico (run: python assets/make_icon.py)."""
from PIL import Image, ImageDraw
import os

def draw(size):
    s = size * 4  # supersample
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    r = int(s * 0.22)
    # background gradient (blue -> indigo)
    grad = Image.new("RGBA", (s, s))
    gd = ImageDraw.Draw(grad)
    for y in range(s):
        t = y / (s - 1)
        c = (int(37 + (79 - 37) * t), int(99 + (70 - 99) * t), int(235 + (229 - 235) * t), 255)
        gd.line([(0, y), (s, y)], fill=c)
    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, s - 1, s - 1], radius=r, fill=255)
    img.paste(grad, (0, 0), mask)
    # the floating prompter panel (dark, rounded bottom, flush to top)
    px0, px1 = int(s * 0.16), int(s * 0.84)
    py0, py1 = int(s * 0.0), int(s * 0.52)
    panel = Image.new("L", (s, s), 0)
    pd = ImageDraw.Draw(panel)
    pd.rounded_rectangle([px0, py0 - int(s * 0.2), px1, py1], radius=int(s * 0.12), fill=255)
    pm = Image.new("L", (s, s), 0)
    pm.paste(panel, (0, 0), mask)
    img.paste(Image.new("RGBA", (s, s), (15, 23, 42, 255)), (0, 0), pm)
    # text lines inside the panel
    lw = int(s * 0.055)
    for i, (a, b, alpha) in enumerate([(0.26, 0.74, 255), (0.30, 0.70, 200), (0.34, 0.66, 120)]):
        y = int(s * (0.16 + i * 0.11))
        d.rounded_rectangle([int(s * a), y, int(s * b), y + lw], radius=lw // 2, fill=(255, 255, 255, alpha))
    # little speech waves under the panel
    for i, h in enumerate([0.10, 0.18, 0.26, 0.18, 0.10]):
        x = int(s * (0.34 + i * 0.08))
        cy = int(s * 0.74)
        hh = int(s * h / 2)
        d.rounded_rectangle([x, cy - hh, x + lw, cy + hh], radius=lw // 2, fill=(255, 255, 255, 235))
    return img.resize((size, size), Image.LANCZOS)

here = os.path.dirname(os.path.abspath(__file__))
sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
imgs = [draw(z) for z in sizes]
imgs[-1].save(os.path.join(here, "icon.ico"), sizes=[(z, z) for z in sizes], append_images=imgs[:-1])
imgs[-1].save(os.path.join(here, "icon.png"))
print("ok")
