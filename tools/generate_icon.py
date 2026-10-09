from pathlib import Path
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"
ASSETS.mkdir(exist_ok=True)

size = 256
image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
draw = ImageDraw.Draw(image)

draw.rounded_rectangle((8, 8, 248, 248), radius=55, fill=(111, 83, 66, 255))
draw.rounded_rectangle((40, 52, 216, 204), radius=23, outline=(255, 252, 248, 255), width=13)
draw.line((46, 92, 210, 92), fill=(255, 252, 248, 255), width=12)
draw.ellipse((61, 69, 72, 80), fill=(255, 252, 248, 255))
draw.ellipse((82, 69, 93, 80), fill=(255, 252, 248, 255))
draw.line((98, 174, 159, 113), fill=(255, 252, 248, 255), width=15, joint="curve")
draw.line((130, 113, 159, 113, 159, 143), fill=(255, 252, 248, 255), width=13, joint="curve")

image.save(ASSETS / "AppIcon.png")
image.save(ASSETS / "AppIcon.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
