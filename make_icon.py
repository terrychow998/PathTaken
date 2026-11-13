"""生成 app.ico —— 纯标准库实现（不需要 Pillow）。
画一个圆角渐变方块 + 三根柱状条，输出多尺寸 32 位 ICO。
用法： python make_icon.py
"""
import math
import os
import struct

SIZES = [16, 32, 48, 64, 128, 256]
SS = 4  # 超采样倍数

C1 = (76, 141, 255)
C2 = (46, 196, 139)
BAR = (255, 255, 255)


def sd_round_box(px, py, cx, cy, half, r):
    qx = abs(px - cx) - (half - r)
    qy = abs(py - cy) - (half - r)
    ax, ay = max(qx, 0.0), max(qy, 0.0)
    return math.hypot(ax, ay) + min(max(qx, qy), 0.0) - r


def sd_bar(px, py, x0, x1, y0, y1, r):
    cx, cy = (x0 + x1) / 2.0, (y0 + y1) / 2.0
    hx, hy = (x1 - x0) / 2.0 - r, (y1 - y0) / 2.0 - r
    qx, qy = abs(px - cx) - hx, abs(py - cy) - hy
    ax, ay = max(qx, 0.0), max(qy, 0.0)
    return math.hypot(ax, ay) + min(max(qx, qy), 0.0) - r


BARS = [
    (0.255, 0.355, 0.560, 0.760),
    (0.450, 0.550, 0.440, 0.760),
    (0.645, 0.745, 0.320, 0.760),
]


def render(size):
    w = size * SS
    out = []
    for y in range(size):
        row = []
        for x in range(size):
            acc_a = 0.0
            acc_r = acc_g = acc_b = 0.0
            for sy in range(SS):
                for sx in range(SS):
                    u = (x * SS + sx + 0.5) / w
                    v = (y * SS + sy + 0.5) / w
                    d = sd_round_box(u, v, 0.5, 0.5, 0.5, 0.22)
                    if d > 0:
                        continue
                    t = max(0.0, min(1.0, (u + v) / 2.0))
                    r = C1[0] + (C2[0] - C1[0]) * t
                    g = C1[1] + (C2[1] - C1[1]) * t
                    b = C1[2] + (C2[2] - C1[2]) * t
                    for (bx0, bx1, by0, by1) in BARS:
                        if sd_bar(u, v, bx0, bx1, by0, by1, 0.028) <= 0:
                            r, g, b = BAR
                            break
                    acc_a += 1.0
                    acc_r += r
                    acc_g += g
                    acc_b += b
            n = SS * SS
            a = acc_a / n
            if acc_a <= 0:
                row.append((0, 0, 0, 0))
            else:
                row.append((
                    int(round(acc_r / acc_a)),
                    int(round(acc_g / acc_a)),
                    int(round(acc_b / acc_a)),
                    int(round(255 * a)),
                ))
        out.append(row)
    return out


def to_dib(rows, size):
    # BITMAPINFOHEADER：高度写两倍（XOR + AND）
    hdr = struct.pack("<IiiHHIIiiII", 40, size, size * 2, 1, 32, 0, size * size * 4, 0, 0, 0, 0)
    data = bytearray()
    for y in range(size - 1, -1, -1):
        for x in range(size):
            r, g, b, a = rows[y][x]
            data += bytes((b, g, r, a))
    mask_row = ((size + 31) // 32) * 4
    mask = b"\x00" * (mask_row * size)
    return hdr + bytes(data) + mask


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    images = [(s, to_dib(render(s), s)) for s in SIZES]

    out = bytearray()
    out += struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    for s, blob in images:
        dim = 0 if s >= 256 else s
        out += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
    for s, blob in images:
        out += blob

    path = os.path.join(here, "app.ico")
    with open(path, "wb") as f:
        f.write(out)
    print("written:", path, len(out), "bytes")


if __name__ == "__main__":
    main()
