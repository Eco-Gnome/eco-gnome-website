"""Atlas de textures d'un set : une tuile carrée par skin, rangées en grille dans une image WebP."""
from PIL import Image, ImageChops

def build_atlas(texture_paths):
    """texture_paths : liste ordonnée (index = tuile) de chemins, ou de (chemin ou None = blanc, teinte rgb 0..1[, (map de métallicité,
    facteur)[, (lignes, colonnes, i)]]) : la tuile est multipliée par la teinte, comme l'albédo par _TopTint/_SideTint/_DetailTint
    dans le shader du jeu, puis assombrie là où le matériau est métallique ; le dernier champ prend la tranche i d'une
    image importée en tableau par Unity (flipbook lu de gauche à droite, de haut en bas). Retourne (image atlas RGB carrée, côté d'une tuile) : tuiles de
    1024 px (le jeu étale sa texture sur 5 blocs, 512 px seraient flous) dans un atlas de 2048 (≤ 4) ou 4096 (≤ 16) ;
    au-delà, 512 px dans 4096 (≤ 64)."""
    n = len(texture_paths)
    tile_px, atlas_px = (1024, 2048) if n <= 4 else (1024, 4096) if n <= 16 else (512, 4096)
    per_row = atlas_px // tile_px
    if n > per_row * per_row:
        raise ValueError('too many textures for one atlas (%d > %d)' % (n, per_row * per_row))
    atlas = Image.new('RGB', (atlas_px, atlas_px), (0, 0, 0))
    sources = {}
    for i, entry in enumerate(texture_paths):
        path, tint, metal, crop = (tuple(entry) + (None, None))[:4] if isinstance(entry, tuple) else (entry, None, None, None)
        if path not in sources: sources[path] = Image.open(path).convert('RGB') if path else Image.new('RGB', (tile_px, tile_px), (255, 255, 255))
        tile = sources[path]
        if crop:
            rows, cols, k = crop
            w, h = tile.width // cols, tile.height // rows
            tile = tile.crop(((k % cols) * w, (k // cols) * h, (k % cols + 1) * w, (k // cols + 1) * h))
        tile = tile.resize((tile_px, tile_px), Image.LANCZOS)
        if tint: tile = Image.merge('RGB', [band.point(lambda v, f=f: round(v * f)) for band, f in zip(tile.split(), tint)])
        if metal:   # (map de métallicité, facteur) : pixel × (1 − facteur × R de la map)
            m = Image.open(metal[0]).convert('RGB').resize((tile_px, tile_px), Image.LANCZOS).getchannel('R').point(lambda v, k=metal[1]: round(255 * (1 - k * v / 255)))
            tile = Image.merge('RGB', [ImageChops.multiply(band, m) for band in tile.split()])
        atlas.paste(tile, ((i % per_row) * tile_px, (i // per_row) * tile_px))
    return atlas, tile_px


def save_atlas(atlas, webp_path, preview_png_path):
    atlas.save(webp_path, 'WEBP', quality=85, method=6)
    atlas.save(preview_png_path, 'PNG', optimize=True)
