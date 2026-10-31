# Genera assets/app.ico (icono multi-resolución de la aplicación).
from PIL import Image, ImageDraw, ImageFont
import sys

def render(size=256):
    s = size / 256
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # cuerpo
    d.rounded_rectangle([16*s, 28*s, 240*s, 240*s], radius=36*s, fill=(255, 255, 255, 255), outline=(0, 90, 158, 255), width=max(1, int(6*s)))
    # cabecera
    d.rounded_rectangle([16*s, 28*s, 240*s, 92*s], radius=36*s, fill=(0, 120, 212, 255))
    d.rectangle([16*s, 64*s, 240*s, 92*s], fill=(0, 120, 212, 255))
    # anillas
    for x in (76, 180):
        d.rounded_rectangle([(x-10)*s, 8*s, (x+10)*s, 52*s], radius=10*s, fill=(40, 40, 40, 255))
    # columna de semanas
    d.rectangle([28*s, 104*s, 70*s, 228*s], fill=(222, 236, 249, 255))
    # rejilla de días
    for r in range(4):
        for c in range(5):
            x = 82 + c * 30
            y = 108 + r * 30
            col = (0, 120, 212, 255) if (r, c) == (2, 3) else (150, 150, 150, 255)
            d.rounded_rectangle([x*s, y*s, (x+20)*s, (y+20)*s], radius=4*s, fill=col)
        d.rounded_rectangle([38*s, (108 + r*30)*s, 60*s, (128 + r*30)*s], radius=4*s, fill=(0, 90, 158, 255))
    return img

base = render(256)
if sys.argv[1].endswith(".png"):
    base.save(sys.argv[1]); sys.exit()
base.save(sys.argv[1], sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)])
