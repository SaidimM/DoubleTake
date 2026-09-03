#!/usr/bin/env python3
"""
Convert SMIL SVG Animations to High-Resolution, High-FPS Animated WebP and MP4 Video.
Uses Edge headless browser via DevTools Protocol to precisely step through animation timestamps and FFmpeg for encoding.
"""

import argparse
import base64
import json
import os
import subprocess
import tempfile
import time
import urllib.request
import websocket


def render_svg_frames(
    svg_path: str,
    temp_dir: str,
    duration: float = 8.0,
    fps: int = 50,
    width: int = 800,
    height: int = 520,
    scale: float = 2.0,
    edge_path: str = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
):
    frames_dir = os.path.join(temp_dir, "frames")
    os.makedirs(frames_dir, exist_ok=True)

    with open(svg_path, "r", encoding="utf-8") as f:
        svg_content = f.read()

    html_content = f"""<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<style>
  * {{ margin: 0; padding: 0; box-sizing: border-box; }}
  html, body {{
    background: transparent;
    overflow: hidden;
    width: {width}px;
    height: {height}px;
  }}
  svg {{
    display: block;
    width: {width}px;
    height: {height}px;
  }}
</style>
</head>
<body>
{svg_content}
</body>
</html>
"""
    html_file = os.path.join(temp_dir, "index.html")
    with open(html_file, "w", encoding="utf-8") as f:
        f.write(html_content)

    import socket
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.bind(('127.0.0.1', 0))
        port = s.getsockname()[1]
    cmd = [
        edge_path,
        "--headless=new",
        f"--remote-debugging-port={port}",
        "--remote-allow-origins=*",
        f"--user-data-dir={temp_dir}",
        f"--window-size={width},{height}",
        "--hide-scrollbars",
        "--disable-background-networking",
        "--disable-default-apps",
        "--no-first-run",
        "about:blank",
    ]

    proc = subprocess.Popen(cmd)
    time.sleep(1.5)

    msg_id = 0

    def send_cmd(ws, method, params=None):
        nonlocal msg_id
        msg_id += 1
        req = {"id": msg_id, "method": method, "params": params or {}}
        ws.send(json.dumps(req))
        while True:
            res = json.loads(ws.recv())
            if res.get("id") == msg_id:
                return res

    try:
        with urllib.request.urlopen(f"http://localhost:{port}/json") as response:
            pages = json.loads(response.read().decode())
            target_page = next((p for p in pages if p.get("type") == "page"), pages[0])
            ws_url = target_page["webSocketDebuggerUrl"]

        ws = websocket.create_connection(ws_url)
        send_cmd(ws, "Page.enable")

        url = f"file:///{html_file.replace(os.sep, '/')}"
        send_cmd(ws, "Page.navigate", {"url": url})

        # Wait for page ready
        for _ in range(50):
            time.sleep(0.1)
            res = send_cmd(
                ws,
                "Runtime.evaluate",
                {
                    "expression": "document.readyState === 'complete' && !!document.querySelector('svg')"
                },
            )
            if res.get("result", {}).get("result", {}).get("value") is True:
                break

        # Wait for fonts to be ready
        send_cmd(
            ws,
            "Runtime.evaluate",
            {"expression": "document.fonts.ready", "awaitPromise": True},
        )
        time.sleep(0.5)

        # Set device scale factor (e.g. 2.0x for 1600x1040 crisp retina rendering)
        send_cmd(
            ws,
            "Emulation.setDeviceMetricsOverride",
            {
                "width": width,
                "height": height,
                "deviceScaleFactor": scale,
                "mobile": False,
            },
        )

        # Pause animations
        send_cmd(
            ws,
            "Runtime.evaluate",
            {
                "expression": "const svg = document.querySelector('svg'); svg.pauseAnimations();"
            },
        )

        total_frames = int(duration * fps)
        t_step = 1.0 / fps

        print(f"[*] Capturing {total_frames} frames ({int(width * scale)}x{int(height * scale)} @ {fps} FPS)...")
        for i in range(total_frames):
            t = i * t_step
            send_cmd(
                ws,
                "Runtime.evaluate",
                {
                    "expression": f"document.querySelector('svg').setCurrentTime({t});"
                },
            )
            shot = send_cmd(ws, "Page.captureScreenshot", {"format": "png"})
            img_data = base64.b64decode(shot["result"]["data"])
            frame_filename = os.path.join(frames_dir, f"frame_{i:05d}.png")
            with open(frame_filename, "wb") as f:
                f.write(img_data)

            if (i + 1) % 50 == 0 or (i + 1) == total_frames:
                print(f"    -> Progress: {i + 1}/{total_frames} frames (t={t:.2f}s)")

        ws.close()
    finally:
        proc.terminate()
        proc.wait()

    return frames_dir, total_frames


def encode_outputs(
    frames_dir: str,
    fps: int,
    output_webp: str,
    output_mp4: str = None,
    output_rednote_mp4: str = None,
    quality: int = 90,
    ffmpeg_path: str = r"C:\ffmpeg\ffmpeg-8.0.1-essentials_build\bin\ffmpeg.exe",
):
    # 1. Encode High-Res High-FPS Animated WebP
    if output_webp:
        print(f"[*] Encoding Animated WebP -> {output_webp}")
        os.makedirs(os.path.dirname(os.path.abspath(output_webp)), exist_ok=True)
        ffmpeg_cmd = [
            ffmpeg_path,
            "-y",
            "-framerate", str(fps),
            "-i", os.path.join(frames_dir, "frame_%05d.png"),
            "-vcodec", "libwebp",
            "-lossless", "0",
            "-q:v", str(quality),
            "-compression_level", "6",
            "-loop", "0",
            "-an",
            output_webp,
        ]
        res = subprocess.run(ffmpeg_cmd, capture_output=True, text=True)
        if res.returncode != 0:
            print("FFmpeg WebP error:", res.stderr)
        else:
            print(f"[✓] WebP created: {output_webp} ({os.path.getsize(output_webp)/1024:.1f} KB)")

    # 2. Encode High-Res MP4 Video
    if output_mp4:
        print(f"[*] Encoding MP4 Video -> {output_mp4}")
        os.makedirs(os.path.dirname(os.path.abspath(output_mp4)), exist_ok=True)
        ffmpeg_cmd = [
            ffmpeg_path,
            "-y",
            "-framerate", str(fps),
            "-i", os.path.join(frames_dir, "frame_%05d.png"),
            "-c:v", "libx264",
            "-crf", "16",
            "-preset", "slow",
            "-pix_fmt", "yuv420p",
            output_mp4,
        ]
        res = subprocess.run(ffmpeg_cmd, capture_output=True, text=True)
        if res.returncode != 0:
            print("FFmpeg MP4 error:", res.stderr)
        else:
            print(f"[✓] MP4 created: {output_mp4} ({os.path.getsize(output_mp4)/1024:.1f} KB)")

    # 3. Encode RedNote-Optimized 3:4 Vertical Video (1080x1440)
    if output_rednote_mp4:
        print(f"[*] Encoding RedNote 3:4 Video (1080x1440) -> {output_rednote_mp4}")
        os.makedirs(os.path.dirname(os.path.abspath(output_rednote_mp4)), exist_ok=True)
        # Pad and place on dark aesthetic background (1080x1440) with title header (no text under animation)
        filter_complex = (
            "scale=1040:676:flags=lanczos,"
            "pad=1080:1440:(1080-iw)/2:(1440-ih)/2+50:color='#131826',"
            "drawtext=text='DoubleTake':fontsize=68:fontcolor=white:x=(w-text_w)/2:y=200:font='Segoe UI',"
            "drawtext=text='Windows 划词双击 Ctrl 即时翻译':fontsize=36:fontcolor='#93c5fd':x=(w-text_w)/2:y=285:font='Microsoft YaHei'"
        )
        ffmpeg_cmd = [
            ffmpeg_path,
            "-y",
            "-framerate", str(fps),
            "-i", os.path.join(frames_dir, "frame_%05d.png"),
            "-vf", filter_complex,
            "-c:v", "libx264",
            "-crf", "18",
            "-preset", "slow",
            "-pix_fmt", "yuv420p",
            output_rednote_mp4,
        ]
        res = subprocess.run(ffmpeg_cmd, capture_output=True, text=True)
        if res.returncode != 0:
            print("FFmpeg RedNote 3:4 error:", res.stderr)
        else:
            print(f"[✓] RedNote 3:4 MP4 created: {output_rednote_mp4} ({os.path.getsize(output_rednote_mp4)/1024:.1f} KB)")


def main():
    parser = argparse.ArgumentParser(description="Convert animated SVG to high-res WebP and MP4")
    parser.add_argument("--svg", default=r"assets/demo.svg", help="Path to input SVG file")
    parser.add_argument("--webp", default=r"assets/demo.webp", help="Path to output WebP file")
    parser.add_argument("--mp4", default=r"assets/demo.mp4", help="Path to output MP4 file")
    parser.add_argument("--rednote", default=r"assets/demo_rednote_3x4.mp4", help="Path to output RedNote 3:4 MP4")
    parser.add_argument("--duration", type=float, default=8.0, help="Duration in seconds")
    parser.add_argument("--fps", type=int, default=50, help="Frames per second (e.g. 50 or 60)")
    parser.add_argument("--scale", type=float, default=2.0, help="Scale factor (2.0 = 1600x1040)")
    parser.add_argument("--quality", type=int, default=88, help="WebP quality (0-100)")
    args = parser.parse_args()

    temp_dir = tempfile.mkdtemp(prefix="svg_render_")
    frames_dir, _ = render_svg_frames(
        svg_path=os.path.abspath(args.svg),
        temp_dir=temp_dir,
        duration=args.duration,
        fps=args.fps,
        scale=args.scale,
    )

    encode_outputs(
        frames_dir=frames_dir,
        fps=args.fps,
        output_webp=os.path.abspath(args.webp) if args.webp else None,
        output_mp4=os.path.abspath(args.mp4) if args.mp4 else None,
        output_rednote_mp4=os.path.abspath(args.rednote) if args.rednote else None,
        quality=args.quality,
    )


if __name__ == "__main__":
    main()
