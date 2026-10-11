"""Gera as texturas procedurais do Buteco Digital (bolas, placas, piso, azulejo, tijolo).

Uso: python3 Tools/gen_textures.py
Saída: Assets/_Buteco/Art/Textures/
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageOps

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "_Buteco", "Art", "Textures")
os.makedirs(OUT, exist_ok=True)

FONT_BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
FONT_SANS = "/usr/share/fonts/truetype/noto/NotoSans-Bold.ttf"

BALL_COLORS = {
    1: (240, 200, 30),
    2: (30, 70, 190),
    3: (210, 35, 35),
    4: (95, 40, 150),
    5: (240, 120, 20),
    6: (20, 130, 60),
    7: (120, 25, 30),
    8: (15, 15, 15),
}


def font(path, size):
    return ImageFont.truetype(path, size)


def ball_texture(n):
    """Textura equiretangular (UV da esfera padrão da Unity)."""
    w, h = 512, 256
    white = (245, 242, 230)
    if n == 0:
        img = Image.new("RGB", (w, h), white)
        d = ImageDraw.Draw(img)
        # pontinho vermelho clássico da bola branca
        for cx in (w * 0.25, w * 0.75):
            d.ellipse([cx - 9, h / 2 - 9, cx + 9, h / 2 + 9], fill=(200, 30, 30))
        return img

    base = BALL_COLORS[n if n <= 8 else n - 8]
    stripe = n >= 9
    img = Image.new("RGB", (w, h), white if stripe else base)
    d = ImageDraw.Draw(img)
    if stripe:
        d.rectangle([0, h * 0.28, w, h * 0.72], fill=base)

    f = font(FONT_BOLD, 40 if n < 10 else 34)
    for cx in (w * 0.25, w * 0.75):
        r = 36
        d.ellipse([cx - r, h / 2 - r, cx + r, h / 2 + r], fill=white)
        txt = str(n)
        bbox = d.textbbox((0, 0), txt, font=f)
        tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
        d.text((cx - tw / 2 - bbox[0], h / 2 - th / 2 - bbox[1]), txt, font=f, fill=(20, 20, 20))
        if n in (6, 9):
            d.line([cx - 10, h / 2 + th / 2 + 4, cx + 10, h / 2 + th / 2 + 4], fill=(20, 20, 20), width=3)
    # a UV da esfera da Unity é espelhada vista de fora
    return ImageOps.mirror(img)


def caquinho_floor():
    """Piso vermelhão com caquinhos (cerâmica quebrada) — clássico de buteco."""
    s = 1024
    img = Image.new("RGB", (s, s), (150, 38, 30))
    d = ImageDraw.Draw(img)
    rnd = random.Random(7)
    pts = [(rnd.uniform(0, s), rnd.uniform(0, s)) for _ in range(260)]
    reds = [(168, 44, 34), (150, 36, 28), (180, 52, 38), (140, 32, 26), (160, 40, 30)]
    accents = [(215, 205, 180), (40, 35, 30), (225, 180, 60)]
    # Voronoi (toroidal, para ladrilhar) vetorizado em resolução 1/4
    import numpy as np
    q = s // 4
    P = np.array(pts) / 4
    ys, xs = np.mgrid[0:q, 0:q]
    best = np.full((q, q), 1e18)
    idx = np.zeros((q, q), dtype=np.int32)
    for i, (sx, sy) in enumerate(P):
        dx = np.abs(xs - sx); dx = np.minimum(dx, q - dx)
        dy = np.abs(ys - sy); dy = np.minimum(dy, q - dy)
        dd = dx * dx + dy * dy
        m = dd < best
        best[m] = dd[m]; idx[m] = i
    colors = np.array([rnd.choice(accents) if rnd.random() < 0.06 else rnd.choice(reds) for _ in pts], dtype=np.uint8)
    big = np.kron(idx, np.ones((4, 4), dtype=np.int32))
    img = Image.fromarray(colors[big])
    d = ImageDraw.Draw(img)
    class _C:  # adaptador para o laço do rejunte
        def __getitem__(self, k): return int(idx[k[1], k[0]])
    cl = _C()
    # rejunte entre os caquinhos
    for y in range(1, s // 4 - 1):
        for x in range(1, s // 4 - 1):
            c = cl[x, y]
            if cl[x + 1, y] != c or cl[x, y + 1] != c:
                d.rectangle([x * 4, y * 4, x * 4 + 3, y * 4 + 3], fill=(95, 85, 75))
    img = img.filter(ImageFilter.GaussianBlur(0.6))
    return img


def azulejo():
    """Azulejo branco 15x15 com rejunte — meia parede do buteco."""
    s = 256
    img = Image.new("RGB", (s, s), (232, 236, 230))
    d = ImageDraw.Draw(img)
    n = 4
    step = s // n
    for i in range(n + 1):
        d.line([0, i * step, s, i * step], fill=(170, 175, 170), width=3)
        d.line([i * step, 0, i * step, s], fill=(170, 175, 170), width=3)
    # faixa decorativa no topo (azulejo de faixa azul)
    return img


def azulejo_faixa():
    s = 256
    img = Image.new("RGB", (s, 64), (232, 236, 230))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 18, s, 46], fill=(40, 90, 160))
    for x in range(0, s, 32):
        d.polygon([(x + 16, 22), (x + 28, 32), (x + 16, 42), (x + 4, 32)], fill=(230, 235, 240))
    d.line([0, 0, s, 0], fill=(170, 175, 170), width=3)
    return img


def tijolo():
    """Tijolo baiano aparente (sem reboco) — paredes das casas da viela."""
    w, h = 512, 512
    img = Image.new("RGB", (w, h), (120, 110, 100))
    d = ImageDraw.Draw(img)
    rnd = random.Random(3)
    bh, bw = 32, 96
    for row in range(h // bh):
        off = (row % 2) * bw // 2
        for col in range(-1, w // bw + 1):
            x0 = col * bw + off + 3
            y0 = row * bh + 3
            base = (rnd.randint(170, 200), rnd.randint(85, 105), rnd.randint(55, 70))
            d.rectangle([x0, y0, x0 + bw - 6, y0 + bh - 6], fill=base)
    return img.filter(ImageFilter.GaussianBlur(0.5))


def placa_buteco():
    w, h = 1024, 256
    img = Image.new("RGB", (w, h), (200, 30, 35))
    d = ImageDraw.Draw(img)
    d.rectangle([10, 10, w - 10, h - 10], outline=(250, 210, 40), width=10)
    txt = "BUTECO DIGITAL"
    size = 120
    while True:
        f = font(FONT_BOLD, size)
        bbox = d.textbbox((0, 0), txt, font=f)
        tw = bbox[2] - bbox[0]
        if tw < w - 80:
            break
        size -= 4
    d.text(((w - tw) / 2 + 4, 44 + 4), txt, font=f, fill=(90, 10, 10))
    d.text(((w - tw) / 2, 44), txt, font=f, fill=(255, 235, 120))
    f2 = font(FONT_SANS, 34)
    sub = "SINUCA  •  CERVEJA GELADA  •  PETISCOS"
    bbox = d.textbbox((0, 0), sub, font=f2)
    d.text(((w - (bbox[2] - bbox[0])) / 2, 190), sub, font=f2, fill=(255, 255, 255))
    return img


def lousa_cardapio():
    w, h = 512, 640
    img = Image.new("RGB", (w, h), (30, 45, 35))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w - 1, h - 1], outline=(120, 80, 40), width=22)
    chalk = (235, 235, 225)
    yellow = (250, 220, 90)
    d.text((70, 50), "CARDÁPIO", font=font(FONT_BOLD, 64), fill=yellow)
    items = [
        ("Cerveja 600ml", "12,00"),
        ("Caipirinha", "15,00"),
        ("Refri lata", "6,00"),
        ("Pastel", "8,00"),
        ("Coxinha", "7,00"),
        ("Torresmo", "18,00"),
        ("Porção de fritas", "22,00"),
        ("Ficha sinuca", "3,00"),
    ]
    f = font(FONT_SANS, 30)
    y = 150
    for name, price in items:
        d.text((50, y), name, font=f, fill=chalk)
        d.text((360, y), price, font=f, fill=chalk)
        y += 52
    d.text((60, 575), "FIADO SÓ AMANHÃ", font=font(FONT_BOLD, 34), fill=(240, 120, 110))
    return img


def cartaz_samba():
    w, h = 384, 512
    img = Image.new("RGB", (w, h), (250, 200, 40))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w, 150], fill=(30, 110, 60))
    d.text((30, 30), "RODA DE", font=font(FONT_BOLD, 52), fill=(255, 255, 255))
    d.text((30, 160), "SAMBA", font=font(FONT_BOLD, 96), fill=(180, 30, 30))
    d.text((30, 290), "SÁBADO 16H", font=font(FONT_BOLD, 44), fill=(30, 30, 30))
    d.text((30, 360), "aqui no buteco", font=font(FONT_SANS, 36), fill=(30, 30, 30))
    d.text((30, 430), "traga seu pandeiro!", font=font(FONT_SANS, 30), fill=(30, 110, 60))
    return img


def campeonato():
    w, h = 384, 512
    img = Image.new("RGB", (w, h), (240, 240, 235))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w, 120], fill=(20, 100, 50))
    d.text((24, 22), "TORNEIO DE", font=font(FONT_BOLD, 40), fill=(255, 255, 255))
    d.text((24, 66), "SINUCA", font=font(FONT_BOLD, 44), fill=(250, 220, 60))
    d.ellipse([110, 160, 274, 324], fill=(15, 15, 15))
    d.ellipse([160, 210, 224, 274], fill=(245, 242, 230))
    d.text((176, 214), "8", font=font(FONT_BOLD, 48), fill=(15, 15, 15))
    d.text((40, 360), "Duplas e individual", font=font(FONT_SANS, 30), fill=(30, 30, 30))
    d.text((40, 410), "Prêmio: 1 grade", font=font(FONT_BOLD, 34), fill=(180, 30, 30))
    return img


def janela_noite():
    """Vidro de janela com luz quente ou apagada."""
    w, h = 64, 64
    lit = Image.new("RGB", (w, h), (255, 200, 120))
    d = ImageDraw.Draw(lit)
    d.line([w // 2, 0, w // 2, h], fill=(90, 60, 30), width=4)
    d.line([0, h // 2, w, h // 2], fill=(90, 60, 30), width=4)
    return lit


def main():
    for n in range(16):
        ball_texture(n).save(os.path.join(OUT, f"ball_{n:02d}.png"))
    caquinho_floor().save(os.path.join(OUT, "piso_caquinho.png"))
    azulejo().save(os.path.join(OUT, "azulejo.png"))
    azulejo_faixa().save(os.path.join(OUT, "azulejo_faixa.png"))
    tijolo().save(os.path.join(OUT, "tijolo.png"))
    placa_buteco().save(os.path.join(OUT, "placa_buteco.png"))
    lousa_cardapio().save(os.path.join(OUT, "lousa_cardapio.png"))
    cartaz_samba().save(os.path.join(OUT, "cartaz_samba.png"))
    campeonato().save(os.path.join(OUT, "cartaz_torneio.png"))
    janela_noite().save(os.path.join(OUT, "janela_acesa.png"))
    print("ok", OUT)


if __name__ == "__main__":
    main()
