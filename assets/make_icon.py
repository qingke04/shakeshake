from PIL import Image, ImageDraw, ImageFilter
import os

S = 1024
top = (46, 62, 92)
bot = (17, 25, 40)

grad = Image.new("RGBA", (S, S))
gd = ImageDraw.Draw(grad)
for y in range(S):
    t = y / (S - 1)
    gd.line([(0, y), (S, y)], fill=(int(top[0]+(bot[0]-top[0])*t), int(top[1]+(bot[1]-top[1])*t), int(top[2]+(bot[2]-top[2])*t), 255))
mask = Image.new("L", (S, S), 0)
ImageDraw.Draw(mask).rounded_rectangle([0, 0, S-1, S-1], radius=230, fill=255)
img = Image.composite(grad, Image.new("RGBA", (S, S), (0,0,0,0)), mask)

h = 800.0
w = h * 0.62
pts = [(0.02,0.00),(0.02,0.74),(0.26,0.52),(0.41,0.98),(0.60,0.90),(0.45,0.44),(0.74,0.41)]
pad = 70
layer = Image.new("RGBA", (int(w)+pad*2, int(h)+pad*2), (0,0,0,0))
poly = [(pad+p[0]*w, pad+p[1]*h) for p in pts]

sh = Image.new("RGBA", layer.size, (0,0,0,0))
ImageDraw.Draw(sh).polygon([(x+16,y+22) for x,y in poly], fill=(0,0,0,130))
sh = sh.filter(ImageFilter.GaussianBlur(20))
layer = Image.alpha_composite(layer, sh)

ld = ImageDraw.Draw(layer)
ld.polygon(poly, fill=(255,255,255,255))
ld.line(poly+[poly[0]], fill=(255,255,255,255), width=20, joint="curve")

img.alpha_composite(layer, (int((S-layer.width)/2)+18, int((S-layer.height)/2)+6))
img.save("assets/icon_master.png")
sizes = [16,20,24,32,40,48,64,128,256]
img.save("assets/app.ico", format="ICO", sizes=[(s,s) for s in sizes])
for s in (256,32,16):
    im = img.resize((s,s), Image.LANCZOS); im.save("assets/preview_%d.png" % s)
    if s == 16: im.resize((256,256), Image.NEAREST).save("assets/preview_16_zoom.png")
print("ok", os.path.getsize("assets/app.ico"))
