"""Writes the throttle plate's two SVGs: the worn plate with its slot and scale, and the knob.

Remake art, not game data: a 1930s lever plate drawn from shapes alone, because Godot's SVG
rasteriser (ThorVG) reads no filters and no <text>. The patina is seeded blotches and the scale
labels are stroked glyphs. ThrottlePlate.cs reads the geometry constants below; keep them in step.

    python render_throttle_plate.py   (writes beside itself)
"""
import math
import os
import random

W, H = 242.0, 389.0          # the plate, in SVG units
PAD = 6.0                    # room for the drop shadow
SLOT_X = 66.0                # the slot's centre line, which the knob rides
MAX_Y, IDLE_Y = 84.0, 319.0  # the knob centre at full and at idle (ThrottlePlate.cs)
KNOB_R = 37.0
TAN = "#d3b27c"

rng = random.Random(1930)


def blob(cx, cy, r, jitter=0.45, points=9):
    """A closed, smooth, irregular blotch through jittered points on a circle."""
    pts = []
    for i in range(points):
        a = (i / points) * math.tau + rng.uniform(-0.25, 0.25)
        rr = r * rng.uniform(1 - jitter, 1 + jitter * 0.5)
        pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
    mids = [((pts[i][0] + pts[(i + 1) % points][0]) / 2, (pts[i][1] + pts[(i + 1) % points][1]) / 2)
            for i in range(points)]
    d = f"M{mids[-1][0]:.1f} {mids[-1][1]:.1f}"
    for i in range(points):
        d += f" Q{pts[i][0]:.1f} {pts[i][1]:.1f} {mids[i][0]:.1f} {mids[i][1]:.1f}"
    return d + " Z"


def edge_point():
    """A point on the plate, weighted toward its rim, its screws and the slot's rim."""
    pick = rng.random()
    if pick < 0.55:
        side = rng.randrange(4)
        depth = rng.expovariate(1 / 9.0)
        t = rng.uniform(0, 1)
        return [(t * W, depth), (t * W, H - depth), (depth, t * H), (W - depth, t * H)][side]
    if pick < 0.75:
        sx, sy = rng.choice(SCREWS)
        a, d = rng.uniform(0, math.tau), rng.uniform(9, 20)
        return sx + math.cos(a) * d, sy + math.sin(a) * d
    if pick < 0.88:
        return SLOT_X + rng.choice((-1, 1)) * rng.uniform(27, 34), rng.uniform(46, 356)
    return rng.uniform(0, W), rng.uniform(0, H)


SCREWS = [(22.0, 22.0), (W - 22.0, 22.0), (22.0, H - 22.0), (W - 22.0, H - 22.0)]

# A monoline grotesque on a 10 x 14 cap grid (digits on 5 x 8), drawn as strokes.
LETTERS = {
    "M": (10, ["M0 14 V0 L5 9 L10 0 V14"]),
    "A": (10, ["M0 14 L5 0 L10 14", "M1.9 9.4 H8.1"]),
    "X": (10, ["M0 0 L10 14", "M10 0 L0 14"]),
    "I": (0, ["M0 0 V14"]),
    "D": (9, ["M0 0 V14 H3.5 C7.6 14 9 11 9 7 C9 3 7.6 0 3.5 0 Z"]),
    "L": (8, ["M0 0 V14 H8"]),
    "E": (8, ["M8 0 H0 V14 H8", "M0 7 H6.5"]),
}
DIGITS = {
    "1": "M0.6 1.6 L2.8 0 V8",
    "2": "M0 2.1 C0 0.6 1.1 0 2.5 0 C4 0 5 0.8 5 2.2 C5 4.1 0 5.8 0 8 H5.2",
    "3": "M0 0.4 C0.8 0.1 1.6 0 2.4 0 C4.1 0 5 0.8 5 2 C5 3.2 4 3.9 2.2 3.9 C4.1 3.9 5.2 4.7 5.2 6 "
         "C5.2 7.3 4 8 2.5 8 C1.5 8 0.7 7.8 0 7.4",
    "4": "M3.9 8 V0 L0 5.6 H5.6",
}


def word(text, cx, cy, unit, width):
    """Stroked capitals centred on (cx, cy), one cap-grid unit = unit SVG units."""
    gap = 3.0
    total = sum(LETTERS[c][0] for c in text) + gap * (len(text) - 1)
    x = cx - total * unit / 2
    out = []
    for c in text:
        adv, strokes = LETTERS[c]
        for d in strokes:
            out.append(f'<path transform="translate({x:.2f} {cy - 7 * unit:.2f}) scale({unit})" d="{d}"/>')
        x += (adv + gap) * unit
    return f'<g fill="none" stroke="{TAN}" stroke-width="{width / unit:.3f}" stroke-linejoin="miter">{"".join(out)}</g>'


def fraction(num, den, cx, cy, unit, width):
    """A stacked fraction: numerator up-left, a slash, denominator down-right, on a 14-unit box."""
    ox, oy = cx - 6.5 * unit, cy - 7 * unit
    parts = [
        f'<path transform="translate({ox:.2f} {oy:.2f}) scale({unit * 0.85})" d="{DIGITS[num]}"/>',
        f'<path transform="translate({ox:.2f} {oy:.2f}) scale({unit})" d="M10 0.6 L3 13.4"/>',
        f'<path transform="translate({ox + 7.6 * unit:.2f} {oy + 7.2 * unit:.2f}) scale({unit * 0.85})" d="{DIGITS[den]}"/>',
    ]
    return (f'<g fill="none" stroke="{TAN}" stroke-width="{width / unit:.3f}" stroke-linecap="butt" '
            f'stroke-linejoin="round">{"".join(parts)}</g>')


def screw(cx, cy):
    a = rng.uniform(0, math.pi / 2)
    dx, dy = math.cos(a) * 6.5, math.sin(a) * 6.5
    return (
        f'<circle cx="{cx}" cy="{cy + 1.2}" r="11" fill="#000" opacity="0.45"/>'
        f'<circle cx="{cx}" cy="{cy}" r="10" fill="url(#screw)" stroke="#221d17" stroke-width="1.2"/>'
        f'<path d="M{cx - dx:.2f} {cy - dy:.2f} L{cx + dx:.2f} {cy + dy:.2f} M{cx + dy:.2f} {cy - dx:.2f} '
        f'L{cx - dy:.2f} {cy + dx:.2f}" stroke="#1c1814" stroke-width="2.4" stroke-linecap="round"/>'
        f'<path d="M{cx - dx + 0.8:.2f} {cy - dy + 0.8:.2f} L{cx + dx + 0.8:.2f} {cy + dy + 0.8:.2f}" '
        f'stroke="#e8e0cf" stroke-width="0.6" opacity="0.5"/>'
    )


def plate():
    body = f'<rect x="0" y="0" width="{W}" height="{H}" rx="14"/>'
    out = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{W + 2 * PAD:.0f}" height="{H + 2 * PAD:.0f}" '
        f'viewBox="{-PAD} {-PAD} {W + 2 * PAD:.0f} {H + 2 * PAD:.0f}">',
        "<defs>",
        f'<clipPath id="plate">{body}</clipPath>',
        '<linearGradient id="base" x1="0" y1="0" x2="1" y2="1">'
        '<stop offset="0" stop-color="#77716a"/><stop offset="0.55" stop-color="#5f5a52"/>'
        '<stop offset="1" stop-color="#433e37"/></linearGradient>',
        '<radialGradient id="grime" cx="0.5" cy="0.5" r="0.62">'
        '<stop offset="0.55" stop-color="#2a1c10" stop-opacity="0"/>'
        '<stop offset="1" stop-color="#2a1c10" stop-opacity="0.7"/></radialGradient>',
        '<linearGradient id="bevel" x1="0" y1="0" x2="1" y2="1">'
        '<stop offset="0" stop-color="#e2d9c6" stop-opacity="0.75"/>'
        '<stop offset="0.5" stop-color="#8a8274" stop-opacity="0.2"/>'
        '<stop offset="1" stop-color="#14110d" stop-opacity="0.85"/></linearGradient>',
        '<radialGradient id="screw" cx="0.4" cy="0.35" r="0.7">'
        '<stop offset="0" stop-color="#ddd5c4"/><stop offset="0.55" stop-color="#8b8272"/>'
        '<stop offset="1" stop-color="#3a342b"/></radialGradient>',
        '<linearGradient id="chrome" x1="0" y1="0" x2="1" y2="0">'
        '<stop offset="0" stop-color="#2f2b26"/><stop offset="0.18" stop-color="#b9b2a3"/>'
        '<stop offset="0.4" stop-color="#77715f"/><stop offset="0.62" stop-color="#4c473f"/>'
        '<stop offset="0.82" stop-color="#cdc5b4"/><stop offset="1" stop-color="#3a352f"/></linearGradient>',
        '<linearGradient id="well" x1="0" y1="0" x2="1" y2="0">'
        '<stop offset="0" stop-color="#000"/><stop offset="0.45" stop-color="#1c1814"/>'
        '<stop offset="0.7" stop-color="#2a241d"/><stop offset="1" stop-color="#030303"/></linearGradient>',
        '<linearGradient id="wellshade" x1="0" y1="0" x2="0" y2="1">'
        '<stop offset="0" stop-color="#000" stop-opacity="0.9"/><stop offset="0.12" stop-color="#000" stop-opacity="0"/>'
        '<stop offset="0.9" stop-color="#000" stop-opacity="0"/><stop offset="1" stop-color="#000" stop-opacity="0.6"/>'
        '</linearGradient>',
        '<linearGradient id="panel" x1="0" y1="0" x2="1" y2="1">'
        '<stop offset="0" stop-color="#1f1d1a"/><stop offset="1" stop-color="#0b0a09"/></linearGradient>',
        "</defs>",
        # The drop shadow keeps the plate legible over bright sky.
        f'<rect x="2" y="4" width="{W}" height="{H}" rx="15" fill="#000" opacity="0.5"/>',
        f'<rect x="0" y="0" width="{W}" height="{H}" rx="14" fill="url(#base)"/>',
        '<g clip-path="url(#plate)">',
    ]
    # Mottled, worn paint: grey-beige chips over darker primer.
    for _ in range(90):
        tone = rng.choice(("#9a9385", "#3f3a33", "#8a8274", "#4f4941", "#6b6458"))
        out.append(f'<path d="{blob(rng.uniform(0, W), rng.uniform(0, H), rng.uniform(10, 40), 0.6, 11)}" '
                   f'fill="{tone}" opacity="{rng.uniform(0.18, 0.4):.2f}"/>')
    # Rust, heaviest at the rim, round the screws and along the slot: ragged patches with a
    # dark pitted core, plus a scatter of flecks.
    for _ in range(110):
        x, y = edge_point()
        r = rng.uniform(3, 13)
        out.append(f'<path d="{blob(x, y, r, 0.7, 13)}" fill="{rng.choice(("#5e3218", "#7a4120", "#4b2914", "#8d4f25"))}" '
                   f'opacity="{rng.uniform(0.45, 0.8):.2f}"/>')
        out.append(f'<path d="{blob(x + rng.uniform(-2, 2), y + rng.uniform(-2, 2), r * 0.5, 0.7, 11)}" '
                   f'fill="#2e1a0d" opacity="{rng.uniform(0.4, 0.75):.2f}"/>')
    for _ in range(160):
        x, y = edge_point()
        out.append(f'<path d="{blob(x, y, rng.uniform(0.8, 2.6), 0.6, 7)}" fill="#8b4a22" '
                   f'opacity="{rng.uniform(0.4, 0.8):.2f}"/>')
    out.append(f'<rect x="0" y="0" width="{W}" height="{H}" fill="url(#grime)"/>')
    for _ in range(420):
        out.append(f'<circle cx="{rng.uniform(0, W):.1f}" cy="{rng.uniform(0, H):.1f}" '
                   f'r="{rng.uniform(0.4, 1.4):.2f}" fill="#2a241e" opacity="{rng.uniform(0.3, 0.7):.2f}"/>')
    for _ in range(45):
        x, y, a, length = rng.uniform(8, W - 8), rng.uniform(8, H - 8), rng.uniform(0, math.pi), rng.uniform(4, 16)
        out.append(f'<path d="M{x:.1f} {y:.1f} l{math.cos(a) * length:.1f} {math.sin(a) * length:.1f}" '
                   f'stroke="#d4ccbb" stroke-width="0.5" opacity="0.35"/>')
    out.append("</g>")
    out.append(f'<rect x="1.5" y="1.5" width="{W - 3}" height="{H - 3}" rx="13" fill="none" '
               f'stroke="url(#bevel)" stroke-width="3"/>')
    out.append(f'<rect x="0" y="0" width="{W}" height="{H}" rx="14" fill="none" stroke="#17130f" stroke-width="1.6"/>')

    # The slot: a chrome rim round a dark well, the knob's track.
    out.append(f'<rect x="{SLOT_X - 28}" y="44" width="56" height="314" rx="28" fill="#000" opacity="0.45"/>')
    out.append(f'<rect x="{SLOT_X - 27}" y="46" width="54" height="310" rx="27" fill="url(#chrome)" '
               f'stroke="#1d1915" stroke-width="1.2"/>')
    out.append(f'<rect x="{SLOT_X - 15}" y="58" width="30" height="286" rx="15" fill="url(#well)"/>')
    out.append(f'<rect x="{SLOT_X - 15}" y="58" width="30" height="286" rx="15" fill="url(#wellshade)"/>')
    for _ in range(14):
        x = SLOT_X + rng.choice((-1, 1)) * rng.uniform(17, 25)
        out.append(f'<path d="{blob(x, rng.uniform(52, 350), rng.uniform(1, 3))}" fill="#7a4120" opacity="0.6"/>')

    # The scale: a black panel, a spine with ticks, the labels.
    out.append('<rect x="123" y="44" width="94" height="314" rx="9" fill="#000" opacity="0.5"/>')
    out.append('<rect x="125" y="46" width="90" height="310" rx="8" fill="url(#panel)" stroke="#3d352a" stroke-width="1.6"/>')
    for _ in range(120):
        out.append(f'<circle cx="{rng.uniform(128, 212):.1f}" cy="{rng.uniform(50, 352):.1f}" '
                   f'r="{rng.uniform(0.3, 0.9):.2f}" fill="#bfb6a3" opacity="{rng.uniform(0.05, 0.18):.2f}"/>')
    step = (IDLE_Y - MAX_Y) / 4
    ticks = [f"M131 {MAX_Y - 6:.1f} V{IDLE_Y + 6:.1f}"]
    for i in range(5):
        y = MAX_Y + i * step
        ticks.append(f"M131 {y:.1f} H143")
    for i in range(4):
        y = MAX_Y + (i + 0.5) * step
        ticks.append(f"M131 {y:.1f} H137")
    out.append(f'<path d="{" ".join(ticks)}" fill="none" stroke="{TAN}" stroke-width="2"/>')
    out.append(word("MAX", 177, MAX_Y, 1.25, 2.6))
    out.append(fraction("3", "4", 175, MAX_Y + step, 1.3, 2.4))
    out.append(fraction("1", "2", 175, MAX_Y + 2 * step, 1.3, 2.4))
    out.append(fraction("1", "4", 175, MAX_Y + 3 * step, 1.3, 2.4))
    out.append(word("IDLE", 177, IDLE_Y, 1.25, 2.6))

    for sx, sy in SCREWS:
        out.append(screw(sx, sy))
    out.append("</svg>")
    return "\n".join(out)


def knob():
    size = 2 * (KNOB_R + 10)
    c = size / 2
    out = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{size:.0f}" height="{size:.0f}" viewBox="0 0 {size:.0f} {size:.0f}">',
        "<defs>",
        '<radialGradient id="shadow" cx="0.5" cy="0.5" r="0.5">'
        '<stop offset="0.55" stop-color="#000" stop-opacity="0.65"/>'
        '<stop offset="1" stop-color="#000" stop-opacity="0"/></radialGradient>',
        '<radialGradient id="ball" cx="0.5" cy="0.5" r="0.5" fx="0.36" fy="0.3">'
        '<stop offset="0" stop-color="#77746e"/><stop offset="0.35" stop-color="#2c2a27"/>'
        '<stop offset="0.85" stop-color="#0b0a09"/><stop offset="1" stop-color="#1c1a17"/></radialGradient>',
        '<radialGradient id="shine" cx="0.5" cy="0.5" r="0.5">'
        '<stop offset="0" stop-color="#fff" stop-opacity="0.85"/>'
        '<stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient>',
        '<linearGradient id="rim" x1="0" y1="0" x2="1" y2="1">'
        '<stop offset="0.6" stop-color="#8d8577" stop-opacity="0"/>'
        '<stop offset="1" stop-color="#b5ab98" stop-opacity="0.55"/></linearGradient>',
        "</defs>",
        f'<ellipse cx="{c + 4}" cy="{c + 7}" rx="{KNOB_R + 7}" ry="{KNOB_R + 5}" fill="url(#shadow)"/>',
        f'<circle cx="{c}" cy="{c}" r="{KNOB_R}" fill="url(#ball)"/>',
        f'<circle cx="{c}" cy="{c}" r="{KNOB_R - 1}" fill="none" stroke="url(#rim)" stroke-width="2"/>',
    ]
    for _ in range(18):
        a, d = rng.uniform(0, math.tau), rng.uniform(4, KNOB_R - 6)
        x, y, t = c + math.cos(a) * d, c + math.sin(a) * d, rng.uniform(0, math.pi)
        length = rng.uniform(2, 7)
        out.append(f'<path d="M{x:.1f} {y:.1f} l{math.cos(t) * length:.1f} {math.sin(t) * length:.1f}" '
                   f'stroke="#c9c2b4" stroke-width="0.5" opacity="0.3"/>')
    out.append(f'<ellipse cx="{c - 11}" cy="{c - 13}" rx="10" ry="7" transform="rotate(-35 {c - 11} {c - 13})" '
               f'fill="url(#shine)"/>')
    out.append(f'<circle cx="{c - 12}" cy="{c - 14}" r="2.2" fill="#fff" opacity="0.9"/>')
    out.append("</svg>")
    return "\n".join(out)


if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    for name, svg in (("throttle_plate.svg.bin", plate()), ("throttle_knob.svg.bin", knob())):
        with open(os.path.join(here, name), "w", encoding="ascii", newline="\n") as f:
            f.write(svg + "\n")
